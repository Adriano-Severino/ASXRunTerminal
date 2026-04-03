namespace ASXRunTerminal.Core;

internal readonly record struct PromptHistoryEntry(
    DateTimeOffset TimestampUtc,
    string Command,
    string Prompt,
    string Response,
    string? Model);
