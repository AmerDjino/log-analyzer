using System.Text.Json;

namespace LogAnalyzer.Core.Validation;

/// <summary>Builds the violation paths, such as <c>findings[0].line_refs[0]</c>. The response as a whole is the empty path.</summary>
internal static class ResponsePath
{
    public const string Root = "";

    public static string Field(string path, string name)
    {
        // A name that is not a plain identifier, such as an unknown field "max tokens", is quoted: findings[0]["max tokens"].
        if (!IsPlainName(name))
        {
            return $"{path}[{JsonSerializer.Serialize(name)}]";
        }

        return path.Length == 0 ? name : $"{path}.{name}";
    }

    public static string Item(string path, int index) => $"{path}[{index}]";

    private static bool IsPlainName(string name) =>
        name.Length > 0
        && !char.IsAsciiDigit(name[0])
        && name.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
}
