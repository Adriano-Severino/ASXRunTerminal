using ASXRunTerminal.Core;
using System.Text.Json;

namespace ASXRunTerminal.Config;

internal static class WorkspacePatchAuditFile
{
    internal const string AuditFileName = "patch-audit";

    public static string EnsureExists(Func<string?>? userHomeResolver = null)
    {
        var auditPath = GetAuditPath(userHomeResolver);
        var auditDirectory = Path.GetDirectoryName(auditPath)
            ?? throw new InvalidOperationException(
                "Nao foi possivel resolver o diretorio da trilha de auditoria local.");

        Directory.CreateDirectory(auditDirectory);
        if (File.Exists(auditPath))
        {
            return auditPath;
        }

        File.WriteAllText(auditPath, string.Empty);
        return auditPath;
    }

    public static IReadOnlyList<WorkspacePatchAuditEntry> Load(Func<string?>? userHomeResolver = null)
    {
        var auditPath = EnsureExists(userHomeResolver);
        var lines = File.ReadAllLines(auditPath);
        var entries = new List<WorkspacePatchAuditEntry>(lines.Length);

        for (var index = 0; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            var line = lines[index].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            PersistedPatchAuditEntry persistedEntry;
            try
            {
                persistedEntry = JsonSerializer.Deserialize<PersistedPatchAuditEntry>(line);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Linha de auditoria invalida em '{auditPath}' (linha {lineNumber}).",
                    ex);
            }

            if (persistedEntry == default)
            {
                throw new InvalidOperationException(
                    $"Linha de auditoria invalida em '{auditPath}' (linha {lineNumber}).");
            }

            try
            {
                WorkspacePatchAuditEntry auditEntry = persistedEntry;
                entries.Add(auditEntry);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException(
                    $"Linha de auditoria invalida em '{auditPath}' (linha {lineNumber}).",
                    ex);
            }
        }

