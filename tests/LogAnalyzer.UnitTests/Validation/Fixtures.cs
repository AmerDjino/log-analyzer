using LogAnalyzer.Core.Validation;

namespace LogAnalyzer.UnitTests.Validation;

/// <summary>
/// Validation inputs built from the canned model responses and logs under Fixtures/. A response is named after its
/// scenario id, such as "log-analysis/complete-finding-is-accepted.json"; responses that are deliberately not one JSON
/// object are .txt files.
/// </summary>
internal static class Fixtures
{
    /// <summary>
    /// 40 lines. Line 1 reads "Unhandled exception. System.NullReferenceException", line 12
    /// "fail: Orders.Api[0] Database connection pool exhausted" and line 30
    /// "warn: Orders.Api[0] Payment gateway responded in 8432 ms".
    /// </summary>
    public const string OrdersLog = "orders-api-40.log";

    /// <summary>120 lines, for a citation past the end of a longer log.</summary>
    public const string LongOrdersLog = "orders-api-120.log";

    public static ValidationInput Input(string response, string log = OrdersLog, StopReason stopReason = StopReason.Completed) =>
        new(Response(response), stopReason, LogLines(log));

    public static string Response(string name) => File.ReadAllText(PathOf("responses", name));

    public static IReadOnlyList<string> LogLines(string name) => File.ReadAllLines(PathOf("logs", name));

    private static string PathOf(string folder, string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", folder, name);
}
