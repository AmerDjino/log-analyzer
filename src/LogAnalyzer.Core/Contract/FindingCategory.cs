namespace LogAnalyzer.Core.Contract;

/// <summary>What kind of problem a finding is. The contract spells each value in lowercase, such as "timeout".</summary>
public enum FindingCategory
{
    /// <summary>Unhandled or logged exceptions.</summary>
    Exception,

    /// <summary>Operations exceeding a time limit.</summary>
    Timeout,

    /// <summary>Failing calls to databases, HTTP services or queues.</summary>
    Dependency,

    /// <summary>Exhausted memory, connections, threads or disk.</summary>
    Resource,

    /// <summary>Authentication, authorization or suspicious access.</summary>
    Security,

    /// <summary>Slow operations that still succeed.</summary>
    Performance,

    /// <summary>Missing or invalid settings.</summary>
    Configuration,

    /// <summary>Anything else.</summary>
    Other,
}