        return entries
            .OrderByDescending(static entry => entry.TimestampUtc)
            .ThenByDescending(static entry => entry.SessionSequence)
            .ToArray();
    }

    public static string Append(
        WorkspacePatchAuditEntry entry,
        Func<string?>? userHomeResolver = null)
    {
        var auditPath = EnsureExists(userHomeResolver);
        PersistedPatchAuditEntry persistedEntry = entry;
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
                "Nao foi possivel resolver o diretorio home do usuario para criar a trilha de auditoria local.");
        }

        return userHome.Trim();
    }

    private static string? ResolveUserHomeFromEnvironment()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static string NormalizeChangeKind(WorkspacePatchChangeKind kind)
    {
        return kind switch
        {
            WorkspacePatchChangeKind.Create => "create",
            WorkspacePatchChangeKind.Edit => "edit",
            WorkspacePatchChangeKind.Delete => "delete",
            _ => throw new InvalidOperationException(
                $"O tipo de mudanca '{kind}' nao e suportado para persistencia de auditoria.")
        };
    }

    private static WorkspacePatchChangeKind ParseChangeKind(string? rawKind)
    {
        if (string.Equals(rawKind, "create", StringComparison.OrdinalIgnoreCase))
        {
            return WorkspacePatchChangeKind.Create;
        }

        if (string.Equals(rawKind, "edit", StringComparison.OrdinalIgnoreCase))
        {
            return WorkspacePatchChangeKind.Edit;
        }

        if (string.Equals(rawKind, "delete", StringComparison.OrdinalIgnoreCase))
        {
            return WorkspacePatchChangeKind.Delete;
        }

        throw new InvalidOperationException(
            $"Tipo de mudanca de auditoria invalido: '{rawKind}'.");
    }

    private readonly record struct PersistedPatchAuditEntry(
        DateTimeOffset TimestampUtc,
        string SessionId,
        long SessionSequence,
        string Command,
        string WorkspaceRootDirectory,
        string PatchRequestFilePath,
        bool IsPreviewOnly,
        int PlannedChangeCount,
        int AppliedChangeCount,
        int SkippedChangeCount,
        string UnifiedDiff,
        PersistedPatchAuditChangeEntry[]? Files)
    {
        public static implicit operator PersistedPatchAuditEntry(WorkspacePatchAuditEntry entry)
        {
            if (entry.Files is null)
            {
                throw new InvalidOperationException(
                    "A trilha de auditoria exige a colecao de arquivos alterados.");
            }

            return new PersistedPatchAuditEntry(
                TimestampUtc: entry.TimestampUtc,
                SessionId: entry.SessionId,
                SessionSequence: entry.SessionSequence,
                Command: entry.Command,
                WorkspaceRootDirectory: entry.WorkspaceRootDirectory,
                PatchRequestFilePath: entry.PatchRequestFilePath,
                IsPreviewOnly: entry.IsPreviewOnly,
                PlannedChangeCount: entry.PlannedChangeCount,
                AppliedChangeCount: entry.AppliedChangeCount,
                SkippedChangeCount: entry.SkippedChangeCount,
                UnifiedDiff: entry.UnifiedDiff,
                Files: entry.Files
                    .Select(static item => (PersistedPatchAuditChangeEntry)item)
                    .ToArray());
        }

        public static implicit operator WorkspacePatchAuditEntry(PersistedPatchAuditEntry entry)
        {
            if (entry.TimestampUtc == default)
            {
                throw new InvalidOperationException(
                    "O campo 'TimestampUtc' da auditoria deve estar preenchido.");
            }

            if (string.IsNullOrWhiteSpace(entry.SessionId))
            {
                throw new InvalidOperationException(
                    "O campo 'SessionId' da auditoria nao pode estar vazio.");
            }

            if (entry.SessionSequence <= 0)
            {
                throw new InvalidOperationException(
                    "O campo 'SessionSequence' da auditoria deve ser maior que zero.");
            }

            if (string.IsNullOrWhiteSpace(entry.Command))
            {
                throw new InvalidOperationException(
                    "O campo 'Command' da auditoria nao pode estar vazio.");
            }

            if (string.IsNullOrWhiteSpace(entry.WorkspaceRootDirectory))
            {
                throw new InvalidOperationException(
                    "O campo 'WorkspaceRootDirectory' da auditoria nao pode estar vazio.");
            }

            if (string.IsNullOrWhiteSpace(entry.PatchRequestFilePath))
            {
                throw new InvalidOperationException(
                    "O campo 'PatchRequestFilePath' da auditoria nao pode estar vazio.");
            }

            if (entry.PlannedChangeCount < 0
                || entry.AppliedChangeCount < 0
                || entry.SkippedChangeCount < 0)
            {
                throw new InvalidOperationException(
                    "A auditoria nao aceita contadores negativos de mudancas.");
            }

            if (entry.Files is null || entry.Files.Length == 0)
            {
                throw new InvalidOperationException(
                    "A trilha de auditoria exige ao menos um arquivo registrado.");
            }

            return new WorkspacePatchAuditEntry(
                TimestampUtc: entry.TimestampUtc,
                SessionId: entry.SessionId.Trim(),
                SessionSequence: entry.SessionSequence,
                Command: entry.Command.Trim(),
                WorkspaceRootDirectory: entry.WorkspaceRootDirectory.Trim(),
                PatchRequestFilePath: entry.PatchRequestFilePath.Trim(),
                IsPreviewOnly: entry.IsPreviewOnly,
                PlannedChangeCount: entry.PlannedChangeCount,
                AppliedChangeCount: entry.AppliedChangeCount,
                SkippedChangeCount: entry.SkippedChangeCount,
                UnifiedDiff: entry.UnifiedDiff ?? string.Empty,
                Files: entry.Files
                    .Select(static item => (WorkspacePatchAuditChangeEntry)item)
                    .ToArray());
        }
    }

    private readonly record struct PersistedPatchAuditChangeEntry(
        string Kind,
        string Path,
        string ResolvedPath,
        bool HasChanges,
        string UnifiedDiff)
    {
        public static implicit operator PersistedPatchAuditChangeEntry(WorkspacePatchAuditChangeEntry entry)
        {
            return new PersistedPatchAuditChangeEntry(
                Kind: NormalizeChangeKind(entry.Kind),
                Path: entry.Path,
                ResolvedPath: entry.ResolvedPath,
                HasChanges: entry.HasChanges,
                UnifiedDiff: entry.UnifiedDiff);
        }

        public static implicit operator WorkspacePatchAuditChangeEntry(PersistedPatchAuditChangeEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Path))
            {
                throw new InvalidOperationException(
                    "O campo 'Path' da auditoria de arquivo nao pode estar vazio.");
            }

            if (string.IsNullOrWhiteSpace(entry.ResolvedPath))
            {
                throw new InvalidOperationException(
                    "O campo 'ResolvedPath' da auditoria de arquivo nao pode estar vazio.");
            }

            return new WorkspacePatchAuditChangeEntry(
                Kind: ParseChangeKind(entry.Kind),
                Path: entry.Path.Trim(),
                ResolvedPath: entry.ResolvedPath.Trim(),
                HasChanges: entry.HasChanges,
                UnifiedDiff: entry.UnifiedDiff ?? string.Empty);
        }
    }
}
