using LogAnalyzer.Core.Processing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>
/// The API started again against an existing database, as after a restart: the real worker, the real clock, and the
/// stand-in model unless a test gives another. Disposing it stops the API gracefully.
/// </summary>
public sealed class RestartedApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    private readonly IAnalysisModel? _model;

    public RestartedApiFactory(string connectionString, IAnalysisModel? model = null)
    {
        _connectionString = connectionString;
        _model = model;
        // https, so the client keeps Secure cookies and sends them back.
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _connectionString,
            }));
        if (_model is { } model)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAnalysisModel>();
                services.AddSingleton<IAnalysisModel>(model);
            });
        }
    }
}

/// <summary>A model that never answers: each call waits until it is cancelled. <see cref="Called"/> completes at the first call.</summary>
public sealed class BlockingAnalysisModel : IAnalysisModel
{
    private readonly TaskCompletionSource _called = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Called => _called.Task;

    public string ConfiguredModel => "blocking";

    public string ConfiguredPromptVersion => "blocking";

    public async Task<ModelReply> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        _called.TrySetResult();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("An infinite delay ended.");
    }
}
