using System.Text;

namespace LogAnalyzer.Core.Ingestion;

/// <summary>Cleans the file name a person submits with an upload.</summary>
public static class FileNames
{
    public const int MaxLength = 255;

    /// <summary>
    /// The last path segment, without control characters, trimmed and cut to <see cref="MaxLength"/> characters
    /// (Unicode code points). Null when nothing is left, as for a paste.
    /// </summary>
    public static string? Clean(string? fileName)
    {
        if (fileName is null)
        {
            return null;
        }

        var lastSegment = fileName[(fileName.LastIndexOfAny(['/', '\\']) + 1)..];
        var cleaned = new StringBuilder(lastSegment.Length);
        foreach (var rune in lastSegment.EnumerateRunes())
        {
            if (!Rune.IsControl(rune))
            {
                cleaned.Append(rune.ToString());
            }
        }

        var trimmed = cleaned.ToString().Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        return trimmed.EnumerateRunes().Count() <= MaxLength
            ? trimmed
            : string.Concat(trimmed.EnumerateRunes().Take(MaxLength).Select(rune => rune.ToString()));
    }
}
