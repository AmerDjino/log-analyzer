namespace LogAnalyzer.Core.Contract;

/// <summary>
/// The output contract, schemas/analysis-result.schema.json, embedded in this assembly. The validator checks every
/// model response against it, and from add-claude-integration on it is sent to the model as the output format.
/// </summary>
public static class AnalysisContract
{
    private const string SchemaResourceName = "LogAnalyzer.Core.Contract.analysis-result.schema.json";

    /// <summary>The schema's JSON text, exactly as in the file.</summary>
    public static string SchemaJson { get; } = ReadSchema();

    private static string ReadSchema()
    {
        using var stream = typeof(AnalysisContract).Assembly.GetManifestResourceStream(SchemaResourceName)
            ?? throw new InvalidOperationException($"The embedded resource '{SchemaResourceName}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
