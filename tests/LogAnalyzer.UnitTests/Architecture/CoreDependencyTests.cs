using LogAnalyzer.Core;

namespace LogAnalyzer.UnitTests.Architecture;

/// <summary>
/// Keeps LogAnalyzer.Core free of infrastructure, so its validation and orchestration logic
/// can always be tested without a network, a database or a model.
/// </summary>
public sealed class CoreDependencyTests
{
    private static readonly string[] ForbiddenPrefixes =
    [
        "LogAnalyzer.",
        "Npgsql",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Anthropic",
    ];

    [Fact]
    public void Core_DoesNotReferenceOtherProjectsOrInfrastructurePackages()
    {
        var violations = typeof(CoreAssembly).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => ForbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(violations);
    }
}
