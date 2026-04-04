using ASXRunTerminal.Config;
using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ASXRunTerminal;

internal static class Program
{
    private const string CliName = "asxrun";
    private const string ModelFlag = "--model";
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
    private static readonly Func<CancellationTokenSource, Action, IDisposable> DefaultCancelSignalRegistration =
        static (cancellationTokenSource, onCancellationRequested) =>
            RegisterConsoleCancelHandler(cancellationTokenSource, onCancellationRequested);
    private static readonly Action DefaultUserConfigInitializer =
        static () => _ = UserConfigFile.EnsureExists();
    private static readonly Action NoOpUserConfigInitializer = static () => { };

    public static int Main(string[] args)
    {
        return Run(
            args,
            DefaultPromptExecutor,
            DefaultHealthcheckExecutor,
            DefaultModelsExecutor,
            DefaultCancelSignalRegistration,
            DefaultUserConfigInitializer,
            applyConfiguredTheme: true);
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
        Action? historyClearer = null)
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

        try
        {
            userConfigInitializer();

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

            if (parseResult.AskPrompt is not null)
            {
                return ExecuteAsk(
                    parseResult.AskPrompt,
                    parseResult.SelectedModel,
                    promptExecutor,
                    cancelSignalRegistration);
            }

            if (parseResult.StartChat)
            {
                return ExecuteChat(parseResult.SelectedModel, promptExecutor, cancelSignalRegistration);
            }

            if (parseResult.RunDoctor)
            {
                return ExecuteDoctor(healthcheckExecutor);
            }

            if (parseResult.RunModels)
            {
                return ExecuteModels(modelsExecutor);
            }

            if (parseResult.RunHistory)
            {
                return ExecuteHistory(
                    historyLoader,
                    historyClearer,
                    parseResult.ClearHistory);
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

            if (parseResult.ShowSkillName is not null)
            {
                return ExecuteShowSkill(parseResult.ShowSkillName);
            }

            if (parseResult.SkillPrompt is not null && parseResult.RunSkillName is not null)
            {
                return ExecuteSkill(
                    parseResult.RunSkillName,
                    parseResult.SkillPrompt,
                    parseResult.SelectedModel,
                    promptExecutor,
                    cancelSignalRegistration);
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

        if (args.Length > 0 && string.Equals(args[0], "config", StringComparison.OrdinalIgnoreCase))
        {
            return ParseConfigArguments(args);
        }

        if (args.Length > 0 && string.Equals(args[0], "history", StringComparison.OrdinalIgnoreCase))
        {
            return ParseHistoryArguments(args);
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
                detail: "O comando 'skills' aceita apenas 'skills' ou 'skills show <nome>'.",
                suggestion: $"Exemplo: {CliName} skills show code-review."));
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
        Console.WriteLine($"  {CliName} chat [--model <modelo>]");
        Console.WriteLine($"  {CliName} doctor");
        Console.WriteLine($"  {CliName} models");
        Console.WriteLine($"  {CliName} history [--clear]");
        Console.WriteLine($"  {CliName} config get <chave>");
        Console.WriteLine($"  {CliName} config set <chave> <valor>");
        Console.WriteLine($"  {CliName} skills");
        Console.WriteLine($"  {CliName} skills show <nome>");
        Console.WriteLine($"  {CliName} skill <nome> [--model <modelo>] \"prompt\"");
        Console.WriteLine();
        Console.WriteLine("Opcoes:");
        Console.WriteLine("  -h, --help       Exibe ajuda.");
        Console.WriteLine("  -v, --version    Exibe a versao.");
        Console.WriteLine($"  --model <nome>   Seleciona o modelo Ollama para 'ask' e 'chat' (padrao: {OllamaModelDefaults.DefaultModel}).");
        Console.WriteLine($"  {OllamaModelDefaults.DefaultModelEnvironmentVariable}=<nome>");
        Console.WriteLine("                   Sobrescreve o modelo padrao quando --model nao e informado.");
        Console.WriteLine();
        Console.WriteLine("Comandos:");
        Console.WriteLine("  ask \"prompt\"    Executa um prompt unico.");
        Console.WriteLine("  chat             Inicia o modo interativo.");
        Console.WriteLine("  doctor           Valida a disponibilidade do Ollama.");
        Console.WriteLine("  models           Lista os modelos locais do Ollama.");
        Console.WriteLine("  history          Exibe ou limpa o historico local de prompts.");
        Console.WriteLine("  config           Le e atualiza configuracoes locais do usuario.");
        Console.WriteLine($"                   Chaves suportadas: {GetSupportedConfigKeysLabel()}.");
        Console.WriteLine("  skills           Lista as skills padrao disponiveis.");
        Console.WriteLine("  skills show      Exibe os detalhes de uma skill.");
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
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration)
    {
        ConsoleLogger.Info("Executando comando unico 'ask'.");
        var wasCancelled = ExecutePrompt(prompt, model, promptExecutor, cancelSignalRegistration);
        return wasCancelled
            ? (int)CliExitCode.Cancelled
            : (int)CliExitCode.Success;
    }

    private static int ExecuteChat(
        string? model,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration)
    {
        ConsoleLogger.Info("Modo interativo iniciado. Digite 'exit' para sair.");

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();

            if (input is null || IsExitCommand(input))
            {
                ConsoleLogger.Info("Modo interativo encerrado.");
                return (int)CliExitCode.Success;
            }

            var prompt = input.Trim();
            if (prompt.Length == 0)
            {
                continue;
            }

            var wasCancelled = ExecutePrompt(prompt, model, promptExecutor, cancelSignalRegistration);
            if (wasCancelled)
            {
                ConsoleLogger.Info("Prompt cancelado. Digite outro prompt ou 'exit' para sair.");
            }
        }

    }

