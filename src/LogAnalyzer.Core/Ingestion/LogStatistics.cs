using System.Globalization;
using System.Text.RegularExpressions;

namespace LogAnalyzer.Core.Ingestion;

/// <summary>A line's log level. A line has at most one.</summary>
public enum LineLevel
{
    Critical,
    Error,
    Warning,
    Info,
    Debug,
    Trace,
}

/// <summary>A timestamp found on a line: the date and time as written, and the UTC offset if the line has one.</summary>
public readonly record struct LineTimestamp(DateTime Time, TimeSpan? Offset);

/// <summary>The number of lines at each level.</summary>
public sealed record LevelCounts(int Critical, int Error, int Warning, int Info, int Debug, int Trace);

/// <summary>
/// The earliest and latest timestamps of a log. With <paramref name="Utc"/> true both are UTC instants; otherwise
/// both are times as the log wrote them, without an offset.
/// </summary>
public sealed record TimeRange(DateTime Start, DateTime End, bool Utc);

/// <summary>Finds a line's level. docs/runs-api.md lists the recognized forms.</summary>
public static partial class LineLevels
{
    // A marker counts only when it starts within this many characters, so message text further on cannot set the level.
    private const int MarkerStartLimit = 100;

    // Enough of the line to hold a marker that starts just before the limit.
    private const int ScanLength = MarkerStartLimit + 60;

    private static readonly Dictionary<string, LineLevel> LevelNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["crit"] = LineLevel.Critical, ["critical"] = LineLevel.Critical, ["fatal"] = LineLevel.Critical, ["ftl"] = LineLevel.Critical,
        ["fail"] = LineLevel.Error, ["error"] = LineLevel.Error, ["err"] = LineLevel.Error, ["severe"] = LineLevel.Error,
        ["warn"] = LineLevel.Warning, ["warning"] = LineLevel.Warning, ["wrn"] = LineLevel.Warning,
        ["info"] = LineLevel.Info, ["information"] = LineLevel.Info, ["inf"] = LineLevel.Info,
        ["dbug"] = LineLevel.Debug, ["debug"] = LineLevel.Debug, ["dbg"] = LineLevel.Debug,
        ["trce"] = LineLevel.Trace, ["trace"] = LineLevel.Trace, ["trc"] = LineLevel.Trace, ["verbose"] = LineLevel.Trace, ["vrb"] = LineLevel.Trace,
    };

    /// <summary>The level of the leftmost marker that starts within the line's first 100 characters, if any.</summary>
    public static LineLevel? Detect(string line)
    {
        var window = line.Length <= ScanLength ? line : line[..ScanLength];
        for (var match = Marker().Match(window); match.Success && match.Index < MarkerStartLimit; match = match.NextMatch())
        {
            var name = match.Groups["prefix"].Success ? match.Groups["prefix"].Value
                : match.Groups["bracketed"].Success ? match.Groups["bracketed"].Value
                : match.Groups["capitals"].Success ? match.Groups["capitals"].Value
                : match.Groups["keyed"].Value;
            if (LevelNames.TryGetValue(name, out var level))
            {
                return level;
            }
        }

        return null;
    }

    // Four forms, tried left to right along the line:
    // a .NET console prefix in lowercase, followed by a colon ("fail:");
    // a level name as the last word inside square brackets, in any case ("[ERR]", "[09:00:01 WRN]");
    // a level name in capitals as a whole word ("ERROR");
    // a key followed by a level name, in any case ("level=error", "\"@l\":\"Error\"").
    [GeneratedRegex(
        """(?:^|(?<=\s))(?<prefix>trce|dbug|info|warn|fail|crit):|\[(?:[^\[\]]*\s)?(?<bracketed>[A-Za-z]+)\]|\b(?<capitals>FATAL|CRITICAL|CRIT|ERROR|SEVERE|WARNING|WARN|INFORMATION|INFO|DEBUG|TRACE|VERBOSE)\b|(?:\b(?i:level|lvl|severity)="?|"(?i:level|@l|loglevel|severity)"\s*:\s*")(?<keyed>[A-Za-z]+)""",
        RegexOptions.CultureInvariant)]
    private static partial Regex Marker();
}

/// <summary>Finds a line's timestamp. docs/runs-api.md lists the recognized forms.</summary>
public static partial class LineTimestamps
{
    // A timestamp counts only when it starts within this many characters, so dates inside messages do not.
    private const int TimestampStartLimit = 40;

    private static readonly TimeSpan MaxOffset = TimeSpan.FromHours(14);

