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
public readonly record struct LogTextResult(string[] Lines, LogTextProblem? Problem);

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
    // The lines are counted first, so they go straight into an array of the right size: a log of very short lines
    // has millions of them.
    private static string[] SplitLines(string text)
    {
        var lines = new string[CountLines(text)];
        var line = 0;
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character is not ('\r' or '\n'))
            {
                continue;
            }

            lines[line++] = text[start..index];
            if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                index++;
            }

            start = index + 1;
        }

        if (start < text.Length)
        {
            lines[line] = text[start..];
        }

        return lines;
    }

    // One line per line break, where CRLF is one break, plus a last line that has none.
    private static int CountLines(string text)
    {
        var count = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n' || (text[index] == '\r' && (index + 1 == text.Length || text[index + 1] != '\n')))
            {
                count++;
            }
        }

        return text.Length > 0 && text[^1] is not ('\r' or '\n') ? count + 1 : count;
    }
}
