namespace LogAnalyzer.Core.Contract;

/// <summary>The contract's spelling of severities and categories: lowercase, as in the schema, and as they are stored and returned.</summary>
public static class ContractValues
{
    public static string ToContractValue(this Severity severity) => Spelling(severity);

    public static string ToContractValue(this FindingCategory category) => Spelling(category);

    // Every member name is a single word, so its lowercase form is the contract's spelling. ContractSchemaTests checks it against the schema.
    private static string Spelling<T>(T value)
        where T : struct, Enum =>
        Enum.IsDefined(value)
            ? value.ToString().ToLowerInvariant()
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"Not a defined {typeof(T).Name}.");
}
