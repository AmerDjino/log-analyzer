namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>Logs under Fixtures/logs. orders-api-40.log is shared with the unit tests' validation fixtures.</summary>
public static class FixtureLogs
{
    /// <summary>40 lines: line 12 reads "fail: Orders.Api[0] Database connection pool exhausted".</summary>
    public const string OrdersApi40 = "orders-api-40.log";

    /// <summary>10 lines: 3 at error level (<c>fail:</c>), 2 at warning level (<c>warn:</c>), 4 info and a stack frame.</summary>
    public const string Levels10 = "levels-10.log";

    /// <summary>4 lines without level markers or timestamps.</summary>
    public const string NoLevels = "no-levels.log";

    public static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "logs", name));
}
