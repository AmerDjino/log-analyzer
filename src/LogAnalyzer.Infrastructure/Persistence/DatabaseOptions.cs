namespace LogAnalyzer.Infrastructure.Persistence;

/// <summary>Connection settings for the PostgreSQL database.</summary>
public sealed class DatabaseOptions
{
    /// <summary>Name of the connection string in configuration: <c>ConnectionStrings:Postgres</c>.</summary>
    public const string ConnectionStringName = "Postgres";

    /// <summary>Npgsql connection string.</summary>
    public string ConnectionString { get; set; } = string.Empty;
}
