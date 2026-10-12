namespace LogAnalyzer.Infrastructure.Models;

/// <summary>One version of the prompts: <c>prompts/&lt;version&gt;/system.md</c> and <c>correction.md</c>.</summary>
/// <param name="Version">Such as <c>log-analysis/v1</c>.</param>
public sealed record PromptTemplate(string Version, string System, string Correction);

/// <summary>The prompt templates, by version.</summary>
public interface IPromptTemplates
{
    /// <summary>The templates of this version, or null when there are none.</summary>
    PromptTemplate? Find(string version);
}

/// <summary>
/// The templates under <c>prompts/</c>, embedded in this assembly, so a version cannot change after the API is built. A
/// version is a folder that holds both <c>system.md</c> and <c>correction.md</c>.
/// </summary>
public sealed class EmbeddedPromptTemplates : IPromptTemplates
{
    // Each file is embedded as prompts/<version>/<file>, with backslashes when the API is built on Windows.
    private const string Prefix = "prompts/";

    private const string SystemFile = "/system.md";

    private const string CorrectionFile = "/correction.md";

    private static readonly Lazy<IReadOnlyDictionary<string, PromptTemplate>> Templates = new(Load);

    public PromptTemplate? Find(string version) => Templates.Value.GetValueOrDefault(version);

    private static Dictionary<string, PromptTemplate> Load()
    {
        var assembly = typeof(EmbeddedPromptTemplates).Assembly;
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            var path = resource.Replace('\\', '/');
            if (path.StartsWith(Prefix, StringComparison.Ordinal))
            {
                using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
                // A checkout on Windows may have CRLF line breaks; the text sent is the same either way.
                files[path[Prefix.Length..]] = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
            }
        }

        var templates = new Dictionary<string, PromptTemplate>(StringComparer.Ordinal);
        foreach (var (path, system) in files.Where(file => file.Key.EndsWith(SystemFile, StringComparison.Ordinal)))
        {
            var version = path[..^SystemFile.Length];
            if (files.TryGetValue(version + CorrectionFile, out var correction))
            {
                templates[version] = new PromptTemplate(version, system, correction);
            }
        }

        return templates;
    }
}
