using System.Runtime.CompilerServices;

namespace LogAnalyzer.Evals;

/// <summary>
/// The committed golden logs and case files are what <see cref="GoldenLogGenerator"/> writes. Needs no key. With
/// UPDATE_GOLDEN_LOGS=1, the test rewrites them in the source folder instead.
/// </summary>
public sealed class GoldenLogTests
{
    [Fact]
    public void Generator_ReproducesTheCommittedFiles()
    {
        var update = Environment.GetEnvironmentVariable("UPDATE_GOLDEN_LOGS") == "1";
        var root = update ? SourceFolder() : AppContext.BaseDirectory;
        foreach (var golden in GoldenLogGenerator.Generate())
        {
            var logPath = Path.Combine(root, "GoldenLogs", golden.LogPath);
            var casePath = Path.Combine(root, "Cases", $"{golden.Scenario}.json");
            if (update)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                Directory.CreateDirectory(Path.GetDirectoryName(casePath)!);
                File.WriteAllText(logPath, golden.Log);
                File.WriteAllText(casePath, golden.CaseJson);
                continue;
            }

            // A Windows checkout may have CRLF line breaks; the content is the same.
            Assert.True(
                File.Exists(logPath) && Normalize(File.ReadAllText(logPath)) == golden.Log
                && File.Exists(casePath) && Normalize(File.ReadAllText(casePath)) == golden.CaseJson,
                $"{golden.Scenario} differs from what the generator writes. Run the test with UPDATE_GOLDEN_LOGS=1 to rewrite it.");
        }
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string SourceFolder([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
