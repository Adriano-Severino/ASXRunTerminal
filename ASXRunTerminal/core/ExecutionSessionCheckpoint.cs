namespace ASXRunTerminal.Core;

internal enum ExecutionCheckpointStatus
{
    InProgress,
    Completed,
    Failed,
    Cancelled
}

internal readonly record struct ExecutionSessionCheckpoint(
    DateTimeOffset TimestampUtc,
    string SessionId,
    string Command,
    string Stage,
    ExecutionCheckpointStatus Status,
    string Prompt,
    string? Model,
    string? SkillName,
    string Detail);
