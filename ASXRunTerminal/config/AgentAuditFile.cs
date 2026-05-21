using ASXRunTerminal.Core;
using System.Text.Json;

namespace ASXRunTerminal.Config;

internal static class AgentAuditFile
{
    internal const string AuditFileName = "agent-audit";

    public static string EnsureExists(Func<string?>? userHomeResolver = null)
    {
        var auditPath = GetAuditPath(userHomeResolver);
        var auditDirectory = Path.GetDirectoryName(auditPath)
            ?? throw new InvalidOperationException(
                "Nao foi possivel resolver o diretorio da trilha de auditoria do agente.");

        Directory.CreateDirectory(auditDirectory);
        if (File.Exists(auditPath))
        {
            return auditPath;
        }

        File.WriteAllText(auditPath, string.Empty);
        return auditPath;
    }

    public static IReadOnlyList<AgentAuditEntry> Load(Func<string?>? userHomeResolver = null)
    {
        var auditPath = EnsureExists(userHomeResolver);
        var lines = File.ReadAllLines(auditPath);
        var entries = new List<AgentAuditEntry>(lines.Length);

        for (var index = 0; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            var line = lines[index].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            PersistedAgentAuditEntry persistedEntry;
            try
            {
                persistedEntry = JsonSerializer.Deserialize<PersistedAgentAuditEntry>(line);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Linha de auditoria do agente invalida em '{auditPath}' (linha {lineNumber}).",
                    ex);
            }

            if (persistedEntry == default)
            {
                throw new InvalidOperationException(
                    $"Linha de auditoria do agente invalida em '{auditPath}' (linha {lineNumber}).");
            }

            try
            {
                AgentAuditEntry auditEntry = persistedEntry;
                entries.Add(auditEntry);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException(
                    $"Linha de auditoria do agente invalida em '{auditPath}' (linha {lineNumber}).",
                    ex);
            }
        }

