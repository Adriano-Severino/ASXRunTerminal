using ASXRunTerminal.Core;
using System.Text.Json;

namespace ASXRunTerminal.Config;

internal static class ExecutionCheckpointFile
{
    internal const string CheckpointFileName = "execution-checkpoints";

    public static string EnsureExists(Func<string?>? userHomeResolver = null)
    {
        var checkpointPath = GetCheckpointPath(userHomeResolver);
        var checkpointDirectory = Path.GetDirectoryName(checkpointPath)
            ?? throw new InvalidOperationException(
                "Nao foi possivel resolver o diretorio de checkpoints de execucao.");

        Directory.CreateDirectory(checkpointDirectory);
        if (File.Exists(checkpointPath))
        {
            return checkpointPath;
        }

        File.WriteAllText(checkpointPath, string.Empty);
        return checkpointPath;
    }

    public static IReadOnlyList<ExecutionSessionCheckpoint> Load(Func<string?>? userHomeResolver = null)
    {
        var checkpointPath = EnsureExists(userHomeResolver);
        var lines = File.ReadAllLines(checkpointPath);
        var entries = new List<ExecutionSessionCheckpoint>(lines.Length);

        for (var index = 0; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            var line = lines[index].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            PersistedExecutionCheckpoint persistedCheckpoint;
            try
            {
                persistedCheckpoint = JsonSerializer.Deserialize<PersistedExecutionCheckpoint>(line);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Linha de checkpoint invalida em '{checkpointPath}' (linha {lineNumber}).",
                    ex);
            }

            if (persistedCheckpoint == default)
            {
                throw new InvalidOperationException(
                    $"Linha de checkpoint invalida em '{checkpointPath}' (linha {lineNumber}).");
            }

            try
            {
                ExecutionSessionCheckpoint checkpoint = persistedCheckpoint;
                entries.Add(checkpoint);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException(
                    $"Linha de checkpoint invalida em '{checkpointPath}' (linha {lineNumber}).",
                    ex);
            }
        }

