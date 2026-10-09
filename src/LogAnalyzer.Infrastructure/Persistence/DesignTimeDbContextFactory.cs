using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LogAnalyzer.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> create the context without the API's configuration.
/// Adding a migration never connects, so the connection string is only a placeholder.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LogAnalyzerDbContext>
{
    public LogAnalyzerDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<LogAnalyzerDbContext>()
            .UseNpgsql("Host=localhost;Database=loganalyzer")
            .UseSnakeCaseNamingConvention()
            .Options);
}
