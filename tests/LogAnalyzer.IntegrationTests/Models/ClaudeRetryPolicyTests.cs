using LogAnalyzer.Infrastructure.Models;

namespace LogAnalyzer.IntegrationTests.Models;

/// <summary>Which answers of the model service are retried, and how long the adapter waits before each retry.</summary>
public sealed class ClaudeRetryPolicyTests
{
    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(529)]
    public void IsTransient_AcceptsRateLimitOverloadAndServerErrors(int status)
    {
        Assert.True(ClaudeRetryPolicy.IsTransient(status));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(413)]
    [InlineData(422)]
    public void IsTransient_RefusesOtherClientErrors(int status)
    {
        Assert.False(ClaudeRetryPolicy.IsTransient(status));
    }

    [Theory]
    [InlineData(1, 1.0)]
    [InlineData(2, 2.0)]
    [InlineData(3, 4.0)]
    [InlineData(5, 16.0)]
    [InlineData(6, 30.0)]
    [InlineData(10, 30.0)]
    public void Delay_DoublesFromOneSecondUpToThirty(int retry, double seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), ClaudeRetryPolicy.Delay(retry, retryAfter: null, jitter: 0.5));
    }

    [Theory]
    [InlineData(0.0, 0.8)]
    [InlineData(0.999999, 1.2)]
    public void Delay_VariesByUpToAFifth(double jitter, double seconds)
    {
        Assert.Equal(seconds, ClaudeRetryPolicy.Delay(1, retryAfter: null, jitter).TotalSeconds, precision: 3);
    }

    [Fact]
    public void Delay_FollowsRetryAfter()
    {
        Assert.Equal(TimeSpan.FromSeconds(3), ClaudeRetryPolicy.Delay(1, retryAfter: TimeSpan.FromSeconds(3), jitter: 0.5));
    }

    [Fact]
    public void Delay_CapsRetryAfterAtThirtySeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), ClaudeRetryPolicy.Delay(1, retryAfter: TimeSpan.FromMinutes(5), jitter: 0.5));
    }

    [Theory]
    [InlineData("3", 3.0)]
    [InlineData("0.5", 0.5)]
    [InlineData(" 2 ", 2.0)]
    public void ParseRetryAfter_ReadsSeconds(string header, double seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), ClaudeRetryPolicy.ParseRetryAfter(header));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("soon")]
    [InlineData("-1")]
    public void ParseRetryAfter_IgnoresWhatIsNotANumberOfSeconds(string? header)
    {
        Assert.Null(ClaudeRetryPolicy.ParseRetryAfter(header));
    }
}
