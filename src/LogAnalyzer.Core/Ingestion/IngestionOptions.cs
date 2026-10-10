using System.ComponentModel.DataAnnotations;

namespace LogAnalyzer.Core.Ingestion;

/// <summary>Settings for accepting logs: the <c>Ingestion</c> configuration section, checked when the API starts.</summary>
public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";

    /// <summary>10 MB, where 1 MB is 1,048,576 bytes.</summary>
    public const int DefaultMaxSubmissionBytes = 10_485_760;

    /// <summary>
    /// The largest submission accepted, in bytes. At most 100 MB: the API holds a submission in memory several times
    /// over, and PostgreSQL stores a run's lines as one value, which can hold at most 1 GB.
    /// </summary>
    [Range(1, 104_857_600, ErrorMessage = "Ingestion:MaxSubmissionBytes must be between 1 and 104,857,600 bytes (100 MB).")]
    public int MaxSubmissionBytes { get; set; } = DefaultMaxSubmissionBytes;
}
