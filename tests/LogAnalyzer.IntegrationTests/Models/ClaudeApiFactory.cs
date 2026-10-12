using System.Collections.Concurrent;
using LogAnalyzer.Infrastructure.Models;
using LogAnalyzer.Infrastructure.Runs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogAnalyzer.IntegrationTests.Models;

/// <summary>
/// The PostgreSQL-backed API with Claude as the provider, calling <see cref="FakeMessagesApi"/> instead of Anthropic,
/// with the fake key, a 5-second timeout and 2 retries. The real clock runs, so timeouts, delays and latency are real.
/// The worker is removed: a test submits, calls <see cref="ProcessQueuedRunsAsync"/> and reads. Prompt templates come
/// from <see cref="FixedPromptTemplates"/> with v1 and v2, so a test can switch versions without a shipped v2.
/// </summary>
public sealed class ClaudeApiFactory : PostgresApiFactory
{
    public FakeMessagesApi Messages { get; } = new();

    /// <summary>Every message the API logged, with its exception, from every category and level.</summary>
    public LogCapture Logs { get; } = new();

    /// <summary>Processes queued runs one at a time, oldest first, as the worker does, until none is left.</summary>
    public Task ProcessQueuedRunsAsync(CancellationToken cancellationToken) => ProcessQueuedRunsAsync(this, cancellationToken);

    /// <summary>Processes queued runs with the given host, as <see cref="ProcessQueuedRunsAsync(CancellationToken)"/> does.</summary>
    public static async Task ProcessQueuedRunsAsync(WebApplicationFactory<Program> host, CancellationToken cancellationToken)
    {
        var processor = host.Services.GetRequiredService<RunProcessor>();
        while (await processor.ProcessNextAsync(cancellationToken))
        {
        }
    }

    /// <summary>
    /// The API started again on this database, configured with another prompt version, as after a configuration change.
    /// It shares the fake Messages API and the log capture.
    /// </summary>
    public WebApplicationFactory<Program> WithPromptVersion(string promptVersion) =>
        new ClaudeHost(ConnectionString, this, promptVersion);

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            Messages.Dispose();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        Configure(builder, this, "log-analysis/v1");
    }

    // The settings and services every Claude host shares.
    private static void Configure(IWebHostBuilder builder, ClaudeApiFactory factory, string promptVersion)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Model:Provider"] = ModelProviders.Claude,
                ["Model:Claude:ApiKey"] = ClaudeTestSettings.ApiKey,
                ["Model:Claude:BaseUrl"] = factory.Messages.Url,
                ["Model:Claude:PromptVersion"] = promptVersion,
                ["Model:Claude:Timeout"] = "00:00:05",
                ["Model:Claude:MaxRetries"] = "2",
            }));
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(factory.Logs));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPromptTemplates>();
            services.AddSingleton<IPromptTemplates>(new FixedPromptTemplates("log-analysis/v1", "log-analysis/v2"));
            foreach (var worker in services.Where(IsRunWorker).ToList())
            {
                services.Remove(worker);
            }
        });
    }

    private static bool IsRunWorker(ServiceDescriptor service) =>
        service.ServiceType == typeof(IHostedService) && service.ImplementationType?.Name == "RunWorker";

    private sealed class ClaudeHost : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;

        private readonly ClaudeApiFactory _factory;

        private readonly string _promptVersion;

        public ClaudeHost(string connectionString, ClaudeApiFactory factory, string promptVersion)
        {
            _connectionString = connectionString;
            _factory = factory;
            _promptVersion = promptVersion;
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
            Configure(builder, _factory, _promptVersion);
        }
    }
}

/// <summary>Captures everything the API logs, as text, so a test can search it.</summary>
public sealed class LogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IReadOnlyList<string> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue($"{logLevel} {category}: {formatter(state, exception)} {exception}");
    }
}
