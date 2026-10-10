using LogAnalyzer.Core.Ingestion;

namespace LogAnalyzer.UnitTests.Ingestion;

/// <summary>The timestamp-format table and the time-range rules from design.md, and how they add up to statistics.</summary>
public sealed class LogStatisticsTests
{
    [Theory]
    [InlineData("2026-10-10T09:00:01Z fail: boom", "2026-10-10T09:00:01", "00:00")]
    [InlineData("2026-10-10T09:00:01.123+02:00 info: started", "2026-10-10T09:00:01.123", "02:00")]
    [InlineData("2026-10-10 09:15:02.123 +02:00 [ERR] Database unavailable", "2026-10-10T09:15:02.123", "02:00")]
    [InlineData("2026-10-10 09:15:02.1234567-0500 WARN x", "2026-10-10T09:15:02.1234567", "-05:00")]
    [InlineData("2026-10-10 09:15:02,123 ERROR [main] boom", "2026-10-10T09:15:02.123", null)]
    [InlineData("{\"@t\":\"2026-10-10T09:00:01.1234567Z\",\"@l\":\"Error\"}", "2026-10-10T09:00:01.1234567", "00:00")]
    [InlineData("[2026-10-10 09:00:01] INFO started", "2026-10-10T09:00:01", null)]
    public void Find_ReadsIso8601DateAndTime(string line, string expectedValue, string? expectedOffset)
    {
        var timestamp = LineTimestamps.Find(line);

        Assert.NotNull(timestamp);
        Assert.Equal(DateTime.Parse(expectedValue, System.Globalization.CultureInfo.InvariantCulture), timestamp.Value.Time);
        Assert.Equal(
            expectedOffset is null ? null : TimeSpan.Parse(expectedOffset, System.Globalization.CultureInfo.InvariantCulture),
            timestamp.Value.Offset);
    }

    [Theory]
    [InlineData("2026-13-10 09:00:01 month 13")]
    [InlineData("2026-10-10 25:00:00 hour 25")]
    [InlineData("09:00:01 time only")]
    [InlineData("Oct 10 09:00:01 syslog")]
    [InlineData("order placed for delivery on the date of 2026-10-10 09:00:01")]
    public void Find_IgnoresImpossibleDatesTimeOnlyStampsAndLateDates(string line) =>
        Assert.Null(LineTimestamps.Find(line));

    [Fact]
    public void Compute_CountsLinesAtEachLevel()
    {
        string[] lines =
        [
            "info: a", "fail: b", "info: c", "warn: d", "fail: e",
            "   at Stack.Frame()", "info: f", "warn: g", "fail: h", "info: i",
        ];

        var statistics = LogStatistics.Compute(lines);

        Assert.Equal(10, statistics.LineCount);
        Assert.Equal(new LevelCounts(Critical: 0, Error: 3, Warning: 2, Info: 4, Debug: 0, Trace: 0), statistics.Levels);
    }

    [Fact]
    public void Compute_ReportsNoTimeRangeWithoutTimestamps()
    {
        var statistics = LogStatistics.Compute(["plain text", "more text"]);

        Assert.Equal(new LevelCounts(0, 0, 0, 0, 0, 0), statistics.Levels);
        Assert.Null(statistics.TimeRange);
    }

    [Fact]
    public void Compute_SpansTheEarliestAndLatestInstants()
    {
        var statistics = LogStatistics.Compute(
            ["2026-10-10T09:00:05Z a", "2026-10-10T11:00:01+02:00 b", "2026-10-10T09:00:03Z c"]);

        Assert.Equal(new TimeRange(Utc("2026-10-10T09:00:01"), Utc("2026-10-10T09:00:05"), Utc: true), statistics.TimeRange);
        Assert.Equal(DateTimeKind.Utc, statistics.TimeRange!.Start.Kind);
    }

    [Fact]
    public void Compute_KeepsTimesWithoutAnOffsetAsWritten()
    {
        var statistics = LogStatistics.Compute(["2026-10-10 09:00:05 a", "2026-10-10 09:00:01 b"]);

        Assert.Equal(new TimeRange(Local("2026-10-10T09:00:01"), Local("2026-10-10T09:00:05"), Utc: false), statistics.TimeRange);
        Assert.Equal(DateTimeKind.Unspecified, statistics.TimeRange!.Start.Kind);
    }

    // The first timestamp decides the kind; timestamps of the other kind are ignored.
    [Fact]
    public void Compute_IgnoresTimestampsOfTheOtherKind()
    {
        var withoutOffsetFirst = LogStatistics.Compute(
            ["2026-10-10 09:00:05 a", "2026-10-10T08:00:00Z b", "2026-10-10 09:00:07 c"]);
        var withOffsetFirst = LogStatistics.Compute(
            ["2026-10-10T09:00:05Z a", "2026-10-10 23:00:00 b", "2026-10-10T09:00:07Z c"]);

        Assert.Equal(new TimeRange(Local("2026-10-10T09:00:05"), Local("2026-10-10T09:00:07"), Utc: false), withoutOffsetFirst.TimeRange);
        Assert.Equal(new TimeRange(Utc("2026-10-10T09:00:05"), Utc("2026-10-10T09:00:07"), Utc: true), withOffsetFirst.TimeRange);
    }

    private static DateTime Utc(string value) =>
        DateTime.SpecifyKind(DateTime.Parse(value, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);

    private static DateTime Local(string value) =>
        DateTime.SpecifyKind(DateTime.Parse(value, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Unspecified);
}
