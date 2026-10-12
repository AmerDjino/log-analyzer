using System.Runtime.CompilerServices;

namespace LogAnalyzer.Infrastructure.Models;

/// <summary>
/// Keeps the retry-after header of the model service's latest answer in the current call, because the SDK's exceptions do
/// not expose response headers. A caller starts tracking before a try, and reads the box after it fails.
/// </summary>
internal sealed class RetryAfterHandler : DelegatingHandler
{
    private static readonly AsyncLocal<StrongBox<string?>?> Current = new();

    public RetryAfterHandler()
        : base(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
    {
    }

    /// <summary>A box that receives the retry-after header of answers sent from here on in this call.</summary>
    public static StrongBox<string?> Track()
    {
        var box = new StrongBox<string?>();
        Current.Value = box;
        return box;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (Current.Value is { } box)
        {
            box.Value = response.Headers.TryGetValues("retry-after", out var values) ? values.FirstOrDefault() : null;
        }

        return response;
    }
}
