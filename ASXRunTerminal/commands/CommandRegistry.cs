using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;
using ASXRunTerminal.Config;

namespace ASXRunTerminal.Commands;

/// <summary>
/// Registry for managing and routing CLI commands.
/// </summary>
internal sealed class CommandRegistry
{
    private readonly Dictionary<string, ICommand> _commands;
    private readonly ICommand _defaultCommand;

    public CommandRegistry(
        AskCommand askCommand,
        ChatCommand chatCommand,
        AgentCommand agentCommand,
        CodeReviewCommand codeReviewCommand,
        DoctorCommand doctorCommand,
        ModelsCommand modelsCommand,
        ContextCommand contextCommand,
        PatchCommand patchCommand,
        HistoryCommand historyCommand,
        ResumeCommand resumeCommand,
        McpCommand mcpCommand,
        ConfigCommand configCommand,
        SkillsCommand skillsCommand,
        SkillCommand skillCommand,
        HelpCommand helpCommand,
        VersionCommand versionCommand)
    {
        _commands = new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase)
        {
            { "ask", askCommand },
            { "chat", chatCommand },
            { "agent", agentCommand },
            { "code-review", codeReviewCommand },
            { "doctor", doctorCommand },
            { "models", modelsCommand },
            { "context", contextCommand },
            { "patch", patchCommand },
            { "history", historyCommand },
            { "resume", resumeCommand },
            { "mcp", mcpCommand },
            { "config", configCommand },
            { "skills", skillsCommand },
            { "skill", skillCommand },
            { "help", helpCommand },
            { "version", versionCommand }
        };

        // Default to chat mode when no arguments provided
        _defaultCommand = chatCommand;
    }

    /// <summary>
    /// Registers a command with the registry.
    /// </summary>
    public void RegisterCommand(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _commands[command.Name] = command;
    }

    /// <summary>
    /// Gets a command by name.
    /// </summary>
    public bool TryGetCommand(string name, out ICommand? command)
    {
        return _commands.TryGetValue(name, out command);
    }

    /// <summary>
    /// Gets all registered commands.
    /// </summary>
    public IReadOnlyDictionary<string, ICommand> GetAllCommands()
    {
        return _commands;
    }

    /// <summary>
    /// Routes the command execution based on the provided arguments.
    /// </summary>
    public async Task<int> RouteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        // Handle no arguments - default to chat mode
        if (args.Length == 0)
        {
            var defaultParseResult = _defaultCommand.ParseArguments(args);
            return await _defaultCommand.ExecuteAsync(defaultParseResult, cancellationToken);
        }

        // Handle global options (--help, --version)
        if (args.Length == 1)
        {
            if (string.Equals(args[0], "--help", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(args[0], "-h", StringComparison.OrdinalIgnoreCase))
            {
                var helpCommand = _commands["help"];
                var parseResult = helpCommand.ParseArguments(args);
                return await helpCommand.ExecuteAsync(parseResult, cancellationToken);
            }

            if (string.Equals(args[0], "--version", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(args[0], "-v", StringComparison.OrdinalIgnoreCase))
            {
                var versionCommand = _commands["version"];
                var parseResult = versionCommand.ParseArguments(args);
                return await versionCommand.ExecuteAsync(parseResult, cancellationToken);
            }
        }

        // Route to specific command
        var commandName = args[0];
        if (TryGetCommand(commandName, out var command))
        {
            var parseResult = command.ParseArguments(args);
            return await command.ExecuteAsync(parseResult, cancellationToken);
        }

        // Unknown command - show help
        ConsoleLogger.Error($"Comando desconhecido: {commandName}");
        var helpParseResult = _commands["help"].ParseArguments(["--help"]);
        return await _commands["help"].ExecuteAsync(helpParseResult, cancellationToken);
    }

    /// <summary>
    /// Creates a command registry with all default commands.
    /// </summary>
    public static CommandRegistry CreateDefault(
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationToken, Task<OllamaHealthcheckResult>> healthcheckExecutor,
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> modelsExecutor,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration,
        Action<ExecutionSessionCheckpoint> executionCheckpointAppender,
        IToolRuntime toolRuntime,
        Func<AgentAuditEntry, string> agentAuditAppender,
        Func<IReadOnlyList<PromptHistoryEntry>> historyLoader,
        Action historyClearer,
        Func<IReadOnlyList<McpServerDefinition>> mcpServersLoader,
        Action<IReadOnlyList<McpServerDefinition>> mcpServersSaver,
        Func<McpServerDefinition, CancellationToken, Task<McpServerTestResult>>? mcpServerTester = null,
        Func<UserRuntimeConfig>? configLoader = null,
        Action<UserRuntimeConfig>? configSaver = null,
        Func<IReadOnlyList<ExecutionSessionCheckpoint>>? executionCheckpointLoader = null)
    {
        ArgumentNullException.ThrowIfNull(promptExecutor);
        ArgumentNullException.ThrowIfNull(healthcheckExecutor);
        ArgumentNullException.ThrowIfNull(modelsExecutor);
        ArgumentNullException.ThrowIfNull(cancelSignalRegistration);
        ArgumentNullException.ThrowIfNull(executionCheckpointAppender);
        ArgumentNullException.ThrowIfNull(toolRuntime);
        ArgumentNullException.ThrowIfNull(agentAuditAppender);
        ArgumentNullException.ThrowIfNull(historyLoader);
        ArgumentNullException.ThrowIfNull(historyClearer);
        ArgumentNullException.ThrowIfNull(mcpServersLoader);
        ArgumentNullException.ThrowIfNull(mcpServersSaver);

        var configLoaderSafe = configLoader ?? (() => UserConfigFile.Load());
        var configSaverSafe = configSaver ?? (config => UserConfigFile.Save(config));
        var executionCheckpointLoaderSafe = executionCheckpointLoader ?? (() => ExecutionCheckpointFile.Load());

        var askCommand = new AskCommand(promptExecutor, cancelSignalRegistration, executionCheckpointAppender);
        var chatCommand = new ChatCommand(promptExecutor, modelsExecutor, toolRuntime, cancelSignalRegistration, historyLoader);
        var agentCommand = new AgentCommand(promptExecutor, cancelSignalRegistration, executionCheckpointAppender, toolRuntime, agentAuditAppender);
        var codeReviewCommand = new CodeReviewCommand();
        var doctorCommand = new DoctorCommand(healthcheckExecutor);
        var modelsCommand = new ModelsCommand(modelsExecutor);
        var contextCommand = new ContextCommand();
        var patchCommand = new PatchCommand();
        var historyCommand = new HistoryCommand(historyLoader, historyClearer);
        var resumeCommand = new ResumeCommand(executionCheckpointLoaderSafe);
        var mcpCommand = new McpCommand(mcpServersLoader, mcpServersSaver, mcpServerTester);
        var configCommand = new ConfigCommand(configLoaderSafe, configSaverSafe);
        var skillsCommand = new SkillsCommand();
        var skillCommand = new SkillCommand(promptExecutor, cancelSignalRegistration, executionCheckpointAppender);
        var helpCommand = new HelpCommand();
        var versionCommand = new VersionCommand();

        return new CommandRegistry(
            askCommand,
            chatCommand,
            agentCommand,
            codeReviewCommand,
            doctorCommand,
            modelsCommand,
            contextCommand,
            patchCommand,
            historyCommand,
            resumeCommand,
            mcpCommand,
            configCommand,
            skillsCommand,
            skillCommand,
            helpCommand,
            versionCommand);
    }
}
