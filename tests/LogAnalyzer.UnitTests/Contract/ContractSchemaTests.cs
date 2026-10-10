using System.Text.Json;
using System.Text.Json.Nodes;
using LogAnalyzer.Core.Contract;

namespace LogAnalyzer.UnitTests.Contract;

/// <summary>Keeps the contract types and schemas/analysis-result.schema.json describing the same fields and values.</summary>
public sealed class ContractSchemaTests
{
    private static readonly JsonObject AnalysisSchema = JsonNode.Parse(AnalysisContract.SchemaJson)!.AsObject();

    private static JsonObject FindingSchema => AnalysisSchema["properties"]!["findings"]!["items"]!.AsObject();

    [Fact]
    public void Schema_DefinesAndRequiresExactlyTheFieldsOfAnalysis()
    {
        Assert.Equal(FieldNames<Analysis>(), PropertyNames(AnalysisSchema));
        Assert.Equal(FieldNames<Analysis>(), RequiredNames(AnalysisSchema));
    }

    [Fact]
    public void Schema_DefinesAndRequiresExactlyTheFieldsOfFinding()
    {
        Assert.Equal(FieldNames<Finding>(), PropertyNames(FindingSchema));
        Assert.Equal(FieldNames<Finding>(), RequiredNames(FindingSchema));
    }

    [Fact]
    public void Schema_AllowsExactlyTheSeverities() =>
        Assert.Equal(EnumValues<Severity>(), AllowedValues(FindingSchema, "severity"));

    [Fact]
    public void Schema_AllowsExactlyTheCategories() =>
        Assert.Equal(EnumValues<FindingCategory>(), AllowedValues(FindingSchema, "category"));

    // The contract's field names are the record properties in snake_case, such as line_refs for LineRefs.
    private static string[] FieldNames<T>() =>
        [.. typeof(T).GetProperties().Select(property => JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name)).Order(StringComparer.Ordinal)];

    private static string[] PropertyNames(JsonObject schema) =>
        [.. schema["properties"]!.AsObject().Select(property => property.Key).Order(StringComparer.Ordinal)];

    private static string[] RequiredNames(JsonObject schema) =>
        [.. schema["required"]!.AsArray().Select(name => (string)name!).Order(StringComparer.Ordinal)];

    private static string[] EnumValues<T>()
        where T : struct, Enum =>
        [.. Enum.GetNames<T>().Select(name => name.ToLowerInvariant())];

    private static string[] AllowedValues(JsonObject schema, string property) =>
        [.. schema["properties"]![property]!["enum"]!.AsArray().Select(value => (string)value!)];
}
