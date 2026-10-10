using System.Net;
using LogAnalyzer.IntegrationTests.Accounts;

namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>
/// Submissions sent to a real Kestrel server without Content-Length, as chunked bodies. Kestrel counts chunk framing
/// toward its own body size limit, so only the endpoint's count of the log's bytes may decide.
/// </summary>
public sealed class KestrelSubmissionTests(KestrelApiFactory factory) : IClassFixture<KestrelApiFactory>
{
    [Fact]
    public async Task Submit_RejectsOversizedChunkedBody()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await factory.CreateSignedInClientAsync(cancellationToken);
        using var body = new UnknownLengthContent(GeneratedBodies.LogOfSize(RunsApiFactory.MaxSubmissionBytes + 1));

        using var response = await client.PostAsync("/runs", body, cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.RequestEntityTooLarge, "INPUT_TOO_LARGE", cancellationToken);
    }

    // The wire bytes stay bounded: Kestrel refuses a body whose framing is out of proportion, and that refusal is
    // answered like any body over the limit.
    [Fact]
    public async Task Submit_RefusesChunkedBodyWithExcessiveFraming()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await factory.CreateSignedInClientAsync(cancellationToken);
        using var body = new OneBytePerChunkContent(GeneratedBodies.LogOfSize(RunsApiFactory.MaxSubmissionBytes / 2));

        using var response = await client.PostAsync("/runs", body, cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.RequestEntityTooLarge, "INPUT_TOO_LARGE", cancellationToken);
    }

    [Fact]
    public async Task Submit_AcceptsChunkedBodyAtTheLimit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await factory.CreateSignedInClientAsync(cancellationToken);
        using var body = new UnknownLengthContent(GeneratedBodies.LogOfSize(RunsApiFactory.MaxSubmissionBytes));

        using var response = await client.PostAsync("/runs", body, cancellationToken);

        await RunsClient.AcceptedRunIdAsync(response, cancellationToken);
    }
}