        return entries
            .OrderByDescending(static checkpoint => checkpoint.TimestampUtc)
            .ToArray();
    }

    public static string Append(
        ExecutionSessionCheckpoint checkpoint,
        Func<string?>? userHomeResolver = null)
    {
        var checkpointPath = EnsureExists(userHomeResolver);
        PersistedExecutionCheckpoint persistedCheckpoint = checkpoint;
        var content = JsonSerializer.Serialize(persistedCheckpoint);
        File.AppendAllText(checkpointPath, $"{content}{Environment.NewLine}");
        return checkpointPath;
    }

    public static void Clear(Func<string?>? userHomeResolver = null)
    {
        var checkpointPath = EnsureExists(userHomeResolver);
        File.WriteAllText(checkpointPath, string.Empty);
    }

    internal static string GetCheckpointPath(Func<string?>? userHomeResolver = null)
    {
        var userHome = ResolveUserHome(userHomeResolver);
        return Path.Combine(userHome, UserConfigFile.ConfigDirectoryName, CheckpointFileName);
    }

    private static string ResolveUserHome(Func<string?>? userHomeResolver)
    {
        var resolver = userHomeResolver ?? ResolveUserHomeFromEnvironment;
        var userHome = resolver();
        if (string.IsNullOrWhiteSpace(userHome))
        {
            throw new InvalidOperationException(
                "Nao foi possivel resolver o diretorio home do usuario para checkpoints de execucao.");
        }

        return userHome.Trim();
    }

    private static string? ResolveUserHomeFromEnvironment()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static string NormalizeStatus(ExecutionCheckpointStatus status)
    {
        return status switch
        {
            ExecutionCheckpointStatus.InProgress => "in-progress",
            ExecutionCheckpointStatus.Completed => "completed",
            ExecutionCheckpointStatus.Failed => "failed",
            ExecutionCheckpointStatus.Cancelled => "cancelled",
            _ => throw new InvalidOperationException(
                $"O status de checkpoint '{status}' nao e suportado.")
        };
    }

    private static ExecutionCheckpointStatus ParseStatus(string? rawStatus)
    {
        if (string.Equals(rawStatus, "in-progress", StringComparison.OrdinalIgnoreCase))
        {
            return ExecutionCheckpointStatus.InProgress;
        }

        if (string.Equals(rawStatus, "completed", StringComparison.OrdinalIgnoreCase))
        {
            return ExecutionCheckpointStatus.Completed;
        }

        if (string.Equals(rawStatus, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return ExecutionCheckpointStatus.Failed;
        }

        if (string.Equals(rawStatus, "cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return ExecutionCheckpointStatus.Cancelled;
        }

        throw new InvalidOperationException(
            $"Status de checkpoint invalido: '{rawStatus}'.");
    }

    private readonly record struct PersistedExecutionCheckpoint(
        DateTimeOffset TimestampUtc,
        string SessionId,
        string Command,
        string Stage,
        string Status,
        string Prompt,
        string? Model,
        string? SkillName,
        string Detail)
    {
        public static implicit operator PersistedExecutionCheckpoint(ExecutionSessionCheckpoint checkpoint)
        {
            return new PersistedExecutionCheckpoint(
                TimestampUtc: checkpoint.TimestampUtc,
                SessionId: checkpoint.SessionId,
                Command: checkpoint.Command,
                Stage: checkpoint.Stage,
                Status: NormalizeStatus(checkpoint.Status),
                Prompt: checkpoint.Prompt,
                Model: checkpoint.Model,
                SkillName: checkpoint.SkillName,
                Detail: checkpoint.Detail);
        }

        public static implicit operator ExecutionSessionCheckpoint(PersistedExecutionCheckpoint persistedCheckpoint)
        {
            if (persistedCheckpoint.TimestampUtc == default)
            {
                throw new InvalidOperationException(
                    "O campo 'TimestampUtc' do checkpoint deve estar preenchido.");
            }

            if (string.IsNullOrWhiteSpace(persistedCheckpoint.SessionId))
            {
                throw new InvalidOperationException(
                    "O campo 'SessionId' do checkpoint nao pode estar vazio.");
            }

            if (string.IsNullOrWhiteSpace(persistedCheckpoint.Command))
            {
                throw new InvalidOperationException(
                    "O campo 'Command' do checkpoint nao pode estar vazio.");
            }

            if (string.IsNullOrWhiteSpace(persistedCheckpoint.Stage))
            {
                throw new InvalidOperationException(
                    "O campo 'Stage' do checkpoint nao pode estar vazio.");
            }

            if (string.IsNullOrWhiteSpace(persistedCheckpoint.Prompt))
            {
                throw new InvalidOperationException(
                    "O campo 'Prompt' do checkpoint nao pode estar vazio.");
            }

            var normalizedCommand = persistedCheckpoint.Command.Trim();
            var normalizedSkillName = string.IsNullOrWhiteSpace(persistedCheckpoint.SkillName)
                ? null
                : persistedCheckpoint.SkillName.Trim();

            if (string.Equals(normalizedCommand, "skill", StringComparison.OrdinalIgnoreCase)
                && normalizedSkillName is null)
            {
                throw new InvalidOperationException(
                    "O campo 'SkillName' do checkpoint e obrigatorio para o comando 'skill'.");
            }

            return new ExecutionSessionCheckpoint(
                TimestampUtc: persistedCheckpoint.TimestampUtc,
                SessionId: persistedCheckpoint.SessionId.Trim(),
                Command: normalizedCommand,
                Stage: persistedCheckpoint.Stage.Trim(),
                Status: ParseStatus(persistedCheckpoint.Status),
                Prompt: persistedCheckpoint.Prompt.Trim(),
                Model: string.IsNullOrWhiteSpace(persistedCheckpoint.Model) ? null : persistedCheckpoint.Model.Trim(),
                SkillName: normalizedSkillName,
                Detail: persistedCheckpoint.Detail ?? string.Empty);
        }
    }
}
