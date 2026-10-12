using System.Globalization;

namespace LogAnalyzer.Infrastructure.Models;

/// <summary>
/// Which model service answers are retried, and how long to wait before each retry. Rate limiting (429), overload (529)
/// and other server errors (5xx) may pass, and so may a timeout or a connection failure. Any other client error, such as
/// a rejected key (401) or a bad request (400), cannot be fixed by sending the request again.
/// </summary>
public static class ClaudeRetryPolicy
{
    public static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

    public static bool IsTransient(int status) => status is 429 or >= 500;

    /// <summary>
    /// How long to wait before retry number <paramref name="retry"/>, from 1: the service's retry-after when it gave one,
    /// otherwise 1 second, doubling with each retry, varied by up to a fifth either way so clients do not retry in step.
    /// Never more than 30 seconds.
    /// </summary>
    /// <param name="jitter">A number from 0 up to 1, such as <see cref="Random.NextDouble"/>.</param>
    public static TimeSpan Delay(int retry, TimeSpan? retryAfter, double jitter)
    {
        if (retryAfter is { } hint)
        {
            return hint < MaxDelay ? hint : MaxDelay;
        }

        var backoff = FirstDelay * Math.Pow(2, Math.Min(retry - 1, 10));
        return backoff >= MaxDelay ? MaxDelay : backoff * (0.8 + (0.4 * jitter));
    }

    /// <summary>A retry-after header given in seconds, or null when it is missing or not a number of seconds.</summary>
    public static TimeSpan? ParseRetryAfter(string? header) =>
        double.TryParse(header, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
            CultureInfo.InvariantCulture, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : null;
}