        return entries
            .OrderByDescending(static entry => entry.TimestampUtc)
            .ThenByDescending(static entry => entry.SessionSequence)
            .ToArray();
    }

    public static string Append(
        AgentAuditEntry entry,
        Func<string?>? userHomeResolver = null)
    {
        var auditPath = EnsureExists(userHomeResolver);
        PersistedAgentAuditEntry persistedEntry = entry;
        var content = JsonSerializer.Serialize(persistedEntry);
        File.AppendAllText(auditPath, $"{content}{Environment.NewLine}");
        return auditPath;
    }

    public static void Clear(Func<string?>? userHomeResolver = null)
    {
        var auditPath = EnsureExists(userHomeResolver);
        File.WriteAllText(auditPath, string.Empty);
    }

    internal static string GetAuditPath(Func<string?>? userHomeResolver = null)
    {
        var userHome = ResolveUserHome(userHomeResolver);
        return Path.Combine(userHome, UserConfigFile.ConfigDirectoryName, AuditFileName);
    }

    private static string ResolveUserHome(Func<string?>? userHomeResolver)
    {
        var resolver = userHomeResolver ?? ResolveUserHomeFromEnvironment;
        var userHome = resolver();
        if (string.IsNullOrWhiteSpace(userHome))
        {
            throw new InvalidOperationException(
                "Nao foi possivel resolver o diretorio home do usuario para criar a trilha de auditoria do agente.");
        }

        return userHome.Trim();
    }

    private static string? ResolveUserHomeFromEnvironment()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static string Sanitize(string? value)
    {
        return SecretMasker.Mask(value).Trim();
    }

    private readonly record struct PersistedAgentAuditEntry(
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
        double? DurationMilliseconds,
        bool IsTimedOut,
        bool IsCancelled,
        PersistedAgentAuditChangeEntry[]? Changes)
    {
        public static implicit operator PersistedAgentAuditEntry(AgentAuditEntry entry)
        {
            if (entry.Changes is null)
            {
                throw new InvalidOperationException(
                    "A trilha de auditoria do agente exige a colecao de mudancas.");
            }

            if (entry.Duration is { } duration && duration < TimeSpan.Zero)
            {
                throw new InvalidOperationException(
                    "A duracao de auditoria do agente nao pode ser negativa.");
            }

            return new PersistedAgentAuditEntry(
                TimestampUtc: entry.TimestampUtc,
                SessionId: entry.SessionId,
                SessionSequence: entry.SessionSequence,
                Command: entry.Command,
                WorkspaceRootDirectory: entry.WorkspaceRootDirectory,
                Objective: Sanitize(entry.Objective),
                Model: string.IsNullOrWhiteSpace(entry.Model) ? null : Sanitize(entry.Model),
                AutonomyLevel: entry.AutonomyLevel,
                HasExplicitSensitiveOperationApproval: entry.HasExplicitSensitiveOperationApproval,
                Iteration: entry.Iteration,
                Phase: entry.Phase,
                EventType: entry.EventType,
                Status: entry.Status,
                Summary: Sanitize(entry.Summary),
                Detail: Sanitize(entry.Detail),
                ToolName: string.IsNullOrWhiteSpace(entry.ToolName) ? null : Sanitize(entry.ToolName),
                CommandLine: string.IsNullOrWhiteSpace(entry.CommandLine) ? null : Sanitize(entry.CommandLine),
                ExitCode: entry.ExitCode,
                IsSuccess: entry.IsSuccess,
                DurationMilliseconds: entry.Duration?.TotalMilliseconds,
                IsTimedOut: entry.IsTimedOut,
                IsCancelled: entry.IsCancelled,
                Changes: entry.Changes
                    .Select(static change => (PersistedAgentAuditChangeEntry)change)
                    .ToArray());
        }

        public static implicit operator AgentAuditEntry(PersistedAgentAuditEntry entry)
        {
            if (entry.TimestampUtc == default)
            {
                throw new InvalidOperationException(
                    "O campo 'TimestampUtc' da auditoria do agente deve estar preenchido.");
            }

            if (string.IsNullOrWhiteSpace(entry.SessionId))
            {
                throw new InvalidOperationException(
                    "O campo 'SessionId' da auditoria do agente nao pode estar vazio.");
            }

            if (entry.SessionSequence <= 0)
            {
                throw new InvalidOperationException(
                    "O campo 'SessionSequence' da auditoria do agente deve ser maior que zero.");
            }

            if (string.IsNullOrWhiteSpace(entry.Command))
            {
                throw new InvalidOperationException(
                    "O campo 'Command' da auditoria do agente nao pode estar vazio.");
            }

            if (string.IsNullOrWhiteSpace(entry.WorkspaceRootDirectory))
            {
                throw new InvalidOperationException(
                    "O campo 'WorkspaceRootDirectory' da auditoria do agente nao pode estar vazio.");
            }

            if (string.IsNullOrWhiteSpace(entry.Objective))
            {
                throw new InvalidOperationException(
                    "O campo 'Objective' da auditoria do agente nao pode estar vazio.");
            }

            if (string.IsNullOrWhiteSpace(entry.AutonomyLevel))
            {
                throw new InvalidOperationException(
                    "O campo 'AutonomyLevel' da auditoria do agente nao pode estar vazio.");
            }

            if (entry.Iteration < 0)
            {
                throw new InvalidOperationException(
                    "O campo 'Iteration' da auditoria do agente nao pode ser negativo.");
            }

            if (string.IsNullOrWhiteSpace(entry.Phase))
            {
                throw new InvalidOperationException(
                    "O campo 'Phase' da auditoria do agente nao pode estar vazio.");
            }

            if (string.IsNullOrWhiteSpace(entry.EventType))
            {
                throw new InvalidOperationException(
                    "O campo 'EventType' da auditoria do agente nao pode estar vazio.");
            }

            if (string.IsNullOrWhiteSpace(entry.Status))
            {
                throw new InvalidOperationException(
                    "O campo 'Status' da auditoria do agente nao pode estar vazio.");
            }

            if (string.IsNullOrWhiteSpace(entry.Summary))
            {
                throw new InvalidOperationException(
                    "O campo 'Summary' da auditoria do agente nao pode estar vazio.");
            }

            if (entry.DurationMilliseconds is { } durationMilliseconds && durationMilliseconds < 0)
            {
                throw new InvalidOperationException(
                    "O campo 'DurationMilliseconds' da auditoria do agente nao pode ser negativo.");
            }

            return new AgentAuditEntry(
                TimestampUtc: entry.TimestampUtc,
                SessionId: entry.SessionId.Trim(),
                SessionSequence: entry.SessionSequence,
                Command: entry.Command.Trim(),
                WorkspaceRootDirectory: entry.WorkspaceRootDirectory.Trim(),
                Objective: entry.Objective.Trim(),
                Model: string.IsNullOrWhiteSpace(entry.Model) ? null : entry.Model.Trim(),
                AutonomyLevel: entry.AutonomyLevel.Trim(),
                HasExplicitSensitiveOperationApproval: entry.HasExplicitSensitiveOperationApproval,
                Iteration: entry.Iteration,
                Phase: entry.Phase.Trim(),
                EventType: entry.EventType.Trim(),
                Status: entry.Status.Trim(),
                Summary: entry.Summary.Trim(),
                Detail: entry.Detail ?? string.Empty,
                ToolName: string.IsNullOrWhiteSpace(entry.ToolName) ? null : entry.ToolName.Trim(),
                CommandLine: string.IsNullOrWhiteSpace(entry.CommandLine) ? null : entry.CommandLine.Trim(),
                ExitCode: entry.ExitCode,
                IsSuccess: entry.IsSuccess,
                Duration: entry.DurationMilliseconds is null
                    ? null
                    : TimeSpan.FromMilliseconds(entry.DurationMilliseconds.Value),
                IsTimedOut: entry.IsTimedOut,
                IsCancelled: entry.IsCancelled,
                Changes: entry.Changes is null
                    ? Array.Empty<AgentAuditChangeEntry>()
                    : entry.Changes
                        .Select(static change => (AgentAuditChangeEntry)change)
                        .ToArray());
        }
    }

    private readonly record struct PersistedAgentAuditChangeEntry(
        string Path,
        string Kind,
        bool IsDestructive)
    {
        public static implicit operator PersistedAgentAuditChangeEntry(AgentAuditChangeEntry entry)
        {
            return new PersistedAgentAuditChangeEntry(
                Path: Sanitize(entry.Path),
                Kind: Sanitize(entry.Kind),
                IsDestructive: entry.IsDestructive);
        }

        public static implicit operator AgentAuditChangeEntry(PersistedAgentAuditChangeEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Path))
            {
                throw new InvalidOperationException(
                    "O campo 'Path' da mudanca auditada do agente nao pode estar vazio.");
            }

            if (string.IsNullOrWhiteSpace(entry.Kind))
            {
                throw new InvalidOperationException(
                    "O campo 'Kind' da mudanca auditada do agente nao pode estar vazio.");
            }

            return new AgentAuditChangeEntry(
                Path: entry.Path.Trim(),
                Kind: entry.Kind.Trim(),
                IsDestructive: entry.IsDestructive);
        }
    }
}
