using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LogAnalyzer.Core.Validation;

/// <summary>
/// Reads JSON numbers exactly from the text the model wrote, so a whole number is told apart from one with a tiny
/// fraction, and very large numbers are not rounded into range.
/// </summary>
internal static class JsonNumbers
{
    /// <summary>
    /// Whether the value is a whole number, as the schema's "integer" type means it: 12, 12.0 and 1.2e1 all are.
    /// A whole number beyond the range of <see cref="long"/> comes back as <see cref="long.MaxValue"/> or <see cref="long.MinValue"/>.
    /// </summary>
    public static bool TryGetWholeNumber(JsonNode? value, out long number)
    {
        number = 0;
        return value is JsonValue jsonValue
            && jsonValue.GetValueKind() == JsonValueKind.Number
            && jsonValue.TryGetValue(out JsonElement element)
            && TryParseWholeNumber(element.GetRawText(), out number);
    }

    /// <summary>The number as the model wrote it, for messages.</summary>
    public static string Text(JsonNode? value) =>
        value is JsonValue jsonValue && jsonValue.TryGetValue(out JsonElement element) ? element.GetRawText() : value?.ToJsonString() ?? "null";

    // The parser has already checked the JSON number grammar: -?digits(.digits)?([eE][+-]?digits)?
    private static bool TryParseWholeNumber(string text, out long number)
    {
        number = 0;
        var negative = text.StartsWith('-');
        var unsigned = negative ? text[1..] : text;
        var exponentAt = unsigned.IndexOfAny(['e', 'E']);
        var mantissa = exponentAt < 0 ? unsigned : unsigned[..exponentAt];
        var exponent = exponentAt < 0
            ? BigInteger.Zero
            : BigInteger.Parse(unsigned[(exponentAt + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var pointAt = mantissa.IndexOf('.');
        var fraction = pointAt < 0 ? string.Empty : mantissa[(pointAt + 1)..];

        // The value is digits × 10^scale, with no leading or trailing zeros in digits.
        var digits = mantissa.Replace(".", string.Empty, StringComparison.Ordinal).TrimStart('0');
        var significant = digits.TrimEnd('0');
        var scale = exponent - fraction.Length + (digits.Length - significant.Length);

        if (significant.Length == 0)
        {
            return true;
        }

        if (scale < 0)
        {
            return false;
        }

        // Up to 18 digits always fit in a long; anything longer is outside every range the validator checks.
        if (significant.Length + scale > 18)
        {
            number = negative ? long.MinValue : long.MaxValue;
            return true;
        }

        number = long.Parse(significant, CultureInfo.InvariantCulture) * (long)BigInteger.Pow(10, (int)scale);
        if (negative)
        {
            number = -number;
        }

        return true;
    }
}
