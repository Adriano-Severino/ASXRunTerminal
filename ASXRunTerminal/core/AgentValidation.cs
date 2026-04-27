namespace ASXRunTerminal.Core;

internal readonly record struct AgentValidationCommand(
    string Name,
    string CommandLine,
    TimeSpan Timeout)
{
    public static implicit operator AgentValidationCommand(
        (string Name, string CommandLine, TimeSpan Timeout) tuple)
    {
        return new AgentValidationCommand(
            Name: tuple.Name,
            CommandLine: tuple.CommandLine,
            Timeout: tuple.Timeout);
    }
}

internal readonly record struct AgentValidationCommandResult(
    string Name,
    string CommandLine,
    bool IsSuccess,
    int ExitCode,
    string StdOut,
    string StdErr,
    TimeSpan Duration,
    bool IsTimedOut,
    bool IsCancelled)
{
    public static AgentValidationCommandResult FromToolResult(
        AgentValidationCommand command,
        ToolExecutionResult toolResult)
    {
        return new AgentValidationCommandResult(
            Name: command.Name,
            CommandLine: command.CommandLine,
            IsSuccess: toolResult.IsSuccess,
            ExitCode: toolResult.ExitCode,
            StdOut: toolResult.StdOut,
            StdErr: toolResult.StdErr,
            Duration: toolResult.Duration,
            IsTimedOut: toolResult.IsTimedOut,
            IsCancelled: toolResult.IsCancelled);
    }
}

internal readonly record struct AgentValidationReport(
    bool WasRequired,
    bool CommandsDiscovered,
    IReadOnlyList<AgentValidationCommandResult> Results)
{
    public static AgentValidationReport NotRequired()
    {
        return new AgentValidationReport(
            WasRequired: false,
            CommandsDiscovered: false,
            Results: Array.Empty<AgentValidationCommandResult>());
    }

    public static AgentValidationReport NoCommandsDiscovered()
    {
        return new AgentValidationReport(
            WasRequired: true,
            CommandsDiscovered: false,
            Results: Array.Empty<AgentValidationCommandResult>());
    }

    public bool HasFailures =>
        Results.Any(static result => !result.IsSuccess);

    public bool IsSuccessful =>
        WasRequired
        && CommandsDiscovered
        && Results.Count > 0
        && !HasFailures;
}

internal sealed class AgentValidationCommandRunner
{
    private readonly IToolRuntime _toolRuntime;

    public AgentValidationCommandRunner(IToolRuntime toolRuntime)
    {
        _toolRuntime = toolRuntime ?? throw new ArgumentNullException(nameof(toolRuntime));
    }

    public async Task<AgentValidationReport> RunAsync(
        string workspaceRootDirectory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceRootDirectory))
        {
            throw new ArgumentException(
                "O diretorio raiz do workspace nao pode estar vazio.",
                nameof(workspaceRootDirectory));
        }

        var commands = AgentValidationCommandCatalog.Discover(workspaceRootDirectory);
        if (commands.Count == 0)
        {
            return AgentValidationReport.NoCommandsDiscovered();
        }

        var results = new List<AgentValidationCommandResult>(commands.Count);
        foreach (var command in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var script = BuildShellScript(workspaceRootDirectory, command.CommandLine);
            var request = new ToolExecutionRequest(
                ToolName: "shell",
                Arguments: new Dictionary<string, string>
                {
                    ["script"] = script
                },
                Timeout: command.Timeout);

            var toolResult = await _toolRuntime.ExecuteAsync(request, cancellationToken);
            results.Add(AgentValidationCommandResult.FromToolResult(command, toolResult));
        }

        return new AgentValidationReport(
            WasRequired: true,
            CommandsDiscovered: true,
            Results: results);
    }

    private static string BuildShellScript(
        string workspaceRootDirectory,
        string commandLine)
    {
        return OperatingSystem.IsWindows()
            ? BuildPowerShellScript(workspaceRootDirectory, commandLine)
            : BuildPosixShellScript(workspaceRootDirectory, commandLine);
    }

    private static string BuildPowerShellScript(
        string workspaceRootDirectory,
        string commandLine)
    {
        return string.Join(
            Environment.NewLine,
            [
                "Set-StrictMode -Version Latest",
                "$ErrorActionPreference = 'Stop'",
                $"Set-Location -LiteralPath {QuotePowerShell(workspaceRootDirectory)}",
                commandLine,
                "if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }"
            ]);
    }

    private static string BuildPosixShellScript(
        string workspaceRootDirectory,
        string commandLine)
    {
        return
            $"""
            set -e
            cd {QuotePosix(workspaceRootDirectory)}
            {commandLine}
            """;
    }

    private static string QuotePowerShell(string value)
    {
        return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }

    private static string QuotePosix(string value)
    {
        return $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
    }
}

