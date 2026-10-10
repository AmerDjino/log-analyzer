using System.Text;
using System.Text.Unicode;

namespace LogAnalyzer.Core.Ingestion;

/// <summary>Why submitted content cannot be accepted as a log.</summary>
public enum LogTextProblem
{
    /// <summary>The bytes are not valid UTF-8, or contain a NUL byte (UNSUPPORTED_CONTENT).</summary>
    UnsupportedContent,

    /// <summary>Nothing but white space is left (EMPTY_INPUT).</summary>
    Empty,
}

/// <summary>A submitted log read into lines, or why it cannot be accepted.</summary>
/// <param name="Lines">The log's lines, line N at index N - 1, without their line breaks. Empty when there is a problem.</param>
public readonly record struct LogTextResult(IReadOnlyList<string> Lines, LogTextProblem? Problem);

/// <summary>Reads a submitted log's bytes. docs/runs-api.md describes the rules.</summary>
public static class LogText
{
    private static ReadOnlySpan<byte> Utf8ByteOrderMark => [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// Checks the content, then splits it into lines. The checks run in a fixed order, so each submission gets one
    /// answer: invalid UTF-8 or a NUL byte first, then, after one leading byte order mark is removed, emptiness.
    /// </summary>
    public static LogTextResult Read(ReadOnlySpan<byte> content)
    {
        if (content.Contains((byte)0) || !Utf8.IsValid(content))
        {
            return new LogTextResult([], LogTextProblem.UnsupportedContent);
        }

        if (content.StartsWith(Utf8ByteOrderMark))
        {
            content = content[Utf8ByteOrderMark.Length..];
        }

        var text = Encoding.UTF8.GetString(content);
        return string.IsNullOrWhiteSpace(text)
            ? new LogTextResult([], LogTextProblem.Empty)
            : new LogTextResult(SplitLines(text), null);
    }

    // CRLF, LF and a lone CR each end a line. A line break at the very end does not start another line; every other
    // character, including U+2028 and NEL, stays in its line, as editors and File.ReadAllLines show it.
    private static List<string> SplitLines(string text)
    {
        var lines = new List<string>();
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character is not ('\r' or '\n'))
            {
                continue;
            }

            lines.Add(text[start..index]);
            if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                index++;
            }

            start = index + 1;
        }

        if (start < text.Length)
        {
            lines.Add(text[start..]);
        }

        return lines;
    }
}
