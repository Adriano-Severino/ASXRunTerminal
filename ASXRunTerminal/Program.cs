using ASXRunTerminal.Config;
using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace ASXRunTerminal;

internal static class Program
{
    private const string CliName = "asxrun";
    private const string ModelFlag = "--model";
    private const string AgentMaxStepsFlag = "--max-steps";
    private const string AgentMaxTimeFlag = "--max-time";
    private const string AgentMaxCostFlag = "--max-cost";
    private const string AgentMaxStepsAliasFlag = "--max_steps";
    private const string AgentMaxTimeAliasFlag = "--max_time";
    private const string AgentMaxCostAliasFlag = "--max_cost";
    private const string InteractiveChatPromptPrefix = "> ";
    private const int AgentAutonomousMaxIterations = 8;
    private const int AgentAutoCorrectionMaxAttempts = 2;
    private const string AgentVerificationStatusDone = "done";
    private const string AgentVerificationStatusRefine = "refine";
    private const string AgentCodeChangeStatusChanged = "changed";
    private const string AgentCodeChangeStatusNoChange = "no-change";
    private const string AgentCodeChangeStatusUnknown = "unknown";
    private const string AgentLoopCheckpointStage = "agent-loop";
    private const string AgentLoopCheckpointKind = "agent-loop-resume-v1";
    private const int AgentPromptContextExcerptMaxCharacters = 2500;
    private const int AgentValidationOutputExcerptMaxCharacters = 1200;
    private const int AgentProjectContextSampleLimit = 8;
    private const int AgentProjectGitHistoryCommitLimit = 5;
    private const int AgentProjectGitSubjectMaxCharacters = 120;
    private const int ResilienceRetryAttempts = 3;
    private const int ResilienceCircuitFailureThreshold = 3;
    private static readonly TimeSpan ResilienceRetryDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan ResilienceCircuitOpenDuration = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan AgentProjectGitHistoryCommandTimeout = TimeSpan.FromSeconds(2);
    private static readonly HashSet<string> AgentProjectCodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".fs",
        ".vb",
        ".js",
        ".jsx",
        ".ts",
        ".tsx",
        ".py",
        ".java",
        ".kt",
        ".go",
        ".rs",
        ".swift",
        ".php",
        ".rb",
        ".c",
        ".cpp",
        ".h",
        ".hpp",
        ".sql",
        ".ps1",
        ".sh"
    };
    private static readonly HashSet<string> AgentProjectDocumentationExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md",
        ".mdx",
        ".rst",
        ".adoc",
        ".txt"
    };
    private static readonly HashSet<string> AgentProjectDocumentationFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "readme",
        "readme.md",
        "changelog",
        "changelog.md",
        "license",
        "license.md",
        "contributing",
        "contributing.md"
    };
    private static readonly string[] InteractiveChatCommandSuggestions =
    [
        "/help",
        "/clear",
        "/models",
        "/tools",
        "/exit"
    ];
    private static readonly string[] CliCommandSuggestions =
    [
        "ask",
        "agent",
        "chat",
        "doctor",
        "models",
        "context",
        "patch",
        "history",
        "resume",
        "config",
        "mcp",
        "skills",
        "skill"
    ];
    private static readonly string[] CliOptionSuggestions =
    [
        "--model",
        "--max-steps",
        "--max-time",
        "--max-cost",
        "--dry-run",
        "--help",
        "--version",
        "--clear"
    ];
    private static int _executionStateSpinnerStep;
    private static readonly Func<string, string?, CancellationToken, IAsyncEnumerable<string>> DefaultPromptExecutor =
        static (prompt, model, cancellationToken) => ExecuteDefaultPromptStreamAsync(prompt, model, cancellationToken);
    private static readonly Func<CancellationToken, Task<OllamaHealthcheckResult>> DefaultHealthcheckExecutor =
        static cancellationToken => ExecuteDefaultHealthcheckAsync(cancellationToken);
    private static readonly Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> DefaultModelsExecutor =
        static cancellationToken => ExecuteDefaultModelsAsync(cancellationToken);
    private static readonly Func<UserRuntimeConfig> DefaultConfigLoader =
        static () => UserConfigFile.Load();
    private static readonly Action<UserRuntimeConfig> DefaultConfigSaver =
        static config => UserConfigFile.Save(config);
    private static readonly Func<IReadOnlyList<PromptHistoryEntry>> DefaultHistoryLoader =
        static () => UserHistoryFile.Load();
    private static readonly Action DefaultHistoryClearer =
        static () => UserHistoryFile.Clear();
    private static readonly Func<IReadOnlyList<McpServerDefinition>> DefaultMcpServersLoader =
        static () => McpServerCatalogFile.Load();
    private static readonly Action<IReadOnlyList<McpServerDefinition>> DefaultMcpServersSaver =
        static servers => McpServerCatalogFile.Save(servers);
    private static readonly Func<McpServerDefinition, CancellationToken, Task<McpServerTestResult>> DefaultMcpServerTester =
        static (server, cancellationToken) => ExecuteDefaultMcpServerTestAsync(server, cancellationToken);
    private static readonly Func<CancellationTokenSource, Action, IDisposable> DefaultCancelSignalRegistration =
        static (cancellationTokenSource, onCancellationRequested) =>
            RegisterConsoleCancelHandler(cancellationTokenSource, onCancellationRequested);
    private static readonly Func<WorkspacePatchAuditEntry, string> DefaultWorkspacePatchAuditAppender =
        static entry => WorkspacePatchAuditFile.Append(entry);
    private static readonly Action<ExecutionSessionCheckpoint> DefaultExecutionCheckpointAppender =
        static checkpoint => ExecutionCheckpointFile.Append(checkpoint);
    private static readonly Func<IReadOnlyList<ExecutionSessionCheckpoint>> DefaultExecutionCheckpointLoader =
        static () => ExecutionCheckpointFile.Load();
    private static readonly Action DefaultUserConfigInitializer =
        static () => _ = UserConfigFile.EnsureExists();
    private static readonly Action NoOpUserConfigInitializer = static () => { };
    private static readonly Func<WorkspacePatchAuditEntry, string> NoOpWorkspacePatchAuditAppender =
        static _ => string.Empty;
    private static readonly Action<ExecutionSessionCheckpoint> NoOpExecutionCheckpointAppender =
        static _ => { };
    private static readonly Func<IReadOnlyList<ExecutionSessionCheckpoint>> NoOpExecutionCheckpointLoader =
        static () => Array.Empty<ExecutionSessionCheckpoint>();
    private static readonly string CurrentExecutionSessionId = Guid.NewGuid().ToString("N");
    private static long _workspacePatchAuditSequence;

    public static int Main(string[] args)
    {
        return Run(
            args,
            DefaultPromptExecutor,
            DefaultHealthcheckExecutor,
            DefaultModelsExecutor,
            DefaultCancelSignalRegistration,
            DefaultUserConfigInitializer,
            applyConfiguredTheme: true,
            workspacePatchAuditAppender: DefaultWorkspacePatchAuditAppender,
            executionCheckpointAppender: DefaultExecutionCheckpointAppender,
            executionCheckpointLoader: DefaultExecutionCheckpointLoader);
    }

    internal static int RunForTests(string[] args)
    {
        return Run(
            args,
            DefaultPromptExecutor,
            DefaultHealthcheckExecutor,
            DefaultModelsExecutor,
            DefaultCancelSignalRegistration,
            NoOpUserConfigInitializer);
    }

    internal static int RunForTests(string[] args, Action userConfigInitializer)
    {
        return Run(
            args,
            DefaultPromptExecutor,
            DefaultHealthcheckExecutor,
            DefaultModelsExecutor,
            DefaultCancelSignalRegistration,
            userConfigInitializer);
    }

    internal static int RunForTests(string[] args, Func<string, string> promptExecutor)
    {
        return Run(
            args,
            WrapLegacyPromptExecutor(promptExecutor),
            DefaultHealthcheckExecutor,
            DefaultModelsExecutor,
            DefaultCancelSignalRegistration,
            NoOpUserConfigInitializer);
    }

    internal static int RunForTests(
        string[] args,
        Func<string, CancellationToken, IAsyncEnumerable<string>> promptExecutor)
    {
        ArgumentNullException.ThrowIfNull(promptExecutor);

        return Run(
            args,
            WrapPromptExecutorWithoutModel(promptExecutor),
            DefaultHealthcheckExecutor,
            DefaultModelsExecutor,
            DefaultCancelSignalRegistration,
            NoOpUserConfigInitializer);
    }

    internal static int RunForTests(
        string[] args,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor)
    {
        return Run(
            args,
            promptExecutor,
            DefaultHealthcheckExecutor,
            DefaultModelsExecutor,
            DefaultCancelSignalRegistration,
            NoOpUserConfigInitializer);
    }

    internal static int RunForTests(
        string[] args,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        IToolRuntime toolRuntime)
    {
        ArgumentNullException.ThrowIfNull(promptExecutor);
        ArgumentNullException.ThrowIfNull(toolRuntime);

        return Run(
            args,
            promptExecutor,
            DefaultHealthcheckExecutor,
            DefaultModelsExecutor,
            DefaultCancelSignalRegistration,
            NoOpUserConfigInitializer,
            toolRuntimeOverride: toolRuntime);
    }

    internal static int RunForTests(
        string[] args,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> modelsExecutor)
    {
        ArgumentNullException.ThrowIfNull(promptExecutor);
        ArgumentNullException.ThrowIfNull(modelsExecutor);

        return Run(
            args,
            promptExecutor,
            DefaultHealthcheckExecutor,
            modelsExecutor,
            DefaultCancelSignalRegistration,
            NoOpUserConfigInitializer);
    }

    internal static int RunForTests(
        string[] args,
        Func<string, string> promptExecutor,
        Func<CancellationToken, Task<OllamaHealthcheckResult>> healthcheckExecutor)
    {
        return Run(
            args,
            WrapLegacyPromptExecutor(promptExecutor),
            healthcheckExecutor,
            DefaultModelsExecutor,
            DefaultCancelSignalRegistration,
            NoOpUserConfigInitializer);
    }

    internal static int RunForTests(
        string[] args,
        Func<string, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationToken, Task<OllamaHealthcheckResult>> healthcheckExecutor)
    {
        ArgumentNullException.ThrowIfNull(promptExecutor);

        return Run(
            args,
            WrapPromptExecutorWithoutModel(promptExecutor),
            healthcheckExecutor,
            DefaultModelsExecutor,
            DefaultCancelSignalRegistration,
            NoOpUserConfigInitializer);
    }

    internal static int RunForTests(
        string[] args,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationToken, Task<OllamaHealthcheckResult>> healthcheckExecutor)
    {
        return Run(
            args,
            promptExecutor,
            healthcheckExecutor,
            DefaultModelsExecutor,
            DefaultCancelSignalRegistration,
            NoOpUserConfigInitializer);
    }

    internal static int RunForTests(
        string[] args,
        Func<string, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration)
    {
        ArgumentNullException.ThrowIfNull(promptExecutor);

        return Run(
            args,
            WrapPromptExecutorWithoutModel(promptExecutor),
            DefaultHealthcheckExecutor,
            DefaultModelsExecutor,
            cancelSignalRegistration,
            NoOpUserConfigInitializer);
    }

    internal static int RunForTests(
        string[] args,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration)
    {
        return Run(
            args,
            promptExecutor,
            DefaultHealthcheckExecutor,
            DefaultModelsExecutor,
            cancelSignalRegistration,
            NoOpUserConfigInitializer);
    }

    internal static int RunForTests(
        string[] args,
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> modelsExecutor)
    {
        return Run(
            args,
            DefaultPromptExecutor,
            DefaultHealthcheckExecutor,
            modelsExecutor,
            DefaultCancelSignalRegistration,
            NoOpUserConfigInitializer);
    }

    internal static int RunForTests(
        string[] args,
        Func<UserRuntimeConfig> configLoader,
        Action<UserRuntimeConfig> configSaver)
    {
        return Run(
            args,
            DefaultPromptExecutor,
            DefaultHealthcheckExecutor,
            DefaultModelsExecutor,
            DefaultCancelSignalRegistration,
            NoOpUserConfigInitializer,
            configLoader: configLoader,
            configSaver: configSaver);
    }

    internal static int RunForTests(
        string[] args,
        Func<IReadOnlyList<PromptHistoryEntry>> historyLoader)
    {
        ArgumentNullException.ThrowIfNull(historyLoader);

        return Run(
            args,
            DefaultPromptExecutor,
            DefaultHealthcheckExecutor,
            DefaultModelsExecutor,
            DefaultCancelSignalRegistration,
            NoOpUserConfigInitializer,
            historyLoader: historyLoader);
    }

    internal static int RunForTests(
        string[] args,
        Func<IReadOnlyList<PromptHistoryEntry>> historyLoader,
        Action historyClearer)
    {
        ArgumentNullException.ThrowIfNull(historyLoader);
        ArgumentNullException.ThrowIfNull(historyClearer);

        return Run(
            args,
            DefaultPromptExecutor,
            DefaultHealthcheckExecutor,
            DefaultModelsExecutor,
            DefaultCancelSignalRegistration,
            NoOpUserConfigInitializer,
            historyLoader: historyLoader,
            historyClearer: historyClearer);
    }

    internal static int RunForTests(
        string[] args,
        Func<IReadOnlyList<McpServerDefinition>> mcpServersLoader,
        Action<IReadOnlyList<McpServerDefinition>> mcpServersSaver,
        Func<McpServerDefinition, CancellationToken, Task<McpServerTestResult>>? mcpServerTester = null)
    {
        ArgumentNullException.ThrowIfNull(mcpServersLoader);
        ArgumentNullException.ThrowIfNull(mcpServersSaver);

        return Run(
            args,
            DefaultPromptExecutor,
            DefaultHealthcheckExecutor,
            DefaultModelsExecutor,
            DefaultCancelSignalRegistration,
            NoOpUserConfigInitializer,
            mcpServersLoader: mcpServersLoader,
            mcpServersSaver: mcpServersSaver,
            mcpServerTester: mcpServerTester ?? DefaultMcpServerTester);
    }

    internal static int RunForTests(
        string[] args,
        Func<WorkspacePatchAuditEntry, string> workspacePatchAuditAppender)
    {
        ArgumentNullException.ThrowIfNull(workspacePatchAuditAppender);

        return Run(
            args,
            DefaultPromptExecutor,
            DefaultHealthcheckExecutor,
            DefaultModelsExecutor,
            DefaultCancelSignalRegistration,
            NoOpUserConfigInitializer,
            workspacePatchAuditAppender: workspacePatchAuditAppender);
    }

    internal static int RunForTests(
        string[] args,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Action<ExecutionSessionCheckpoint> executionCheckpointAppender,
        Func<IReadOnlyList<ExecutionSessionCheckpoint>> executionCheckpointLoader)
    {
        ArgumentNullException.ThrowIfNull(promptExecutor);
        ArgumentNullException.ThrowIfNull(executionCheckpointAppender);
        ArgumentNullException.ThrowIfNull(executionCheckpointLoader);

        return Run(
            args,
            promptExecutor,
            DefaultHealthcheckExecutor,
            DefaultModelsExecutor,
            DefaultCancelSignalRegistration,
            NoOpUserConfigInitializer,
            executionCheckpointAppender: executionCheckpointAppender,
            executionCheckpointLoader: executionCheckpointLoader);
    }

    private static int Run(
        string[] args,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationToken, Task<OllamaHealthcheckResult>> healthcheckExecutor,
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> modelsExecutor,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration,
        Action userConfigInitializer,
        bool applyConfiguredTheme = false,
        Func<UserRuntimeConfig>? configLoader = null,
        Action<UserRuntimeConfig>? configSaver = null,
        Func<IReadOnlyList<PromptHistoryEntry>>? historyLoader = null,
        Action? historyClearer = null,
        Func<IReadOnlyList<McpServerDefinition>>? mcpServersLoader = null,
        Action<IReadOnlyList<McpServerDefinition>>? mcpServersSaver = null,
        Func<McpServerDefinition, CancellationToken, Task<McpServerTestResult>>? mcpServerTester = null,
        Func<WorkspacePatchAuditEntry, string>? workspacePatchAuditAppender = null,
        Action<ExecutionSessionCheckpoint>? executionCheckpointAppender = null,
        Func<IReadOnlyList<ExecutionSessionCheckpoint>>? executionCheckpointLoader = null,
        IToolRuntime? toolRuntimeOverride = null)
    {
        ArgumentNullException.ThrowIfNull(promptExecutor);
        ArgumentNullException.ThrowIfNull(healthcheckExecutor);
        ArgumentNullException.ThrowIfNull(modelsExecutor);
        ArgumentNullException.ThrowIfNull(cancelSignalRegistration);
        ArgumentNullException.ThrowIfNull(userConfigInitializer);

        configLoader ??= DefaultConfigLoader;
        configSaver ??= DefaultConfigSaver;
        historyLoader ??= DefaultHistoryLoader;
        historyClearer ??= DefaultHistoryClearer;
        mcpServersLoader ??= DefaultMcpServersLoader;
        mcpServersSaver ??= DefaultMcpServersSaver;
        mcpServerTester ??= DefaultMcpServerTester;
        workspacePatchAuditAppender ??= NoOpWorkspacePatchAuditAppender;
        executionCheckpointAppender ??= NoOpExecutionCheckpointAppender;
        executionCheckpointLoader ??= NoOpExecutionCheckpointLoader;
        var promptResilience = new ResilienceState("Ollama/prompt");
        var healthcheckResilience = new ResilienceState("Ollama/healthcheck");
        var modelsResilience = new ResilienceState("Ollama/models");
        var mcpResilience = new ResilienceState("MCP/test");
        var resilientPromptExecutor = WrapPromptExecutorWithResilience(promptExecutor, promptResilience);
        var resilientHealthcheckExecutor = WrapHealthcheckExecutorWithResilience(
            healthcheckExecutor,
            healthcheckResilience);
        var resilientModelsExecutor = WrapModelsExecutorWithResilience(modelsExecutor, modelsResilience);
        var fallbackPromptExecutor = WrapPromptExecutorWithModelFallback(
            resilientPromptExecutor,
            resilientModelsExecutor);
        var resilientMcpServerTester = WrapMcpServerTesterWithResilience(mcpServerTester, mcpResilience);

        try
        {
            userConfigInitializer();
            var toolRuntime = toolRuntimeOverride ?? CreateDefaultToolRuntime();

            ConsoleLogger.ConfigureTheme(UserRuntimeConfig.Default.Theme);
            if (applyConfiguredTheme)
            {
                TryConfigureTerminalTheme(configLoader);
            }

            var parseResult = ParseArguments(args);

            if (parseResult.Error is CliFriendlyError error)
            {
                WriteFriendlyError(error);
                return (int)error.ExitCode;
            }

            if (parseResult.ShowHelp)
            {
                WriteHelp();
                return (int)CliExitCode.Success;
            }

            if (parseResult.ShowVersion)
            {
                WriteVersion();
                return (int)CliExitCode.Success;
            }

            if (parseResult.RunResume)
            {
                return ExecuteResume(
                    parseResult.ResumeSessionId,
                    fallbackPromptExecutor,
                    cancelSignalRegistration,
                    executionCheckpointAppender,
                    executionCheckpointLoader,
                    toolRuntime);
            }

            if (parseResult.RunAgent && parseResult.AgentObjective is not null)
            {
                return ExecuteAgent(
                    parseResult.AgentObjective,
                    parseResult.SelectedModel,
                    parseResult.AgentMaxSteps,
                    parseResult.AgentMaxTime,
                    parseResult.AgentMaxCost,
                    fallbackPromptExecutor,
                    cancelSignalRegistration,
                    executionCheckpointAppender,
                    toolRuntime);
            }

            if (parseResult.AskPrompt is not null)
            {
                return ExecuteAsk(
                    parseResult.AskPrompt,
                    parseResult.SelectedModel,
                    fallbackPromptExecutor,
                    cancelSignalRegistration,
                    executionCheckpointAppender);
            }

            if (parseResult.StartChat)
            {
                return ExecuteChat(
                    parseResult.SelectedModel,
                    fallbackPromptExecutor,
                    resilientModelsExecutor,
                    toolRuntime,
                    cancelSignalRegistration,
                    historyLoader);
            }

            if (parseResult.RunDoctor)
            {
                return ExecuteDoctor(resilientHealthcheckExecutor);
            }

            if (parseResult.RunModels)
            {
                return ExecuteModels(resilientModelsExecutor);
            }

            if (parseResult.RunContext)
            {
                return ExecuteContext();
            }

            if (parseResult.PatchRequestFilePath is string patchRequestFilePath)
            {
                return ExecutePatch(
                    patchRequestFilePath,
                    parseResult.PatchDryRun,
                    workspacePatchAuditAppender);
            }

            if (parseResult.RunHistory)
            {
                return ExecuteHistory(
                    historyLoader,
                    historyClearer,
                    parseResult.ClearHistory);
            }

            if (parseResult.RunMcpList)
            {
                return ExecuteMcpList(mcpServersLoader);
            }

            if (parseResult.McpServerToAdd is McpServerDefinition serverToAdd)
            {
                return ExecuteMcpAdd(
                    serverToAdd,
                    mcpServersLoader,
                    mcpServersSaver);
            }

            if (parseResult.McpServerNameToRemove is string serverNameToRemove)
            {
                return ExecuteMcpRemove(
                    serverNameToRemove,
                    mcpServersLoader,
                    mcpServersSaver);
            }

            if (parseResult.McpServerNameToTest is string serverNameToTest)
            {
                return ExecuteMcpTest(
                    serverNameToTest,
                    mcpServersLoader,
                    resilientMcpServerTester);
            }

            if (parseResult.ConfigGetKey is not null)
            {
                return ExecuteConfigGet(parseResult.ConfigGetKey, configLoader);
            }

            if (parseResult.ConfigSetKey is not null && parseResult.ConfigSetValue is not null)
            {
                return ExecuteConfigSet(
                    parseResult.ConfigSetKey,
                    parseResult.ConfigSetValue,
                    configLoader,
                    configSaver);
            }

            if (parseResult.RunSkills)
            {
                return ExecuteSkills();
            }

            if (parseResult.RunSkillsReload)
            {
                return ExecuteSkillsReload();
            }

            if (parseResult.ShowSkillName is not null)
            {
                return ExecuteShowSkill(parseResult.ShowSkillName);
            }

            if (parseResult.RunSkillsInit)
            {
                return ExecuteSkillsInit();
            }

            if (parseResult.SkillPrompt is not null && parseResult.RunSkillName is not null)
            {
                return ExecuteSkill(
                    parseResult.RunSkillName,
                    parseResult.SkillPrompt,
                    parseResult.SelectedModel,
                    fallbackPromptExecutor,
                    cancelSignalRegistration,
                    executionCheckpointAppender);
            }

            ConsoleLogger.Info("ASXRunTerminal CLI inicializado.");
            return (int)CliExitCode.Success;
        }
        catch (Exception ex)
        {
            var friendlyError = CliFriendlyError.Runtime($"Ocorreu um erro interno: {ex.Message}");
            WriteFriendlyError(friendlyError);
            return (int)friendlyError.ExitCode;
        }
    }

    private static ParseResult ParseArguments(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "agent", StringComparison.OrdinalIgnoreCase))
        {
            return ParseAgentArguments(args);
        }

        if (args.Length > 0 && string.Equals(args[0], "ask", StringComparison.OrdinalIgnoreCase))
        {
            return ParseAskArguments(args);
        }

        if (args.Length > 0 && string.Equals(args[0], "chat", StringComparison.OrdinalIgnoreCase))
        {
            return ParseChatArguments(args);
        }

        if (args.Length > 0 && string.Equals(args[0], "doctor", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length > 1)
            {
                return new ParseResult(
                    ShowHelp: false,
                    ShowVersion: false,
                    AskPrompt: null,
                    StartChat: false,
                    RunDoctor: false,
                    RunModels: false,
                    SelectedModel: null,
                    Error: CliFriendlyError.InvalidArguments(
                        detail: "O comando 'doctor' nao aceita argumentos adicionais.",
                        suggestion: $"Exemplo: {CliName} doctor."));
            }

            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: true,
                RunModels: false,
                SelectedModel: null,
                Error: null);
        }

        if (args.Length > 0 && string.Equals(args[0], "models", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length > 1)
            {
                return new ParseResult(
                    ShowHelp: false,
                    ShowVersion: false,
                    AskPrompt: null,
                    StartChat: false,
                    RunDoctor: false,
                    RunModels: false,
                    SelectedModel: null,
                    Error: CliFriendlyError.InvalidArguments(
                        detail: "O comando 'models' nao aceita argumentos adicionais.",
                        suggestion: $"Exemplo: {CliName} models."));
            }

            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: true,
                SelectedModel: null,
                Error: null);
        }

        if (args.Length > 0 && string.Equals(args[0], "context", StringComparison.OrdinalIgnoreCase))
        {
            return ParseContextArguments(args);
        }

        if (args.Length > 0 && string.Equals(args[0], "patch", StringComparison.OrdinalIgnoreCase))
        {
            return ParsePatchArguments(args);
        }

        if (args.Length > 0 && string.Equals(args[0], "config", StringComparison.OrdinalIgnoreCase))
        {
            return ParseConfigArguments(args);
        }

        if (args.Length > 0 && string.Equals(args[0], "history", StringComparison.OrdinalIgnoreCase))
        {
            return ParseHistoryArguments(args);
        }

        if (args.Length > 0 && string.Equals(args[0], "resume", StringComparison.OrdinalIgnoreCase))
        {
            return ParseResumeArguments(args);
        }

        if (args.Length > 0 && string.Equals(args[0], "mcp", StringComparison.OrdinalIgnoreCase))
        {
            return ParseMcpArguments(args);
        }

        if (args.Length > 0 && string.Equals(args[0], "skills", StringComparison.OrdinalIgnoreCase))
        {
            return ParseSkillsArguments(args);
        }

        if (args.Length > 0 && string.Equals(args[0], "skill", StringComparison.OrdinalIgnoreCase))
        {
            return ParseSkillArguments(args);
        }

        var showHelp = false;
        var showVersion = false;

        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                case "--version":
                case "-v":
                    showVersion = true;
                    break;
                default:
                    return new ParseResult(
                        ShowHelp: false,
                        ShowVersion: false,
                        AskPrompt: null,
                        StartChat: false,
                        RunDoctor: false,
                        RunModels: false,
                        SelectedModel: null,
                        Error: CliFriendlyError.InvalidArguments(
                            detail: $"A opcao '{arg}' nao e reconhecida."));
            }
        }

        if (showHelp && showVersion)
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: CliFriendlyError.InvalidArguments(
                    detail: "Escolha apenas uma opcao por execucao: --help ou --version."));
        }

        return new ParseResult(
            ShowHelp: showHelp,
            ShowVersion: showVersion,
            AskPrompt: null,
            StartChat: false,
            RunDoctor: false,
            RunModels: false,
            SelectedModel: null,
            Error: null);
    }

    private static ParseResult ParseConfigArguments(string[] args)
    {
        if (args.Length < 2)
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: CliFriendlyError.InvalidArguments(
                    detail: "O comando 'config' exige uma acao: 'set' ou 'get'.",
                    suggestion: $"Exemplos: {CliName} config get {UserConfigFile.DefaultModelKey} | {CliName} config set {UserConfigFile.DefaultModelKey} {OllamaModelDefaults.DefaultModel}."));
        }

        var action = args[1].Trim();
        if (string.Equals(action, "get", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 3)
            {
                return new ParseResult(
                    ShowHelp: false,
                    ShowVersion: false,
                    AskPrompt: null,
                    StartChat: false,
                    RunDoctor: false,
                    RunModels: false,
                    SelectedModel: null,
                    Error: CliFriendlyError.InvalidArguments(
                        detail: "O comando 'config get' exige exatamente uma chave.",
                        suggestion: $"Exemplo: {CliName} config get {UserConfigFile.DefaultModelKey}."));
            }

            return ParseConfigGetKey(args[2]);
        }

        if (string.Equals(action, "set", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 4)
            {
                return new ParseResult(
                    ShowHelp: false,
                    ShowVersion: false,
                    AskPrompt: null,
                    StartChat: false,
                    RunDoctor: false,
                    RunModels: false,
                    SelectedModel: null,
                    Error: CliFriendlyError.InvalidArguments(
                        detail: "O comando 'config set' exige uma chave e um valor.",
                        suggestion: $"Exemplo: {CliName} config set {UserConfigFile.DefaultModelKey} {OllamaModelDefaults.DefaultModel}."));
            }

            return ParseConfigSetArguments(args[2], args[3..]);
        }

        return new ParseResult(
            ShowHelp: false,
            ShowVersion: false,
            AskPrompt: null,
            StartChat: false,
            RunDoctor: false,
            RunModels: false,
            SelectedModel: null,
            Error: CliFriendlyError.InvalidArguments(
                detail: $"A acao '{action}' nao e suportada no comando 'config'. Use 'set' ou 'get'.",
                suggestion: $"Exemplos: {CliName} config get {UserConfigFile.DefaultModelKey} | {CliName} config set {UserConfigFile.DefaultModelKey} {OllamaModelDefaults.DefaultModel}."));
    }

    private static ParseResult ParseHistoryArguments(string[] args)
    {
        if (args.Length == 1)
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: null,
                RunHistory: true);
        }

        if (args.Length == 2 && string.Equals(args[1], "--clear", StringComparison.OrdinalIgnoreCase))
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: null,
                RunHistory: true,
                ClearHistory: true);
        }

        return new ParseResult(
            ShowHelp: false,
            ShowVersion: false,
            AskPrompt: null,
            StartChat: false,
            RunDoctor: false,
            RunModels: false,
            SelectedModel: null,
            Error: CliFriendlyError.InvalidArguments(
                detail: "O comando 'history' aceita apenas a opcao '--clear'.",
                suggestion: $"Exemplos: {CliName} history | {CliName} history --clear."));
    }

    private static ParseResult ParseResumeArguments(string[] args)
    {
        if (args.Length == 1)
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: null,
                RunResume: true);
        }

        if (args.Length == 2)
        {
            var sessionId = args[1].Trim();
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return new ParseResult(
                    ShowHelp: false,
                    ShowVersion: false,
                    AskPrompt: null,
                    StartChat: false,
                    RunDoctor: false,
                    RunModels: false,
                    SelectedModel: null,
                    Error: CliFriendlyError.InvalidArguments(
                        detail: "O identificador de sessao informado para 'resume' esta vazio.",
                        suggestion: $"Exemplo: {CliName} resume <session-id>."));
            }

            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: null,
                RunResume: true,
                ResumeSessionId: sessionId);
        }

        return new ParseResult(
            ShowHelp: false,
            ShowVersion: false,
            AskPrompt: null,
            StartChat: false,
            RunDoctor: false,
            RunModels: false,
            SelectedModel: null,
            Error: CliFriendlyError.InvalidArguments(
                detail: "O comando 'resume' aceita no maximo um identificador de sessao.",
                suggestion: $"Exemplos: {CliName} resume | {CliName} resume <session-id>."));
    }

    private static ParseResult ParseContextArguments(string[] args)
    {
        if (args.Length == 1)
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: null,
                RunContext: true);
        }

        return new ParseResult(
            ShowHelp: false,
            ShowVersion: false,
            AskPrompt: null,
            StartChat: false,
            RunDoctor: false,
            RunModels: false,
            SelectedModel: null,
            Error: CliFriendlyError.InvalidArguments(
                detail: "O comando 'context' nao aceita argumentos adicionais.",
                suggestion: $"Exemplo: {CliName} context."));
    }

    private static ParseResult ParsePatchArguments(string[] args)
    {
        var dryRun = false;
        string? patchRequestFilePath = null;

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];

            if (string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                if (dryRun)
                {
                    return new ParseResult(
                        ShowHelp: false,
                        ShowVersion: false,
                        AskPrompt: null,
                        StartChat: false,
                        RunDoctor: false,
                        RunModels: false,
                        SelectedModel: null,
                        Error: CliFriendlyError.InvalidArguments(
                            detail: "A opcao '--dry-run' foi informada mais de uma vez no comando 'patch'.",
                            suggestion: $"Exemplo: {CliName} patch --dry-run patch.json."));
                }

                dryRun = true;
                continue;
            }

            if (argument.StartsWith("-", StringComparison.Ordinal))
            {
                return new ParseResult(
                    ShowHelp: false,
                    ShowVersion: false,
                    AskPrompt: null,
                    StartChat: false,
                    RunDoctor: false,
                    RunModels: false,
                    SelectedModel: null,
                    Error: CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{argument}' nao e reconhecida no comando 'patch'.",
                        suggestion: $"Exemplo: {CliName} patch --dry-run patch.json."));
            }

            if (patchRequestFilePath is not null)
            {
                return new ParseResult(
                    ShowHelp: false,
                    ShowVersion: false,
                    AskPrompt: null,
                    StartChat: false,
                    RunDoctor: false,
                    RunModels: false,
                    SelectedModel: null,
                    Error: CliFriendlyError.InvalidArguments(
                        detail: "O comando 'patch' aceita apenas um arquivo JSON de requisicao.",
                        suggestion: $"Exemplo: {CliName} patch patch.json."));
            }

            patchRequestFilePath = argument.Trim();
        }

        if (string.IsNullOrWhiteSpace(patchRequestFilePath))
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: CliFriendlyError.InvalidArguments(
                    detail: "O comando 'patch' exige um arquivo JSON com a requisicao de mudancas.",
                    suggestion: $"Exemplo: {CliName} patch --dry-run patch.json."));
        }

        return new ParseResult(
            ShowHelp: false,
            ShowVersion: false,
            AskPrompt: null,
            StartChat: false,
            RunDoctor: false,
            RunModels: false,
            SelectedModel: null,
            Error: null,
            PatchRequestFilePath: patchRequestFilePath,
            PatchDryRun: dryRun);
    }

    private static ParseResult ParseMcpArguments(string[] args)
    {
        if (args.Length < 2)
        {
            return BuildMcpParseResult(
                error: CliFriendlyError.InvalidArguments(
                    detail: "O comando 'mcp' exige uma acao: 'list', 'add', 'remove' ou 'test'.",
                    suggestion: $"Exemplos: {CliName} mcp list | {CliName} mcp add meu-servidor --command node --arg server.js."));
        }

        var action = args[1].Trim();
        if (string.Equals(action, "list", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 2)
            {
                return BuildMcpParseResult(
                    error: CliFriendlyError.InvalidArguments(
                        detail: "O comando 'mcp list' nao aceita argumentos adicionais.",
                        suggestion: $"Exemplo: {CliName} mcp list."));
            }

            return BuildMcpParseResult(runMcpList: true);
        }

        if (string.Equals(action, "add", StringComparison.OrdinalIgnoreCase))
        {
            return ParseMcpAddArguments(args);
        }

        if (string.Equals(action, "remove", StringComparison.OrdinalIgnoreCase))
        {
            return ParseMcpRemoveArguments(args);
        }

        if (string.Equals(action, "test", StringComparison.OrdinalIgnoreCase))
        {
            return ParseMcpTestArguments(args);
        }

        return BuildMcpParseResult(
            error: CliFriendlyError.InvalidArguments(
                detail: $"A acao '{action}' nao e suportada no comando 'mcp'. Use 'list', 'add', 'remove' ou 'test'.",
                suggestion: $"Exemplos: {CliName} mcp list | {CliName} mcp add meu-servidor --command node --arg server.js."));
    }

    private static ParseResult ParseMcpAddArguments(string[] args)
    {
        if (args.Length < 3 || string.IsNullOrWhiteSpace(args[2]))
        {
            return BuildMcpParseResult(
                error: CliFriendlyError.InvalidArguments(
                    detail: "Voce precisa informar um nome para o comando 'mcp add'.",
                    suggestion: $"Exemplo: {CliName} mcp add meu-servidor --command node --arg server.js."));
        }

        var serverName = args[2].Trim();
        if (serverName.StartsWith('-'))
        {
            return BuildMcpParseResult(
                error: CliFriendlyError.InvalidArguments(
                    detail: "O nome informado para o comando 'mcp add' e invalido.",
                    suggestion: $"Exemplo: {CliName} mcp add meu-servidor --command node --arg server.js."));
        }

        string? command = null;
        var commandArguments = new List<string>();
        string? workingDirectory = null;
        var environmentVariables = new Dictionary<string, string>(StringComparer.Ordinal);
        string? endpoint = null;
        var transportKind = McpRemoteTransportKind.Http;
        var transportInformed = false;
        string? messageEndpoint = null;
        string? bearerToken = null;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 3; index < args.Length; index++)
        {
            if (TryReadOptionValue(
                    args,
                    ref index,
                    "--command",
                    out var commandValue,
                    out var optionError))
            {
                if (optionError is CliFriendlyError error)
                {
                    return BuildMcpParseResult(error: error);
                }

                if (command is not null)
                {
                    return BuildMcpParseResult(
                        error: CliFriendlyError.InvalidArguments(
                            detail: "A opcao '--command' foi informada mais de uma vez no comando 'mcp add'.",
                            suggestion: $"Exemplo: {CliName} mcp add {serverName} --command node --arg server.js."));
                }

                command = commandValue;
                continue;
            }

            if (TryReadOptionValue(
                    args,
                    ref index,
                    "--arg",
                    out var argumentValue,
                    out optionError))
            {
                if (optionError is CliFriendlyError error)
                {
                    return BuildMcpParseResult(error: error);
                }

                commandArguments.Add(argumentValue);
                continue;
            }

            if (TryReadOptionValue(
                    args,
                    ref index,
                    "--cwd",
                    out var workingDirectoryValue,
                    out optionError))
            {
                if (optionError is CliFriendlyError error)
                {
                    return BuildMcpParseResult(error: error);
                }

                if (workingDirectory is not null)
                {
                    return BuildMcpParseResult(
                        error: CliFriendlyError.InvalidArguments(
                            detail: "A opcao '--cwd' foi informada mais de uma vez no comando 'mcp add'.",
                            suggestion: $"Exemplo: {CliName} mcp add {serverName} --command node --cwd . --arg server.js."));
                }

                workingDirectory = workingDirectoryValue;
                continue;
            }

            if (TryReadOptionValue(
                    args,
                    ref index,
                    "--env",
                    out var environmentVariableValue,
                    out optionError))
            {
                if (optionError is CliFriendlyError error)
                {
                    return BuildMcpParseResult(error: error);
                }

                if (!TryParseNameValuePair(environmentVariableValue, out var key, out var value))
                {
                    return BuildMcpParseResult(
                        error: CliFriendlyError.InvalidArguments(
                            detail: "A opcao '--env' exige o formato CHAVE=VALOR.",
                            suggestion: $"Exemplo: {CliName} mcp add {serverName} --command node --env NODE_ENV=production."));
                }

                environmentVariables[key] = value;
                continue;
            }

            if (TryReadOptionValue(
                    args,
                    ref index,
                    "--url",
                    out var endpointValue,
                    out optionError))
            {
                if (optionError is CliFriendlyError error)
                {
                    return BuildMcpParseResult(error: error);
                }

                if (endpoint is not null)
                {
                    return BuildMcpParseResult(
                        error: CliFriendlyError.InvalidArguments(
                            detail: "A opcao '--url' foi informada mais de uma vez no comando 'mcp add'.",
                            suggestion: $"Exemplo: {CliName} mcp add {serverName} --url https://mcp.example.com/rpc."));
                }

                endpoint = endpointValue;
                continue;
            }

            if (TryReadOptionValue(
                    args,
                    ref index,
                    "--transport",
                    out var transportValue,
                    out optionError))
            {
                if (optionError is CliFriendlyError error)
                {
                    return BuildMcpParseResult(error: error);
                }

                if (transportInformed)
                {
                    return BuildMcpParseResult(
                        error: CliFriendlyError.InvalidArguments(
                            detail: "A opcao '--transport' foi informada mais de uma vez no comando 'mcp add'.",
                            suggestion: $"Exemplo: {CliName} mcp add {serverName} --url https://mcp.example.com/sse --transport sse."));
                }

                if (string.Equals(transportValue, "http", StringComparison.OrdinalIgnoreCase))
                {
                    transportKind = McpRemoteTransportKind.Http;
                }
                else if (string.Equals(transportValue, "sse", StringComparison.OrdinalIgnoreCase))
                {
                    transportKind = McpRemoteTransportKind.Sse;
                }
                else
                {
                    return BuildMcpParseResult(
                        error: CliFriendlyError.InvalidArguments(
                            detail: "A opcao '--transport' aceita apenas 'http' ou 'sse'.",
                            suggestion: $"Exemplo: {CliName} mcp add {serverName} --url https://mcp.example.com/sse --transport sse."));
                }
                transportInformed = true;
                continue;
            }

            if (TryReadOptionValue(
                    args,
                    ref index,
                    "--message-url",
                    out var messageEndpointValue,
                    out optionError))
            {
                if (optionError is CliFriendlyError error)
                {
                    return BuildMcpParseResult(error: error);
                }

                if (messageEndpoint is not null)
                {
                    return BuildMcpParseResult(
                        error: CliFriendlyError.InvalidArguments(
                            detail: "A opcao '--message-url' foi informada mais de uma vez no comando 'mcp add'.",
                            suggestion: $"Exemplo: {CliName} mcp add {serverName} --url https://mcp.example.com/sse --message-url https://mcp.example.com/messages."));
                }

                messageEndpoint = messageEndpointValue;
                continue;
            }

            if (TryReadOptionValue(
                    args,
                    ref index,
                    "--bearer-token",
                    out var bearerTokenValue,
                    out optionError))
            {
                if (optionError is CliFriendlyError error)
                {
                    return BuildMcpParseResult(error: error);
                }

                if (bearerToken is not null)
                {
                    return BuildMcpParseResult(
                        error: CliFriendlyError.InvalidArguments(
                            detail: "A opcao '--bearer-token' foi informada mais de uma vez no comando 'mcp add'.",
                            suggestion: $"Exemplo: {CliName} mcp add {serverName} --url https://mcp.example.com/rpc --bearer-token <token>."));
                }

                bearerToken = bearerTokenValue;
                continue;
            }

            if (TryReadOptionValue(
                    args,
                    ref index,
                    "--header",
                    out var headerValue,
                    out optionError))
            {
                if (optionError is CliFriendlyError error)
                {
                    return BuildMcpParseResult(error: error);
                }

                if (!TryParseNameValuePair(headerValue, out var headerName, out var headerContent))
                {
                    return BuildMcpParseResult(
                        error: CliFriendlyError.InvalidArguments(
                            detail: "A opcao '--header' exige o formato NOME=VALOR.",
                            suggestion: $"Exemplo: {CliName} mcp add {serverName} --url https://mcp.example.com/rpc --header X-Api-Key=abc."));
                }

                headers[headerName] = headerContent;
                continue;
            }

            if (string.Equals(args[index], "--transport", StringComparison.OrdinalIgnoreCase)
                || args[index].StartsWith("--transport=", StringComparison.OrdinalIgnoreCase))
            {
                return BuildMcpParseResult(
                    error: CliFriendlyError.InvalidArguments(
                        detail: "A opcao '--transport' aceita apenas 'http' ou 'sse'.",
                        suggestion: $"Exemplo: {CliName} mcp add {serverName} --url https://mcp.example.com/sse --transport sse."));
            }

            return BuildMcpParseResult(
                error: CliFriendlyError.InvalidArguments(
                    detail: $"A opcao '{args[index]}' nao e reconhecida no comando 'mcp add'.",
                    suggestion: $"Exemplo: {CliName} mcp add {serverName} --command node --arg server.js."));
        }

        var hasStdioConfiguration = !string.IsNullOrWhiteSpace(command);
        var hasRemoteConfiguration = !string.IsNullOrWhiteSpace(endpoint);
        if (hasStdioConfiguration == hasRemoteConfiguration)
        {
            return BuildMcpParseResult(
                error: CliFriendlyError.InvalidArguments(
                    detail: "Informe exatamente um tipo de servidor MCP: --command <cmd> (stdio) ou --url <endpoint> (remoto).",
                    suggestion: $"Exemplos: {CliName} mcp add {serverName} --command node --arg server.js | {CliName} mcp add {serverName} --url https://mcp.example.com/rpc."));
        }

        if (hasStdioConfiguration)
        {
            if (transportInformed || messageEndpoint is not null || bearerToken is not null || headers.Count > 0)
            {
                return BuildMcpParseResult(
                    error: CliFriendlyError.InvalidArguments(
                        detail: "Opcoes remotas (--transport, --message-url, --bearer-token, --header) nao podem ser usadas com '--command'.",
                        suggestion: $"Exemplo: {CliName} mcp add {serverName} --command node --arg server.js."));
            }

            McpServerProcessOptions processOptions;
            try
            {
                processOptions = new McpServerProcessOptions(
                    command: command!,
                    arguments: commandArguments,
                    workingDirectory: workingDirectory,
                    environmentVariables: environmentVariables);
            }
            catch (ArgumentException ex)
            {
                return BuildMcpParseResult(
                    error: CliFriendlyError.InvalidArguments(
                        detail: ex.Message,
                        suggestion: $"Exemplo: {CliName} mcp add {serverName} --command node --arg server.js."));
            }

            return BuildMcpParseResult(
                mcpServerToAdd: McpServerDefinition.Stdio(serverName, processOptions));
        }

        if (commandArguments.Count > 0 || workingDirectory is not null || environmentVariables.Count > 0)
        {
            return BuildMcpParseResult(
                error: CliFriendlyError.InvalidArguments(
                    detail: "Opcoes de processo local (--arg, --cwd, --env) nao podem ser usadas com '--url'.",
                    suggestion: $"Exemplo: {CliName} mcp add {serverName} --url https://mcp.example.com/rpc."));
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        {
            return BuildMcpParseResult(
                error: CliFriendlyError.InvalidArguments(
                    detail: "A opcao '--url' exige uma URL absoluta.",
                    suggestion: $"Exemplo: {CliName} mcp add {serverName} --url https://mcp.example.com/rpc."));
        }

        Uri? messageEndpointUri = null;
        if (!string.IsNullOrWhiteSpace(messageEndpoint)
            && !Uri.TryCreate(messageEndpoint, UriKind.Absolute, out messageEndpointUri))
        {
            return BuildMcpParseResult(
                error: CliFriendlyError.InvalidArguments(
                    detail: "A opcao '--message-url' exige uma URL absoluta.",
                    suggestion: $"Exemplo: {CliName} mcp add {serverName} --url https://mcp.example.com/sse --message-url https://mcp.example.com/messages."));
        }

        McpServerRemoteOptions remoteOptions;
        try
        {
            remoteOptions = new McpServerRemoteOptions(
                endpoint: endpointUri,
                transportKind: transportKind,
                messageEndpoint: messageEndpointUri,
                authentication: string.IsNullOrWhiteSpace(bearerToken)
                    ? McpAuthenticationOptions.None
                    : McpAuthenticationOptions.Bearer(bearerToken),
                headers: headers);
        }
        catch (ArgumentException ex)
        {
            return BuildMcpParseResult(
                error: CliFriendlyError.InvalidArguments(
                    detail: ex.Message,
                    suggestion: $"Exemplo: {CliName} mcp add {serverName} --url https://mcp.example.com/rpc."));
        }

        return BuildMcpParseResult(
            mcpServerToAdd: McpServerDefinition.Remote(serverName, remoteOptions));
    }

    private static ParseResult ParseMcpRemoveArguments(string[] args)
    {
        if (args.Length != 3 || string.IsNullOrWhiteSpace(args[2]))
        {
            return BuildMcpParseResult(
                error: CliFriendlyError.InvalidArguments(
                    detail: "O comando 'mcp remove' exige exatamente um nome de servidor.",
                    suggestion: $"Exemplo: {CliName} mcp remove meu-servidor."));
        }

        var serverName = args[2].Trim();
        if (serverName.StartsWith('-'))
        {
            return BuildMcpParseResult(
                error: CliFriendlyError.InvalidArguments(
                    detail: "O nome informado para o comando 'mcp remove' e invalido.",
                    suggestion: $"Exemplo: {CliName} mcp remove meu-servidor."));
        }

        return BuildMcpParseResult(mcpServerNameToRemove: serverName);
    }

    private static ParseResult ParseMcpTestArguments(string[] args)
    {
        if (args.Length != 3 || string.IsNullOrWhiteSpace(args[2]))
        {
            return BuildMcpParseResult(
                error: CliFriendlyError.InvalidArguments(
                    detail: "O comando 'mcp test' exige exatamente um nome de servidor.",
                    suggestion: $"Exemplo: {CliName} mcp test meu-servidor."));
        }

        var serverName = args[2].Trim();
        if (serverName.StartsWith('-'))
        {
            return BuildMcpParseResult(
                error: CliFriendlyError.InvalidArguments(
                    detail: "O nome informado para o comando 'mcp test' e invalido.",
                    suggestion: $"Exemplo: {CliName} mcp test meu-servidor."));
        }

        return BuildMcpParseResult(mcpServerNameToTest: serverName);
    }

    private static ParseResult BuildMcpParseResult(
        CliFriendlyError? error = null,
        bool runMcpList = false,
        McpServerDefinition? mcpServerToAdd = null,
        string? mcpServerNameToRemove = null,
        string? mcpServerNameToTest = null)
    {
        return new ParseResult(
            ShowHelp: false,
            ShowVersion: false,
            AskPrompt: null,
            StartChat: false,
            RunDoctor: false,
            RunModels: false,
            SelectedModel: null,
            Error: error,
            RunMcpList: runMcpList,
            McpServerToAdd: mcpServerToAdd,
            McpServerNameToRemove: mcpServerNameToRemove,
            McpServerNameToTest: mcpServerNameToTest);
    }

    private static bool TryReadOptionValue(
        string[] args,
        ref int index,
        string optionName,
        out string optionValue,
        out CliFriendlyError? error)
    {
        optionValue = string.Empty;
        error = null;

        var argument = args[index];
        if (string.Equals(argument, optionName, StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Length)
            {
                error = CliFriendlyError.InvalidArguments(
                    detail: $"A opcao '{optionName}' exige um valor.",
                    suggestion: $"Use '{optionName} <valor>'.");
                return true;
            }

            optionValue = args[++index].Trim();
            if (string.IsNullOrWhiteSpace(optionValue))
            {
                error = CliFriendlyError.InvalidArguments(
                    detail: $"A opcao '{optionName}' exige um valor.",
                    suggestion: $"Use '{optionName} <valor>'.");
            }

            return true;
        }

        var prefix = $"{optionName}=";
        if (!argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        optionValue = argument[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(optionValue))
        {
            error = CliFriendlyError.InvalidArguments(
                detail: $"A opcao '{optionName}' exige um valor.",
                suggestion: $"Use '{optionName}=<valor>'.");
        }

        return true;
    }

    private static bool TryParseNameValuePair(
        string rawValue,
        out string name,
        out string value)
    {
        name = string.Empty;
        value = string.Empty;

        var separatorIndex = rawValue.IndexOf('=');
        if (separatorIndex <= 0 || separatorIndex == rawValue.Length - 1)
        {
            return false;
        }

        name = rawValue[..separatorIndex].Trim();
        value = rawValue[(separatorIndex + 1)..].Trim();
        return !string.IsNullOrWhiteSpace(name)
            && !string.IsNullOrWhiteSpace(value);
    }

    private static ParseResult ParseConfigGetKey(string rawKey)
    {
        if (!TryResolveConfigKey(rawKey, out var configKey))
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: BuildInvalidConfigKeyError(rawKey));
        }

        return new ParseResult(
            ShowHelp: false,
            ShowVersion: false,
            AskPrompt: null,
            StartChat: false,
            RunDoctor: false,
            RunModels: false,
            SelectedModel: null,
            Error: null,
            ConfigGetKey: configKey);
    }

    private static ParseResult ParseConfigSetArguments(string rawKey, string[] rawValueParts)
    {
        if (!TryResolveConfigKey(rawKey, out var configKey))
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: BuildInvalidConfigKeyError(rawKey));
        }

        var configValue = string.Join(' ', rawValueParts).Trim();
        if (string.IsNullOrWhiteSpace(configValue))
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: CliFriendlyError.InvalidArguments(
                    detail: $"Voce precisa informar um valor para a chave '{configKey}'.",
                    suggestion: $"Exemplo: {CliName} config set {configKey} <valor>."));
        }

        return new ParseResult(
            ShowHelp: false,
            ShowVersion: false,
            AskPrompt: null,
            StartChat: false,
            RunDoctor: false,
            RunModels: false,
            SelectedModel: null,
            Error: null,
            ConfigSetKey: configKey,
            ConfigSetValue: configValue);
    }

    private static bool TryResolveConfigKey(string rawKey, out string configKey)
    {
        if (UserConfigFile.TryNormalizeSupportedKey(rawKey, out var normalizedKey)
            && normalizedKey is not null)
        {
            configKey = normalizedKey;
            return true;
        }

        configKey = string.Empty;
        return false;
    }

    private static CliFriendlyError BuildInvalidConfigKeyError(string rawKey)
    {
        var key = rawKey.Trim();
        return CliFriendlyError.InvalidArguments(
            detail: $"A chave de configuracao '{key}' nao e suportada.",
            suggestion: $"Chaves suportadas: {GetSupportedConfigKeysLabel()}.");
    }

    private static ParseResult ParseAskArguments(string[] args)
    {
        var commandArguments = args.Skip(1).ToArray();
        var optionError = TryExtractModelOption(
            commandArguments,
            commandName: "ask",
            usageExample: $"{CliName} ask --model {OllamaModelDefaults.DefaultModel} \"seu prompt\".",
            out var selectedModel,
            out var remainingArguments);

        if (optionError is CliFriendlyError error)
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: error);
        }

        if (remainingArguments.Count == 0)
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: CliFriendlyError.InvalidArguments(
                    detail: "Voce precisa informar um prompt para o comando 'ask'.",
                    suggestion: $"Exemplo: {CliName} ask \"seu prompt\"."));
        }

        var prompt = string.Join(' ', remainingArguments).Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: CliFriendlyError.InvalidArguments(
                    detail: "O prompt informado para o comando 'ask' esta vazio.",
                    suggestion: $"Exemplo: {CliName} ask \"seu prompt\"."));
        }

        return new ParseResult(
            ShowHelp: false,
            ShowVersion: false,
            AskPrompt: prompt,
            StartChat: false,
            RunDoctor: false,
            RunModels: false,
            SelectedModel: selectedModel,
            Error: null);
    }

    private static ParseResult ParseAgentArguments(string[] args)
    {
        var commandArguments = args.Skip(1).ToArray();
        var optionError = TryExtractAgentOptions(
            commandArguments,
            out var selectedModel,
            out var maxSteps,
            out var maxTime,
            out var maxCost,
            out var remainingArguments);

        if (optionError is CliFriendlyError error)
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: error);
        }

        if (remainingArguments.Count == 0)
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: CliFriendlyError.InvalidArguments(
                    detail: "Voce precisa informar um objetivo para o comando 'agent'.",
                    suggestion: $"Exemplo: {CliName} agent \"seu objetivo\"."));
        }

        var objective = string.Join(' ', remainingArguments).Trim();
        if (string.IsNullOrWhiteSpace(objective))
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: CliFriendlyError.InvalidArguments(
                    detail: "O objetivo informado para o comando 'agent' esta vazio.",
                    suggestion: $"Exemplo: {CliName} agent \"seu objetivo\"."));
        }

        return new ParseResult(
            ShowHelp: false,
            ShowVersion: false,
            AskPrompt: null,
            StartChat: false,
            RunDoctor: false,
            RunModels: false,
            SelectedModel: selectedModel,
            Error: null,
            RunAgent: true,
            AgentObjective: objective,
            AgentMaxSteps: maxSteps,
            AgentMaxTime: maxTime,
            AgentMaxCost: maxCost);
    }

    private static ParseResult ParseChatArguments(string[] args)
    {
        var commandArguments = args.Skip(1).ToArray();
        var optionError = TryExtractModelOption(
            commandArguments,
            commandName: "chat",
            usageExample: $"{CliName} chat --model {OllamaModelDefaults.DefaultModel}.",
            out var selectedModel,
            out var remainingArguments);

        if (optionError is CliFriendlyError error)
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: error);
        }

        if (remainingArguments.Count > 0)
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: CliFriendlyError.InvalidArguments(
                    detail: "O comando 'chat' nao aceita argumentos adicionais.",
                    suggestion: $"Exemplo: {CliName} chat."));
        }

        return new ParseResult(
            ShowHelp: false,
            ShowVersion: false,
            AskPrompt: null,
            StartChat: true,
            RunDoctor: false,
            RunModels: false,
            SelectedModel: selectedModel,
            Error: null);
    }

    private static ParseResult ParseSkillsArguments(string[] args)
    {
        if (args.Length == 1)
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: null,
                RunSkills: true);
        }

        if (args.Length >= 2 && string.Equals(args[1], "init", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length > 2)
            {
                return new ParseResult(
                    ShowHelp: false,
                    ShowVersion: false,
                    AskPrompt: null,
                    StartChat: false,
                    RunDoctor: false,
                    RunModels: false,
                    SelectedModel: null,
                    Error: CliFriendlyError.InvalidArguments(
                        detail: "O comando 'skills init' nao aceita argumentos adicionais.",
                        suggestion: $"Exemplo: {CliName} skills init."));
            }

            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: null,
                RunSkillsInit: true);
        }

        if (args.Length >= 2 && string.Equals(args[1], "reload", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length > 2)
            {
                return new ParseResult(
                    ShowHelp: false,
                    ShowVersion: false,
                    AskPrompt: null,
                    StartChat: false,
                    RunDoctor: false,
                    RunModels: false,
                    SelectedModel: null,
                    Error: CliFriendlyError.InvalidArguments(
                        detail: "O comando 'skills reload' nao aceita argumentos adicionais.",
                        suggestion: $"Exemplo: {CliName} skills reload."));
            }

            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: null,
                RunSkillsReload: true);
        }

        if (args.Length == 3 && string.Equals(args[1], "show", StringComparison.OrdinalIgnoreCase))
        {
            var skillName = args[2].Trim();
            if (string.IsNullOrWhiteSpace(skillName))
            {
                return new ParseResult(
                    ShowHelp: false,
                    ShowVersion: false,
                    AskPrompt: null,
                    StartChat: false,
                    RunDoctor: false,
                    RunModels: false,
                    SelectedModel: null,
                    Error: CliFriendlyError.InvalidArguments(
                        detail: "Voce precisa informar o nome da skill para o comando 'skills show'.",
                        suggestion: $"Exemplo: {CliName} skills show code-review."));
            }

            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: null,
                ShowSkillName: skillName);
        }

        return new ParseResult(
            ShowHelp: false,
            ShowVersion: false,
            AskPrompt: null,
            StartChat: false,
            RunDoctor: false,
            RunModels: false,
            SelectedModel: null,
            Error: CliFriendlyError.InvalidArguments(
                detail: "O comando 'skills' aceita apenas 'skills', 'skills show <nome>', 'skills init' ou 'skills reload'.",
                suggestion: $"Exemplo: {CliName} skills init."));
    }

    private static ParseResult ParseSkillArguments(string[] args)
    {
        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: CliFriendlyError.InvalidArguments(
                    detail: "Voce precisa informar o nome da skill para o comando 'skill'.",
                    suggestion: $"Exemplo: {CliName} skill code-review \"seu prompt\"."));
        }

        var skillName = args[1].Trim();
        if (skillName.StartsWith('-'))
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: CliFriendlyError.InvalidArguments(
                    detail: "O nome da skill informado para o comando 'skill' e invalido.",
                    suggestion: $"Exemplo: {CliName} skill code-review \"seu prompt\"."));
        }

        var commandArguments = args.Skip(2).ToArray();
        var optionError = TryExtractModelOption(
            commandArguments,
            commandName: "skill",
            usageExample: $"{CliName} skill code-review --model {OllamaModelDefaults.DefaultModel} \"seu prompt\".",
            out var selectedModel,
            out var remainingArguments);

        if (optionError is CliFriendlyError error)
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: error);
        }

        if (remainingArguments.Count == 0)
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: CliFriendlyError.InvalidArguments(
                    detail: "Voce precisa informar um prompt para o comando 'skill'.",
                    suggestion: $"Exemplo: {CliName} skill {skillName} \"seu prompt\"."));
        }

        var prompt = string.Join(' ', remainingArguments).Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return new ParseResult(
                ShowHelp: false,
                ShowVersion: false,
                AskPrompt: null,
                StartChat: false,
                RunDoctor: false,
                RunModels: false,
                SelectedModel: null,
                Error: CliFriendlyError.InvalidArguments(
                    detail: "O prompt informado para o comando 'skill' esta vazio.",
                    suggestion: $"Exemplo: {CliName} skill {skillName} \"seu prompt\"."));
        }

        return new ParseResult(
            ShowHelp: false,
            ShowVersion: false,
            AskPrompt: null,
            StartChat: false,
            RunDoctor: false,
            RunModels: false,
            SelectedModel: selectedModel,
            Error: null,
            RunSkillName: skillName,
            SkillPrompt: prompt);
    }

    private static CliFriendlyError? TryExtractAgentOptions(
        string[] arguments,
        out string? selectedModel,
        out int? maxSteps,
        out TimeSpan? maxTime,
        out decimal? maxCost,
        out List<string> remainingArguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        selectedModel = null;
        maxSteps = null;
        maxTime = null;
        maxCost = null;
        remainingArguments = [];

        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];

            if (string.Equals(argument, "--", StringComparison.Ordinal))
            {
                remainingArguments.AddRange(arguments[(index + 1)..]);
                break;
            }

            if (string.Equals(argument, ModelFlag, StringComparison.OrdinalIgnoreCase))
            {
                if (selectedModel is not null)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{ModelFlag}' foi informada mais de uma vez no comando 'agent'.",
                        suggestion: $"Exemplo: {CliName} agent --model {OllamaModelDefaults.DefaultModel} \"seu objetivo\".");
                }

                if (index + 1 >= arguments.Length)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{ModelFlag}' exige um nome de modelo.",
                        suggestion: $"Exemplo: {CliName} agent --model {OllamaModelDefaults.DefaultModel} \"seu objetivo\".");
                }

                var candidate = arguments[++index].Trim();
                if (string.IsNullOrWhiteSpace(candidate) || candidate.StartsWith('-'))
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{ModelFlag}' exige um nome de modelo.",
                        suggestion: $"Exemplo: {CliName} agent --model {OllamaModelDefaults.DefaultModel} \"seu objetivo\".");
                }

                selectedModel = candidate;
                continue;
            }

            if (argument.StartsWith($"{ModelFlag}=", StringComparison.OrdinalIgnoreCase))
            {
                if (selectedModel is not null)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{ModelFlag}' foi informada mais de uma vez no comando 'agent'.",
                        suggestion: $"Exemplo: {CliName} agent --model {OllamaModelDefaults.DefaultModel} \"seu objetivo\".");
                }

                var candidate = argument[(ModelFlag.Length + 1)..].Trim();
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{ModelFlag}' exige um nome de modelo.",
                        suggestion: $"Exemplo: {CliName} agent --model {OllamaModelDefaults.DefaultModel} \"seu objetivo\".");
                }

                selectedModel = candidate;
                continue;
            }

            if (TryReadOptionValueWithAlias(
                arguments,
                ref index,
                AgentMaxStepsFlag,
                AgentMaxStepsAliasFlag,
                out var maxStepsValue,
                out var maxStepsError))
            {
                if (maxStepsError is CliFriendlyError error)
                {
                    return error;
                }

                if (maxSteps is not null)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{AgentMaxStepsFlag}' foi informada mais de uma vez no comando 'agent'.",
                        suggestion: $"Exemplo: {CliName} agent {AgentMaxStepsFlag} 6 \"seu objetivo\".");
                }

                if (!int.TryParse(
                    maxStepsValue,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsedMaxSteps)
                    || parsedMaxSteps <= 0)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{AgentMaxStepsFlag}' exige um numero inteiro positivo.",
                        suggestion: $"Exemplo: {CliName} agent {AgentMaxStepsFlag} 6 \"seu objetivo\".");
                }

                maxSteps = parsedMaxSteps;
                continue;
            }

            if (TryReadOptionValueWithAlias(
                arguments,
                ref index,
                AgentMaxTimeFlag,
                AgentMaxTimeAliasFlag,
                out var maxTimeValue,
                out var maxTimeError))
            {
                if (maxTimeError is CliFriendlyError error)
                {
                    return error;
                }

                if (maxTime is not null)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{AgentMaxTimeFlag}' foi informada mais de uma vez no comando 'agent'.",
                        suggestion: $"Exemplo: {CliName} agent {AgentMaxTimeFlag} 90 \"seu objetivo\".");
                }

                if (!TryParsePositiveDuration(maxTimeValue, out var parsedMaxTime))
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{AgentMaxTimeFlag}' exige um valor positivo em segundos ou hh:mm:ss.",
                        suggestion: $"Exemplo: {CliName} agent {AgentMaxTimeFlag} 90 \"seu objetivo\".");
                }

                maxTime = parsedMaxTime;
                continue;
            }

            if (TryReadOptionValueWithAlias(
                arguments,
                ref index,
                AgentMaxCostFlag,
                AgentMaxCostAliasFlag,
                out var maxCostValue,
                out var maxCostError))
            {
                if (maxCostError is CliFriendlyError error)
                {
                    return error;
                }

                if (maxCost is not null)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{AgentMaxCostFlag}' foi informada mais de uma vez no comando 'agent'.",
                        suggestion: $"Exemplo: {CliName} agent {AgentMaxCostFlag} 2000 \"seu objetivo\".");
                }

                if (!decimal.TryParse(
                    maxCostValue,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsedMaxCost)
                    || parsedMaxCost <= 0m)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{AgentMaxCostFlag}' exige um numero positivo.",
                        suggestion: $"Exemplo: {CliName} agent {AgentMaxCostFlag} 2000 \"seu objetivo\".");
                }

                maxCost = parsedMaxCost;
                continue;
            }

            remainingArguments.Add(argument);
        }

        return null;
    }

    private static bool TryReadOptionValueWithAlias(
        string[] args,
        ref int index,
        string optionName,
        string aliasOptionName,
        out string optionValue,
        out CliFriendlyError? error)
    {
        if (TryReadOptionValue(args, ref index, optionName, out optionValue, out error))
        {
            return true;
        }

        return TryReadOptionValue(args, ref index, aliasOptionName, out optionValue, out error);
    }

    private static bool TryParsePositiveDuration(string rawValue, out TimeSpan parsedDuration)
    {
        if (double.TryParse(
            rawValue,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var seconds)
            && double.IsFinite(seconds)
            && seconds > 0d)
        {
            parsedDuration = TimeSpan.FromSeconds(seconds);
            return true;
        }

        if (TimeSpan.TryParse(rawValue, CultureInfo.InvariantCulture, out var duration)
            && duration > TimeSpan.Zero)
        {
            parsedDuration = duration;
            return true;
        }

        parsedDuration = TimeSpan.Zero;
        return false;
    }

    private static CliFriendlyError? TryExtractModelOption(
        string[] arguments,
        string commandName,
        string usageExample,
        out string? selectedModel,
        out List<string> remainingArguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentException.ThrowIfNullOrWhiteSpace(usageExample);

        selectedModel = null;
        remainingArguments = [];

        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];

            if (string.Equals(argument, "--", StringComparison.Ordinal))
            {
                remainingArguments.AddRange(arguments[(index + 1)..]);
                break;
            }

            if (string.Equals(argument, ModelFlag, StringComparison.OrdinalIgnoreCase))
            {
                if (selectedModel is not null)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{ModelFlag}' foi informada mais de uma vez no comando '{commandName}'.",
                        suggestion: $"Exemplo: {usageExample}");
                }

                if (index + 1 >= arguments.Length)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{ModelFlag}' exige um nome de modelo.",
                        suggestion: $"Exemplo: {usageExample}");
                }

                var candidate = arguments[++index].Trim();
                if (string.IsNullOrWhiteSpace(candidate) || candidate.StartsWith('-'))
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{ModelFlag}' exige um nome de modelo.",
                        suggestion: $"Exemplo: {usageExample}");
                }

                selectedModel = candidate;
                continue;
            }

            if (argument.StartsWith($"{ModelFlag}=", StringComparison.OrdinalIgnoreCase))
            {
                if (selectedModel is not null)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{ModelFlag}' foi informada mais de uma vez no comando '{commandName}'.",
                        suggestion: $"Exemplo: {usageExample}");
                }

                var candidate = argument[(ModelFlag.Length + 1)..].Trim();
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{ModelFlag}' exige um nome de modelo.",
                        suggestion: $"Exemplo: {usageExample}");
                }

                selectedModel = candidate;
                continue;
            }

            remainingArguments.Add(argument);
        }

        return null;
    }

    private static void WriteHelp()
    {
        TerminalHeader helpHeader = TerminalVisualComponents.BuildHeader(
            "ASXRunTerminal CLI",
            "Terminal local para produtividade com IA.");
        Console.WriteLine((string)helpHeader);
        Console.WriteLine();
        Console.WriteLine("Uso:");
        Console.WriteLine($"  {CliName} [opcao]");
        Console.WriteLine($"  {CliName} ask [--model <modelo>] \"prompt\"");
        Console.WriteLine($"  {CliName} agent [--model <modelo>] \"objetivo\"");
        Console.WriteLine($"  {CliName} chat [--model <modelo>]");
        Console.WriteLine($"  {CliName} doctor");
        Console.WriteLine($"  {CliName} models");
        Console.WriteLine($"  {CliName} context");
        Console.WriteLine($"  {CliName} patch [--dry-run] <arquivo-json>");
        Console.WriteLine($"  {CliName} history [--clear]");
        Console.WriteLine($"  {CliName} resume [<session-id>]");
        Console.WriteLine($"  {CliName} mcp list");
        Console.WriteLine($"  {CliName} mcp add <nome> --command <cmd> [--arg <valor>]...");
        Console.WriteLine($"  {CliName} mcp add <nome> --url <endpoint> [--transport http|sse]");
        Console.WriteLine($"  {CliName} mcp remove <nome>");
        Console.WriteLine($"  {CliName} mcp test <nome>");
        Console.WriteLine($"  {CliName} config get <chave>");
        Console.WriteLine($"  {CliName} config set <chave> <valor>");
        Console.WriteLine($"  {CliName} skills");
        Console.WriteLine($"  {CliName} skills show <nome>");
        Console.WriteLine($"  {CliName} skills init");
        Console.WriteLine($"  {CliName} skills reload");
        Console.WriteLine($"  {CliName} skill <nome> [--model <modelo>] \"prompt\"");
        Console.WriteLine();
        Console.WriteLine("Opcoes:");
        Console.WriteLine("  -h, --help       Exibe ajuda.");
        Console.WriteLine("  -v, --version    Exibe a versao.");
        Console.WriteLine($"  --model <nome>   Seleciona o modelo Ollama para 'ask', 'agent', 'chat' e 'skill' (padrao: {OllamaModelDefaults.DefaultModel}).");
        Console.WriteLine($"  {AgentMaxStepsFlag} <n>  Limita iteracoes do comando 'agent' por sessao (padrao: {AgentAutonomousMaxIterations}).");
        Console.WriteLine($"  {AgentMaxTimeFlag} <v>   Limita duracao da sessao do 'agent' (segundos ou hh:mm:ss).");
        Console.WriteLine($"  {AgentMaxCostFlag} <v>   Limita custo estimado do 'agent' em caracteres (prompt + resposta).");
        Console.WriteLine($"  {OllamaModelDefaults.DefaultModelEnvironmentVariable}=<nome>");
        Console.WriteLine("                   Sobrescreve o modelo padrao quando --model nao e informado.");
        Console.WriteLine();
        Console.WriteLine("Comandos:");
        Console.WriteLine("  ask \"prompt\"    Executa um prompt unico.");
        Console.WriteLine("  agent \"objetivo\" Inicia o modo agente autonomo orientado por objetivo.");
        Console.WriteLine("                   Executa o loop plan -> execute -> verify -> refine ate concluir.");
        Console.WriteLine($"                   Opcional: use {AgentMaxStepsFlag}, {AgentMaxTimeFlag} e {AgentMaxCostFlag} para controlar orcamento da sessao.");
        Console.WriteLine("  chat             Inicia o modo interativo.");
        Console.WriteLine("                   Comandos no chat: /help, /clear, /models, /tools, /exit.");
        Console.WriteLine("  doctor           Valida a disponibilidade do Ollama.");
        Console.WriteLine("  models           Lista os modelos locais do Ollama.");
        Console.WriteLine("  context          Exibe resumo do workspace atual e do indice de contexto.");
        Console.WriteLine("  patch            Aplica mudancas de arquivo via patch JSON e imprime diff unificado.");
        Console.WriteLine("                   Use '--dry-run' para apenas visualizar o diff sem alterar arquivos.");
        Console.WriteLine("                   Mudancas destrutivas (delete) exigem confirmacao explicita.");
        Console.WriteLine("                   Cada execucao registra trilha de auditoria local por sessao.");
        Console.WriteLine("  history          Exibe ou limpa o historico local de prompts.");
        Console.WriteLine("  resume           Retoma a ultima sessao interrompida de ask/agent/skill.");
        Console.WriteLine("                   Opcional: informe um session-id especifico para retomar.");
        Console.WriteLine("  mcp              Gerencia servidores MCP locais/remotos e executa teste de conectividade.");
        Console.WriteLine("  config           Le e atualiza configuracoes locais do usuario.");
        Console.WriteLine($"                   Chaves suportadas: {GetSupportedConfigKeysLabel()}.");
        Console.WriteLine("  skills           Lista as skills disponiveis.");
        Console.WriteLine("  skills show      Exibe os detalhes de uma skill.");
        Console.WriteLine("  skills init      Cria um template SKILL.md no diretorio atual.");
        Console.WriteLine("  skills reload    Recarrega o cache de skills sem reiniciar o CLI.");
        Console.WriteLine("  skill            Executa um prompt usando uma skill padrao.");
        Console.WriteLine();
        Console.WriteLine("Codigos de saida:");
        Console.WriteLine($"  {(int)CliExitCode.Success}  Sucesso.");
        Console.WriteLine($"  {(int)CliExitCode.RuntimeError}  Erro em tempo de execucao.");
        Console.WriteLine($"  {(int)CliExitCode.InvalidArguments}  Argumentos invalidos.");
        Console.WriteLine($"  {(int)CliExitCode.Cancelled}  Execucao cancelada pelo usuario.");
        Console.WriteLine((string)TerminalVisualComponents.BuildSeparator(width: 48));
    }

    private static void WriteVersion()
    {
        Console.WriteLine($"{CliName} {GetVersion()}");
    }

    private static int ExecuteAsk(
        string prompt,
        string? model,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration,
        Action<ExecutionSessionCheckpoint> executionCheckpointAppender)
    {
        ArgumentNullException.ThrowIfNull(executionCheckpointAppender);

        ConsoleLogger.Info("Executando comando unico 'ask'.");
        var checkpointContext = CreatePromptCheckpointContext(
            command: "ask",
            prompt: prompt,
            model: model,
            skillName: null,
            executionCheckpointAppender: executionCheckpointAppender);
        var wasCancelled = ExecutePrompt(
            prompt,
            model,
            promptExecutor,
            cancelSignalRegistration,
            checkpointContext);
        return wasCancelled
            ? (int)CliExitCode.Cancelled
            : (int)CliExitCode.Success;
    }

    private static int ExecuteAgent(
        string objective,
        string? model,
        int? maxSteps,
        TimeSpan? maxTime,
        decimal? maxCost,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration,
        Action<ExecutionSessionCheckpoint> executionCheckpointAppender,
        IToolRuntime toolRuntime,
        AgentAutonomousLoopState? resumeLoopState = null,
        string? checkpointSessionId = null)
    {
        ArgumentNullException.ThrowIfNull(promptExecutor);
        ArgumentNullException.ThrowIfNull(cancelSignalRegistration);
        ArgumentNullException.ThrowIfNull(executionCheckpointAppender);
        ArgumentNullException.ThrowIfNull(toolRuntime);

        ConsoleLogger.Info("Iniciando modo agente autonomo por objetivo.");
        var sessionBudget = new AgentSessionBudget(
            MaxSteps: maxSteps ?? AgentAutonomousMaxIterations,
            MaxTime: maxTime,
            MaxCost: maxCost);
        ConsoleLogger.Info(
            $"Orcamento da sessao do agente: max_steps={sessionBudget.MaxSteps}, " +
            $"max_time={FormatOptionalBudgetDuration(sessionBudget.MaxTime)}, " +
            $"max_cost={FormatOptionalBudgetValue(sessionBudget.MaxCost)}.");
        var normalizedResumeLoopState = NormalizeAgentAutonomousLoopState(
            resumeLoopState ?? AgentAutonomousLoopState.Initial,
            sessionBudget.MaxSteps);
        if (resumeLoopState is AgentAutonomousLoopState)
        {
            ConsoleLogger.Info(
                $"Retomando loop autonomo a partir da iteracao {normalizedResumeLoopState.NextIteration}/{sessionBudget.MaxSteps}.");
        }

        ConsoleLogger.Info(
            "Coletando contexto de engenharia do projeto antes do ciclo autonomo.");
        var projectContext = LoadAgentProjectContextSnapshot();
        ConsoleLogger.Info(
            $"Contexto de engenharia carregado: codigo={projectContext.CodeFileCount}, " +
            $"testes={projectContext.TestFileCount}, docs={projectContext.DocumentationFileCount}, " +
            $"git={projectContext.GitHistorySummary}.");

        var executionPlan = AgentObjectivePlanner.Build(objective);
        var checkpointContext = CreatePromptCheckpointContext(
            command: "agent",
            prompt: objective,
            model: model,
            skillName: null,
            executionCheckpointAppender: executionCheckpointAppender,
            sessionId: checkpointSessionId);

        var loopResult = ExecuteAgentAutonomousLoop(
            executionPlan,
            model,
            sessionBudget,
            promptExecutor,
            cancelSignalRegistration,
            projectContext,
            normalizedResumeLoopState,
            checkpointContext,
            toolRuntime);

        if (loopResult.WasCancelled)
        {
            return (int)CliExitCode.Cancelled;
        }

        if (!loopResult.IsConcluded)
        {
            var notConcludedDetail = loopResult.BudgetLimitKind switch
            {
                AgentBudgetLimitKind.MaxTime =>
                    "Loop autonomo interrompido por limite de tempo da sessao " +
                    $"(max_time={FormatRequiredBudgetDuration(sessionBudget.MaxTime)}, " +
                    $"decorrido={FormatRequiredBudgetDuration(loopResult.Elapsed)}).",
                AgentBudgetLimitKind.MaxCost =>
                    "Loop autonomo interrompido por limite de custo da sessao " +
                    $"(max_cost={FormatRequiredBudgetValue(sessionBudget.MaxCost)}, " +
                    $"custo_atual={FormatRequiredBudgetValue(loopResult.AccumulatedCost)}).",
                _ =>
                    $"Loop autonomo interrompido apos {loopResult.IterationCount} iteracao(oes) " +
                    "sem sinal explicito de conclusao na fase verify."
            };
            WriteExecutionStateAndCheckpoint(
                ExecutionState.Error,
                notConcludedDetail,
                checkpointContext,
                ExecutionCheckpointStatus.Failed);

            var notConcludedError = loopResult.BudgetLimitKind switch
            {
                AgentBudgetLimitKind.MaxTime => CliFriendlyError.Runtime(
                    detail: "O modo agente atingiu o limite de tempo da sessao.",
                    suggestion:
                    $"Ajuste '{AgentMaxTimeFlag}', reduza o escopo do objetivo ou execute com checkpoints menores."),
                AgentBudgetLimitKind.MaxCost => CliFriendlyError.Runtime(
                    detail: "O modo agente atingiu o limite de custo da sessao.",
                    suggestion:
                    $"Ajuste '{AgentMaxCostFlag}', reduza o objetivo ou escolha um modelo mais economico."),
                _ => CliFriendlyError.Runtime(
                    detail: "O modo agente nao conseguiu concluir o objetivo com seguranca.",
                    suggestion:
                    "Refine o objetivo com escopo menor ou execute novamente com instrucoes de verificacao mais explicitas.")
            };
            WriteFriendlyError(notConcludedError);
            return (int)notConcludedError.ExitCode;
        }

        WriteExecutionStateAndCheckpoint(
            ExecutionState.Completed,
            $"Loop autonomo concluido apos {loopResult.IterationCount} iteracao(oes).",
            checkpointContext,
            ExecutionCheckpointStatus.Completed);
        return (int)CliExitCode.Success;
    }

    private static AgentAutonomousLoopResult ExecuteAgentAutonomousLoop(
        AgentExecutionPlan executionPlan,
        string? model,
        AgentSessionBudget sessionBudget,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration,
        AgentProjectContextSnapshot projectContext,
        AgentAutonomousLoopState initialLoopState,
        PromptExecutionCheckpointContext checkpointContext,
        IToolRuntime toolRuntime)
    {
        var previousVerificationOutput = initialLoopState.PreviousVerificationOutput;
        var previousRefinementOutput = initialLoopState.PreviousRefinementOutput;
        var resumedElapsed = initialLoopState.Elapsed;
        var stopwatch = Stopwatch.StartNew();
        var accumulatedCost = initialLoopState.AccumulatedCost;

        for (var iteration = initialLoopState.NextIteration; iteration <= sessionBudget.MaxSteps; iteration++)
        {
            var elapsedAtIterationStart = GetCurrentAgentLoopElapsed(resumedElapsed, stopwatch);
            if (ResolveBudgetExceededResult(
                sessionBudget,
                iteration,
                elapsedAtIterationStart,
                accumulatedCost) is AgentAutonomousLoopResult budgetExceededAtIterationStart)
            {
                return budgetExceededAtIterationStart;
            }

            AppendAgentAutonomousLoopCheckpoint(
                checkpointContext,
                sessionBudget,
                new AgentAutonomousLoopState(
                    NextIteration: iteration,
                    PreviousVerificationOutput: previousVerificationOutput,
                    PreviousRefinementOutput: previousRefinementOutput,
                    Elapsed: elapsedAtIterationStart,
                    AccumulatedCost: accumulatedCost));

            ConsoleLogger.Info(
                $"Ciclo autonomo {iteration}/{sessionBudget.MaxSteps}: fase plan.");
            var planPrompt = BuildAgentPlanPhasePrompt(
                executionPlan,
                iteration,
                projectContext,
                previousVerificationOutput,
                previousRefinementOutput);
            var planResult = ExecutePromptAndCapture(
                planPrompt,
                model,
                promptExecutor,
                cancelSignalRegistration,
                checkpointContext);
            if (planResult.WasCancelled)
            {
                return AgentAutonomousLoopResult.Cancelled(
                    iteration,
                    GetCurrentAgentLoopElapsed(resumedElapsed, stopwatch),
                    accumulatedCost);
            }

            accumulatedCost += EstimateAgentStepCost(planPrompt, planResult.StreamMetrics);
            var elapsedAfterPlan = GetCurrentAgentLoopElapsed(resumedElapsed, stopwatch);
            if (ResolveBudgetExceededResult(
                sessionBudget,
                iteration,
                elapsedAfterPlan,
                accumulatedCost) is AgentAutonomousLoopResult budgetExceededAfterPlan)
            {
                return budgetExceededAfterPlan;
            }

            ConsoleLogger.Info(
                $"Ciclo autonomo {iteration}/{sessionBudget.MaxSteps}: fase execute.");
            var executePrompt = BuildAgentExecutePhasePrompt(
                executionPlan,
                iteration,
                projectContext,
                planResult.StreamMetrics.ResponseText,
                previousRefinementOutput);
            var executeResult = ExecutePromptAndCapture(
                executePrompt,
                model,
                promptExecutor,
                cancelSignalRegistration,
                checkpointContext);
            if (executeResult.WasCancelled)
            {
                return AgentAutonomousLoopResult.Cancelled(
                    iteration,
                    GetCurrentAgentLoopElapsed(resumedElapsed, stopwatch),
                    accumulatedCost);
            }

            var executeEvidence = ParseAgentCodeChangeEvidence(
                executeResult.StreamMetrics.ResponseText);
            ConsoleLogger.Info(BuildAgentCodeChangeEvidenceLogMessage(executeEvidence));
            var validationReport = ExecuteAgentValidationAfterChangeBlock(
                executeEvidence,
                projectContext,
                toolRuntime);
            var latestExecutionOutput = executeResult.StreamMetrics.ResponseText;
            var autoCorrectionResult = ExecuteAgentAutoCorrectionWhenValidationFails(
                executionPlan,
                iteration,
                projectContext,
                planResult.StreamMetrics.ResponseText,
                latestExecutionOutput,
                executeEvidence,
                validationReport,
                model,
                promptExecutor,
                cancelSignalRegistration,
                checkpointContext,
                toolRuntime);
            if (autoCorrectionResult.WasCancelled)
            {
                return AgentAutonomousLoopResult.Cancelled(
                    iteration,
                    GetCurrentAgentLoopElapsed(resumedElapsed, stopwatch),
                    accumulatedCost);
            }

            latestExecutionOutput = autoCorrectionResult.LatestExecutionOutput;
            executeEvidence = autoCorrectionResult.LatestChangeEvidence;
            validationReport = autoCorrectionResult.ValidationReport;

            accumulatedCost += EstimateAgentStepCost(executePrompt, executeResult.StreamMetrics);
            accumulatedCost += autoCorrectionResult.AdditionalCost;
            var elapsedAfterExecute = GetCurrentAgentLoopElapsed(resumedElapsed, stopwatch);
            if (ResolveBudgetExceededResult(
                sessionBudget,
                iteration,
                elapsedAfterExecute,
                accumulatedCost) is AgentAutonomousLoopResult budgetExceededAfterExecute)
            {
                return budgetExceededAfterExecute;
            }

            ConsoleLogger.Info(
                $"Ciclo autonomo {iteration}/{sessionBudget.MaxSteps}: fase verify.");
            var verifyPrompt = BuildAgentVerifyPhasePrompt(
                executionPlan,
                iteration,
                projectContext,
                planResult.StreamMetrics.ResponseText,
                latestExecutionOutput,
                executeEvidence,
                validationReport);
            var verifyResult = ExecutePromptAndCapture(
                verifyPrompt,
                model,
                promptExecutor,
                cancelSignalRegistration,
                checkpointContext);
            if (verifyResult.WasCancelled)
            {
                return AgentAutonomousLoopResult.Cancelled(
                    iteration,
                    GetCurrentAgentLoopElapsed(resumedElapsed, stopwatch),
                    accumulatedCost);
            }

            accumulatedCost += EstimateAgentStepCost(verifyPrompt, verifyResult.StreamMetrics);
            var elapsedAfterVerify = GetCurrentAgentLoopElapsed(resumedElapsed, stopwatch);
            if (ResolveBudgetExceededResult(
                sessionBudget,
                iteration,
                elapsedAfterVerify,
                accumulatedCost) is AgentAutonomousLoopResult budgetExceededAfterVerify)
            {
                return budgetExceededAfterVerify;
            }

            previousVerificationOutput = verifyResult.StreamMetrics.ResponseText;
            var verificationDecision = ParseAgentVerificationDecision(previousVerificationOutput);
            if (verificationDecision.IsConcluded && !executeEvidence.IsCompliant)
            {
                ConsoleLogger.Info(
                    "Verificacao marcou status 'done', mas as evidencias de diff/justificativa por mudanca estao incompletas. Forcando refine.");
                verificationDecision = AgentVerificationDecision.NeedsRefine("missing-change-evidence");
            }
            else if (verificationDecision.IsConcluded && validationReport.HasFailures)
            {
                ConsoleLogger.Info(
                    "Verificacao marcou status 'done', mas a validacao automatica pos-mudanca falhou. Forcando refine.");
                verificationDecision = AgentVerificationDecision.NeedsRefine("validation-failed");
            }

            if (verificationDecision.IsConcluded)
            {
                ConsoleLogger.Info(
                    $"Verificacao marcou status '{verificationDecision.Status}'. Objetivo concluido.");
                return AgentAutonomousLoopResult.Concluded(
                    iteration,
                    GetCurrentAgentLoopElapsed(resumedElapsed, stopwatch),
                    accumulatedCost);
            }

            if (iteration >= sessionBudget.MaxSteps)
            {
                break;
            }

            ConsoleLogger.Info(
                $"Verificacao marcou status '{verificationDecision.Status}'. Iniciando fase refine.");
            var refinePrompt = BuildAgentRefinePhasePrompt(
                executionPlan,
                iteration,
                projectContext,
                planResult.StreamMetrics.ResponseText,
                latestExecutionOutput,
                verifyResult.StreamMetrics.ResponseText);
            var refineResult = ExecutePromptAndCapture(
                refinePrompt,
                model,
                promptExecutor,
                cancelSignalRegistration,
                checkpointContext);
            if (refineResult.WasCancelled)
            {
                return AgentAutonomousLoopResult.Cancelled(
                    iteration,
                    GetCurrentAgentLoopElapsed(resumedElapsed, stopwatch),
                    accumulatedCost);
            }

            accumulatedCost += EstimateAgentStepCost(refinePrompt, refineResult.StreamMetrics);
            var elapsedAfterRefine = GetCurrentAgentLoopElapsed(resumedElapsed, stopwatch);
            if (ResolveBudgetExceededResult(
                sessionBudget,
                iteration,
                elapsedAfterRefine,
                accumulatedCost) is AgentAutonomousLoopResult budgetExceededAfterRefine)
            {
                return budgetExceededAfterRefine;
            }

            previousRefinementOutput = refineResult.StreamMetrics.ResponseText;
        }

        return AgentAutonomousLoopResult.NotConcluded(
            sessionBudget.MaxSteps,
            GetCurrentAgentLoopElapsed(resumedElapsed, stopwatch),
            accumulatedCost);
    }

    private static AgentValidationReport ExecuteAgentValidationAfterChangeBlock(
        AgentCodeChangeEvidence executeEvidence,
        AgentProjectContextSnapshot projectContext,
        IToolRuntime toolRuntime)
    {
        if (!ShouldRunAgentValidationAfterChangeBlock(executeEvidence))
        {
            return AgentValidationReport.NotRequired();
        }

        ConsoleLogger.Info(
            "Validacao automatica pos-mudanca: bloco de alteracoes detectado; executando build, test e lint.");

        var runner = new AgentValidationCommandRunner(toolRuntime);
        var validationReport = runner
            .RunAsync(projectContext.WorkspaceRootDirectory, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        WriteAgentValidationLog(validationReport);
        return validationReport;
    }

    private static AgentAutoCorrectionResult ExecuteAgentAutoCorrectionWhenValidationFails(
        AgentExecutionPlan executionPlan,
        int iteration,
        AgentProjectContextSnapshot projectContext,
        string latestPlanOutput,
        string latestExecutionOutput,
        AgentCodeChangeEvidence latestChangeEvidence,
        AgentValidationReport validationReport,
        string? model,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration,
        PromptExecutionCheckpointContext checkpointContext,
        IToolRuntime toolRuntime)
    {
        if (!ShouldRunAgentAutoCorrection(validationReport))
        {
            return AgentAutoCorrectionResult.NotRequired(
                latestExecutionOutput,
                latestChangeEvidence,
                validationReport);
        }

        var currentExecutionOutput = latestExecutionOutput;
        var currentChangeEvidence = latestChangeEvidence;
        var currentValidationReport = validationReport;
        var autoCorrectionTranscript = new StringBuilder();
        var additionalCost = 0m;

        for (var attempt = 1; attempt <= AgentAutoCorrectionMaxAttempts; attempt++)
        {
            ConsoleLogger.Info(
                $"Auto-correcao de validacao: tentativa {attempt}/{AgentAutoCorrectionMaxAttempts} apos falha em build/test/lint.");
            var correctionPrompt = BuildAgentAutoCorrectionPrompt(
                executionPlan,
                iteration,
                projectContext,
                latestPlanOutput,
                currentExecutionOutput,
                currentValidationReport,
                attempt,
                AgentAutoCorrectionMaxAttempts);
            var correctionResult = ExecutePromptAndCapture(
                correctionPrompt,
                model,
                promptExecutor,
                cancelSignalRegistration,
                checkpointContext);
            if (correctionResult.WasCancelled)
            {
                return AgentAutoCorrectionResult.Cancelled(
                    currentExecutionOutput,
                    currentChangeEvidence,
                    currentValidationReport,
                    additionalCost);
            }

            additionalCost += EstimateAgentStepCost(
                correctionPrompt,
                correctionResult.StreamMetrics);
            var correctionOutput = correctionResult.StreamMetrics.ResponseText;
            AppendAgentAutoCorrectionTranscript(
                autoCorrectionTranscript,
                attempt,
                correctionOutput);

            var correctionEvidence = ParseAgentCodeChangeEvidence(correctionOutput);
            ConsoleLogger.Info(BuildAgentAutoCorrectionEvidenceLogMessage(attempt, correctionEvidence));
            currentExecutionOutput = BuildAgentExecutionOutputWithAutoCorrection(
                latestExecutionOutput,
                autoCorrectionTranscript.ToString());

            if (correctionEvidence.RequiresValidation || correctionEvidence.DeclaredNoCodeChanges)
            {
                currentChangeEvidence = correctionEvidence;
            }

            if (!ShouldRunAgentValidationAfterChangeBlock(correctionEvidence))
            {
                ConsoleLogger.Info(
                    "Auto-correcao de validacao: tentativa nao declarou bloco de mudancas validavel; mantendo falha atual.");
            }
            else
            {
                currentChangeEvidence = correctionEvidence;
                currentValidationReport = ExecuteAgentValidationAfterChangeBlock(
                    correctionEvidence,
                    projectContext,
                    toolRuntime);

                if (!currentValidationReport.HasFailures)
                {
                    ConsoleLogger.Info(
                        $"Auto-correcao de validacao: validacao passou apos tentativa {attempt}/{AgentAutoCorrectionMaxAttempts}.");
                    break;
                }
            }

            if (!currentValidationReport.HasFailures)
            {
                break;
            }

            if (attempt < AgentAutoCorrectionMaxAttempts)
            {
                ConsoleLogger.Info(
                    "Auto-correcao de validacao: validacao ainda falhou; preparando nova tentativa.");
                continue;
            }

            ConsoleLogger.Info(
                $"Auto-correcao de validacao: limite de {AgentAutoCorrectionMaxAttempts} tentativa(s) atingido; mantendo falha para verify/refine.");
        }

        return new AgentAutoCorrectionResult(
            WasCancelled: false,
            LatestExecutionOutput: currentExecutionOutput,
            LatestChangeEvidence: currentChangeEvidence,
            ValidationReport: currentValidationReport,
            AdditionalCost: additionalCost);
    }

    private static bool ShouldRunAgentAutoCorrection(AgentValidationReport validationReport)
    {
        return validationReport.WasRequired
            && validationReport.CommandsDiscovered
            && validationReport.HasFailures;
    }

    private static void AppendAgentAutoCorrectionTranscript(
        StringBuilder builder,
        int attempt,
        string correctionOutput)
    {
        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.AppendLine($"[AUTO-CORRECAO TENTATIVA {attempt}]");
        builder.Append(BuildPromptContextExcerpt(
            correctionOutput,
            "A tentativa nao retornou evidencias."));
    }

    private static string BuildAgentExecutionOutputWithAutoCorrection(
        string originalExecutionOutput,
        string autoCorrectionTranscript)
    {
        if (string.IsNullOrWhiteSpace(autoCorrectionTranscript))
        {
            return originalExecutionOutput;
        }

        return
            $"""
            [EXECUCAO ORIGINAL]
            {BuildPromptContextExcerpt(originalExecutionOutput, "Sem evidencias de execucao original.")}

            [AUTO-CORRECAO POS-VALIDACAO]
            {autoCorrectionTranscript.Trim()}
            """;
    }

    private static bool ShouldRunAgentValidationAfterChangeBlock(
        AgentCodeChangeEvidence executeEvidence)
    {
        return executeEvidence.HasDeclaredCodeChanges
            && executeEvidence.IsCompliant;
    }

    private static void WriteAgentValidationLog(AgentValidationReport validationReport)
    {
        if (!validationReport.WasRequired)
        {
            return;
        }

        if (!validationReport.CommandsDiscovered)
        {
            ConsoleLogger.Info(
                "Validacao automatica pos-mudanca: nenhum comando build/test/lint foi descoberto para este workspace.");
            return;
        }

        foreach (var result in validationReport.Results)
        {
            var status = result.IsSuccess ? "passou" : "falhou";
            ConsoleLogger.Info(
                $"Validacao automatica '{result.Name}' {status} " +
                $"(exit_code={result.ExitCode}, duracao={FormatRequiredBudgetDuration(result.Duration)}).");
        }
    }

    private static AgentProjectContextSnapshot LoadAgentProjectContextSnapshot()
    {
        try
        {
            var workspaceRoot = WorkspaceRootDetector.Resolve();
            var workspaceIndex = WorkspaceContextFileIndexCatalog.GetOrCreate(workspaceRoot.DirectoryPath);
            var indexedFilePaths = workspaceIndex.CurrentMap.Entries
                .Where(static entry => entry.Kind == WorkspaceEntryKind.File)
                .Select(static entry => NormalizeAgentProjectRelativePath(entry.RelativePath))
                .ToArray();

            var codeFiles = new List<string>();
            var testFiles = new List<string>();
            var documentationFiles = new List<string>();

            foreach (var indexedFilePath in indexedFilePaths)
            {
                if (IsAgentProjectTestFile(indexedFilePath))
                {
                    testFiles.Add(indexedFilePath);
                }

                if (IsAgentProjectDocumentationFile(indexedFilePath))
                {
                    documentationFiles.Add(indexedFilePath);
                }

                if (IsAgentProjectCodeFile(indexedFilePath))
                {
                    codeFiles.Add(indexedFilePath);
                }
            }

            var recentGitCommits = TryReadAgentRecentGitHistory(
                workspaceRoot.DirectoryPath,
                out var gitHistorySummary);

            return new AgentProjectContextSnapshot(
                WorkspaceRootDirectory: workspaceRoot.DirectoryPath,
                WorkspaceRootKind: workspaceRoot.Kind,
                IndexedFileCount: indexedFilePaths.Length,
                CodeFileCount: codeFiles.Count,
                TestFileCount: testFiles.Count,
                DocumentationFileCount: documentationFiles.Count,
                CodeFileSamples: codeFiles.Take(AgentProjectContextSampleLimit).ToArray(),
                TestFileSamples: testFiles.Take(AgentProjectContextSampleLimit).ToArray(),
                DocumentationFileSamples: documentationFiles.Take(AgentProjectContextSampleLimit).ToArray(),
                RecentGitCommits: recentGitCommits,
                GitHistorySummary: gitHistorySummary);
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or DirectoryNotFoundException
            or IOException
            or UnauthorizedAccessException)
        {
            var fallbackDirectory = TryResolveCurrentDirectoryForAgentContext();
            var failureDetail = TruncateAgentProjectContextValue(ex.Message, 180);
            return new AgentProjectContextSnapshot(
                WorkspaceRootDirectory: fallbackDirectory,
                WorkspaceRootKind: WorkspaceRootKind.CurrentDirectory,
                IndexedFileCount: 0,
                CodeFileCount: 0,
                TestFileCount: 0,
                DocumentationFileCount: 0,
                CodeFileSamples: [],
                TestFileSamples: [],
                DocumentationFileSamples: [],
                RecentGitCommits: [],
                GitHistorySummary: $"indisponivel ({failureDetail})");
        }
    }

    private static string TryResolveCurrentDirectoryForAgentContext()
    {
        try
        {
            return Directory.GetCurrentDirectory();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"<diretorio-indisponivel: {TruncateAgentProjectContextValue(ex.Message, 100)}>";
        }
    }

    private static IReadOnlyList<string> TryReadAgentRecentGitHistory(
        string workspaceRootDirectory,
        out string summary)
    {
        var gitDirectoryPath = Path.Combine(workspaceRootDirectory, ".git");
        if (!Directory.Exists(gitDirectoryPath) && !File.Exists(gitDirectoryPath))
        {
            summary = "repositorio git nao detectado.";
            return [];
        }

        try
        {
            using var process = new Process
            {
                StartInfo = BuildAgentGitHistoryProcessStartInfo(workspaceRootDirectory)
            };

            if (!process.Start())
            {
                summary = "nao foi possivel iniciar o comando git.";
                return [];
            }

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();

            if (!process.WaitForExit((int)AgentProjectGitHistoryCommandTimeout.TotalMilliseconds))
            {
                TryKillAgentGitHistoryProcess(process);
                summary =
                    $"comando git excedeu o limite de {FormatRequiredBudgetDuration(AgentProjectGitHistoryCommandTimeout)}.";
                return [];
            }

            if (process.ExitCode != 0)
            {
                var standardErrorExcerpt = TruncateAgentProjectContextValue(standardError, 180);
                summary = standardErrorExcerpt.Length == 0
                    ? $"git retornou codigo {process.ExitCode}."
                    : $"git retornou codigo {process.ExitCode}: {standardErrorExcerpt}";
                return [];
            }

            var commits = ParseAgentRecentGitHistory(standardOutput);
            summary = commits.Count == 0
                ? "sem commits recentes identificados."
                : $"{commits.Count} commit(s) recentes.";
            return commits;
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or Win32Exception
            or IOException
            or UnauthorizedAccessException)
        {
            summary = $"indisponivel ({TruncateAgentProjectContextValue(ex.Message, 180)})";
            return [];
        }
    }

    private static ProcessStartInfo BuildAgentGitHistoryProcessStartInfo(string workspaceRootDirectory)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "git",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        processStartInfo.ArgumentList.Add("-C");
        processStartInfo.ArgumentList.Add(workspaceRootDirectory);
        processStartInfo.ArgumentList.Add("--no-pager");
        processStartInfo.ArgumentList.Add("log");
        processStartInfo.ArgumentList.Add("--date=short");
        processStartInfo.ArgumentList.Add("--pretty=format:%h|%ad|%s");
        processStartInfo.ArgumentList.Add("-n");
        processStartInfo.ArgumentList.Add(
            AgentProjectGitHistoryCommitLimit.ToString(CultureInfo.InvariantCulture));

        return processStartInfo;
    }

    private static void TryKillAgentGitHistoryProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or Win32Exception
            or NotSupportedException)
        {
        }
    }

    private static IReadOnlyList<string> ParseAgentRecentGitHistory(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            return [];
        }

        var commits = new List<string>(AgentProjectGitHistoryCommitLimit);
        var lines = rawOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(AgentProjectGitHistoryCommitLimit);

        foreach (var line in lines)
        {
            var parts = line.Split('|', 3, StringSplitOptions.TrimEntries);
            if (parts.Length == 3)
            {
                var hash = parts[0];
                var date = parts[1];
                var subject = TruncateAgentProjectContextValue(parts[2], AgentProjectGitSubjectMaxCharacters);
                if (hash.Length > 0 && date.Length > 0 && subject.Length > 0)
                {
                    commits.Add($"{hash} ({date}) {subject}");
                    continue;
                }
            }

            var normalizedLine = TruncateAgentProjectContextValue(
                line,
                AgentProjectGitSubjectMaxCharacters + 40);
            if (normalizedLine.Length > 0)
            {
                commits.Add(normalizedLine);
            }
        }

        return commits;
    }

    private static bool IsAgentProjectCodeFile(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        if (IsAgentProjectTestFile(relativePath) || IsAgentProjectDocumentationFile(relativePath))
        {
            return false;
        }

        var extension = Path.GetExtension(relativePath);
        return AgentProjectCodeExtensions.Contains(extension);
    }

    private static bool IsAgentProjectTestFile(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var normalizedPath = NormalizeAgentProjectRelativePath(relativePath);
        if (normalizedPath.StartsWith("test/", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith("tests/", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.Contains("/test/", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.Contains("/tests/", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.Contains("/__tests__/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var fileName = GetAgentProjectLeafPathSegment(normalizedPath);
        return fileName.EndsWith("test.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("tests.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains(".test.", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains(".spec.", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("test_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAgentProjectDocumentationFile(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var normalizedPath = NormalizeAgentProjectRelativePath(relativePath);
        if (normalizedPath.StartsWith("docs/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var fileName = GetAgentProjectLeafPathSegment(normalizedPath);
        if (AgentProjectDocumentationFileNames.Contains(fileName))
        {
            return true;
        }

        var extension = Path.GetExtension(fileName);
        return AgentProjectDocumentationExtensions.Contains(extension);
    }

    private static string NormalizeAgentProjectRelativePath(string relativePath)
    {
        return relativePath
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/')
            .Trim('/');
    }

    private static string GetAgentProjectLeafPathSegment(string normalizedPath)
    {
        var separatorIndex = normalizedPath.LastIndexOf('/');
        if (separatorIndex < 0)
        {
            return normalizedPath;
        }

        return separatorIndex + 1 >= normalizedPath.Length
            ? string.Empty
            : normalizedPath[(separatorIndex + 1)..];
    }

    private static string TruncateAgentProjectContextValue(string value, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = string.Join(
            " ",
            value.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (normalized.Length <= maxCharacters)
        {
            return normalized;
        }

        var safeMaxCharacters = Math.Max(1, maxCharacters);
        return $"{normalized[..safeMaxCharacters].TrimEnd()}...";
    }

    private static string BuildAgentProjectContextPrompt(AgentProjectContextSnapshot projectContext)
    {
        var codeSummary = BuildAgentProjectPathSummary(
            projectContext.CodeFileSamples,
            projectContext.CodeFileCount,
            fallback: "nenhum arquivo de codigo identificado.");
        var testSummary = BuildAgentProjectPathSummary(
            projectContext.TestFileSamples,
            projectContext.TestFileCount,
            fallback: "nenhum arquivo de teste identificado.");
        var documentationSummary = BuildAgentProjectPathSummary(
            projectContext.DocumentationFileSamples,
            projectContext.DocumentationFileCount,
            fallback: "nenhum arquivo de documentacao identificado.");
        var gitHistorySummary = projectContext.RecentGitCommits.Count == 0
            ? projectContext.GitHistorySummary
            : string.Join(" | ", projectContext.RecentGitCommits);

        return
            $"""
            - raiz-workspace: {projectContext.WorkspaceRootDirectory}
            - tipo-raiz: {GetWorkspaceRootKindLabel(projectContext.WorkspaceRootKind)}
            - arquivos-indexados: {projectContext.IndexedFileCount}
            - codigo ({projectContext.CodeFileCount}): {codeSummary}
            - testes ({projectContext.TestFileCount}): {testSummary}
            - docs ({projectContext.DocumentationFileCount}): {documentationSummary}
            - historico git recente: {gitHistorySummary}
            """;
    }

    private static string BuildAgentProjectPathSummary(
        IReadOnlyList<string> samples,
        int totalCount,
        string fallback)
    {
        if (totalCount <= 0 || samples.Count == 0)
        {
            return fallback;
        }

        var summary = string.Join(", ", samples);
        if (totalCount <= samples.Count)
        {
            return summary;
        }

        return $"{summary}, ... (+{totalCount - samples.Count})";
    }

    private static AgentAutonomousLoopState NormalizeAgentAutonomousLoopState(
        AgentAutonomousLoopState state,
        int maxSteps)
    {
        var boundedMaxSteps = Math.Max(1, maxSteps);
        var normalizedIteration = state.NextIteration <= 0
            ? 1
            : Math.Min(state.NextIteration, boundedMaxSteps);
        var normalizedElapsed = state.Elapsed < TimeSpan.Zero
            ? TimeSpan.Zero
            : state.Elapsed;
        var normalizedCost = state.AccumulatedCost < 0m
            ? 0m
            : state.AccumulatedCost;

        return new AgentAutonomousLoopState(
            NextIteration: normalizedIteration,
            PreviousVerificationOutput: state.PreviousVerificationOutput ?? string.Empty,
            PreviousRefinementOutput: state.PreviousRefinementOutput ?? string.Empty,
            Elapsed: normalizedElapsed,
            AccumulatedCost: normalizedCost);
    }

    private static TimeSpan GetCurrentAgentLoopElapsed(
        TimeSpan resumedElapsed,
        Stopwatch stopwatch)
    {
        var elapsed = resumedElapsed + stopwatch.Elapsed;
        return elapsed < TimeSpan.Zero
            ? TimeSpan.Zero
            : elapsed;
    }

    private static void AppendAgentAutonomousLoopCheckpoint(
        PromptExecutionCheckpointContext checkpointContext,
        AgentSessionBudget sessionBudget,
        AgentAutonomousLoopState loopState)
    {
        var checkpointPayload = new AgentLoopCheckpointPayload(
            Kind: AgentLoopCheckpointKind,
            Version: 1,
            NextIteration: loopState.NextIteration,
            MaxSteps: sessionBudget.MaxSteps,
            MaxTimeSeconds: sessionBudget.MaxTime?.TotalSeconds,
            MaxCost: sessionBudget.MaxCost,
            AccumulatedCost: loopState.AccumulatedCost,
            ElapsedSeconds: loopState.Elapsed.TotalSeconds,
            PreviousVerificationOutput: BuildPromptContextExcerpt(
                loopState.PreviousVerificationOutput,
                string.Empty),
            PreviousRefinementOutput: BuildPromptContextExcerpt(
                loopState.PreviousRefinementOutput,
                string.Empty));
        var checkpoint = new ExecutionSessionCheckpoint(
            TimestampUtc: DateTimeOffset.UtcNow,
            SessionId: checkpointContext.SessionId,
            Command: checkpointContext.Command,
            Stage: AgentLoopCheckpointStage,
            Status: ExecutionCheckpointStatus.InProgress,
            Prompt: checkpointContext.Prompt,
            Model: checkpointContext.Model,
            SkillName: checkpointContext.SkillName,
            Detail: JsonSerializer.Serialize(checkpointPayload));
        TryAppendExecutionCheckpoint(checkpoint, checkpointContext.ExecutionCheckpointAppender);
    }

    private static AgentAutonomousLoopResult? ResolveBudgetExceededResult(
        AgentSessionBudget sessionBudget,
        int iteration,
        TimeSpan elapsed,
        decimal accumulatedCost)
    {
        if (sessionBudget.MaxTime is TimeSpan maxTime
            && elapsed > maxTime)
        {
            return AgentAutonomousLoopResult.MaxTimeExceeded(
                iteration,
                elapsed,
                accumulatedCost);
        }

        if (sessionBudget.MaxCost is decimal maxCost
            && accumulatedCost > maxCost)
        {
            return AgentAutonomousLoopResult.MaxCostExceeded(
                iteration,
                elapsed,
                accumulatedCost);
        }

        return null;
    }

    private static decimal EstimateAgentStepCost(
        string prompt,
        PromptStreamMetrics streamMetrics)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var promptCharacterCount = prompt.Length;
        var responseCharacterCount = Math.Max(0, streamMetrics.CharacterCount);
        return promptCharacterCount + responseCharacterCount;
    }

    private static string FormatOptionalBudgetDuration(TimeSpan? duration)
    {
        return duration is TimeSpan value
            ? FormatRequiredBudgetDuration(value)
            : "sem-limite";
    }

    private static string FormatRequiredBudgetDuration(TimeSpan? duration)
    {
        return duration is TimeSpan value
            ? FormatRequiredBudgetDuration(value)
            : "0s";
    }

    private static string FormatRequiredBudgetDuration(TimeSpan duration)
    {
        return $"{duration.TotalSeconds:0.###}s";
    }

    private static string FormatOptionalBudgetValue(decimal? value)
    {
        return value is decimal budgetValue
            ? FormatRequiredBudgetValue(budgetValue)
            : "sem-limite";
    }

    private static string FormatRequiredBudgetValue(decimal? value)
    {
        return value is decimal budgetValue
            ? FormatRequiredBudgetValue(budgetValue)
            : "0";
    }

    private static string FormatRequiredBudgetValue(decimal value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static int ExecuteResume(
        string? sessionId,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration,
        Action<ExecutionSessionCheckpoint> executionCheckpointAppender,
        Func<IReadOnlyList<ExecutionSessionCheckpoint>> executionCheckpointLoader,
        IToolRuntime toolRuntime)
    {
        ArgumentNullException.ThrowIfNull(promptExecutor);
        ArgumentNullException.ThrowIfNull(cancelSignalRegistration);
        ArgumentNullException.ThrowIfNull(executionCheckpointAppender);
        ArgumentNullException.ThrowIfNull(executionCheckpointLoader);
        ArgumentNullException.ThrowIfNull(toolRuntime);

        ConsoleLogger.Info("Buscando checkpoint de sessao interrompida.");

        IReadOnlyList<ExecutionSessionCheckpoint> checkpoints;
        try
        {
            checkpoints = executionCheckpointLoader();
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or IOException
            or UnauthorizedAccessException
            or JsonException)
        {
            var loadError = CliFriendlyError.Runtime(
                detail: $"Nao foi possivel carregar checkpoints locais de execucao. {ex.Message}",
                suggestion: "Verifique o arquivo de checkpoints em ~/.asxrun e tente novamente.");
            WriteFriendlyError(loadError);
            return (int)loadError.ExitCode;
        }

        var latestCheckpoints = GetLatestCheckpointBySession(checkpoints);
        var normalizedSessionId = string.IsNullOrWhiteSpace(sessionId)
            ? null
            : sessionId.Trim();

        ExecutionSessionCheckpoint? selectedCheckpoint = null;

        if (normalizedSessionId is not null)
        {
            foreach (var checkpoint in latestCheckpoints)
            {
                if (!string.Equals(checkpoint.SessionId, normalizedSessionId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                selectedCheckpoint = checkpoint;
                break;
            }

            if (selectedCheckpoint is null)
            {
                var notFoundError = CliFriendlyError.InvalidArguments(
                    detail: $"Nenhum checkpoint foi encontrado para a sessao '{normalizedSessionId}'.",
                    suggestion: $"Execute '{CliName} resume' para retomar a sessao interrompida mais recente.");
                WriteFriendlyError(notFoundError);
                return (int)notFoundError.ExitCode;
            }
        }
        else
        {
            foreach (var checkpoint in latestCheckpoints)
            {
                if (!IsResumablePromptCheckpoint(checkpoint))
                {
                    continue;
                }

                selectedCheckpoint = checkpoint;
                break;
            }

            if (selectedCheckpoint is null)
            {
                var missingError = CliFriendlyError.Runtime(
                    detail: "Nenhuma sessao interrompida de ask/agent/skill foi encontrada para retomar.",
                    suggestion: $"Execute '{CliName} ask \"seu prompt\"', '{CliName} agent \"seu objetivo\"' ou '{CliName} skill <nome> \"seu prompt\"' e use '{CliName} resume' se houver interrupcao.");
                WriteFriendlyError(missingError);
                return (int)missingError.ExitCode;
            }
        }

        var checkpointToResume = selectedCheckpoint.Value;
        if (!IsResumablePromptCheckpoint(checkpointToResume))
        {
            var notResumableError = CliFriendlyError.Runtime(
                detail: $"A sessao '{checkpointToResume.SessionId}' nao esta interrompida ou nao pode ser retomada.",
                suggestion: $"Use '{CliName} resume' sem argumentos para buscar outra sessao interrompida.");
            WriteFriendlyError(notResumableError);
            return (int)notResumableError.ExitCode;
        }

        ConsoleLogger.Info(
            $"Retomando sessao '{checkpointToResume.SessionId}' do comando '{checkpointToResume.Command}' a partir da etapa '{checkpointToResume.Stage}'.");

        if (string.Equals(checkpointToResume.Command, "ask", StringComparison.OrdinalIgnoreCase))
        {
            return ExecuteAsk(
                checkpointToResume.Prompt,
                checkpointToResume.Model,
                promptExecutor,
                cancelSignalRegistration,
                executionCheckpointAppender);
        }

        if (string.Equals(checkpointToResume.Command, "agent", StringComparison.OrdinalIgnoreCase))
        {
            if (TryResolveAgentResumeCheckpointState(checkpoints, checkpointToResume, out var resumeCheckpointState))
            {
                return ExecuteAgent(
                    checkpointToResume.Prompt,
                    checkpointToResume.Model,
                    maxSteps: resumeCheckpointState.SessionBudget.MaxSteps,
                    maxTime: resumeCheckpointState.SessionBudget.MaxTime,
                    maxCost: resumeCheckpointState.SessionBudget.MaxCost,
                    promptExecutor,
                    cancelSignalRegistration,
                    executionCheckpointAppender,
                    toolRuntime,
                    resumeLoopState: resumeCheckpointState.LoopState,
                    checkpointSessionId: checkpointToResume.SessionId);
            }

            ConsoleLogger.Info(
                "Checkpoint do agente sem metadados de retomada incremental. Reiniciando loop a partir da iteracao 1.");
            return ExecuteAgent(
                checkpointToResume.Prompt,
                checkpointToResume.Model,
                maxSteps: null,
                maxTime: null,
                maxCost: null,
                promptExecutor,
                cancelSignalRegistration,
                executionCheckpointAppender,
                toolRuntime,
                checkpointSessionId: checkpointToResume.SessionId);
        }

        return ExecuteSkill(
            checkpointToResume.SkillName!,
            checkpointToResume.Prompt,
            checkpointToResume.Model,
            promptExecutor,
            cancelSignalRegistration,
            executionCheckpointAppender);
    }

    private static IReadOnlyList<ExecutionSessionCheckpoint> GetLatestCheckpointBySession(
        IReadOnlyList<ExecutionSessionCheckpoint> checkpoints)
    {
        ArgumentNullException.ThrowIfNull(checkpoints);

        return checkpoints
            .OrderByDescending(static checkpoint => checkpoint.TimestampUtc)
            .GroupBy(static checkpoint => checkpoint.SessionId, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderByDescending(static checkpoint => checkpoint.TimestampUtc)
            .ToArray();
    }

    private static bool IsResumablePromptCheckpoint(ExecutionSessionCheckpoint checkpoint)
    {
        if (checkpoint.Status == ExecutionCheckpointStatus.Completed)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(checkpoint.Prompt))
        {
            return false;
        }

        if (string.Equals(checkpoint.Command, "ask", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(checkpoint.Command, "agent", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(checkpoint.Command, "skill", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(checkpoint.SkillName);
    }

    private static bool TryResolveAgentResumeCheckpointState(
        IReadOnlyList<ExecutionSessionCheckpoint> checkpoints,
        ExecutionSessionCheckpoint checkpointToResume,
        out AgentResumeCheckpointState resumeCheckpointState)
    {
        ArgumentNullException.ThrowIfNull(checkpoints);

        foreach (var checkpoint in checkpoints.OrderByDescending(static checkpoint => checkpoint.TimestampUtc))
        {
            if (!string.Equals(
                checkpoint.SessionId,
                checkpointToResume.SessionId,
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(checkpoint.Command, "agent", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(
                checkpoint.Stage,
                AgentLoopCheckpointStage,
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryParseAgentLoopCheckpointPayload(checkpoint.Detail, out resumeCheckpointState))
            {
                continue;
            }

            ConsoleLogger.Info(
                $"Checkpoint incremental do agente identificado: iteracao {resumeCheckpointState.LoopState.NextIteration}/{resumeCheckpointState.SessionBudget.MaxSteps}.");
            return true;
        }

        resumeCheckpointState = default;
        return false;
    }

    private static bool TryParseAgentLoopCheckpointPayload(
        string checkpointDetail,
        out AgentResumeCheckpointState resumeCheckpointState)
    {
        resumeCheckpointState = default;

        if (string.IsNullOrWhiteSpace(checkpointDetail))
        {
            return false;
        }

        AgentLoopCheckpointPayloadDocument? payloadDocument;
        try
        {
            payloadDocument = JsonSerializer.Deserialize<AgentLoopCheckpointPayloadDocument>(checkpointDetail);
        }
        catch (JsonException)
        {
            return false;
        }

        if (payloadDocument is null
            || !string.Equals(payloadDocument.Kind, AgentLoopCheckpointKind, StringComparison.OrdinalIgnoreCase)
            || payloadDocument.Version != 1
            || payloadDocument.NextIteration is not int nextIteration
            || nextIteration <= 0
            || payloadDocument.MaxSteps is not int maxSteps
            || maxSteps <= 0)
        {
            return false;
        }

        TimeSpan? maxTime = null;
        if (payloadDocument.MaxTimeSeconds is double maxTimeSeconds)
        {
            if (!double.IsFinite(maxTimeSeconds) || maxTimeSeconds <= 0d)
            {
                return false;
            }

            maxTime = TimeSpan.FromSeconds(maxTimeSeconds);
        }

        decimal? maxCost = null;
        if (payloadDocument.MaxCost is decimal maxCostValue)
        {
            if (maxCostValue <= 0m)
            {
                return false;
            }

            maxCost = maxCostValue;
        }

        var accumulatedCost = payloadDocument.AccumulatedCost ?? 0m;
        if (accumulatedCost < 0m)
        {
            accumulatedCost = 0m;
        }

        var elapsedSeconds = payloadDocument.ElapsedSeconds ?? 0d;
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0d)
        {
            elapsedSeconds = 0d;
        }

        var loopState = NormalizeAgentAutonomousLoopState(
            new AgentAutonomousLoopState(
                NextIteration: Math.Min(Math.Max(1, nextIteration), maxSteps),
                PreviousVerificationOutput: payloadDocument.PreviousVerificationOutput ?? string.Empty,
                PreviousRefinementOutput: payloadDocument.PreviousRefinementOutput ?? string.Empty,
                Elapsed: TimeSpan.FromSeconds(elapsedSeconds),
                AccumulatedCost: accumulatedCost),
            maxSteps);

        resumeCheckpointState = new AgentResumeCheckpointState(
            SessionBudget: new AgentSessionBudget(
                MaxSteps: maxSteps,
                MaxTime: maxTime,
                MaxCost: maxCost),
            LoopState: loopState);
        return true;
    }

    private static int ExecuteChat(
        string? model,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> modelsExecutor,
        IToolRuntime toolRuntime,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration,
        Func<IReadOnlyList<PromptHistoryEntry>> historyLoader)
    {
        ConsoleLogger.Info("Modo interativo iniciado. Digite 'exit' para sair.");
        ConsoleLogger.Info("Comandos interativos disponiveis: /help, /clear, /models, /tools, /exit.");
        ConsoleLogger.Info("Atalhos do teclado: setas para historico, Ctrl+R para busca incremental, Tab para autocomplete e Esc para cancelar busca.");

        var historyPrompts = LoadInteractiveChatHistoryPrompts(historyLoader);
        var inputNavigator = new ChatInputHistoryNavigator(historyPrompts);
        var modelSuggestions = new Lazy<IReadOnlyList<string>>(
            () => LoadInteractiveChatAutocompleteModelNames(model, modelsExecutor),
            isThreadSafe: false);
        var autocompleteEngine = new ChatAutocompleteEngine(
            InteractiveChatCommandSuggestions,
            CliCommandSuggestions,
            CliOptionSuggestions,
            () => modelSuggestions.Value);

        while (true)
        {
            var input = ReadInteractiveChatInput(inputNavigator, autocompleteEngine);

            if (input is null || IsExitCommand(input))
            {
                ConsoleLogger.Info("Modo interativo encerrado.");
                return (int)CliExitCode.Success;
            }

            var interactiveCommandResult = TryHandleInteractiveChatCommand(input, modelsExecutor, toolRuntime);
            if (interactiveCommandResult == InteractiveChatCommandResult.Exit)
            {
                ConsoleLogger.Info("Modo interativo encerrado.");
                return (int)CliExitCode.Success;
            }

            if (interactiveCommandResult == InteractiveChatCommandResult.Handled)
            {
                continue;
            }

            var prompt = input.Trim();
            if (prompt.Length == 0)
            {
                continue;
            }

            inputNavigator.RememberPrompt(prompt);
            var wasCancelled = ExecutePrompt(prompt, model, promptExecutor, cancelSignalRegistration);
            if (wasCancelled)
            {
                ConsoleLogger.Info("Prompt cancelado. Digite outro prompt ou 'exit' para sair.");
            }
        }

    }

    private static InteractiveChatCommandResult TryHandleInteractiveChatCommand(
        string input,
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> modelsExecutor,
        IToolRuntime toolRuntime)
    {
        var trimmedInput = input.Trim();
        if (!trimmedInput.StartsWith('/'))
        {
            return InteractiveChatCommandResult.NotACommand;
        }

        var separatorIndex = trimmedInput.IndexOfAny([' ', '\t']);
        var command = separatorIndex < 0
            ? trimmedInput
            : trimmedInput[..separatorIndex];
        var commandArguments = separatorIndex < 0
            ? string.Empty
            : trimmedInput[(separatorIndex + 1)..].Trim();

        return command.ToLowerInvariant() switch
        {
            "/help" => HandleChatCommandWithoutArguments(command, commandArguments, WriteInteractiveChatHelp),
            "/clear" => HandleChatCommandWithoutArguments(command, commandArguments, ClearInteractiveChatConsole),
            "/models" => HandleChatCommandWithoutArguments(
                command,
                commandArguments,
                () => ExecuteInteractiveModelsCommand(modelsExecutor)),
            "/tools" => HandleChatCommandWithoutArguments(
                command,
                commandArguments,
                () => WriteInteractiveTools(toolRuntime)),
            "/exit" => HandleExitInteractiveCommand(command, commandArguments),
            _ => HandleUnknownInteractiveCommand(command)
        };
    }

    private static InteractiveChatCommandResult HandleChatCommandWithoutArguments(
        string command,
        string commandArguments,
        Action commandHandler)
    {
        if (!string.IsNullOrWhiteSpace(commandArguments))
        {
            ConsoleLogger.Error(
                $"O comando interativo '{command}' nao aceita argumentos adicionais.");
            ConsoleLogger.Info($"Use '{command}' sem argumentos.");
            return InteractiveChatCommandResult.Handled;
        }

        commandHandler();
        return InteractiveChatCommandResult.Handled;
    }

    private static InteractiveChatCommandResult HandleExitInteractiveCommand(
        string command,
        string commandArguments)
    {
        if (!string.IsNullOrWhiteSpace(commandArguments))
        {
            ConsoleLogger.Error(
                $"O comando interativo '{command}' nao aceita argumentos adicionais.");
            ConsoleLogger.Info($"Use '{command}' sem argumentos.");
            return InteractiveChatCommandResult.Handled;
        }

        return InteractiveChatCommandResult.Exit;
    }

    private static InteractiveChatCommandResult HandleUnknownInteractiveCommand(string command)
    {
        ConsoleLogger.Error($"Comando interativo '{command}' nao e suportado.");
        ConsoleLogger.Info("Use '/help' para listar os comandos interativos disponiveis.");
        return InteractiveChatCommandResult.Handled;
    }

    private static void WriteInteractiveChatHelp()
    {
        ConsoleLogger.Info("Comandos interativos do chat:");
        Console.WriteLine("- /help   Mostra esta ajuda de comandos interativos.");
        Console.WriteLine("- /clear  Limpa a tela atual do terminal.");
        Console.WriteLine("- /models Lista os modelos locais do Ollama.");
        Console.WriteLine("- /tools  Mostra recursos locais disponiveis no CLI.");
        Console.WriteLine("- /exit   Encerra o modo interativo.");
        Console.WriteLine("- Tab     Autocompleta comandos, opcoes e nomes de modelos.");
    }

    private static void ClearInteractiveChatConsole()
    {
        if (!Console.IsOutputRedirected)
        {
            try
            {
                Console.Clear();
            }
            catch
            {
                // Em alguns terminais nao interativos o clear pode nao ser suportado.
            }
        }

        ConsoleLogger.Info("Comando /clear executado.");
    }

    private static void ExecuteInteractiveModelsCommand(
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> modelsExecutor)
    {
        try
        {
            _ = ExecuteModels(modelsExecutor);
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error($"Nao foi possivel listar modelos no chat: {ex.Message}");
            ConsoleLogger.Info("Dica: execute 'asxrun doctor' e tente novamente.");
        }
    }

    private static void WriteInteractiveTools(IToolRuntime toolRuntime)
    {
        ArgumentNullException.ThrowIfNull(toolRuntime);

        ConsoleLogger.Info("Ferramentas registradas no runtime local:");

        var tools = toolRuntime
            .ListTools()
            .OrderBy(static tool => tool.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (tools.Length == 0)
        {
            Console.WriteLine("- Nenhuma ferramenta registrada para a plataforma atual.");
        }
        else
        {
            foreach (var tool in tools)
            {
                var parametersLabel = tool.Parameters.Count == 0
                    ? "sem parametros"
                    : string.Join(
                        ", ",
                        tool.Parameters.Select(static parameter =>
                            parameter.IsRequired
                                ? $"{parameter.Name} (obrigatorio)"
                                : $"{parameter.Name} (opcional)"));
                Console.WriteLine($"- {tool.Name}: {tool.Description} Parametros: {parametersLabel}.");
            }
        }

        Console.WriteLine("- Skills built-in: use 'asxrun skills' e 'asxrun skill <nome> ...'.");
        Console.WriteLine("- Config local: use 'asxrun config get/set'.");
        Console.WriteLine("- Historico local: use 'asxrun history'.");
        Console.WriteLine("- Comandos de suporte: /help, /clear, /models, /tools, /exit.");
    }

    private static IReadOnlyList<string> LoadInteractiveChatHistoryPrompts(
        Func<IReadOnlyList<PromptHistoryEntry>> historyLoader)
    {
        try
        {
            return historyLoader()
                .OrderByDescending(static entry => entry.TimestampUtc)
                .Select(static entry => entry.Prompt.Trim())
                .Where(static prompt => prompt.Length > 0)
                .ToArray();
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error(
                $"Nao foi possivel carregar o historico para navegacao no chat: {ex.Message}");
            ConsoleLogger.Info("O chat continuara sem historico navegavel nesta sessao.");
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<string> LoadInteractiveChatAutocompleteModelNames(
        string? selectedModel,
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> modelsExecutor)
    {
        var modelNames = new List<string>();
        var knownModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddModelName(selectedModel);
        AddModelName(OllamaModelDefaults.DefaultModel);

        try
        {
            var localModels = modelsExecutor(CancellationToken.None).GetAwaiter().GetResult();
            foreach (var localModel in localModels)
            {
                AddModelName(localModel.Name);
            }
        }
        catch (Exception ex)
        {
            ConsoleLogger.Info(
                $"Autocomplete de modelos indisponivel no momento: {ex.Message}");
        }

        return modelNames;

        void AddModelName(string? modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return;
            }

            var normalizedName = modelName.Trim();
            if (!knownModels.Add(normalizedName))
            {
                return;
            }

            modelNames.Add(normalizedName);
        }
    }

    private static string? ReadInteractiveChatInput(
        ChatInputHistoryNavigator navigator,
        ChatAutocompleteEngine autocompleteEngine)
    {
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(autocompleteEngine);

        if (Console.IsInputRedirected)
        {
            Console.Write(InteractiveChatPromptPrefix);
            return Console.ReadLine();
        }

        var previousRenderedLength = 0;
        RenderInteractiveChatInput(navigator, ref previousRenderedLength);

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if ((key.Modifiers & ConsoleModifiers.Control) != 0
                && key.Key == ConsoleKey.R)
            {
                autocompleteEngine.Reset();
                navigator.StartIncrementalSearch();
                RenderInteractiveChatInput(navigator, ref previousRenderedLength);
                continue;
            }

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    autocompleteEngine.Reset();
                    navigator.AcceptIncrementalSearch();
                    Console.WriteLine();
                    return navigator.CurrentInput;
                case ConsoleKey.UpArrow:
                    autocompleteEngine.Reset();
                    if (navigator.IsIncrementalSearchActive)
                    {
                        navigator.CycleIncrementalSearch(olderMatch: true);
                    }
                    else
                    {
                        navigator.MovePrevious();
                    }

                    break;
                case ConsoleKey.DownArrow:
                    autocompleteEngine.Reset();
                    if (navigator.IsIncrementalSearchActive)
                    {
                        navigator.CycleIncrementalSearch(olderMatch: false);
                    }
                    else
                    {
                        navigator.MoveNext();
                    }

                    break;
                case ConsoleKey.Backspace:
                    autocompleteEngine.Reset();
                    if (navigator.IsIncrementalSearchActive)
                    {
                        navigator.RemoveSearchCharacter();
                    }
                    else
                    {
                        navigator.RemoveLastInputCharacter();
                    }

                    break;
                case ConsoleKey.Escape:
                    autocompleteEngine.Reset();
                    if (navigator.IsIncrementalSearchActive)
                    {
                        navigator.CancelIncrementalSearch();
                    }
                    else
                    {
                        navigator.ClearInput();
                    }

                    break;
                case ConsoleKey.Tab:
                    if (!navigator.IsIncrementalSearchActive)
                    {
                        var completedInput = autocompleteEngine.ApplyNextCompletion(navigator.CurrentInput);
                        navigator.ReplaceInput(completedInput);
                    }

                    break;
                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        autocompleteEngine.Reset();
                        if (navigator.IsIncrementalSearchActive)
                        {
                            navigator.AppendSearchCharacter(key.KeyChar);
                        }
                        else
                        {
                            navigator.AppendInputCharacter(key.KeyChar);
                        }
                    }

                    break;
            }

            RenderInteractiveChatInput(navigator, ref previousRenderedLength);
        }
    }

    private static void RenderInteractiveChatInput(
        ChatInputHistoryNavigator navigator,
        ref int previousRenderedLength)
    {
        var searchSuffix = BuildInteractiveChatSearchSuffix(navigator);
        var renderedLine = $"{InteractiveChatPromptPrefix}{navigator.CurrentInput}{searchSuffix}";
        var clearPaddingLength = Math.Max(0, previousRenderedLength - renderedLine.Length);
        var clearPadding = clearPaddingLength == 0
            ? string.Empty
            : new string(' ', clearPaddingLength);

        Console.Write($"\r{renderedLine}{clearPadding}");
        previousRenderedLength = renderedLine.Length;
    }

    private static string BuildInteractiveChatSearchSuffix(ChatInputHistoryNavigator navigator)
    {
        if (!navigator.IsIncrementalSearchActive)
        {
            return string.Empty;
        }

        var searchStatus = navigator.HasIncrementalSearchMatch
            ? "encontrado"
            : "sem resultado";
        return $"  [busca incremental: \"{navigator.SearchQuery}\" - {searchStatus}]";
    }

    private static bool ExecutePrompt(
        string prompt,
        string? model,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration,
        PromptExecutionCheckpointContext? checkpointContext = null)
    {
        var result = ExecutePromptAndCapture(
            prompt,
            model,
            promptExecutor,
            cancelSignalRegistration,
            checkpointContext);
        return result.WasCancelled;
    }

    private static PromptExecutionResult ExecutePromptAndCapture(
        string prompt,
        string? model,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration,
        PromptExecutionCheckpointContext? checkpointContext = null)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        using var cancelRegistration = cancelSignalRegistration(
            cancellationTokenSource,
            static () => ConsoleLogger.Info(
                "Cancelamento solicitado via Ctrl+C. Interrompendo prompt em execucao."));

        WriteExecutionStateAndCheckpoint(ExecutionState.Connecting, detail: null, checkpointContext);
        WriteExecutionStateAndCheckpoint(
            ExecutionState.ToolCall,
            "Encaminhando prompt para o runtime local.",
            checkpointContext);

        try
        {
            WriteExecutionStateAndCheckpoint(ExecutionState.Processing, detail: null, checkpointContext);
            var streamMetrics = StreamPromptResponseAsync(
                prompt,
                model,
                promptExecutor,
                cancellationTokenSource.Token)
                .GetAwaiter()
                .GetResult();
            WriteExecutionStateAndCheckpoint(
                ExecutionState.Diff,
                BuildDiffStateDetail(streamMetrics),
                checkpointContext);
            WriteExecutionStateAndCheckpoint(
                ExecutionState.Completed,
                $"Resposta finalizada com {streamMetrics.ChunkCount} bloco(s) de streaming.",
                checkpointContext,
                ExecutionCheckpointStatus.Completed);
            return new PromptExecutionResult(
                WasCancelled: false,
                StreamMetrics: streamMetrics);
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
            WriteExecutionStateAndCheckpoint(
                ExecutionState.Error,
                "Execucao cancelada pelo usuario.",
                checkpointContext,
                ExecutionCheckpointStatus.Cancelled);
            return new PromptExecutionResult(
                WasCancelled: true,
                StreamMetrics: PromptStreamMetrics.Empty);
        }
        catch (Exception ex)
        {
            WriteExecutionStateAndCheckpoint(
                ExecutionState.Error,
                $"Nao foi possivel executar o prompt: {ex.Message}",
                checkpointContext,
                ExecutionCheckpointStatus.Failed);
            throw;
        }
    }

    private static int ExecuteDoctor(
        Func<CancellationToken, Task<OllamaHealthcheckResult>> healthcheckExecutor)
    {
        ConsoleLogger.Info("Executando diagnostico de conectividade com Ollama.");
        WriteExecutionState(ExecutionState.Connecting);
        WriteExecutionState(ExecutionState.Processing);

        var healthcheck = healthcheckExecutor(CancellationToken.None).GetAwaiter().GetResult();

        if (healthcheck.IsHealthy)
        {
            WriteExecutionState(
                ExecutionState.Completed,
                $"Ollama disponivel. Versao: {healthcheck.Version}.");
            return (int)CliExitCode.Success;
        }

        WriteExecutionState(
            ExecutionState.Error,
            $"Ollama indisponivel. {healthcheck.Error}");
        var error = CliFriendlyError.Runtime(
            detail: "Nao foi possivel validar a disponibilidade do Ollama.",
            suggestion: "Verifique se o servico Ollama esta em execucao e tente novamente.");
        WriteFriendlyError(error);
        return (int)CliExitCode.RuntimeError;
    }

    private static int ExecuteModels(
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> modelsExecutor)
    {
        ConsoleLogger.Info("Listando modelos locais do Ollama.");
        WriteExecutionState(ExecutionState.Connecting);
        WriteExecutionState(ExecutionState.Processing);

        var models = modelsExecutor(CancellationToken.None).GetAwaiter().GetResult();
        if (models.Count == 0)
        {
            WriteExecutionState(ExecutionState.Completed, "Nenhum modelo local encontrado.");
            return (int)CliExitCode.Success;
        }

        WriteExecutionState(
            ExecutionState.Completed,
            $"{models.Count} modelo(s) local(is) encontrado(s).");

        foreach (var model in models.OrderBy(static model => model.Name, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"- {model.Name}");
        }

        return (int)CliExitCode.Success;
    }

    private static int ExecuteContext()
    {
        ConsoleLogger.Info("Inspecionando contexto do workspace atual.");
        WriteExecutionState(ExecutionState.Processing);

        WorkspaceRootResolution workspaceRoot;
        WorkspaceContextFileIndex workspaceIndex;

        try
        {
            workspaceRoot = WorkspaceRootDetector.Resolve();
            workspaceIndex = WorkspaceContextFileIndexCatalog.GetOrCreate(workspaceRoot.DirectoryPath);
            _ = workspaceIndex.Refresh();
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or DirectoryNotFoundException
            or IOException
            or UnauthorizedAccessException)
        {
            var error = CliFriendlyError.Runtime(
                detail: $"Nao foi possivel inspecionar o contexto do workspace atual. {ex.Message}",
                suggestion: "Verifique se o diretorio atual e acessivel e tente novamente.");
            WriteFriendlyError(error);
            return (int)error.ExitCode;
        }

        var workspaceMap = workspaceIndex.CurrentMap;
        var directoryEntries = workspaceMap.Entries.Count(static entry => entry.Kind == WorkspaceEntryKind.Directory);
        var fileEntries = workspaceMap.Entries.Count(static entry => entry.Kind == WorkspaceEntryKind.File);

        WriteExecutionState(
            ExecutionState.Completed,
            "Resumo do workspace atual gerado.");
        Console.WriteLine($"- raiz-workspace: {workspaceRoot.DirectoryPath}");
        Console.WriteLine($"- tipo-raiz: {GetWorkspaceRootKindLabel(workspaceRoot.Kind)}");
        Console.WriteLine($"- versao-indice: {workspaceIndex.Version}");
        Console.WriteLine($"- ultima-indexacao-utc: {workspaceIndex.LastIndexedAtUtc:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"- entradas-indexadas: {workspaceIndex.EntryCount}");
        Console.WriteLine($"- diretorios-mapeados: {directoryEntries}");
        Console.WriteLine($"- arquivos-mapeados: {fileEntries}");
        Console.WriteLine($"- diretorios-visitados: {workspaceMap.VisitedDirectoryCount}");
        Console.WriteLine($"- arquivos-visitados: {workspaceMap.VisitedFileCount}");
        Console.WriteLine($"- entradas-ignoradas: {workspaceMap.IgnoredEntryCount}");
        Console.WriteLine($"- limite-aplicado: {GetWorkspaceMapLimitLabel(workspaceMap.LimitKind)}");
        Console.WriteLine($"- truncado: {(workspaceMap.IsTruncated ? "sim" : "nao")}");

        return (int)CliExitCode.Success;
    }

    private static int ExecutePatch(
        string patchRequestFilePath,
        bool dryRun,
        Func<WorkspacePatchAuditEntry, string> workspacePatchAuditAppender)
    {
        ArgumentNullException.ThrowIfNull(workspacePatchAuditAppender);

        ConsoleLogger.Info("Aplicando patch de workspace.");
        WriteExecutionState(ExecutionState.Processing);

        WorkspaceRootResolution workspaceRoot;
        WorkspacePatchResult patchResult;
        var resolvedPatchRequestFilePath = string.Empty;

        try
        {
            workspaceRoot = WorkspaceRootDetector.Resolve();
            resolvedPatchRequestFilePath = ResolvePatchRequestFilePath(patchRequestFilePath);
            var patchRequest = LoadWorkspacePatchRequest(resolvedPatchRequestFilePath, dryRun);
            var permissionPolicy = WorkspacePermissionPolicyFile.Load(workspaceRoot.DirectoryPath);
            var patchEngine = new WorkspacePatchEngine(
                workspaceRoot.DirectoryPath,
                permissionPolicy);

            if (patchRequest.PreviewOnly)
            {
                patchResult = patchEngine.Apply(patchRequest);
            }
            else
            {
                var previewResult = patchEngine.Apply(patchRequest with { PreviewOnly = true });
                if (!ConfirmDestructivePatchChangesIfRequired(previewResult))
                {
                    WriteExecutionState(
                        ExecutionState.Error,
                        "Patch cancelado pelo usuario para evitar operacoes destrutivas.");
                    return (int)CliExitCode.Cancelled;
                }

                patchResult = patchEngine.Apply(patchRequest);
            }
        }
        catch (Exception ex) when (ex is ArgumentException
            or InvalidOperationException
            or DirectoryNotFoundException
            or FileNotFoundException
            or IOException
            or JsonException
            or UnauthorizedAccessException)
        {
            var error = CliFriendlyError.Runtime(
                detail: $"Nao foi possivel aplicar o patch informado. {ex.Message}",
                suggestion: $"Revise o arquivo JSON e tente novamente. Exemplo: {CliName} patch --dry-run patch.json.");
            WriteFriendlyError(error);
            return (int)error.ExitCode;
        }

        WriteExecutionState(ExecutionState.Diff, BuildPatchDiffStateDetail(patchResult));

        if (string.IsNullOrWhiteSpace(patchResult.UnifiedDiff))
        {
            Console.WriteLine("# Nenhuma diferenca para exibir.");
        }
        else
        {
            Console.WriteLine(patchResult.UnifiedDiff);
        }

        WriteExecutionState(ExecutionState.Completed, BuildPatchCompletedStateDetail(patchResult));
        Console.WriteLine($"- raiz-workspace: {workspaceRoot.DirectoryPath}");
        Console.WriteLine($"- modo-dry-run: {(patchResult.IsPreviewOnly ? "sim" : "nao")}");
        Console.WriteLine($"- alteracoes-planejadas: {patchResult.PlannedChangeCount}");
        Console.WriteLine($"- alteracoes-aplicadas: {patchResult.AppliedChangeCount}");
        Console.WriteLine($"- alteracoes-ignoradas: {patchResult.SkippedChangeCount}");

        var auditEntry = BuildWorkspacePatchAuditEntry(
            workspaceRoot.DirectoryPath,
            resolvedPatchRequestFilePath,
            patchResult);
        var auditPath = TryAppendWorkspacePatchAuditEntry(auditEntry, workspacePatchAuditAppender);

        Console.WriteLine($"- sessao-auditoria: {auditEntry.SessionId}");
        Console.WriteLine($"- sequencia-sessao: {auditEntry.SessionSequence}");

        if (!string.IsNullOrWhiteSpace(auditPath))
        {
            Console.WriteLine($"- arquivo-auditoria: {auditPath}");
        }

        return (int)CliExitCode.Success;
    }

    private static bool ConfirmDestructivePatchChangesIfRequired(WorkspacePatchResult previewResult)
    {
        var destructiveChanges = previewResult.Files
            .Where(static fileResult => fileResult.HasChanges && fileResult.Kind == WorkspacePatchChangeKind.Delete)
            .ToArray();

        if (destructiveChanges.Length == 0)
        {
            return true;
        }

        Console.WriteLine("ATENCAO: O patch contem operacoes destrutivas.");
        foreach (var destructiveChange in destructiveChanges)
        {
            Console.WriteLine($"- delete: {destructiveChange.Path}");
        }

        while (true)
        {
            Console.Write("Confirme com 'sim' para aplicar as alteracoes destrutivas [sim/nao]: ");
            var confirmationInput = Console.ReadLine();

            if (IsAffirmativeConfirmation(confirmationInput))
            {
                return true;
            }

            if (confirmationInput is null || IsNegativeConfirmation(confirmationInput))
            {
                return false;
            }

            Console.WriteLine("Resposta invalida. Digite 'sim' para confirmar ou 'nao' para cancelar.");
        }
    }

    private static bool IsAffirmativeConfirmation(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalizedValue = value.Trim();
        return string.Equals(normalizedValue, "sim", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedValue, "s", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedValue, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedValue, "y", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNegativeConfirmation(string value)
    {
        var normalizedValue = value.Trim();
        return string.Equals(normalizedValue, "nao", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedValue, "n", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedValue, "no", StringComparison.OrdinalIgnoreCase);
    }

    private static WorkspacePatchRequest LoadWorkspacePatchRequest(string patchRequestFilePath, bool dryRun)
    {
        if (!File.Exists(patchRequestFilePath))
        {
            throw new FileNotFoundException(
                $"O arquivo de patch '{patchRequestFilePath}' nao foi encontrado.",
                patchRequestFilePath);
        }

        var patchJson = File.ReadAllText(patchRequestFilePath);
        WorkspacePatchRequestDocument? patchRequestDocument;

        try
        {
            patchRequestDocument = JsonSerializer.Deserialize<WorkspacePatchRequestDocument>(
                patchJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException ex)
        {
            throw new JsonException(
                $"O arquivo de patch '{patchRequestFilePath}' nao contem JSON valido.",
                ex);
        }

        var changes = BuildWorkspacePatchChanges(patchRequestDocument?.Changes);
        return new WorkspacePatchRequest(
            Changes: changes,
            PreviewOnly: dryRun);
    }

    private static IReadOnlyList<WorkspacePatchChange> BuildWorkspacePatchChanges(
        IReadOnlyList<WorkspacePatchChangeDocument>? changeDocuments)
    {
        if (changeDocuments is null || changeDocuments.Count == 0)
        {
            throw new InvalidOperationException(
                "O arquivo de patch deve conter ao menos uma mudanca em 'changes'.");
        }

        var changes = new WorkspacePatchChange[changeDocuments.Count];

        for (var index = 0; index < changeDocuments.Count; index++)
        {
            var changeDocument = changeDocuments[index]
                ?? throw new InvalidOperationException(
                    $"A mudanca na posicao {index + 1} do patch e invalida.");

            if (!TryParseWorkspacePatchChangeKind(changeDocument.Kind, out var changeKind))
            {
                throw new InvalidOperationException(
                    $"A mudanca na posicao {index + 1} possui tipo invalido '{changeDocument.Kind}'. Use 'create', 'edit' ou 'delete'.");
            }

            var changePath = changeDocument.Path?.Trim();
            if (string.IsNullOrWhiteSpace(changePath))
            {
                throw new InvalidOperationException(
                    $"A mudanca na posicao {index + 1} precisa informar o campo 'path'.");
            }

            changes[index] = new WorkspacePatchChange(
                Kind: changeKind,
                Path: changePath,
                Content: changeDocument.Content,
                ExpectedContent: changeDocument.ExpectedContent);
        }

        return changes;
    }

    private static bool TryParseWorkspacePatchChangeKind(string? rawKind, out WorkspacePatchChangeKind changeKind)
    {
        if (string.Equals(rawKind, "create", StringComparison.OrdinalIgnoreCase))
        {
            changeKind = WorkspacePatchChangeKind.Create;
            return true;
        }

        if (string.Equals(rawKind, "edit", StringComparison.OrdinalIgnoreCase))
        {
            changeKind = WorkspacePatchChangeKind.Edit;
            return true;
        }

        if (string.Equals(rawKind, "delete", StringComparison.OrdinalIgnoreCase))
        {
            changeKind = WorkspacePatchChangeKind.Delete;
            return true;
        }

        changeKind = default;
        return false;
    }

    private static string ResolvePatchRequestFilePath(string patchRequestFilePath)
    {
        if (string.IsNullOrWhiteSpace(patchRequestFilePath))
        {
            throw new ArgumentException(
                "O caminho do arquivo de patch e obrigatorio.",
                nameof(patchRequestFilePath));
        }

        var trimmedPath = patchRequestFilePath.Trim();
        var candidatePath = Path.IsPathRooted(trimmedPath)
            ? trimmedPath
            : Path.Combine(Directory.GetCurrentDirectory(), trimmedPath);
        return Path.GetFullPath(candidatePath);
    }

    private static string BuildPatchDiffStateDetail(WorkspacePatchResult patchResult)
    {
        if (!patchResult.HasChanges)
        {
            return "Nenhuma diferenca detectada para o patch informado.";
        }

        return patchResult.IsPreviewOnly
            ? "Modo --dry-run habilitado. Diff gerado sem alterar arquivos."
            : "Diff gerado para alteracoes aplicadas no workspace.";
    }

    private static string BuildPatchCompletedStateDetail(WorkspacePatchResult patchResult)
    {
        if (!patchResult.HasChanges)
        {
            return "Patch processado sem alteracoes efetivas.";
        }

        return patchResult.IsPreviewOnly
            ? $"Patch simulado com {patchResult.PlannedChangeCount} alteracao(oes)."
            : $"{patchResult.AppliedChangeCount} alteracao(oes) aplicada(s) no workspace.";
    }

    private static WorkspacePatchAuditEntry BuildWorkspacePatchAuditEntry(
        string workspaceRootDirectoryPath,
        string patchRequestFilePath,
        WorkspacePatchResult patchResult)
    {
        return WorkspacePatchAuditEntry.FromPatchResult(
            sessionId: CurrentExecutionSessionId,
            sessionSequence: Interlocked.Increment(ref _workspacePatchAuditSequence),
            workspaceRootDirectory: workspaceRootDirectoryPath,
            patchRequestFilePath: patchRequestFilePath,
            patchResult: patchResult);
    }

    private static string? TryAppendWorkspacePatchAuditEntry(
        WorkspacePatchAuditEntry auditEntry,
        Func<WorkspacePatchAuditEntry, string> workspacePatchAuditAppender)
    {
        ArgumentNullException.ThrowIfNull(workspacePatchAuditAppender);

        try
        {
            var auditPath = workspacePatchAuditAppender(auditEntry);
            return string.IsNullOrWhiteSpace(auditPath)
                ? null
                : auditPath.Trim();
        }
        catch (Exception ex) when (ex is ArgumentException
            or InvalidOperationException
            or DirectoryNotFoundException
            or IOException
            or JsonException
            or UnauthorizedAccessException)
        {
            ConsoleLogger.Error(
                $"Nao foi possivel registrar a trilha de auditoria local do patch. {ex.Message}");
            return null;
        }
    }

    private static string GetWorkspaceRootKindLabel(WorkspaceRootKind rootKind)
    {
        return rootKind switch
        {
            WorkspaceRootKind.Monorepo => "monorepo",
            WorkspaceRootKind.SolutionOrWorkspace => "solution/workspace",
            WorkspaceRootKind.Git => "git",
            WorkspaceRootKind.CurrentDirectory => "diretorio-atual",
            _ => "desconhecido"
        };
    }

    private static string GetWorkspaceMapLimitLabel(WorkspaceMapLimitKind limitKind)
    {
        return limitKind switch
        {
            WorkspaceMapLimitKind.None => "none",
            WorkspaceMapLimitKind.MaxEntries => "max-entries",
            WorkspaceMapLimitKind.MaxDepth => "max-depth",
            _ => "desconhecido"
        };
    }

    private static int ExecuteHistory(
        Func<IReadOnlyList<PromptHistoryEntry>> historyLoader,
        Action historyClearer,
        bool clearHistory)
    {
        if (clearHistory)
        {
            ConsoleLogger.Info("Limpando historico local de prompts.");
            historyClearer();
            WriteExecutionState(ExecutionState.Completed, "Historico local limpo com sucesso.");
            return (int)CliExitCode.Success;
        }

        ConsoleLogger.Info("Lendo historico local de prompts.");
        var historyEntries = historyLoader();

        if (historyEntries.Count == 0)
        {
            WriteExecutionState(ExecutionState.Completed, "Nenhum item de historico encontrado.");
            return (int)CliExitCode.Success;
        }

        WriteExecutionState(
            ExecutionState.Completed,
            $"{historyEntries.Count} item(ns) de historico encontrado(s).");

        foreach (var entry in historyEntries.OrderByDescending(static item => item.TimestampUtc))
        {
            var modelLabel = string.IsNullOrWhiteSpace(entry.Model) ? "<padrao>" : entry.Model;
            Console.WriteLine($"- [{entry.TimestampUtc:yyyy-MM-dd HH:mm:ss} UTC] comando={entry.Command} modelo={modelLabel}");
            Console.WriteLine($"  prompt: {entry.Prompt}");
            Console.WriteLine($"  resposta: {entry.Response}");
        }

        return (int)CliExitCode.Success;
    }

    private static int ExecuteMcpList(Func<IReadOnlyList<McpServerDefinition>> mcpServersLoader)
    {
        ConsoleLogger.Info("Listando servidores MCP configurados.");
        var servers = mcpServersLoader();
        if (servers.Count == 0)
        {
            WriteExecutionState(ExecutionState.Completed, "Nenhum servidor MCP configurado.");
            return (int)CliExitCode.Success;
        }

        WriteExecutionState(
            ExecutionState.Completed,
            $"{servers.Count} servidor(es) MCP configurado(s).");

        foreach (var server in servers.OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"- {server.Name} ({GetMcpServerTransportLabel(server)})");

            if (server.ProcessOptions is McpServerProcessOptions processOptions)
            {
                Console.WriteLine($"  command: {processOptions.Command}");
                if (processOptions.Arguments.Count > 0)
                {
                    Console.WriteLine($"  args: {string.Join(' ', processOptions.Arguments)}");
                }

                if (!string.IsNullOrWhiteSpace(processOptions.WorkingDirectory))
                {
                    Console.WriteLine($"  cwd: {processOptions.WorkingDirectory}");
                }

                if (processOptions.EnvironmentVariables.Count > 0)
                {
                    var environmentKeys = processOptions.EnvironmentVariables.Keys
                        .OrderBy(static key => key, StringComparer.Ordinal)
                        .ToArray();
                    Console.WriteLine($"  env: {string.Join(", ", environmentKeys)}");
                }

                continue;
            }

            if (server.RemoteOptions is not McpServerRemoteOptions remoteOptions)
            {
                continue;
            }

            Console.WriteLine($"  endpoint: {remoteOptions.Endpoint.AbsoluteUri}");
            if (remoteOptions.MessageEndpoint is Uri messageEndpoint)
            {
                Console.WriteLine($"  message-endpoint: {messageEndpoint.AbsoluteUri}");
            }

            var authenticationLabel = ResolveMcpAuthenticationLabel(remoteOptions.Authentication);
            Console.WriteLine($"  auth: {authenticationLabel}");

            if (remoteOptions.Headers.Count > 0)
            {
                var headerNames = remoteOptions.Headers.Keys
                    .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                Console.WriteLine($"  headers: {string.Join(", ", headerNames)}");
            }
        }

        return (int)CliExitCode.Success;
    }

    private static int ExecuteMcpAdd(
        McpServerDefinition serverToAdd,
        Func<IReadOnlyList<McpServerDefinition>> mcpServersLoader,
        Action<IReadOnlyList<McpServerDefinition>> mcpServersSaver)
    {
        ConsoleLogger.Info($"Adicionando servidor MCP '{serverToAdd.Name}'.");
        var existingServers = mcpServersLoader();
        if (existingServers.Any(server =>
                string.Equals(server.Name, serverToAdd.Name, StringComparison.OrdinalIgnoreCase)))
        {
            var duplicateError = CliFriendlyError.InvalidArguments(
                detail: $"Ja existe um servidor MCP com o nome '{serverToAdd.Name}'.",
                suggestion: $"Use '{CliName} mcp remove {serverToAdd.Name}' antes de adicionar novamente.");
            WriteFriendlyError(duplicateError);
            return (int)duplicateError.ExitCode;
        }

        var updatedServers = existingServers
            .Append(serverToAdd)
            .ToArray();
        mcpServersSaver(updatedServers);
        WriteExecutionState(
            ExecutionState.Completed,
            $"Servidor MCP '{serverToAdd.Name}' adicionado com sucesso.");
        return (int)CliExitCode.Success;
    }

    private static int ExecuteMcpRemove(
        string serverName,
        Func<IReadOnlyList<McpServerDefinition>> mcpServersLoader,
        Action<IReadOnlyList<McpServerDefinition>> mcpServersSaver)
    {
        ConsoleLogger.Info($"Removendo servidor MCP '{serverName}'.");
        var existingServers = mcpServersLoader();
        var updatedServers = existingServers
            .Where(server => !string.Equals(server.Name, serverName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (updatedServers.Length == existingServers.Count)
        {
            var notFoundError = CliFriendlyError.InvalidArguments(
                detail: $"O servidor MCP '{serverName}' nao foi encontrado.",
                suggestion: $"Use '{CliName} mcp list' para listar servidores configurados.");
            WriteFriendlyError(notFoundError);
            return (int)notFoundError.ExitCode;
        }

        mcpServersSaver(updatedServers);
        WriteExecutionState(
            ExecutionState.Completed,
            $"Servidor MCP '{serverName}' removido com sucesso.");
        return (int)CliExitCode.Success;
    }

    private static int ExecuteMcpTest(
        string serverName,
        Func<IReadOnlyList<McpServerDefinition>> mcpServersLoader,
        Func<McpServerDefinition, CancellationToken, Task<McpServerTestResult>> mcpServerTester)
    {
        ConsoleLogger.Info($"Testando servidor MCP '{serverName}'.");
        var servers = mcpServersLoader();
        var selectedServer = servers.FirstOrDefault(server =>
            string.Equals(server.Name, serverName, StringComparison.OrdinalIgnoreCase));
        if (selectedServer is null)
        {
            var notFoundError = CliFriendlyError.InvalidArguments(
                detail: $"O servidor MCP '{serverName}' nao foi encontrado.",
                suggestion: $"Use '{CliName} mcp list' para listar servidores configurados.");
            WriteFriendlyError(notFoundError);
            return (int)notFoundError.ExitCode;
        }

        try
        {
            WriteExecutionState(ExecutionState.Connecting);
            WriteExecutionState(ExecutionState.Processing);
            var testResult = mcpServerTester(selectedServer, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            if (testResult.IsSuccess)
            {
                WriteExecutionState(
                    ExecutionState.Completed,
                    testResult.Detail);
                return (int)CliExitCode.Success;
            }

            WriteExecutionState(
                ExecutionState.Error,
                testResult.Detail);
            var runtimeError = CliFriendlyError.Runtime(
                detail: $"Nao foi possivel validar o servidor MCP '{serverName}'.",
                suggestion: "Revise os parametros do servidor MCP e tente novamente.");
            WriteFriendlyError(runtimeError);
            return (int)runtimeError.ExitCode;
        }
        catch (Exception ex)
        {
            WriteExecutionState(
                ExecutionState.Error,
                $"Falha ao testar servidor MCP '{serverName}'. {ex.Message}");
            var runtimeError = CliFriendlyError.Runtime(
                detail: $"Nao foi possivel testar o servidor MCP '{serverName}'.",
                suggestion: "Revise os parametros do servidor MCP e tente novamente.");
            WriteFriendlyError(runtimeError);
            return (int)runtimeError.ExitCode;
        }
    }

    private static string GetMcpServerTransportLabel(McpServerDefinition server)
    {
        if (server.ProcessOptions is not null)
        {
            return "stdio";
        }

        return server.RemoteOptions?.TransportKind switch
        {
            McpRemoteTransportKind.Http => "http",
            McpRemoteTransportKind.Sse => "sse",
            _ => "desconhecido"
        };
    }

    private static string ResolveMcpAuthenticationLabel(McpAuthenticationOptions authentication)
    {
        if (!string.IsNullOrWhiteSpace(authentication.AuthorizationScheme))
        {
            return authentication.AuthorizationScheme.Trim().ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(authentication.CustomHeaderName))
        {
            return $"header:{authentication.CustomHeaderName.Trim()}";
        }

        return "none";
    }

    private static int ExecuteConfigGet(
        string configKey,
        Func<UserRuntimeConfig> configLoader)
    {
        ConsoleLogger.Info($"Lendo configuracao '{configKey}'.");
        var config = configLoader();
        var value = UserConfigFile.GetValue(config, configKey);
        Console.WriteLine($"{configKey}={value}");
        return (int)CliExitCode.Success;
    }

    private static int ExecuteConfigSet(
        string configKey,
        string configValue,
        Func<UserRuntimeConfig> configLoader,
        Action<UserRuntimeConfig> configSaver)
    {
        ConsoleLogger.Info($"Atualizando configuracao '{configKey}'.");
        var currentConfig = configLoader();

        UserRuntimeConfig updatedConfig;
        try
        {
            updatedConfig = UserConfigFile.SetValue(currentConfig, configKey, configValue);
        }
        catch (InvalidOperationException ex)
        {
            var error = CliFriendlyError.InvalidArguments(
                detail: ex.Message,
                suggestion: BuildConfigSetSuggestion(configKey));
            WriteFriendlyError(error);
            return (int)error.ExitCode;
        }

        configSaver(updatedConfig);
        if (string.Equals(configKey, UserConfigFile.ThemeKey, StringComparison.Ordinal))
        {
            ConsoleLogger.ConfigureTheme(updatedConfig.Theme);
        }

        var updatedValue = UserConfigFile.GetValue(updatedConfig, configKey);
        ConsoleLogger.Info($"Configuracao atualizada: {configKey}={updatedValue}.");
        return (int)CliExitCode.Success;
    }

    private static int ExecuteSkills()
    {
        ConsoleLogger.Info("Listando skills disponiveis.");
        var skills = SkillCatalog.List()
            .OrderBy(static skill => skill.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (skills.Length == 0)
        {
            WriteExecutionState(ExecutionState.Completed, "Nenhuma skill disponivel.");
            return (int)CliExitCode.Success;
        }

        WriteExecutionState(
            ExecutionState.Completed,
            $"{skills.Length} skill(s) disponivel(is).");

        foreach (var skill in skills)
        {
            Console.WriteLine($"- {skill.Name}: {skill.Description}");
        }

        return (int)CliExitCode.Success;
    }

    private static int ExecuteSkillsReload()
    {
        ConsoleLogger.Info("Recarregando cache de skills.");
        SkillCatalog.ReloadCache();
        WriteExecutionState(
            ExecutionState.Completed,
            "Cache de skills recarregado.");
        return (int)CliExitCode.Success;
    }

    private static int ExecuteSkillsInit()
    {
        ConsoleLogger.Info("Criando template de skill no diretorio atual.");
        var resolvedTemplatePath = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), SkillFileFormat.SkillFileName));

        if (File.Exists(resolvedTemplatePath))
        {
            var alreadyExistsError = CliFriendlyError.InvalidArguments(
                detail: $"O arquivo '{SkillFileFormat.SkillFileName}' ja existe no diretorio atual.",
                suggestion: $"Remova ou renomeie '{resolvedTemplatePath}' e execute '{CliName} skills init' novamente.");
            WriteFriendlyError(alreadyExistsError);
            return (int)alreadyExistsError.ExitCode;
        }

        try
        {
            var templateContent = SkillFileFormat.BuildTemplate();
            File.WriteAllText(resolvedTemplatePath, templateContent);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var runtimeError = CliFriendlyError.Runtime(
                detail: $"Nao foi possivel criar o arquivo de skill em '{resolvedTemplatePath}'. {ex.Message}",
                suggestion: "Verifique permissoes de escrita no diretorio atual e tente novamente.");
            WriteFriendlyError(runtimeError);
            return (int)runtimeError.ExitCode;
        }

        WriteExecutionState(
            ExecutionState.Completed,
            $"Template de skill criado em '{resolvedTemplatePath}'.");
        return (int)CliExitCode.Success;
    }

    private static int ExecuteShowSkill(string skillName)
    {
        if (!SkillCatalog.TryFind(skillName, out var skill))
        {
            var error = CliFriendlyError.InvalidArguments(
                detail: $"A skill '{skillName}' nao foi encontrada.",
                suggestion: $"Use '{CliName} skills' para listar as skills disponiveis.");
            WriteFriendlyError(error);
            return (int)error.ExitCode;
        }

        ConsoleLogger.Info($"Exibindo detalhes da skill '{skill.Name}'.");
        Console.WriteLine($"Skill: {skill.Name}");
        Console.WriteLine($"Descricao: {skill.Description}");
        Console.WriteLine("Instrucoes:");
        Console.WriteLine(skill.Instruction);
        return (int)CliExitCode.Success;
    }

    private static int ExecuteSkill(
        string skillName,
        string prompt,
        string? model,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration,
        Action<ExecutionSessionCheckpoint> executionCheckpointAppender)
    {
        ArgumentNullException.ThrowIfNull(executionCheckpointAppender);

        if (!SkillCatalog.TryFind(skillName, out var skill))
        {
            var error = CliFriendlyError.InvalidArguments(
                detail: $"A skill '{skillName}' nao foi encontrada.",
                suggestion: $"Use '{CliName} skills' para listar as skills disponiveis.");
            WriteFriendlyError(error);
            return (int)error.ExitCode;
        }

        ConsoleLogger.Info($"Executando skill '{skill.Name}'.");
        var promptWithSkill = BuildSkillPrompt(skill, prompt);
        var checkpointContext = CreatePromptCheckpointContext(
            command: "skill",
            prompt: prompt,
            model: model,
            skillName: skill.Name,
            executionCheckpointAppender: executionCheckpointAppender);
        var wasCancelled = ExecutePrompt(
            promptWithSkill,
            model,
            promptExecutor,
            cancelSignalRegistration,
            checkpointContext);
        return wasCancelled
            ? (int)CliExitCode.Cancelled
            : (int)CliExitCode.Success;
    }

    private static string BuildConfigSetSuggestion(string configKey)
    {
        return configKey switch
        {
            UserConfigFile.OllamaHostKey =>
                $"Exemplo: {CliName} config set {UserConfigFile.OllamaHostKey} http://127.0.0.1:11434/.",
            UserConfigFile.DefaultModelKey =>
                $"Exemplo: {CliName} config set {UserConfigFile.DefaultModelKey} {OllamaModelDefaults.DefaultModel}.",
            UserConfigFile.PromptTimeoutSecondsKey =>
                $"Exemplo: {CliName} config set {UserConfigFile.PromptTimeoutSecondsKey} 30.",
            UserConfigFile.HealthcheckTimeoutSecondsKey =>
                $"Exemplo: {CliName} config set {UserConfigFile.HealthcheckTimeoutSecondsKey} 3.",
            UserConfigFile.ModelsTimeoutSecondsKey =>
                $"Exemplo: {CliName} config set {UserConfigFile.ModelsTimeoutSecondsKey} 5.",
            UserConfigFile.ThemeKey =>
                $"Exemplo: {CliName} config set {UserConfigFile.ThemeKey} auto.",
            _ => $"Chaves suportadas: {GetSupportedConfigKeysLabel()}."
        };
    }

    private static string GetSupportedConfigKeysLabel()
    {
        return string.Join(", ", UserConfigFile.SupportedKeys);
    }

    private static void WriteFriendlyError(CliFriendlyError error)
    {
        ConsoleLogger.Error(error.BuildPrimaryMessage());
        ConsoleLogger.Error(error.BuildSuggestionMessage());
    }

    private static void TryConfigureTerminalTheme(Func<UserRuntimeConfig> configLoader)
    {
        ArgumentNullException.ThrowIfNull(configLoader);

        try
        {
            var userConfig = configLoader();
            ConsoleLogger.ConfigureTheme(userConfig.Theme);
        }
        catch
        {
            ConsoleLogger.ConfigureTheme(UserRuntimeConfig.Default.Theme);
        }
    }

    private static bool IsExitCommand(string input)
    {
        return string.Equals(input.Trim(), "exit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.Trim(), "quit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.Trim(), "sair", StringComparison.OrdinalIgnoreCase);
    }

    private enum InteractiveChatCommandResult
    {
        NotACommand,
        Handled,
        Exit
    }

    private static string BuildSkillPrompt(SkillDefinition skill, string prompt)
    {
        return
            $"""
            [SKILL: {skill.Name}]
            {skill.Instruction}

            [TAREFA]
            {prompt}
            """;
    }

    private static string BuildAgentPlanPhasePrompt(
        AgentExecutionPlan executionPlan,
        int iteration,
        AgentProjectContextSnapshot projectContext,
        string previousVerificationOutput,
        string previousRefinementOutput)
    {
        var basePrompt = BuildAgentPromptBase(executionPlan, projectContext, iteration, "plan");

        return
            $"""
            {basePrompt}
            [CONTEXTO DA ITERACAO ANTERIOR]
            Ultima verificacao:
            {BuildPromptContextExcerpt(previousVerificationOutput, "Sem verificacao previa.")}

            Ultimo refinamento:
            {BuildPromptContextExcerpt(previousRefinementOutput, "Sem refinamento previo.")}

            [TAREFA DA FASE PLAN]
            Atualize o plano tatico da iteracao atual.
            Defina escopo, riscos e criterios objetivos para validar a entrega.
            Responda de forma direta, com foco em executar no proximo passo.
            """;
    }

    private static string BuildAgentExecutePhasePrompt(
        AgentExecutionPlan executionPlan,
        int iteration,
        AgentProjectContextSnapshot projectContext,
        string latestPlanOutput,
        string previousRefinementOutput)
    {
        var basePrompt = BuildAgentPromptBase(executionPlan, projectContext, iteration, "execute");

        return
            $"""
            {basePrompt}
            [PLANO TATICO ATUAL]
            {BuildPromptContextExcerpt(latestPlanOutput, "Plano tatico nao disponivel.")}

            [AJUSTES DO ULTIMO REFINE]
            {BuildPromptContextExcerpt(previousRefinementOutput, "Sem ajustes de refine para aplicar.")}

            [TAREFA DA FASE EXECUTE]
            Execute o plano definido para esta iteracao.
            Entregue evidencias concretas do que foi feito e do que ficou pendente.
            Quando houver mudanca de codigo, use obrigatoriamente o formato abaixo:

            CODE_CHANGE_STATUS=<changed|no-change>

            Para cada arquivo alterado, repita:
            CHANGE_FILE=<caminho-relativo>
            CHANGE_KIND=<create|edit|delete|move|rename|test|docs|infra|other>
            ```diff
            <diff unificado da mudanca>
            ```
            TECHNICAL_JUSTIFICATION=<justificativa tecnica curta da mudanca>

            Regras:
            - Se CODE_CHANGE_STATUS=changed, cada CHANGE_FILE deve conter diff e TECHNICAL_JUSTIFICATION proprios.
            - Se nao houver alteracao de codigo, declare CODE_CHANGE_STATUS=no-change e explique as evidencias coletadas.
            """;
    }

    private static string BuildAgentVerifyPhasePrompt(
        AgentExecutionPlan executionPlan,
        int iteration,
        AgentProjectContextSnapshot projectContext,
        string latestPlanOutput,
        string latestExecutionOutput,
        AgentCodeChangeEvidence executeEvidence,
        AgentValidationReport validationReport)
    {
        var basePrompt = BuildAgentPromptBase(executionPlan, projectContext, iteration, "verify");

        return
            $"""
            {basePrompt}
            [PLANO A VALIDAR]
            {BuildPromptContextExcerpt(latestPlanOutput, "Plano tatico nao disponivel.")}

            [EVIDENCIAS DE EXECUCAO]
            {BuildPromptContextExcerpt(latestExecutionOutput, "Sem evidencias de execucao disponiveis.")}

            [RASTREABILIDADE DE MUDANCAS DE CODIGO]
            {BuildAgentCodeChangeEvidenceSummary(executeEvidence)}

            [VALIDACAO AUTOMATICA POS-MUDANCA]
            {BuildAgentValidationReportSummary(validationReport)}

            [TAREFA DA FASE VERIFY]
            Verifique se o objetivo ja pode ser considerado concluido.
            Se houver alteracao de codigo sem diff ou sem justificativa tecnica por mudanca, marque refine.
            Se a validacao automatica pos-mudanca falhou, marque refine e use stdout/stderr para orientar a correcao.
            Se ainda houver lacunas, descreva o que precisa ser refinado.
            Comece obrigatoriamente com:
            VERIFICATION_STATUS=<done|refine>
            Depois explique a justificativa em topicos curtos.
            """;
    }

    private static string BuildAgentAutoCorrectionPrompt(
        AgentExecutionPlan executionPlan,
        int iteration,
        AgentProjectContextSnapshot projectContext,
        string latestPlanOutput,
        string latestExecutionOutput,
        AgentValidationReport validationReport,
        int attempt,
        int maxAttempts)
    {
        var basePrompt = BuildAgentPromptBase(executionPlan, projectContext, iteration, "auto-correct");

        return
            $"""
            {basePrompt}
            [PLANO TATICO]
            {BuildPromptContextExcerpt(latestPlanOutput, "Plano tatico nao disponivel.")}

            [EXECUCAO QUE GEROU FALHA]
            {BuildPromptContextExcerpt(latestExecutionOutput, "Sem contexto de execucao anterior.")}

            [VALIDACAO AUTOMATICA COM FALHA]
            {BuildAgentValidationReportSummary(validationReport)}

            [TENTATIVA DE AUTO-CORRECAO]
            Tentativa: {attempt}/{maxAttempts}

            [TAREFA DA FASE AUTO-CORRECAO]
            Corrija somente as causas objetivas indicadas por stdout/stderr da validacao.
            Preserve o escopo do objetivo e evite refatoracoes sem relacao com a falha.
            Apos corrigir, declare obrigatoriamente o resultado no formato abaixo:

            CODE_CHANGE_STATUS=<changed|no-change>

            Para cada arquivo alterado, repita:
            CHANGE_FILE=<caminho-relativo>
            CHANGE_KIND=<create|edit|delete|move|rename|test|docs|infra|other>
            ```diff
            <diff unificado da mudanca>
            ```
            TECHNICAL_JUSTIFICATION=<por que esta mudanca corrige a falha>

            Regras:
            - Se CODE_CHANGE_STATUS=changed, cada CHANGE_FILE deve conter diff e TECHNICAL_JUSTIFICATION proprios.
            - Se nao houver alteracao possivel nesta tentativa, declare CODE_CHANGE_STATUS=no-change e explique o bloqueio tecnico.
            """;
    }

    private static string BuildAgentRefinePhasePrompt(
        AgentExecutionPlan executionPlan,
        int iteration,
        AgentProjectContextSnapshot projectContext,
        string latestPlanOutput,
        string latestExecutionOutput,
        string latestVerificationOutput)
    {
        var basePrompt = BuildAgentPromptBase(executionPlan, projectContext, iteration, "refine");

        return
            $"""
            {basePrompt}
            [PLANO TATICO]
            {BuildPromptContextExcerpt(latestPlanOutput, "Plano tatico nao disponivel.")}

            [EXECUCAO ANTERIOR]
            {BuildPromptContextExcerpt(latestExecutionOutput, "Sem contexto de execucao anterior.")}

            [FEEDBACK DA VERIFICACAO]
            {BuildPromptContextExcerpt(latestVerificationOutput, "Sem feedback de verificacao.")}

            [TAREFA DA FASE REFINE]
            Corrija as lacunas apontadas na verificacao.
            Replaneje somente o necessario para destravar a proxima iteracao.
            Produza instrucoes objetivas para a proxima fase plan/execute.
            """;
    }

    private static AgentVerificationDecision ParseAgentVerificationDecision(string verificationOutput)
    {
        if (TryParseAgentVerificationStatus(verificationOutput, out var status))
        {
            return string.Equals(status, AgentVerificationStatusDone, StringComparison.OrdinalIgnoreCase)
                ? AgentVerificationDecision.Concluded(status)
                : AgentVerificationDecision.NeedsRefine(status);
        }

        if (verificationOutput.Contains("nao concluido", StringComparison.OrdinalIgnoreCase)
            || verificationOutput.Contains("not concluded", StringComparison.OrdinalIgnoreCase))
        {
            return AgentVerificationDecision.NeedsRefine("heuristic");
        }

        if (verificationOutput.Contains("concluido", StringComparison.OrdinalIgnoreCase)
            || verificationOutput.Contains("objective completed", StringComparison.OrdinalIgnoreCase))
        {
            return AgentVerificationDecision.Concluded("heuristic");
        }

        ConsoleLogger.Info(
            "Nao foi possivel identificar status explicito de verify. Assumindo conclusao.");
        return AgentVerificationDecision.Concluded("assumed");
    }

    private static bool TryParseAgentVerificationStatus(string verificationOutput, out string status)
    {
        status = string.Empty;
        if (string.IsNullOrWhiteSpace(verificationOutput))
        {
            return false;
        }

        const string statusPrefix = "VERIFICATION_STATUS=";
        var lines = verificationOutput
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (!line.StartsWith(statusPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rawStatus = line[statusPrefix.Length..].Trim();
            var normalizedStatus = rawStatus.Trim(
                '"',
                '\'',
                '`',
                '.',
                ',',
                ';',
                ':',
                '!',
                '?',
                '(',
                ')',
                '[',
                ']',
                '{',
                '}',
                '<',
                '>');
            if (string.Equals(normalizedStatus, AgentVerificationStatusDone, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedStatus, AgentVerificationStatusRefine, StringComparison.OrdinalIgnoreCase))
            {
                status = normalizedStatus;
                return true;
            }
        }

        return false;
    }

    private static AgentCodeChangeEvidence ParseAgentCodeChangeEvidence(string executionOutput)
    {
        if (string.IsNullOrWhiteSpace(executionOutput))
        {
            return AgentCodeChangeEvidence.Empty;
        }

        const string statusPrefix = "CODE_CHANGE_STATUS=";
        const string changeFilePrefix = "CHANGE_FILE=";
        const string technicalJustificationPrefix = "TECHNICAL_JUSTIFICATION=";
        const string technicalJustificationAliasPrefix = "JUSTIFICATIVA_TECNICA=";

        var normalizedOutput = executionOutput.Replace("\r", "\n", StringComparison.Ordinal);
        var lines = normalizedOutput.Split('\n', StringSplitOptions.TrimEntries);

        var status = AgentCodeChangeStatusUnknown;
        var changeFileCount = 0;
        var diffBlockCount = 0;
        var technicalJustificationCount = 0;
        var insideDiffFence = false;
        var containsStructuredOutput = false;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith(statusPrefix, StringComparison.OrdinalIgnoreCase))
            {
                status = NormalizeAgentCodeChangeStatus(line[statusPrefix.Length..]);
                containsStructuredOutput = true;
                continue;
            }

            if (line.StartsWith(changeFilePrefix, StringComparison.OrdinalIgnoreCase))
            {
                changeFileCount++;
                containsStructuredOutput = true;
                continue;
            }

            if (line.StartsWith(technicalJustificationPrefix, StringComparison.OrdinalIgnoreCase)
                || line.StartsWith(technicalJustificationAliasPrefix, StringComparison.OrdinalIgnoreCase))
            {
                technicalJustificationCount++;
                containsStructuredOutput = true;
                continue;
            }

            if (line.StartsWith("```diff", StringComparison.OrdinalIgnoreCase))
            {
                insideDiffFence = true;
                continue;
            }

            if (insideDiffFence && line.StartsWith("```", StringComparison.Ordinal))
            {
                insideDiffFence = false;
                diffBlockCount++;
            }
        }

        if (insideDiffFence)
        {
            diffBlockCount++;
        }

        if (diffBlockCount == 0
            && (normalizedOutput.Contains("diff --git", StringComparison.Ordinal)
                || normalizedOutput.Contains("\n@@", StringComparison.Ordinal)
                || normalizedOutput.StartsWith("@@", StringComparison.Ordinal)))
        {
            diffBlockCount = 1;
        }

        return new AgentCodeChangeEvidence(
            Status: status,
            ChangeFileCount: changeFileCount,
            DiffBlockCount: diffBlockCount,
            TechnicalJustificationCount: technicalJustificationCount,
            ContainsStructuredOutput: containsStructuredOutput);
    }

    private static string NormalizeAgentCodeChangeStatus(string rawStatus)
    {
        var normalizedStatus = rawStatus.Trim().Trim(
            '"',
            '\'',
            '`',
            '.',
            ',',
            ';',
            ':',
            '!',
            '?',
            '(',
            ')',
            '[',
            ']',
            '{',
            '}',
            '<',
            '>');
        if (string.Equals(normalizedStatus, AgentCodeChangeStatusChanged, StringComparison.OrdinalIgnoreCase))
        {
            return AgentCodeChangeStatusChanged;
        }

        if (string.Equals(normalizedStatus, AgentCodeChangeStatusNoChange, StringComparison.OrdinalIgnoreCase))
        {
            return AgentCodeChangeStatusNoChange;
        }

        return AgentCodeChangeStatusUnknown;
    }

    private static string BuildAgentCodeChangeEvidenceSummary(AgentCodeChangeEvidence evidence)
    {
        if (evidence.DeclaredNoCodeChanges && evidence.IsCompliant)
        {
            return "Execucao declarou CODE_CHANGE_STATUS=no-change e sem alteracoes estruturadas.";
        }

        if (!evidence.RequiresValidation)
        {
            return "Execucao sem declaracao estruturada de alteracao de codigo.";
        }

        return
            $"status={evidence.Status}; arquivos={evidence.ChangeFileCount}; " +
            $"diffs={evidence.DiffBlockCount}; justificativas={evidence.TechnicalJustificationCount}; " +
            $"saida_estruturada={(evidence.ContainsStructuredOutput ? "sim" : "nao")}; " +
            $"conformidade={(evidence.IsCompliant ? "ok" : "incompleta")}.";
    }

    private static string BuildAgentValidationReportSummary(AgentValidationReport validationReport)
    {
        if (!validationReport.WasRequired)
        {
            return "Nao executada: a fase execute nao declarou bloco de mudancas validavel.";
        }

        if (!validationReport.CommandsDiscovered)
        {
            return "Nao executada: nenhum comando build/test/lint foi descoberto para este workspace.";
        }

        var builder = new StringBuilder();
        builder.AppendLine(validationReport.HasFailures
            ? "Status geral: failed."
            : "Status geral: passed.");

        foreach (var result in validationReport.Results)
        {
            builder.AppendLine(
                $"- {result.Name}: {(result.IsSuccess ? "passed" : "failed")} " +
                $"(exit_code={result.ExitCode}, duracao={FormatRequiredBudgetDuration(result.Duration)}, " +
                $"comando=\"{result.CommandLine}\").");

            if (!string.IsNullOrWhiteSpace(result.StdOut))
            {
                builder.AppendLine(
                    $"  stdout: {BuildPromptContextExcerpt(result.StdOut, "<vazio>", AgentValidationOutputExcerptMaxCharacters)}");
            }

            if (!string.IsNullOrWhiteSpace(result.StdErr))
            {
                builder.AppendLine(
                    $"  stderr: {BuildPromptContextExcerpt(result.StdErr, "<vazio>", AgentValidationOutputExcerptMaxCharacters)}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildAgentCodeChangeEvidenceLogMessage(AgentCodeChangeEvidence evidence)
    {
        if (!evidence.RequiresValidation && !evidence.DeclaredNoCodeChanges)
        {
            return "Fase execute sem mudancas de codigo estruturadas declaradas.";
        }

        return $"Fase execute: {BuildAgentCodeChangeEvidenceSummary(evidence)}";
    }

    private static string BuildAgentAutoCorrectionEvidenceLogMessage(
        int attempt,
        AgentCodeChangeEvidence evidence)
    {
        if (!evidence.RequiresValidation && !evidence.DeclaredNoCodeChanges)
        {
            return
                $"Auto-correcao de validacao: tentativa {attempt} sem mudancas de codigo estruturadas declaradas.";
        }

        return
            $"Auto-correcao de validacao: tentativa {attempt}: {BuildAgentCodeChangeEvidenceSummary(evidence)}";
    }

    private static string BuildAgentPromptBase(
        AgentExecutionPlan executionPlan,
        AgentProjectContextSnapshot projectContext,
        int iteration,
        string phase)
    {
        var projectContextSection = BuildAgentProjectContextPrompt(projectContext);
        var executionPlanSection = BuildAgentExecutionPlanSection(executionPlan);

        return
            $"""
            [MODO: AGENTE AUTONOMO]
            Atue como um desenvolvedor senior com foco em entrega.
            Siga o ciclo: plan -> execute -> verify -> refine.
            Explique premissas, riscos e proximos passos de forma objetiva.
            Quando faltar contexto, sinalize a lacuna e proponha uma acao util.

            [CICLO]
            Iteracao: {iteration}
            Fase atual: {phase}

            [OBJETIVO]
            {executionPlan.Objective}

            [CONTEXTO DE ENGENHARIA DO PROJETO]
            {projectContextSection}

            [PLANO DE EXECUCAO POR ETAPAS]
            {executionPlanSection}
            """;
    }

    private static string BuildPromptContextExcerpt(string value, string fallbackValue)
    {
        return BuildPromptContextExcerpt(
            value,
            fallbackValue,
            AgentPromptContextExcerptMaxCharacters);
    }

    private static string BuildPromptContextExcerpt(
        string value,
        string fallbackValue,
        int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallbackValue;
        }

        var normalizedValue = value.Trim();
        var safeMaxCharacters = Math.Max(1, maxCharacters);
        if (normalizedValue.Length <= safeMaxCharacters)
        {
            return normalizedValue;
        }

        var startIndex = normalizedValue.Length - safeMaxCharacters;
        return $"[...] {normalizedValue[startIndex..]}";
    }

    private static string BuildAgentExecutionPlanSection(AgentExecutionPlan executionPlan)
    {
        var builder = new StringBuilder();

        foreach (var step in executionPlan.Steps)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine($"{step.Order}. [{step.Stage}]");
            builder.AppendLine($"   Acao: {step.Action}");
            builder.Append($"   Entrega esperada: {step.ExpectedOutput}");
        }

        return builder.ToString();
    }

    private static PromptExecutionCheckpointContext CreatePromptCheckpointContext(
        string command,
        string prompt,
        string? model,
        string? skillName,
        Action<ExecutionSessionCheckpoint> executionCheckpointAppender,
        string? sessionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(executionCheckpointAppender);

        return new PromptExecutionCheckpointContext(
            SessionId: string.IsNullOrWhiteSpace(sessionId)
                ? Guid.NewGuid().ToString("N")
                : sessionId.Trim(),
            Command: command.Trim(),
            Prompt: prompt.Trim(),
            Model: string.IsNullOrWhiteSpace(model) ? null : model.Trim(),
            SkillName: string.IsNullOrWhiteSpace(skillName) ? null : skillName.Trim(),
            ExecutionCheckpointAppender: executionCheckpointAppender);
    }

    private static void WriteExecutionStateAndCheckpoint(
        ExecutionState state,
        string? detail,
        PromptExecutionCheckpointContext? checkpointContext,
        ExecutionCheckpointStatus? checkpointStatus = null)
    {
        WriteExecutionState(state, detail);

        if (checkpointContext is not PromptExecutionCheckpointContext context)
        {
            return;
        }

        var checkpoint = new ExecutionSessionCheckpoint(
            TimestampUtc: DateTimeOffset.UtcNow,
            SessionId: context.SessionId,
            Command: context.Command,
            Stage: ResolveCheckpointStage(state),
            Status: checkpointStatus ?? ResolveCheckpointStatus(state),
            Prompt: context.Prompt,
            Model: context.Model,
            SkillName: context.SkillName,
            Detail: detail ?? string.Empty);
        TryAppendExecutionCheckpoint(checkpoint, context.ExecutionCheckpointAppender);
    }

    private static void TryAppendExecutionCheckpoint(
        ExecutionSessionCheckpoint checkpoint,
        Action<ExecutionSessionCheckpoint> executionCheckpointAppender)
    {
        ArgumentNullException.ThrowIfNull(executionCheckpointAppender);

        try
        {
            executionCheckpointAppender(checkpoint);
        }
        catch (Exception ex) when (ex is ArgumentException
            or InvalidOperationException
            or DirectoryNotFoundException
            or IOException
            or JsonException
            or UnauthorizedAccessException)
        {
            ConsoleLogger.Error(
                $"Nao foi possivel registrar checkpoint local da execucao. {ex.Message}");
        }
    }

    private static ExecutionCheckpointStatus ResolveCheckpointStatus(ExecutionState state)
    {
        return state switch
        {
            ExecutionState.Completed => ExecutionCheckpointStatus.Completed,
            ExecutionState.Error => ExecutionCheckpointStatus.Failed,
            _ => ExecutionCheckpointStatus.InProgress
        };
    }

    private static string ResolveCheckpointStage(ExecutionState state)
    {
        return state switch
        {
            ExecutionState.Connecting => "connecting",
            ExecutionState.ToolCall => "tool-call",
            ExecutionState.Processing => "processing",
            ExecutionState.Diff => "diff",
            ExecutionState.Completed => "completed",
            ExecutionState.Error => "error",
            _ => "unknown"
        };
    }

    private static void WriteExecutionState(ExecutionState state, string? detail = null)
    {
        string label = (ExecutionStateLabel)state;
        var message = detail is null
            ? $"Estado de execucao: {label}."
            : $"Estado de execucao: {label}. {detail}";
        var spinnerStep = Interlocked.Increment(ref _executionStateSpinnerStep);
        var visualSuffix = TerminalVisualComponents.BuildExecutionSuffix(state, spinnerStep);
        var finalMessage = $"{message} {visualSuffix}";

        if (state == ExecutionState.Error)
        {
            ConsoleLogger.Error(finalMessage);
            return;
        }

        ConsoleLogger.Info(finalMessage);
    }

    private static string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var buildMetadataSeparator = informationalVersion.IndexOf('+');
            return buildMetadataSeparator >= 0
                ? informationalVersion[..buildMetadataSeparator]
                : informationalVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    private static async Task<McpServerTestResult> ExecuteDefaultMcpServerTestAsync(
        McpServerDefinition server,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);

        await using IMcpClient mcpClient = CreateMcpClient(server);
        await mcpClient.ConnectAsync(cancellationToken);

        var initializeParameters = JsonSerializer.SerializeToElement(new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { },
            clientInfo = new
            {
                name = CliName,
                version = GetVersion()
            }
        });

        var initializeResult = await mcpClient.SendRequestAsync(
            method: "initialize",
            parameters: initializeParameters,
            timeout: TimeSpan.FromSeconds(15),
            cancellationToken: cancellationToken);

        await mcpClient.SendNotificationAsync(
            method: "notifications/initialized",
            cancellationToken: cancellationToken);

        var discoveredTools = await McpToolDiscovery.DiscoverAsync(
            mcpClient,
            timeout: TimeSpan.FromSeconds(15),
            cancellationToken: cancellationToken);

        return McpServerTestResult.Success(
            BuildMcpTestDetail(server, initializeResult, discoveredTools));
    }

    private static string BuildMcpTestDetail(
        McpServerDefinition server,
        JsonElement initializeResult,
        IReadOnlyList<ToolDescriptor> discoveredTools)
    {
        ArgumentNullException.ThrowIfNull(discoveredTools);

        var handshakeDetail = $"Servidor MCP '{server.Name}' respondeu ao handshake com sucesso.";

        if (initializeResult.ValueKind == JsonValueKind.Object
            && initializeResult.TryGetProperty("serverInfo", out var serverInfo)
            && serverInfo.ValueKind == JsonValueKind.Object)
        {
            var serverName = TryReadStringProperty(serverInfo, "name");
            var serverVersion = TryReadStringProperty(serverInfo, "version");

            if (!string.IsNullOrWhiteSpace(serverName) && !string.IsNullOrWhiteSpace(serverVersion))
            {
                handshakeDetail = $"Servidor MCP '{server.Name}' respondeu ao handshake ({serverName} {serverVersion}).";
            }
            else if (!string.IsNullOrWhiteSpace(serverName))
            {
                handshakeDetail = $"Servidor MCP '{server.Name}' respondeu ao handshake ({serverName}).";
            }
        }

        if (discoveredTools.Count == 0)
        {
            return $"{handshakeDetail} Nenhuma ferramenta MCP foi anunciada pelo servidor.";
        }

        var orderedToolNames = discoveredTools
            .Select(static tool => tool.Name)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var listedToolNames = string.Join(", ", orderedToolNames.Take(5));
        var truncatedLabel = orderedToolNames.Length > 5 ? ", ..." : string.Empty;

        return $"{handshakeDetail} Ferramentas MCP descobertas: {orderedToolNames.Length} ({listedToolNames}{truncatedLabel}).";
    }

    private static string? TryReadStringProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var propertyValue)
            || propertyValue.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = propertyValue.GetString();
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static IMcpClient CreateMcpClient(McpServerDefinition server)
    {
        if (server.ProcessOptions is McpServerProcessOptions processOptions)
        {
            return new McpStdioClient(processOptions);
        }

        if (server.RemoteOptions is McpServerRemoteOptions remoteOptions)
        {
            return new McpRemoteClient(remoteOptions);
        }

        throw new InvalidOperationException(
            $"Servidor MCP '{server.Name}' sem configuracao de transporte.");
    }

    private static async Task<OllamaHealthcheckResult> ExecuteDefaultHealthcheckAsync(CancellationToken cancellationToken)
    {
        var userConfig = UserConfigFile.Load();

        using var httpClient = new HttpClient
        {
            Timeout = userConfig.HealthcheckTimeout
        };

        IOllamaHttpClient ollamaClient = new OllamaHttpClient(
            httpClient,
            baseAddress: userConfig.OllamaHost);
        return await ollamaClient.CheckHealthAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<OllamaLocalModel>> ExecuteDefaultModelsAsync(CancellationToken cancellationToken)
    {
        var userConfig = UserConfigFile.Load();

        using var httpClient = new HttpClient
        {
            Timeout = userConfig.ModelsTimeout
        };

        IOllamaHttpClient ollamaClient = new OllamaHttpClient(
            httpClient,
            baseAddress: userConfig.OllamaHost);
        return await ollamaClient.ListLocalModelsAsync(cancellationToken);
    }

    private static async Task<PromptStreamMetrics> StreamPromptResponseAsync(
        string prompt,
        string? model,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        CancellationToken cancellationToken)
    {
        var shouldWriteNewLine = false;
        var chunkCount = 0;
        var characterCount = 0;
        var containsDiffMarkers = false;
        var responseTextBuilder = new StringBuilder();
        var currentDesignSystem = ConsoleLogger.CurrentDesignSystem;
        var responseRenderer = new TerminalResponseRenderer(
            AnsiTerminalRenderer.CreateDefault(currentDesignSystem),
            currentDesignSystem);

        try
        {
            await foreach (var chunk in promptExecutor(prompt, model, cancellationToken).WithCancellation(cancellationToken))
            {
                if (string.IsNullOrEmpty(chunk))
                {
                    continue;
                }

                chunkCount++;
                characterCount += chunk.Length;
                containsDiffMarkers = containsDiffMarkers || ContainsDiffMarkers(chunk);
                responseTextBuilder.Append(chunk);

                var renderedChunk = responseRenderer.RenderChunk(chunk);
                if (renderedChunk.Length == 0)
                {
                    continue;
                }

                Console.Write(renderedChunk);
                shouldWriteNewLine = renderedChunk[^1] is not ('\n' or '\r');
            }
        }
        finally
        {
            var trailingOutput = responseRenderer.Flush();
            if (trailingOutput.Length > 0)
            {
                Console.Write(trailingOutput);
                shouldWriteNewLine = trailingOutput[^1] is not ('\n' or '\r');
            }

            if (shouldWriteNewLine)
            {
                Console.WriteLine();
            }
        }

        return new PromptStreamMetrics(
            ChunkCount: chunkCount,
            CharacterCount: characterCount,
            ContainsDiffMarkers: containsDiffMarkers,
            ResponseText: responseTextBuilder.ToString());
    }

    private static string BuildDiffStateDetail(PromptStreamMetrics streamMetrics)
    {
        var diffDetectionMessage = streamMetrics.ContainsDiffMarkers
            ? "Diff identificado na resposta."
            : "Nenhum bloco diff identificado na resposta.";

        return $"{diffDetectionMessage} Conteudo recebido: {streamMetrics.CharacterCount} caractere(s).";
    }

    private static bool ContainsDiffMarkers(string chunk)
    {
        return chunk.Contains("```diff", StringComparison.OrdinalIgnoreCase)
            || chunk.Contains("diff --git", StringComparison.Ordinal)
            || chunk.Contains("@@", StringComparison.Ordinal)
            || chunk.Contains("*** Begin Patch", StringComparison.Ordinal)
            || chunk.Contains("*** End Patch", StringComparison.Ordinal);
    }

    private static IToolRuntime CreateDefaultToolRuntime()
    {
        return new ToolRuntime(
            new EchoToolProvider(),
            new PowerShellToolProvider(),
            new UnixShellToolProvider());
    }

    private static IDisposable RegisterConsoleCancelHandler(
        CancellationTokenSource cancellationTokenSource,
        Action onCancellationRequested)
    {
        ArgumentNullException.ThrowIfNull(cancellationTokenSource);
        ArgumentNullException.ThrowIfNull(onCancellationRequested);

        ConsoleCancelEventHandler handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            if (cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            onCancellationRequested();
            cancellationTokenSource.Cancel();
        };

        Console.CancelKeyPress += handler;
        return new DisposableAction(() => Console.CancelKeyPress -= handler);
    }

    private static Func<string, string?, CancellationToken, IAsyncEnumerable<string>> WrapPromptExecutorWithModelFallback(
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> modelsExecutor)
    {
        ArgumentNullException.ThrowIfNull(promptExecutor);
        ArgumentNullException.ThrowIfNull(modelsExecutor);

        return (prompt, model, cancellationToken) => ExecutePromptWithModelFallbackAsync(
            prompt,
            model,
            promptExecutor,
            modelsExecutor,
            cancellationToken);
    }

    private static async IAsyncEnumerable<string> ExecutePromptWithModelFallbackAsync(
        string prompt,
        string? model,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> modelsExecutor,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var fallbackCandidates = BuildInitialModelFallbackCandidates(model);
        var localFallbackModelsLoaded = false;

        for (var candidateIndex = 0; candidateIndex < fallbackCandidates.Count; candidateIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidateModel = fallbackCandidates[candidateIndex];
            IAsyncEnumerator<string>? enumerator = null;
            var emittedChunks = false;
            Exception? capturedFailure = null;

            try
            {
                try
                {
                    enumerator = promptExecutor(prompt, candidateModel, cancellationToken)
                        .GetAsyncEnumerator(cancellationToken);
                }
                catch (Exception ex) when (ShouldFallbackToNextModel(ex, emittedChunks, cancellationToken))
                {
                    capturedFailure = ex;
                }

                if (capturedFailure is null && enumerator is not null)
                {
                    while (true)
                    {
                        string currentChunk;
                        try
                        {
                            if (!await enumerator.MoveNextAsync())
                            {
                                break;
                            }

                            currentChunk = enumerator.Current;
                        }
                        catch (Exception ex) when (ShouldFallbackToNextModel(ex, emittedChunks, cancellationToken))
                        {
                            capturedFailure = ex;
                            break;
                        }

                        emittedChunks = true;
                        yield return currentChunk;
                    }
                }

                if (capturedFailure is null)
                {
                    yield break;
                }

                if (!localFallbackModelsLoaded)
                {
                    localFallbackModelsLoaded = true;
                    await TryAppendFallbackLocalModelsAsync(
                        fallbackCandidates,
                        modelsExecutor,
                        cancellationToken);
                }

                var nextCandidateIndex = candidateIndex + 1;
                if (nextCandidateIndex >= fallbackCandidates.Count)
                {
                    throw capturedFailure;
                }

                ConsoleLogger.Info(
                    $"Modelo '{BuildModelFallbackLabel(candidateModel)}' indisponivel. " +
                    $"Tentando fallback para '{BuildModelFallbackLabel(fallbackCandidates[nextCandidateIndex])}'.");
            }
            finally
            {
                if (enumerator is not null)
                {
                    await enumerator.DisposeAsync();
                }
            }
        }

        throw new InvalidOperationException(
            "Nao foi possivel executar o prompt porque nenhum modelo de fallback estava disponivel.");
    }

    private static List<string?> BuildInitialModelFallbackCandidates(string? selectedModel)
    {
        var fallbackCandidates = new List<string?>();
        var knownModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(selectedModel))
        {
            fallbackCandidates.Add(null);
        }
        else
        {
            AddCandidate(selectedModel);
        }

        AddCandidate(OllamaModelDefaults.DefaultModel);
        return fallbackCandidates;

        void AddCandidate(string? modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return;
            }

            var normalizedModelName = modelName.Trim();
            if (!knownModels.Add(normalizedModelName))
            {
                return;
            }

            fallbackCandidates.Add(normalizedModelName);
        }
    }

    private static async Task TryAppendFallbackLocalModelsAsync(
        List<string?> fallbackCandidates,
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> modelsExecutor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fallbackCandidates);
        ArgumentNullException.ThrowIfNull(modelsExecutor);

        var knownModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fallbackCandidate in fallbackCandidates)
        {
            if (string.IsNullOrWhiteSpace(fallbackCandidate))
            {
                continue;
            }

            knownModels.Add(fallbackCandidate.Trim());
        }

        try
        {
            var localModels = await modelsExecutor(cancellationToken);
            foreach (var localModel in localModels)
            {
                if (string.IsNullOrWhiteSpace(localModel.Name))
                {
                    continue;
                }

                var normalizedModelName = localModel.Name.Trim();
                if (!knownModels.Add(normalizedModelName))
                {
                    continue;
                }

                fallbackCandidates.Add(normalizedModelName);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ConsoleLogger.Info(
                $"Nao foi possivel carregar modelos locais para fallback automatico: {ex.Message}");
        }
    }

    private static bool ShouldFallbackToNextModel(
        Exception exception,
        bool emittedChunks,
        CancellationToken cancellationToken)
    {
        if (emittedChunks)
        {
            return false;
        }

        if (exception is OperationCanceledException)
        {
            return false;
        }

        return !cancellationToken.IsCancellationRequested
            && IsModelUnavailableFailure(exception);
    }

    private static bool IsModelUnavailableFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is HttpRequestException { StatusCode: HttpStatusCode.NotFound })
        {
            return true;
        }

        if (IsPotentialModelUnavailableMessage(exception.Message))
        {
            return true;
        }

        if (exception is InvalidOperationException { InnerException: Exception innerException })
        {
            return IsModelUnavailableFailure(innerException);
        }

        if (exception is AggregateException aggregateException)
        {
            foreach (var aggregateInnerException in aggregateException.InnerExceptions)
            {
                if (IsModelUnavailableFailure(aggregateInnerException))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPotentialModelUnavailableMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("HTTP 404", StringComparison.OrdinalIgnoreCase)
            || message.Contains("model", StringComparison.OrdinalIgnoreCase)
            || message.Contains("modelo", StringComparison.OrdinalIgnoreCase)
            || message.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || message.Contains("nao encontrado", StringComparison.OrdinalIgnoreCase)
            || message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
            || message.Contains("indisponivel", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildModelFallbackLabel(string? modelName)
    {
        return string.IsNullOrWhiteSpace(modelName)
            ? "<padrao>"
            : modelName.Trim();
    }

    private static Func<string, string?, CancellationToken, IAsyncEnumerable<string>> WrapPromptExecutorWithResilience(
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        ResilienceState resilienceState)
    {
        ArgumentNullException.ThrowIfNull(promptExecutor);
        ArgumentNullException.ThrowIfNull(resilienceState);

        return (prompt, model, cancellationToken) => ExecutePromptWithResilienceAsync(
            prompt,
            model,
            promptExecutor,
            resilienceState,
            cancellationToken);
    }

    private static async IAsyncEnumerable<string> ExecutePromptWithResilienceAsync(
        string prompt,
        string? model,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        ResilienceState resilienceState,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= ResilienceRetryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            resilienceState.EnsureCallAllowed();

            IAsyncEnumerator<string>? enumerator = null;
            var emittedChunks = false;
            var shouldRetry = false;
            Exception? capturedFailure = null;

            try
            {
                enumerator = promptExecutor(prompt, model, cancellationToken)
                    .GetAsyncEnumerator(cancellationToken);

                while (true)
                {
                    string chunk;
                    try
                    {
                        if (!await enumerator.MoveNextAsync())
                        {
                            break;
                        }

                        chunk = enumerator.Current;
                    }
                    catch (Exception ex) when (IsTransientFailure(ex, cancellationToken))
                    {
                        capturedFailure = ex;
                        shouldRetry = !emittedChunks && CanRetry(attempt);
                        break;
                    }

                    emittedChunks = true;
                    yield return chunk;
                }
            }
            finally
            {
                if (enumerator is not null)
                {
                    await enumerator.DisposeAsync();
                }
            }

            if (capturedFailure is null)
            {
                resilienceState.RegisterSuccess();
                yield break;
            }

            if (!emittedChunks)
            {
                resilienceState.RegisterFailure();
            }

            if (shouldRetry)
            {
                await WaitBeforeRetryAsync(cancellationToken);
                continue;
            }

            throw capturedFailure;
        }

        throw new InvalidOperationException(
            "Nao foi possivel executar o prompt no momento.");
    }

    private static Func<CancellationToken, Task<OllamaHealthcheckResult>> WrapHealthcheckExecutorWithResilience(
        Func<CancellationToken, Task<OllamaHealthcheckResult>> healthcheckExecutor,
        ResilienceState resilienceState)
    {
        ArgumentNullException.ThrowIfNull(healthcheckExecutor);
        ArgumentNullException.ThrowIfNull(resilienceState);

        return cancellationToken => ExecuteHealthcheckWithResilienceAsync(
            healthcheckExecutor,
            resilienceState,
            cancellationToken);
    }

    private static async Task<OllamaHealthcheckResult> ExecuteHealthcheckWithResilienceAsync(
        Func<CancellationToken, Task<OllamaHealthcheckResult>> healthcheckExecutor,
        ResilienceState resilienceState,
        CancellationToken cancellationToken)
    {
        OllamaHealthcheckResult? lastFailureResult = null;

        for (var attempt = 1; attempt <= ResilienceRetryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                resilienceState.EnsureCallAllowed();
            }
            catch (CircuitBreakerOpenException circuitOpenException)
            {
                return OllamaHealthcheckResult.Unhealthy(circuitOpenException.Message);
            }

            try
            {
                var healthcheckResult = await healthcheckExecutor(cancellationToken);
                if (healthcheckResult.IsHealthy)
                {
                    resilienceState.RegisterSuccess();
                    return healthcheckResult;
                }

                lastFailureResult = healthcheckResult;
                resilienceState.RegisterFailure();
            }
            catch (Exception ex) when (IsTransientFailure(ex, cancellationToken))
            {
                lastFailureResult = OllamaHealthcheckResult.Unhealthy(ex.Message);
                resilienceState.RegisterFailure();
            }

            if (!CanRetry(attempt))
            {
                break;
            }

            await WaitBeforeRetryAsync(cancellationToken);
        }

        return lastFailureResult ?? OllamaHealthcheckResult.Unhealthy(
            "Nao foi possivel validar a disponibilidade do Ollama.");
    }

    private static Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> WrapModelsExecutorWithResilience(
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> modelsExecutor,
        ResilienceState resilienceState)
    {
        ArgumentNullException.ThrowIfNull(modelsExecutor);
        ArgumentNullException.ThrowIfNull(resilienceState);

        return cancellationToken => ExecuteModelsWithResilienceAsync(
            modelsExecutor,
            resilienceState,
            cancellationToken);
    }

    private static async Task<IReadOnlyList<OllamaLocalModel>> ExecuteModelsWithResilienceAsync(
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> modelsExecutor,
        ResilienceState resilienceState,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= ResilienceRetryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            resilienceState.EnsureCallAllowed();

            try
            {
                var models = await modelsExecutor(cancellationToken);
                resilienceState.RegisterSuccess();
                return models;
            }
            catch (Exception ex) when (IsTransientFailure(ex, cancellationToken)
                && CanRetry(attempt))
            {
                resilienceState.RegisterFailure();
                await WaitBeforeRetryAsync(cancellationToken);
            }
            catch (Exception ex) when (IsTransientFailure(ex, cancellationToken))
            {
                resilienceState.RegisterFailure();
                throw;
            }
        }

        throw new InvalidOperationException(
            "Nao foi possivel listar os modelos locais do Ollama no momento.");
    }

    private static Func<McpServerDefinition, CancellationToken, Task<McpServerTestResult>> WrapMcpServerTesterWithResilience(
        Func<McpServerDefinition, CancellationToken, Task<McpServerTestResult>> mcpServerTester,
        ResilienceState resilienceState)
    {
        ArgumentNullException.ThrowIfNull(mcpServerTester);
        ArgumentNullException.ThrowIfNull(resilienceState);

        return (server, cancellationToken) => ExecuteMcpServerTestWithResilienceAsync(
            mcpServerTester,
            resilienceState,
            server,
            cancellationToken);
    }

    private static async Task<McpServerTestResult> ExecuteMcpServerTestWithResilienceAsync(
        Func<McpServerDefinition, CancellationToken, Task<McpServerTestResult>> mcpServerTester,
        ResilienceState resilienceState,
        McpServerDefinition server,
        CancellationToken cancellationToken)
    {
        McpServerTestResult? lastFailureResult = null;

        for (var attempt = 1; attempt <= ResilienceRetryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                resilienceState.EnsureCallAllowed();
            }
            catch (CircuitBreakerOpenException circuitOpenException)
            {
                return McpServerTestResult.Failure(circuitOpenException.Message);
            }

            try
            {
                var testResult = await mcpServerTester(server, cancellationToken);
                if (testResult.IsSuccess)
                {
                    resilienceState.RegisterSuccess();
                    return testResult;
                }

                lastFailureResult = testResult;
                resilienceState.RegisterFailure();
            }
            catch (Exception ex) when (IsTransientFailure(ex, cancellationToken))
            {
                resilienceState.RegisterFailure();
                if (!CanRetry(attempt))
                {
                    throw;
                }

                await WaitBeforeRetryAsync(cancellationToken);
                continue;
            }

            if (!CanRetry(attempt))
            {
                break;
            }

            await WaitBeforeRetryAsync(cancellationToken);
        }

        return lastFailureResult ?? McpServerTestResult.Failure(
            "Nao foi possivel testar o servidor MCP no momento.");
    }

    private static bool CanRetry(int attempt)
    {
        return attempt < ResilienceRetryAttempts;
    }

    private static async Task WaitBeforeRetryAsync(CancellationToken cancellationToken)
    {
        if (ResilienceRetryDelay == TimeSpan.Zero)
        {
            return;
        }

        await Task.Delay(ResilienceRetryDelay, cancellationToken);
    }

    private static bool IsTransientFailure(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is CircuitBreakerOpenException)
        {
            return false;
        }

        if (exception is OperationCanceledException)
        {
            return !cancellationToken.IsCancellationRequested;
        }

        if (exception is TimeoutException or HttpRequestException or IOException)
        {
            return true;
        }

        if (exception is InvalidOperationException invalidOperationException
            && invalidOperationException.InnerException is Exception innerException)
        {
            return IsTransientFailure(innerException, cancellationToken);
        }

        return false;
    }

    private static Func<string, string?, CancellationToken, IAsyncEnumerable<string>> WrapLegacyPromptExecutor(
        Func<string, string> promptExecutor)
    {
        ArgumentNullException.ThrowIfNull(promptExecutor);
        return (prompt, _, cancellationToken) => ExecuteLegacyPromptAsStreamAsync(promptExecutor, prompt, cancellationToken);
    }

    private static Func<string, string?, CancellationToken, IAsyncEnumerable<string>> WrapPromptExecutorWithoutModel(
        Func<string, CancellationToken, IAsyncEnumerable<string>> promptExecutor)
    {
        ArgumentNullException.ThrowIfNull(promptExecutor);
        return (prompt, _, cancellationToken) => promptExecutor(prompt, cancellationToken);
    }

    private static async IAsyncEnumerable<string> ExecuteLegacyPromptAsStreamAsync(
        Func<string, string> promptExecutor,
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return promptExecutor(prompt);
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<string> ExecuteDefaultPromptStreamAsync(
        string prompt,
        string? model,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var userConfig = UserConfigFile.Load();
        var resolvedModel = ResolveModelWithConfigFallback(model, userConfig);

        using var httpClient = new HttpClient
        {
            Timeout = userConfig.PromptTimeout
        };

        IOllamaHttpClient ollamaClient = new OllamaHttpClient(
            httpClient,
            baseAddress: userConfig.OllamaHost,
            defaultModel: resolvedModel);
        await foreach (var chunk in ollamaClient.GenerateStreamAsync(prompt, cancellationToken).WithCancellation(cancellationToken))
        {
            yield return chunk;
        }
    }

    private static string ResolveModelWithConfigFallback(string? selectedModel, UserRuntimeConfig userConfig)
    {
        if (!string.IsNullOrWhiteSpace(selectedModel))
        {
            return selectedModel.Trim();
        }

        var configuredByEnvironment = Environment.GetEnvironmentVariable(
            OllamaModelDefaults.DefaultModelEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredByEnvironment))
        {
            return configuredByEnvironment.Trim();
        }

        return userConfig.DefaultModel;
    }

    private sealed class ResilienceState
    {
        private readonly string _operationName;
        private readonly object _lock = new();
        private int _consecutiveFailures;
        private DateTimeOffset? _circuitOpenUntil;

        public ResilienceState(string operationName)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException(
                    "O nome da operacao resiliente nao pode ser vazio.",
                    nameof(operationName));
            }

            _operationName = operationName.Trim();
        }

        public void EnsureCallAllowed()
        {
            lock (_lock)
            {
                if (_circuitOpenUntil is not DateTimeOffset openUntil)
                {
                    return;
                }

                var now = DateTimeOffset.UtcNow;
                if (now >= openUntil)
                {
                    _circuitOpenUntil = null;
                    _consecutiveFailures = 0;
                    return;
                }

                throw new CircuitBreakerOpenException(
                    _operationName,
                    openUntil - now);
            }
        }

        public void RegisterSuccess()
        {
            lock (_lock)
            {
                _consecutiveFailures = 0;
                _circuitOpenUntil = null;
            }
        }

        public void RegisterFailure()
        {
            lock (_lock)
            {
                if (_circuitOpenUntil is not null)
                {
                    return;
                }

                _consecutiveFailures++;
                if (_consecutiveFailures < ResilienceCircuitFailureThreshold)
                {
                    return;
                }

                _consecutiveFailures = 0;
                _circuitOpenUntil = DateTimeOffset.UtcNow + ResilienceCircuitOpenDuration;
            }
        }
    }

    private sealed class CircuitBreakerOpenException : InvalidOperationException
    {
        public CircuitBreakerOpenException(string operationName, TimeSpan retryAfter)
            : base(BuildMessage(operationName, retryAfter))
        {
            OperationName = operationName;
            RetryAfter = retryAfter < TimeSpan.Zero ? TimeSpan.Zero : retryAfter;
        }

        public string OperationName { get; }

        public TimeSpan RetryAfter { get; }

        private static string BuildMessage(string operationName, TimeSpan retryAfter)
        {
            var safeRetryAfter = retryAfter < TimeSpan.Zero ? TimeSpan.Zero : retryAfter;
            var milliseconds = Math.Ceiling(safeRetryAfter.TotalMilliseconds);
            return
                $"Circuit breaker aberto para '{operationName}'. " +
                $"Novas tentativas em aproximadamente {milliseconds:0} ms.";
        }
    }

    private sealed class DisposableAction(Action dispose)
        : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }
    }

    private readonly record struct PromptStreamMetrics(
        int ChunkCount,
        int CharacterCount,
        bool ContainsDiffMarkers,
        string ResponseText)
    {
        public static PromptStreamMetrics Empty => new(
            ChunkCount: 0,
            CharacterCount: 0,
            ContainsDiffMarkers: false,
            ResponseText: string.Empty);
    }

    private readonly record struct PromptExecutionResult(
        bool WasCancelled,
        PromptStreamMetrics StreamMetrics);

    private readonly record struct AgentCodeChangeEvidence(
        string Status,
        int ChangeFileCount,
        int DiffBlockCount,
        int TechnicalJustificationCount,
        bool ContainsStructuredOutput)
    {
        public static AgentCodeChangeEvidence Empty => new(
            Status: AgentCodeChangeStatusUnknown,
            ChangeFileCount: 0,
            DiffBlockCount: 0,
            TechnicalJustificationCount: 0,
            ContainsStructuredOutput: false);

        public bool HasDeclaredCodeChanges =>
            string.Equals(Status, AgentCodeChangeStatusChanged, StringComparison.OrdinalIgnoreCase);

        public bool DeclaredNoCodeChanges =>
            string.Equals(Status, AgentCodeChangeStatusNoChange, StringComparison.OrdinalIgnoreCase);

        public bool RequiresValidation =>
            HasDeclaredCodeChanges
            || ChangeFileCount > 0
            || DiffBlockCount > 0
            || TechnicalJustificationCount > 0;

        public bool IsCompliant
        {
            get
            {
                if (DeclaredNoCodeChanges)
                {
                    return ChangeFileCount == 0
                        && DiffBlockCount == 0
                        && TechnicalJustificationCount == 0;
                }

                if (!RequiresValidation)
                {
                    return true;
                }

                return ChangeFileCount > 0
                    && DiffBlockCount >= ChangeFileCount
                    && TechnicalJustificationCount >= ChangeFileCount;
            }
        }
    }

    private readonly record struct AgentAutoCorrectionResult(
        bool WasCancelled,
        string LatestExecutionOutput,
        AgentCodeChangeEvidence LatestChangeEvidence,
        AgentValidationReport ValidationReport,
        decimal AdditionalCost)
    {
        public static AgentAutoCorrectionResult NotRequired(
            string latestExecutionOutput,
            AgentCodeChangeEvidence latestChangeEvidence,
            AgentValidationReport validationReport)
        {
            return new AgentAutoCorrectionResult(
                WasCancelled: false,
                LatestExecutionOutput: latestExecutionOutput,
                LatestChangeEvidence: latestChangeEvidence,
                ValidationReport: validationReport,
                AdditionalCost: 0m);
        }

        public static AgentAutoCorrectionResult Cancelled(
            string latestExecutionOutput,
            AgentCodeChangeEvidence latestChangeEvidence,
            AgentValidationReport validationReport,
            decimal additionalCost)
        {
            return new AgentAutoCorrectionResult(
                WasCancelled: true,
                LatestExecutionOutput: latestExecutionOutput,
                LatestChangeEvidence: latestChangeEvidence,
                ValidationReport: validationReport,
                AdditionalCost: additionalCost < 0m ? 0m : additionalCost);
        }
    }

    private enum AgentBudgetLimitKind
    {
        None,
        MaxTime,
        MaxCost
    }

    private readonly record struct AgentSessionBudget(
        int MaxSteps,
        TimeSpan? MaxTime,
        decimal? MaxCost);

    private readonly record struct AgentProjectContextSnapshot(
        string WorkspaceRootDirectory,
        WorkspaceRootKind WorkspaceRootKind,
        int IndexedFileCount,
        int CodeFileCount,
        int TestFileCount,
        int DocumentationFileCount,
        IReadOnlyList<string> CodeFileSamples,
        IReadOnlyList<string> TestFileSamples,
        IReadOnlyList<string> DocumentationFileSamples,
        IReadOnlyList<string> RecentGitCommits,
        string GitHistorySummary);

    private readonly record struct AgentAutonomousLoopState(
        int NextIteration,
        string PreviousVerificationOutput,
        string PreviousRefinementOutput,
        TimeSpan Elapsed,
        decimal AccumulatedCost)
    {
        public static AgentAutonomousLoopState Initial => new(
            NextIteration: 1,
            PreviousVerificationOutput: string.Empty,
            PreviousRefinementOutput: string.Empty,
            Elapsed: TimeSpan.Zero,
            AccumulatedCost: 0m);
    }

    private readonly record struct AgentResumeCheckpointState(
        AgentSessionBudget SessionBudget,
        AgentAutonomousLoopState LoopState);

    private readonly record struct AgentLoopCheckpointPayload(
        string Kind,
        int Version,
        int NextIteration,
        int MaxSteps,
        double? MaxTimeSeconds,
        decimal? MaxCost,
        decimal AccumulatedCost,
        double ElapsedSeconds,
        string PreviousVerificationOutput,
        string PreviousRefinementOutput);

    private readonly record struct AgentAutonomousLoopResult(
        bool WasCancelled,
        bool IsConcluded,
        int IterationCount,
        TimeSpan Elapsed,
        decimal AccumulatedCost,
        AgentBudgetLimitKind BudgetLimitKind)
    {
        public static AgentAutonomousLoopResult Cancelled(
            int iterationCount,
            TimeSpan elapsed,
            decimal accumulatedCost)
        {
            return new AgentAutonomousLoopResult(
                WasCancelled: true,
                IsConcluded: false,
                IterationCount: Math.Max(1, iterationCount),
                Elapsed: elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed,
                AccumulatedCost: accumulatedCost < 0m ? 0m : accumulatedCost,
                BudgetLimitKind: AgentBudgetLimitKind.None);
        }

        public static AgentAutonomousLoopResult Concluded(
            int iterationCount,
            TimeSpan elapsed,
            decimal accumulatedCost)
        {
            return new AgentAutonomousLoopResult(
                WasCancelled: false,
                IsConcluded: true,
                IterationCount: Math.Max(1, iterationCount),
                Elapsed: elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed,
                AccumulatedCost: accumulatedCost < 0m ? 0m : accumulatedCost,
                BudgetLimitKind: AgentBudgetLimitKind.None);
        }

        public static AgentAutonomousLoopResult NotConcluded(
            int iterationCount,
            TimeSpan elapsed,
            decimal accumulatedCost)
        {
            return new AgentAutonomousLoopResult(
                WasCancelled: false,
                IsConcluded: false,
                IterationCount: Math.Max(1, iterationCount),
                Elapsed: elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed,
                AccumulatedCost: accumulatedCost < 0m ? 0m : accumulatedCost,
                BudgetLimitKind: AgentBudgetLimitKind.None);
        }

        public static AgentAutonomousLoopResult MaxTimeExceeded(
            int iterationCount,
            TimeSpan elapsed,
            decimal accumulatedCost)
        {
            return new AgentAutonomousLoopResult(
                WasCancelled: false,
                IsConcluded: false,
                IterationCount: Math.Max(1, iterationCount),
                Elapsed: elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed,
                AccumulatedCost: accumulatedCost < 0m ? 0m : accumulatedCost,
                BudgetLimitKind: AgentBudgetLimitKind.MaxTime);
        }

        public static AgentAutonomousLoopResult MaxCostExceeded(
            int iterationCount,
            TimeSpan elapsed,
            decimal accumulatedCost)
        {
            return new AgentAutonomousLoopResult(
                WasCancelled: false,
                IsConcluded: false,
                IterationCount: Math.Max(1, iterationCount),
                Elapsed: elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed,
                AccumulatedCost: accumulatedCost < 0m ? 0m : accumulatedCost,
                BudgetLimitKind: AgentBudgetLimitKind.MaxCost);
        }
    }

    private readonly record struct AgentVerificationDecision(
        bool IsConcluded,
        string Status)
    {
        public static AgentVerificationDecision Concluded(string status)
        {
            return new AgentVerificationDecision(IsConcluded: true, Status: status);
        }

        public static AgentVerificationDecision NeedsRefine(string status)
        {
            return new AgentVerificationDecision(IsConcluded: false, Status: status);
        }
    }

    private sealed class WorkspacePatchRequestDocument
    {
        public WorkspacePatchChangeDocument[]? Changes { get; init; }
    }

    private sealed class AgentLoopCheckpointPayloadDocument
    {
        public string? Kind { get; init; }

        public int? Version { get; init; }

        public int? NextIteration { get; init; }

        public int? MaxSteps { get; init; }

        public double? MaxTimeSeconds { get; init; }

        public decimal? MaxCost { get; init; }

        public decimal? AccumulatedCost { get; init; }

        public double? ElapsedSeconds { get; init; }

        public string? PreviousVerificationOutput { get; init; }

        public string? PreviousRefinementOutput { get; init; }
    }

    private sealed class WorkspacePatchChangeDocument
    {
        public string? Kind { get; init; }

        public string? Path { get; init; }

        public string? Content { get; init; }

        public string? ExpectedContent { get; init; }
    }

    private readonly record struct PromptExecutionCheckpointContext(
        string SessionId,
        string Command,
        string Prompt,
        string? Model,
        string? SkillName,
        Action<ExecutionSessionCheckpoint> ExecutionCheckpointAppender);

    private readonly record struct ParseResult(
        bool ShowHelp,
        bool ShowVersion,
        string? AskPrompt,
        bool StartChat,
        bool RunDoctor,
        bool RunModels,
        string? SelectedModel,
        CliFriendlyError? Error,
        bool RunResume = false,
        string? ResumeSessionId = null,
        string? ConfigGetKey = null,
        string? ConfigSetKey = null,
        string? ConfigSetValue = null,
        bool RunSkills = false,
        bool RunSkillsReload = false,
        bool RunHistory = false,
        bool ClearHistory = false,
        bool RunMcpList = false,
        McpServerDefinition? McpServerToAdd = null,
        string? McpServerNameToRemove = null,
        string? McpServerNameToTest = null,
        bool RunSkillsInit = false,
        string? ShowSkillName = null,
        string? RunSkillName = null,
        string? SkillPrompt = null,
        bool RunContext = false,
        string? PatchRequestFilePath = null,
        bool PatchDryRun = false,
        bool RunAgent = false,
        string? AgentObjective = null,
        int? AgentMaxSteps = null,
        TimeSpan? AgentMaxTime = null,
        decimal? AgentMaxCost = null);
}
