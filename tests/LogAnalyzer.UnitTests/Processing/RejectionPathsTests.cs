using System.Text.RegularExpressions;
using LogAnalyzer.Core.Processing;
using LogAnalyzer.Core.Validation;

namespace LogAnalyzer.UnitTests.Processing;

/// <summary>
/// Rejection paths as stored with an attempt: contract field names and array indexes are kept, and every other name is
/// stored as *, because the model wrote it.
/// </summary>
public sealed partial class RejectionPathsTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("summary", "summary")]
    [InlineData("findings", "findings")]
    [InlineData("findings[0]", "findings[0]")]
    [InlineData("findings[2].line_refs[13]", "findings[2].line_refs[13]")]
    [InlineData("findings[0].suggested_action", "findings[0].suggested_action")]
    [InlineData("findings[0][\"max tokens\"]", "findings[0].*")]
    [InlineData("findings[0].confidence", "findings[0].*")]
    [InlineData("confidence", "*")]
    [InlineData("[\"max tokens\"]", "*")]
    [InlineData("findings[1][\"a]b.c[0]\\\"d\"]", "findings[1].*")]
    public void Mask_KeepsOnlyContractNamesAndIndexes(string path, string expected) =>
        Assert.Equal(expected, RejectionPaths.Mask(path));

    // Paths the validator reports for names a model made up, however odd, are stored without any of their text.
    [Fact]
    public void Mask_LeavesNoNameTheModelWrote()
    {
        string[] lines = ["fail: Orders.Api[0] Database connection pool exhausted"];
        var response =
            """
            {"summary":"x","findings":[{"title":"t","severity":"error","category":"other","line_refs":[1],
            "evidence":"Database connection pool exhausted","explanation":"e","suggested_action":"s",
            "secret.name":1,"[0]":2,"quo\"te":3,"line_refs2":4,"ünïcode":5}],"extra field":true,"other":{"nested":1}}
            """;

        var result = ResponseValidator.Validate(new ValidationInput(response, StopReason.Completed, lines));

        Assert.Equal(7, result.Violations.Count);
        Assert.All(result.Violations, violation => Assert.Matches(StoredPath(), RejectionPaths.Mask(violation.Path)));
    }

    [GeneratedRegex(
        @"^((summary|findings|title|severity|category|line_refs|evidence|explanation|suggested_action|\*)(\[\d+\])*(\.(summary|findings|title|severity|category|line_refs|evidence|explanation|suggested_action|\*)(\[\d+\])*)*)?$")]
    private static partial Regex StoredPath();
}