internal static class AgentValidationCommandCatalog
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan LintTimeout = TimeSpan.FromMinutes(2);

    public static IReadOnlyList<AgentValidationCommand> Discover(string workspaceRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(workspaceRootDirectory)
            || !Directory.Exists(workspaceRootDirectory))
        {
            return Array.Empty<AgentValidationCommand>();
        }

        if (TryResolveDotNetTarget(workspaceRootDirectory, out var dotNetTarget))
        {
            return
            [
                new AgentValidationCommand(
                    Name: "build",
                    CommandLine: $"dotnet build {QuoteCommandArgument(dotNetTarget)} --nologo",
                    Timeout: BuildTimeout),
                new AgentValidationCommand(
                    Name: "test",
                    CommandLine: $"dotnet test {QuoteCommandArgument(dotNetTarget)} --nologo",
                    Timeout: TestTimeout),
                new AgentValidationCommand(
                    Name: "lint",
                    CommandLine: $"dotnet format {QuoteCommandArgument(dotNetTarget)} --verify-no-changes --no-restore",
                    Timeout: LintTimeout)
            ];
        }

        if (File.Exists(Path.Combine(workspaceRootDirectory, "package.json")))
        {
            return
            [
                new AgentValidationCommand(
                    Name: "build",
                    CommandLine: "npm run build --if-present",
                    Timeout: BuildTimeout),
                new AgentValidationCommand(
                    Name: "test",
                    CommandLine: "npm test --if-present",
                    Timeout: TestTimeout),
                new AgentValidationCommand(
                    Name: "lint",
                    CommandLine: "npm run lint --if-present",
                    Timeout: LintTimeout)
            ];
        }

        return Array.Empty<AgentValidationCommand>();
    }

    private static bool TryResolveDotNetTarget(
        string workspaceRootDirectory,
        out string target)
    {
        var candidates = Directory
            .EnumerateFiles(workspaceRootDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(static path =>
            {
                var extension = Path.GetExtension(path);
                return string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(static path => GetDotNetTargetPriority(path))
            .ThenBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (candidates.Length > 0)
        {
            target = Path.GetFileName(candidates[0]);
            return true;
        }

        var projectCandidates = Directory
            .EnumerateFiles(workspaceRootDirectory, "*.csproj", SearchOption.AllDirectories)
            .Where(static path => !IsIgnoredDirectoryPath(path))
            .OrderBy(static path => path.Count(static character => character == Path.DirectorySeparatorChar))
            .ThenBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (projectCandidates.Length == 0)
        {
            target = string.Empty;
            return false;
        }

        target = Path.GetRelativePath(workspaceRootDirectory, projectCandidates[0]);
        return true;
    }

    private static int GetDotNetTargetPriority(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static bool IsIgnoredDirectoryPath(string path)
    {
        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(static segment =>
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "node_modules", StringComparison.OrdinalIgnoreCase));
    }

    private static string QuoteCommandArgument(string value)
    {
        return value.Contains(' ', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : value;
    }
}
