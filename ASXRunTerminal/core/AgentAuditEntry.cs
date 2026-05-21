namespace ASXRunTerminal.Core;

internal readonly record struct AgentAuditChangeEntry(
    string Path,
    string Kind,
    bool IsDestructive)
{
    public static implicit operator AgentAuditChangeEntry(
        (string Path, string Kind, bool IsDestructive) tuple)
    {
        return new AgentAuditChangeEntry(
            Path: tuple.Path,
            Kind: tuple.Kind,
            IsDestructive: tuple.IsDestructive);
    }
}

internal readonly record struct AgentAuditEntry(
    DateTimeOffset TimestampUtc,
    string SessionId,
    long SessionSequence,
    string Command,
    string WorkspaceRootDirectory,
    string Objective,
    string? Model,
    string AutonomyLevel,
    bool HasExplicitSensitiveOperationApproval,
    int Iteration,
    string Phase,
    string EventType,
    string Status,
    string Summary,
    string Detail,
    string? ToolName,
    string? CommandLine,
    int? ExitCode,
    bool? IsSuccess,
    TimeSpan? Duration,
    bool IsTimedOut,
    bool IsCancelled,
    IReadOnlyList<AgentAuditChangeEntry> Changes);