    private static bool ExecutePrompt(
        string prompt,
        string? model,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        using var cancelRegistration = cancelSignalRegistration(
            cancellationTokenSource,
            static () => ConsoleLogger.Info(
                "Cancelamento solicitado via Ctrl+C. Interrompendo prompt em execucao."));

        WriteExecutionState(ExecutionState.Connecting);

        try
        {
            WriteExecutionState(ExecutionState.Processing);
            StreamPromptResponseAsync(prompt, model, promptExecutor, cancellationTokenSource.Token)
                .GetAwaiter()
                .GetResult();
            WriteExecutionState(ExecutionState.Completed);
            return false;
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
            WriteExecutionState(ExecutionState.Error, "Execucao cancelada pelo usuario.");
            return true;
        }
        catch (Exception ex)
        {
            WriteExecutionState(ExecutionState.Error, $"Nao foi possivel executar o prompt: {ex.Message}");
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
        ConsoleLogger.Info("Listando skills padrao disponiveis.");
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
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration)
    {
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
        var wasCancelled = ExecutePrompt(promptWithSkill, model, promptExecutor, cancelSignalRegistration);
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

    private static async Task StreamPromptResponseAsync(
        string prompt,
        string? model,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        CancellationToken cancellationToken)
    {
        var shouldWriteNewLine = false;
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

    private sealed class DisposableAction(Action dispose)
        : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }
    }

    private readonly record struct ParseResult(
        bool ShowHelp,
        bool ShowVersion,
        string? AskPrompt,
        bool StartChat,
        bool RunDoctor,
        bool RunModels,
        string? SelectedModel,
        CliFriendlyError? Error,
        string? ConfigGetKey = null,
        string? ConfigSetKey = null,
        string? ConfigSetValue = null,
        bool RunSkills = false,
        bool RunHistory = false,
        bool ClearHistory = false,
        string? ShowSkillName = null,
        string? RunSkillName = null,
        string? SkillPrompt = null);
}
