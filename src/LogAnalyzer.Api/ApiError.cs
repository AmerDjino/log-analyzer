namespace LogAnalyzer.Api;

/// <summary>
/// An error the API reports: problem details (RFC 9457) with a <c>code</c> member that clients match on.
/// Each code has exactly one status, title and detail, so equal codes always produce equal answers.
/// </summary>
internal sealed record ApiError(string Code, int Status, string Title, string Detail)
{
    public IResult ToResult() =>
        TypedResults.Problem(
            detail: Detail,
            statusCode: Status,
            title: Title,
            extensions: new Dictionary<string, object?> { ["code"] = Code });
}
