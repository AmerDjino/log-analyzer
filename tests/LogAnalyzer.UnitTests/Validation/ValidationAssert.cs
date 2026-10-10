using LogAnalyzer.Core.Contract;
using LogAnalyzer.Core.Validation;

namespace LogAnalyzer.UnitTests.Validation;

internal static class ValidationAssert
{
    public static Analysis Accepted(ValidationResult result)
    {
        Assert.True(
            result.IsAccepted,
            $"Expected the response to be accepted, but it was rejected: {string.Join("; ", result.Violations)}");
        Assert.Empty(result.Violations);
        return result.Analysis;
    }

    /// <summary>Asserts that the response was rejected with exactly these violations, in this order, each with a message.</summary>
    public static void Rejected(ValidationResult result, params (string Code, string Path)[] expected)
    {
        Assert.False(result.IsAccepted, "Expected the response to be rejected, but it was accepted.");
        Assert.Null(result.Analysis);
        Assert.Equal(expected, result.Violations.Select(violation => (violation.Code, violation.Path)));
        Assert.All(result.Violations, violation => Assert.False(string.IsNullOrWhiteSpace(violation.Message)));
    }
}
