using System.Text;

namespace LogAnalyzer.Core.Processing;

/// <summary>
/// The path of a rejection reason as stored with its attempt. Contract field names and array indexes are kept; every
/// other name was written by the model, so it is stored as <c>*</c>: <c>findings[0]["max tokens"]</c> becomes
/// <c>findings[0].*</c>. Anything this class cannot read is masked too, so model text is never stored.
/// </summary>
public static class RejectionPaths
{
    private static readonly HashSet<string> ContractNames =
    [
        "summary", "findings", "title", "severity", "category", "line_refs", "evidence", "explanation", "suggested_action",
    ];

    /// <summary>Masks a validator path, such as <c>findings[0].line_refs[2]</c>; the empty path stays empty.</summary>
    public static string Mask(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var masked = new StringBuilder(path.Length);
        var position = 0;
        while (position < path.Length)
        {
            if (path[position] == '[' && TryReadIndex(path, position, out var index, out var next))
            {
                masked.Append('[').Append(index).Append(']');
                position = next;
            }
            else if (path[position] == '[' && TrySkipQuotedName(path, position, out next))
            {
                AppendName(masked, "*");
                position = next;
            }
            else if (path[position] == '[')
            {
                // Not a form the validator writes: mask the rest rather than risk storing part of it.
                AppendName(masked, "*");
                break;
            }
            else
            {
                if (path[position] == '.')
                {
                    position++;
                }

                var end = position;
                while (end < path.Length && path[end] is not ('.' or '['))
                {
                    end++;
                }

                var name = path[position..end];
                AppendName(masked, ContractNames.Contains(name) ? name : "*");
                position = end;
            }
        }

        return masked.ToString();
    }

    private static void AppendName(StringBuilder masked, string name)
    {
        if (masked.Length > 0)
        {
            masked.Append('.');
        }

        masked.Append(name);
    }

    // [123]
    private static bool TryReadIndex(string path, int start, out int index, out int next)
    {
        index = 0;
        next = start + 1;
        while (next < path.Length && char.IsAsciiDigit(path[next]))
        {
            next++;
        }

        if (next == start + 1 || next >= path.Length || path[next] != ']'
            || !int.TryParse(path.AsSpan(start + 1, next - start - 1), out index))
        {
            return false;
        }

        next++;
        return true;
    }

    // ["a JSON string, with escapes such as \" and \\"]
    private static bool TrySkipQuotedName(string path, int start, out int next)
    {
        next = start + 1;
        if (next >= path.Length || path[next] != '"')
        {
            return false;
        }

        for (next++; next < path.Length; next++)
        {
            if (path[next] == '\\')
            {
                next++;
            }
            else if (path[next] == '"')
            {
                next++;
                if (next < path.Length && path[next] == ']')
                {
                    next++;
                    return true;
                }

                return false;
            }
        }

        return false;
    }
}
