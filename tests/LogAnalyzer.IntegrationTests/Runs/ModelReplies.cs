using System.Text.Json;

namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>Model responses in the output contract's JSON, built per test.</summary>
public static class ModelReplies
{
    /// <summary>A response with this summary and these findings, built with <see cref="Finding"/>.</summary>
    public static string Analysis(string summary, params object[] findings) =>
        JsonSerializer.Serialize(new { summary, findings });

    public static object Finding(
        string title,
        string severity,
        string category,
        int[] lineRefs,
        string evidence,
        string explanation = "The log shows the problem on the cited lines.",
        string suggestedAction = "Check the cited lines and the service they name.") =>
        new
        {
            title,
            severity,
            category,
            line_refs = lineRefs,
            evidence,
            explanation,
            suggested_action = suggestedAction,
        };

    /// <summary>A valid analysis of orders-api-40.log with two findings, both citing real lines.</summary>
    public static string OrdersAnalysis { get; } = Analysis(
        "The orders database ran out of connections, and a call to the inventory service timed out.",
        Finding("Database connection pool exhausted", "critical", "resource", [11, 12], "Database connection pool exhausted"),
        Finding("Inventory service timed out", "error", "timeout", [15], "Request to inventory-service timed out after 30 s"));

    /// <summary>orders-api-40.log has 40 lines, so line 41 is rejected with LINE_OUT_OF_RANGE at findings[0].line_refs[0].</summary>
    public static string CitesLinePastTheEnd(string summary = "The database ran out of connections.") => Analysis(
        summary,
        Finding("Database connection pool exhausted", "critical", "resource", [41], "Database connection pool exhausted"));

    /// <summary>Paraphrased evidence, rejected with EVIDENCE_NOT_FOUND at findings[0].evidence.</summary>
    public static string ParaphrasedEvidence { get; } = Analysis(
        "The database ran out of connections.",
        Finding("Database connection pool exhausted", "critical", "resource", [12], "Database pool is exhausted"));
}
