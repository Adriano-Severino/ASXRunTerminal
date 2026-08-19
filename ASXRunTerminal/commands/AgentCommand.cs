using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;
using System.Globalization;

namespace ASXRunTerminal.Commands;

/// <summary>
/// Command for autonomous agent mode with goal-oriented execution.
/// </summary>
internal sealed class AgentCommand : CommandBase
{
    private const string CliName = "asxrun";
    private const string ModelFlag = "--model";
    private const string AgentMaxStepsFlag = "--max-steps";
    private const string AgentMaxTimeFlag = "--max-time";
    private const string AgentMaxCostFlag = "--max-cost";
    private const string AgentApproveSensitiveFlag = "--approve-sensitive";
    private const string AgentMaxStepsAliasFlag = "--max_steps";
    private const string AgentMaxTimeAliasFlag = "--max_time";
    private const string AgentMaxCostAliasFlag = "--max_cost";
    private const string AgentApproveSensitiveAliasFlag = "--approve_sensitive";

    public override string Name => "agent";
    public override string Description => "Inicia modo agente autonomo orientado por objetivo.";

    private readonly Func<string, string?, CancellationToken, IAsyncEnumerable<string>> _promptExecutor;
    private readonly Func<CancellationTokenSource, Action, IDisposable> _cancelSignalRegistration;
    private readonly Action<ExecutionSessionCheckpoint> _executionCheckpointAppender;
    private readonly IToolRuntime _toolRuntime;
    private readonly Func<AgentAuditEntry, string> _agentAuditAppender;

    public AgentCommand(
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration,
        Action<ExecutionSessionCheckpoint> executionCheckpointAppender,
        IToolRuntime toolRuntime,
        Func<AgentAuditEntry, string> agentAuditAppender)
    {
        _promptExecutor = promptExecutor;
        _cancelSignalRegistration = cancelSignalRegistration;
        _executionCheckpointAppender = executionCheckpointAppender;
        _toolRuntime = toolRuntime;
        _agentAuditAppender = agentAuditAppender;
    }

    public override CommandParseResult ParseArguments(string[] args)
    {
        var commandArguments = args.Skip(1).ToArray();

        // Check for benchmark subcommand
        if (commandArguments.Length > 0
            && string.Equals(commandArguments[0], "benchmark", StringComparison.OrdinalIgnoreCase))
        {
            return ParseAgentBenchmarkArguments(commandArguments[1..]);
        }

        var optionError = TryExtractAgentOptions(
            commandArguments,
            out var selectedModel,
            out var maxSteps,
            out var maxTime,
            out var maxCost,
            out var hasExplicitSensitiveOperationApproval,
            out var remainingArguments);

        if (optionError is not null)
        {
            return Failure(optionError);
        }

        if (remainingArguments.Count == 0)
        {
            return Failure(CliFriendlyError.InvalidArguments(
                detail: "Voce precisa informar um objetivo para o comando 'agent'.",
                suggestion: $"Exemplo: {CliName} agent \"seu objetivo\"."));
        }

        var objective = string.Join(' ', remainingArguments).Trim();
        if (string.IsNullOrWhiteSpace(objective))
        {
            return Failure(CliFriendlyError.InvalidArguments(
                detail: "O objetivo informado para o comando 'agent' esta vazio.",
                suggestion: $"Exemplo: {CliName} agent \"seu objetivo\"."));
        }

        var parameters = new Dictionary<string, object>
        {
            { "objective", objective },
            { "model", selectedModel },
            { "maxSteps", maxSteps },
            { "maxTime", maxTime },
            { "maxCost", maxCost },
            { "hasExplicitSensitiveOperationApproval", hasExplicitSensitiveOperationApproval },
            { "isBenchmark", false }
        };

        return Success(parameters);
    }

    public override Task<int> ExecuteAsync(CommandParseResult parseResult, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        if (parseResult.HasError)
        {
            WriteFriendlyError(parseResult.Error ?? CliFriendlyError.Runtime("Unknown error"));
            return Task.FromResult((int)(parseResult.Error?.ExitCode ?? CliExitCode.RuntimeError));
        }

        var isBenchmark = GetBoolParameter(parseResult.Parameters, "isBenchmark");

        if (isBenchmark)
        {
            return ExecuteAgentBenchmarkAsync(parseResult, cancellationToken);
        }

        // For now, delegate to the existing Program.cs ExecuteAgent method
        // This will be refactored further in subsequent steps
        var objective = GetStringParameter(parseResult.Parameters, "objective");
        var model = GetStringParameter(parseResult.Parameters, "model");
        var maxSteps = GetIntParameter(parseResult.Parameters, "maxSteps");
        var maxTime = GetTimeSpanParameter(parseResult.Parameters, "maxTime");
        var maxCost = parseResult.Parameters.TryGetValue("maxCost", out var costValue) && costValue is decimal decimalCost ? decimalCost : (decimal?)null;
        var hasExplicitSensitiveOperationApproval = GetBoolParameter(parseResult.Parameters, "hasExplicitSensitiveOperationApproval");

        ConsoleLogger.Info("Modo agente (implementacao parcial - use Program.cs por enquanto)");
        return Task.FromResult((int)CliExitCode.Success);
    }

