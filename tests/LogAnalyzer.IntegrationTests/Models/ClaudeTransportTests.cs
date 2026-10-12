using System.Net;
using System.Net.Sockets;
using LogAnalyzer.Core.Processing;
using LogAnalyzer.IntegrationTests.Accounts;
using LogAnalyzer.IntegrationTests.Runs;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LogAnalyzer.IntegrationTests.Models;

/// <summary>
/// Timeouts and retries of Claude calls, against the fake Messages API, with 2 retries. Delays are real, so these tests
/// take a few seconds each.
/// </summary>
public sealed class ClaudeTransportTests(ClaudeApiFactory factory) : IClassFixture<ClaudeApiFactory>
{
    private static readonly string Log = FixtureLogs.Read(FixtureLogs.OrdersApi40);

    // The fake answers the first try after 15 seconds, so a try that is not abandoned shows as a gap of 15 seconds or
    // more. The bounds leave room for a busy test machine.
    [Fact]
    [Trait("Scenario", "model-gateway/slow-call-is-abandoned")]
    public async Task Call_IsAbandonedAfterTimeout()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var timeout = TimeSpan.FromSeconds(5);
        factory.Messages.Reset();
        factory.Messages.Answer(ModelReplies.OrdersAnalysis, delay: TimeSpan.FromSeconds(15));
        factory.Messages.Answer(ModelReplies.OrdersAnalysis);
        await using var host = factory.CreateHost(timeout: timeout);

        var (run, attempts) = await RunAsync(host, cancellationToken);

        Assert.Equal("completed", run.Status);
        Assert.True(Assert.Single(attempts).TransportRetries >= 1, "The abandoned call was not retried.");
        var requests = factory.Messages.Requests;
        var gap = requests[1].ReceivedAt - requests[0].ReceivedAt;
        Assert.True(gap >= timeout && gap < TimeSpan.FromSeconds(14), $"The retry came {gap} after the first try.");
    }

    [Fact]
    [Trait("Scenario", "model-gateway/rate-limit-then-success")]
    public async Task Call_RetriesRateLimitThenSucceeds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.Messages.Reset();
        factory.Messages.Fail(429, "rate_limit_error", retryAfter: "1");
        factory.Messages.Answer(ModelReplies.OrdersAnalysis);

        var (run, attempts) = await RunAsync(factory, cancellationToken);

        Assert.Equal("completed", run.Status);
        Assert.Equal(1, Assert.Single(attempts).TransportRetries);
        Assert.Equal(2, factory.Messages.Requests.Count);
    }

    [Fact]
    [Trait("Scenario", "model-gateway/retries-exhausted")]
    public async Task Call_FailsWithModelUnavailableAfterRetries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.Messages.Reset();
        for (var i = 0; i < 3; i++)
        {
            factory.Messages.Fail(529, "overloaded_error");
        }

        var (run, attempts) = await RunAsync(factory, cancellationToken);

        Assert.Equal("failed", run.Status);
        Assert.Equal(FailureReasons.ModelUnavailable, run.FailureReason);
        Assert.Equal(3, factory.Messages.Requests.Count);
        Assert.Empty(attempts);
    }

    [Fact]
    [Trait("Scenario", "model-gateway/authentication-error-is-not-retried")]
    public async Task Call_DoesNotRetryAuthenticationError()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.Messages.Reset();
        factory.Messages.Fail(401, "authentication_error");
        factory.Messages.Answer(ModelReplies.OrdersAnalysis);

        var (run, _) = await RunAsync(factory, cancellationToken);

        Assert.Equal("failed", run.Status);
        Assert.Equal(FailureReasons.ModelConfigurationError, run.FailureReason);
        Assert.Single(factory.Messages.Requests);
    }

    [Fact]
    public async Task Call_DoesNotRetryBadRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.Messages.Reset();
        factory.Messages.Fail(400, "invalid_request_error");
        factory.Messages.Answer(ModelReplies.OrdersAnalysis);

        var (run, _) = await RunAsync(factory, cancellationToken);

        Assert.Equal("failed", run.Status);
        Assert.Equal(FailureReasons.ModelConfigurationError, run.FailureReason);
        Assert.Single(factory.Messages.Requests);
    }

    // Nothing listens at the address, so every try fails to connect: each is retried, and the run fails as unavailable.
    [Fact]
    public async Task Call_RetriesConnectionFailure()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var unreachable = factory.CreateHost(baseUrl: $"http://127.0.0.1:{ClosedPort()}");
        var retriesBefore = RetriesLogged();

        var (run, _) = await RunAsync(unreachable, cancellationToken);

        Assert.Equal("failed", run.Status);
        Assert.Equal(FailureReasons.ModelUnavailable, run.FailureReason);
        Assert.Equal(2, RetriesLogged() - retriesBefore);
    }

    // Without the hint, the first retry would follow after about one second.
    [Fact]
    public async Task Call_WaitsAsLongAsRetryAfterSays()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.Messages.Reset();
        factory.Messages.Fail(429, "rate_limit_error", retryAfter: "3");
        factory.Messages.Answer(ModelReplies.OrdersAnalysis);

        var (run, _) = await RunAsync(factory, cancellationToken);

        Assert.Equal("completed", run.Status);
        Assert.Contains(factory.Logs.Entries, entry => entry.Contains("retry 1 follows in 00:00:03", StringComparison.Ordinal));
        var requests = factory.Messages.Requests;
        var gap = requests[1].ReceivedAt - requests[0].ReceivedAt;
        Assert.True(gap >= TimeSpan.FromSeconds(3), $"The retry came {gap} after the rate limit, before its retry-after of 3 seconds.");
    }

    [Fact]
    public async Task Attempt_RecordsEveryTransportRetry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.Messages.Reset();
        factory.Messages.Fail(529, "overloaded_error");
        factory.Messages.Fail(500, "api_error");
        factory.Messages.Answer(ModelReplies.OrdersAnalysis);

        var (run, attempts) = await RunAsync(factory, cancellationToken);

        Assert.Equal("completed", run.Status);
        Assert.Equal(2, Assert.Single(attempts).TransportRetries);
    }

    // A port nothing listens on: the system chose it free, and the listener has stopped.
    private static int ClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private int RetriesLogged() =>
        factory.Logs.Entries.Count(entry => entry.Contains("Model call failed; retry", StringComparison.Ordinal));

    // A new person submits the log through the host, the host processes the queue, and the stored run and its attempts
    // are read.
    private async Task<(StoredRun Run, List<StoredAttempt> Attempts)> RunAsync(
        WebApplicationFactory<Program> host, CancellationToken cancellationToken)
    {
        var accounts = new AccountsClient(host.CreateClient());
        await accounts.RegisterAndSignInAsync(AccountsClient.NewEmail(), cancellationToken);
        var runs = new RunsClient(accounts.Http);
        await using var database = new RunsDatabase(factory.ConnectionString);

        var runId = await runs.SubmitAcceptedAsync(Log, fileName: null, cancellationToken);
        await ClaudeApiFactory.ProcessQueuedRunsAsync(host, cancellationToken);

        return (await database.ReadRunAsync(runId, cancellationToken), await database.ReadAttemptsAsync(runId, cancellationToken));
    }
}
