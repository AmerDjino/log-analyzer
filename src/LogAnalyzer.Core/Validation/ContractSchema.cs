using System.Text.Json;
using System.Text.Json.Nodes;

namespace LogAnalyzer.Core.Validation;

/// <summary>
/// Checks a parsed response against the output contract's JSON Schema. It enforces exactly the keywords the contract
/// uses, which are also keywords Claude's structured outputs accept. A schema with any other keyword or type fails to
/// load, so the schema file can never promise a rule that the validator does not enforce.
/// </summary>
internal sealed class ContractSchema
{
    private static readonly HashSet<string> SupportedKeywords =
        ["type", "description", "properties", "required", "additionalProperties", "items", "enum", "minItems"];

    private static readonly HashSet<string> SupportedTypes = ["object", "array", "string", "integer"];

    private readonly JsonObject _schema;

    private ContractSchema(JsonObject schema) => _schema = schema;

    public static ContractSchema Load(string schemaJson)
    {
        var schema = JsonNode.Parse(schemaJson) as JsonObject
            ?? throw new InvalidOperationException("The output contract is not a JSON Schema object.");
        EnsureSupported(schema, "#");
        return new ContractSchema(schema);
    }

    /// <summary>
    /// Adds a SCHEMA_VIOLATION for each place where the response breaks the contract. Closed-set values match
    /// regardless of letter case, because structured outputs do not guarantee the case of enum values.
    /// </summary>
    public void Check(JsonNode? response, List<Violation> violations) =>
        Check(_schema, response, ResponsePath.Root, violations);

    private static void Check(JsonObject schema, JsonNode? value, string path, List<Violation> violations)
    {
        var type = (string)schema["type"]!;
        if (!HasType(value, type))
        {
            violations.Add(SchemaViolation(path, $"Expected {Describe(type)}, found {Describe(value)}."));
            return;
        }

        if (schema["enum"] is JsonArray allowed && !allowed.Any(option => IsSameText((string)option!, (string)value!)))
        {
            violations.Add(SchemaViolation(
                path, $"Expected one of {string.Join(", ", allowed.Select(option => (string)option!))}, found \"{(string)value!}\"."));
        }

        switch (value)
        {
            case JsonObject objectValue:
                CheckObject(schema, objectValue, path, violations);
                break;
            case JsonArray array:
                CheckArray(schema, array, path, violations);
                break;
        }
    }

    private static void CheckObject(JsonObject schema, JsonObject value, string path, List<Violation> violations)
    {
        foreach (var required in schema["required"]?.AsArray() ?? [])
        {
            var name = (string)required!;
            if (!value.ContainsKey(name))
            {
                violations.Add(SchemaViolation(ResponsePath.Field(path, name), "Required field is missing."));
            }
        }

        var properties = schema["properties"]?.AsObject();
        foreach (var (name, child) in value)
        {
            if (properties?[name] is JsonObject propertySchema)
            {
                Check(propertySchema, child, ResponsePath.Field(path, name), violations);
            }
            else
            {
                // EnsureSupported allows only additionalProperties: false, so every object is closed.
                violations.Add(SchemaViolation(ResponsePath.Field(path, name), "Unknown field: the output contract does not define it."));
            }
        }
    }

    private static void CheckArray(JsonObject schema, JsonArray value, string path, List<Violation> violations)
    {
        if (schema["minItems"] is JsonValue minimum && value.Count < minimum.GetValue<int>())
        {
            violations.Add(SchemaViolation(path, $"Expected at least {minimum.GetValue<int>()} item(s), found {value.Count}."));
        }

        if (schema["items"] is JsonObject itemSchema)
        {
            for (var index = 0; index < value.Count; index++)
            {
                Check(itemSchema, value[index], ResponsePath.Item(path, index), violations);
            }
        }
    }

    private static bool HasType(JsonNode? value, string type) => type switch
    {
        "object" => value is JsonObject,
        "array" => value is JsonArray,
        "string" => value is JsonValue jsonValue && jsonValue.GetValueKind() == JsonValueKind.String,
        "integer" => JsonNumbers.TryGetWholeNumber(value, out _),
        _ => false,
    };

    private static bool IsSameText(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static Violation SchemaViolation(string path, string message) => new(ReasonCodes.SchemaViolation, path, message);

    private static string Describe(string type) => type switch
    {
        "object" => "an object",
        "array" => "an array",
        "integer" => "a whole number",
        _ => "a string",
    };

    private static string Describe(JsonNode? value) => value switch
    {
        null => "null",
        JsonObject => "an object",
        JsonArray => "an array",
        _ => value.GetValueKind() switch
        {
            JsonValueKind.String => "a string",
            JsonValueKind.Number => JsonNumbers.TryGetWholeNumber(value, out _) ? "a whole number" : "a number with a fraction",
            _ => "true or false",
        },
    };

    private static void EnsureSupported(JsonObject schema, string location)
    {
        foreach (var (keyword, _) in schema)
        {
            if (!SupportedKeywords.Contains(keyword))
            {
                throw Unsupported(location, $"the keyword \"{keyword}\"");
            }
        }

        if (schema["type"] is not JsonValue type || type.GetValueKind() != JsonValueKind.String || !SupportedTypes.Contains((string)type!))
        {
            throw Unsupported(location, $"the type {schema["type"]?.ToJsonString() ?? "(none)"}");
        }

        if (schema["additionalProperties"] is { } additional && additional.GetValueKind() != JsonValueKind.False)
        {
            throw Unsupported(location, "additionalProperties other than false");
        }

        if (schema["enum"] is JsonArray values && values.Any(value => value?.GetValueKind() != JsonValueKind.String))
        {
            throw Unsupported(location, "an enum value that is not a string");
        }

        if (schema["properties"] is JsonObject properties)
        {
            foreach (var (name, propertySchema) in properties)
            {
                EnsureSupported(propertySchema!.AsObject(), $"{location}/properties/{name}");
            }
        }

        if (schema["items"] is JsonObject items)
        {
            EnsureSupported(items, $"{location}/items");
        }
    }

    private static InvalidOperationException Unsupported(string location, string what) =>
        new($"The output contract uses {what} at {location}, which the validator does not enforce.");
}