    /// <summary>The first ISO 8601 date and time that starts within the line's first 40 characters, if any.</summary>
    public static LineTimestamp? Find(string line)
    {
        for (var match = Timestamp().Match(line); match.Success && match.Index < TimestampStartLimit; match = match.NextMatch())
        {
            if (TryRead(match, out var timestamp))
            {
                return timestamp;
            }
        }

        return null;
    }

    // Impossible dates and times are not timestamps, and neither is one whose UTC value falls outside DateTime's range.
    private static bool TryRead(Match match, out LineTimestamp timestamp)
    {
        timestamp = default;
        var dateTimeText = $"{match.Groups["date"].Value}T{match.Groups["time"].Value}";
        if (!DateTime.TryParseExact(dateTimeText, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return false;
        }

        if (match.Groups["fraction"].Success)
        {
            // Kept to 100 ns, the resolution of DateTime.
            var digits = match.Groups["fraction"].Value.PadRight(7, '0')[..7];
            time = time.AddTicks(long.Parse(digits, CultureInfo.InvariantCulture));
        }

        TimeSpan? offset = null;
        if (match.Groups["offset"].Success)
        {
            if (!TryReadOffset(match.Groups["offset"].Value, out var value))
            {
                return false;
            }

            var utcTicks = time.Ticks - value.Ticks;
            if (utcTicks < DateTime.MinValue.Ticks || utcTicks > DateTime.MaxValue.Ticks)
            {
                return false;
            }

            offset = value;
        }

        timestamp = new LineTimestamp(time, offset);
        return true;
    }

    private static bool TryReadOffset(string text, out TimeSpan offset)
    {
        offset = TimeSpan.Zero;
        if (text == "Z")
        {
            return true;
        }

        var digits = text[1..].Replace(":", string.Empty, StringComparison.Ordinal);
        var hours = int.Parse(digits[..2], CultureInfo.InvariantCulture);
        var minutes = int.Parse(digits[2..], CultureInfo.InvariantCulture);
        if (minutes > 59)
        {
            return false;
        }

        offset = new TimeSpan(hours, minutes, 0);
        if (offset > MaxOffset)
        {
            return false;
        }

        if (text[0] == '-')
        {
            offset = offset.Negate();
        }

        return true;
    }

    // yyyy-MM-dd, then T or a space, then HH:mm:ss, an optional fraction after . or , and an optional offset
    // (Z, ±HH:mm or ±HHmm) that may follow one space, as Serilog writes it.
    [GeneratedRegex(
        @"(?<![0-9])(?<date>[0-9]{4}-[0-9]{2}-[0-9]{2})[T ](?<time>[0-9]{2}:[0-9]{2}:[0-9]{2})(?:[.,](?<fraction>[0-9]{1,9}))?(?:\s?(?<offset>Z|[+-][0-9]{2}:?[0-9]{2}))?",
        RegexOptions.CultureInvariant)]
    private static partial Regex Timestamp();
}

/// <summary>Statistics about a log, computed by code and never taken from model output.</summary>
public sealed record LogStatistics(int LineCount, LevelCounts Levels, TimeRange? TimeRange)
{
    /// <summary>
    /// Counts the lines at each level, and finds the time range: the earliest and latest timestamps of the kind the
    /// first timestamp has. Timestamps with an offset are compared as instants, those without one as written.
    /// </summary>
    public static LogStatistics Compute(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var counts = new int[Enum.GetValues<LineLevel>().Length];
        bool? withOffset = null;
        DateTime? earliest = null, latest = null;

        foreach (var line in lines)
        {
            if (LineLevels.Detect(line) is { } level)
            {
                counts[(int)level]++;
            }

            if (LineTimestamps.Find(line) is not { } timestamp)
            {
                continue;
            }

            var hasOffset = timestamp.Offset is not null;
            withOffset ??= hasOffset;
            if (hasOffset != withOffset)
            {
                continue;
            }

            var value = hasOffset
                ? new DateTime(timestamp.Time.Ticks - timestamp.Offset!.Value.Ticks, DateTimeKind.Utc)
                : timestamp.Time;
            if (earliest is null || value < earliest)
            {
                earliest = value;
            }

            if (latest is null || value > latest)
            {
                latest = value;
            }
        }

        var levels = new LevelCounts(
            counts[(int)LineLevel.Critical],
            counts[(int)LineLevel.Error],
            counts[(int)LineLevel.Warning],
            counts[(int)LineLevel.Info],
            counts[(int)LineLevel.Debug],
            counts[(int)LineLevel.Trace]);
        var timeRange = withOffset is { } utc ? new TimeRange(earliest!.Value, latest!.Value, utc) : null;
        return new LogStatistics(lines.Count, levels, timeRange);
    }
}