    private static Task<int> ExecuteAgentBenchmarkAsync(CommandParseResult parseResult, CancellationToken cancellationToken)
    {
        ConsoleLogger.Info("Modo agente benchmark (implementacao parcial - use Program.cs por enquanto)");
        return Task.FromResult((int)CliExitCode.Success);
    }

    private static CommandParseResult ParseAgentBenchmarkArguments(string[] args)
    {
        // Simplified benchmark parsing for now
        var parameters = new Dictionary<string, object>
        {
            { "isBenchmark", true }
        };

        return new CommandParseResult("agent", parameters);
    }

    private CliFriendlyError? TryExtractAgentOptions(
        string[] arguments,
        out string? selectedModel,
        out int? maxSteps,
        out TimeSpan? maxTime,
        out decimal? maxCost,
        out bool hasExplicitSensitiveOperationApproval,
        out List<string> remainingArguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        selectedModel = null;
        maxSteps = null;
        maxTime = null;
        maxCost = null;
        hasExplicitSensitiveOperationApproval = false;
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
                        detail: $"A opcao '{ModelFlag}' foi informada mais de uma vez no comando '{Name}'.",
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
                        detail: $"A opcao '{ModelFlag}' foi informada mais de uma vez no comando '{Name}'.",
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

            // Handle other agent options (simplified for now)
            if (TryReadOptionValueWithAlias(
                arguments,
                ref index,
                AgentMaxStepsFlag,
                AgentMaxStepsAliasFlag,
                out var maxStepsValue,
                out var maxStepsError))
            {
                if (maxStepsError is not null)
                {
                    return maxStepsError;
                }

                if (maxSteps is not null)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{AgentMaxStepsFlag}' foi informada mais de uma vez no comando '{Name}'.",
                        suggestion: $"Exemplo: {CliName} agent {AgentMaxStepsFlag} 6 \"seu objetivo\".");
                }

                if (int.TryParse(maxStepsValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMaxSteps))
                {
                    maxSteps = parsedMaxSteps;
                }
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
                if (maxTimeError is not null)
                {
                    return maxTimeError;
                }

                if (maxTime is not null)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{AgentMaxTimeFlag}' foi informada mais de uma vez no comando '{Name}'.",
                        suggestion: $"Exemplo: {CliName} agent {AgentMaxTimeFlag} 300 \"seu objetivo\".");
                }

                if (TimeSpan.TryParse(maxTimeValue, out var parsedMaxTime))
                {
                    maxTime = parsedMaxTime;
                }
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
                if (maxCostError is not null)
                {
                    return maxCostError;
                }

                if (maxCost is not null)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{AgentMaxCostFlag}' foi informada mais de uma vez no comando '{Name}'.",
                        suggestion: $"Exemplo: {CliName} agent {AgentMaxCostFlag} 20000 \"seu objetivo\".");
                }

                if (decimal.TryParse(maxCostValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMaxCost))
                {
                    maxCost = parsedMaxCost;
                }
                continue;
            }

            if (TryReadFlagWithAlias(
                arguments,
                ref index,
                AgentApproveSensitiveFlag,
                AgentApproveSensitiveAliasFlag,
                out var approveSensitiveError))
            {
                if (approveSensitiveError is not null)
                {
                    return approveSensitiveError;
                }

                hasExplicitSensitiveOperationApproval = true;
                continue;
            }

            remainingArguments.Add(argument);
        }

        return null;
    }

    private static bool TryReadOptionValueWithAlias(
        string[] arguments,
        ref int index,
        string primaryFlag,
        string aliasFlag,
        out string? value,
        out CliFriendlyError? error)
    {
        value = null;
        error = null;

        var argument = arguments[index];
        bool isPrimaryFlag = string.Equals(argument, primaryFlag, StringComparison.OrdinalIgnoreCase);
        bool isAliasFlag = string.Equals(argument, aliasFlag, StringComparison.OrdinalIgnoreCase);

        if (!isPrimaryFlag && !isAliasFlag)
        {
            return false;
        }

        if (index + 1 >= arguments.Length)
        {
            error = CliFriendlyError.InvalidArguments(
                detail: $"A opcao '{primaryFlag}' exige um valor.",
                suggestion: $"Exemplo: {CliName} agent {primaryFlag} <valor> \"seu objetivo\".");
            return true;
        }

        value = arguments[++index].Trim();
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith('-'))
        {
            error = CliFriendlyError.InvalidArguments(
                detail: $"A opcao '{primaryFlag}' exige um valor valido.",
                suggestion: $"Exemplo: {CliName} agent {primaryFlag} <valor> \"seu objetivo\".");
            return true;
        }

        return true;
    }

    private static bool TryReadFlagWithAlias(
        string[] arguments,
        ref int index,
        string primaryFlag,
        string aliasFlag,
        out CliFriendlyError? error)
    {
        error = null;

        var argument = arguments[index];
        bool isPrimaryFlag = string.Equals(argument, primaryFlag, StringComparison.OrdinalIgnoreCase);
        bool isAliasFlag = string.Equals(argument, aliasFlag, StringComparison.OrdinalIgnoreCase);

        if (!isPrimaryFlag && !isAliasFlag)
        {
            return false;
        }

        return true;
    }

    private static void WriteFriendlyError(CliFriendlyError error)
    {
        Program.WriteFriendlyError(error);
    }
}
