using ASXRunTerminal.Core;
using System.Text.Json;

namespace ASXRunTerminal.Config;

internal static class UserHistoryFile
{
    internal const string HistoryFileName = "history";

    public static string EnsureExists(Func<string?>? userHomeResolver = null)
    {
        var historyPath = GetHistoryPath(userHomeResolver);
        var historyDirectory = Path.GetDirectoryName(historyPath)
            ?? throw new InvalidOperationException(
                "Nao foi possivel resolver o diretorio de historico do usuario.");

        Directory.CreateDirectory(historyDirectory);
        if (File.Exists(historyPath))
        {
            return historyPath;
        }

        File.WriteAllText(historyPath, string.Empty);
        return historyPath;
    }

    public static IReadOnlyList<PromptHistoryEntry> Load(Func<string?>? userHomeResolver = null)
    {
        var historyPath = EnsureExists(userHomeResolver);
        var lines = File.ReadAllLines(historyPath);
        var entries = new List<PromptHistoryEntry>(lines.Length);

        for (var index = 0; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            var line = lines[index].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            PersistedHistoryEntry persistedEntry;
            try
            {
                persistedEntry = JsonSerializer.Deserialize<PersistedHistoryEntry>(line);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Linha de historico invalida em '{historyPath}' (linha {lineNumber}).",
                    ex);
            }

            if (persistedEntry == default)
            {
                throw new InvalidOperationException(
                    $"Linha de historico invalida em '{historyPath}' (linha {lineNumber}).");
            }

            try
            {
                PromptHistoryEntry historyEntry = persistedEntry;
                entries.Add(historyEntry);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException(
                    $"Linha de historico invalida em '{historyPath}' (linha {lineNumber}).",
                    ex);
            }
        }

        return entries
            .OrderByDescending(static entry => entry.TimestampUtc)
            .ToArray();
    }

    public static void Append(
        PromptHistoryEntry entry,
        Func<string?>? userHomeResolver = null)
    {
        var historyPath = EnsureExists(userHomeResolver);
        PersistedHistoryEntry persistedEntry = entry;
        var content = JsonSerializer.Serialize(persistedEntry);
        File.AppendAllText(historyPath, $"{content}{Environment.NewLine}");
    }

    public static void Clear(Func<string?>? userHomeResolver = null)
    {
        var historyPath = EnsureExists(userHomeResolver);
        File.WriteAllText(historyPath, string.Empty);
    }

    internal static string GetHistoryPath(Func<string?>? userHomeResolver = null)
    {
        var userHome = ResolveUserHome(userHomeResolver);
        return Path.Combine(userHome, UserConfigFile.ConfigDirectoryName, HistoryFileName);
    }

    private static string ResolveUserHome(Func<string?>? userHomeResolver)
    {
        var resolver = userHomeResolver ?? ResolveUserHomeFromEnvironment;
        var userHome = resolver();
        if (string.IsNullOrWhiteSpace(userHome))
        {
            throw new InvalidOperationException(
                "Nao foi possivel resolver o diretorio home do usuario para criar o historico local.");
        }

        return userHome.Trim();
    }

    private static string? ResolveUserHomeFromEnvironment()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private readonly record struct PersistedHistoryEntry(
        DateTimeOffset TimestampUtc,
        string Command,
        string Prompt,
        string Response,
        string? Model)
    {
        public static implicit operator PersistedHistoryEntry(PromptHistoryEntry entry)
        {
            return new PersistedHistoryEntry(
                TimestampUtc: entry.TimestampUtc,
                Command: entry.Command,
                Prompt: entry.Prompt,
                Response: entry.Response,
                Model: entry.Model);
        }

        public static implicit operator PromptHistoryEntry(PersistedHistoryEntry entry)
        {
            if (entry.TimestampUtc == default)
            {
                throw new InvalidOperationException(
                    "O campo 'TimestampUtc' do historico deve estar preenchido.");
            }

            if (string.IsNullOrWhiteSpace(entry.Command))
            {
                throw new InvalidOperationException(
                    "O campo 'Command' do historico nao pode estar vazio.");
            }

            if (string.IsNullOrWhiteSpace(entry.Prompt))
            {
                throw new InvalidOperationException(
                    "O campo 'Prompt' do historico nao pode estar vazio.");
            }

            return new PromptHistoryEntry(
                TimestampUtc: entry.TimestampUtc,
                Command: entry.Command.Trim(),
                Prompt: entry.Prompt.Trim(),
                Response: entry.Response ?? string.Empty,
                Model: string.IsNullOrWhiteSpace(entry.Model) ? null : entry.Model.Trim());
        }
    }
}
