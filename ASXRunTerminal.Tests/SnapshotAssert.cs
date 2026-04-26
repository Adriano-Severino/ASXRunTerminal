using System.Runtime.CompilerServices;
using System.Text;

namespace ASXRunTerminal.Tests;

internal static class SnapshotAssert
{
    private const string UpdateSnapshotsEnvironmentVariable = "ASXRUN_UPDATE_SNAPSHOTS";

    public static void Match(
        string snapshotName,
        string actual,
        [CallerFilePath] string callerFilePath = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotName);
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentException.ThrowIfNullOrWhiteSpace(callerFilePath);

        var snapshotsDirectory = Path.Combine(
            Path.GetDirectoryName(callerFilePath)
                ?? throw new InvalidOperationException("Nao foi possivel resolver o diretorio do teste."),
            "Snapshots");
        var snapshotPath = Path.Combine(snapshotsDirectory, $"{snapshotName}.snap");
        var normalizedActual = Normalize(actual);

        var shouldUpdateSnapshots =
            string.Equals(
                Environment.GetEnvironmentVariable(UpdateSnapshotsEnvironmentVariable),
                "1",
                StringComparison.Ordinal);

        if (shouldUpdateSnapshots)
        {
            Directory.CreateDirectory(snapshotsDirectory);
            File.WriteAllText(snapshotPath, normalizedActual, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        if (!File.Exists(snapshotPath))
        {
            Assert.Fail(
                $"Snapshot nao encontrado: {snapshotPath}. Execute os testes com {UpdateSnapshotsEnvironmentVariable}=1 para criar/atualizar snapshots.");
        }

        var expected = Normalize(File.ReadAllText(snapshotPath));
        Assert.Equal(expected, normalizedActual);
    }

    public static string NormalizeForSnapshot(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Normalize(content).Replace("\u001b", "\\u001b", StringComparison.Ordinal);
    }

    private static string Normalize(string content)
    {
        return content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }
}
