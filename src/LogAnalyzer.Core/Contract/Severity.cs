namespace LogAnalyzer.Core.Contract;

/// <summary>How serious a finding is, most serious first. The contract spells each value in lowercase, such as "critical".</summary>
public enum Severity
{
    /// <summary>An outage, crash or data loss.</summary>
    Critical,

    /// <summary>Failed operations without a full outage.</summary>
    Error,

    /// <summary>Degradation or a risk of failure.</summary>
    Warning,

    /// <summary>Noteworthy but harmless.</summary>
    Info,
}
