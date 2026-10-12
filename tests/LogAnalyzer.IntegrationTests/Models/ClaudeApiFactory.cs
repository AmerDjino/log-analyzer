using System.Collections.Concurrent;
using System.Globalization;
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
/// with the fake key and 2 retries. The real clock runs, so timeouts, delays and latency are real. The timeout is
/// 30 seconds, so a fake slowed down by a busy test machine does not time a try out; a test of the timeout starts a host
/// of its own with <see cref="CreateHost"/>. The worker is removed: a test submits, calls
/// <see cref="ProcessQueuedRunsAsync(CancellationToken)"/> and reads. Prompt templates come from
/// <see cref="FixedPromptTemplates"/> with v1 and v2, so a test can switch versions without a shipped v2.
/// </summary>
public sealed class ClaudeApiFactory : PostgresApiFactory
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

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
    /// The API started again on this database with other settings, as after a configuration change. It shares the fake
    /// Messages API, unless given another address, and the log capture.
    /// </summary>
    public WebApplicationFactory<Program> CreateHost(
        string promptVersion = "log-analysis/v1", string? baseUrl = null, TimeSpan? timeout = null) =>
        new ClaudeHost(ConnectionString, this, new HostSettings(promptVersion, baseUrl ?? Messages.Url, timeout ?? DefaultTimeout));

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
        Configure(builder, this, new HostSettings("log-analysis/v1", Messages.Url, DefaultTimeout));
    }

    // The settings and services every Claude host shares.
    private static void Configure(IWebHostBuilder builder, ClaudeApiFactory factory, HostSettings settings)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Model:Provider"] = ModelProviders.Claude,
                ["Model:Claude:ApiKey"] = ClaudeTestSettings.ApiKey,
                ["Model:Claude:BaseUrl"] = settings.BaseUrl,
                ["Model:Claude:PromptVersion"] = settings.PromptVersion,
                ["Model:Claude:Timeout"] = settings.Timeout.ToString("c", CultureInfo.InvariantCulture),
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

    private sealed record HostSettings(string PromptVersion, string BaseUrl, TimeSpan Timeout);

    private sealed class ClaudeHost : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;

        private readonly ClaudeApiFactory _factory;

        private readonly HostSettings _settings;

        public ClaudeHost(string connectionString, ClaudeApiFactory factory, HostSettings settings)
        {
            _connectionString = connectionString;
            _factory = factory;
            _settings = settings;
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
            Configure(builder, _factory, _settings);
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
