using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;
using ASXRunTerminal.Config;

namespace ASXRunTerminal.Commands;

/// <summary>
/// Command for executing a single prompt with the AI model.
/// </summary>
internal sealed class AskCommand : CommandBase
{
    private const string CliName = "asxrun";
    private const string ModelFlag = "--model";

    public override string Name => "ask";
    public override string Description => "Executa um prompt único com streaming de resposta.";

    private readonly Func<string, string?, CancellationToken, IAsyncEnumerable<string>> _promptExecutor;
    private readonly Func<CancellationTokenSource, Action, IDisposable> _cancelSignalRegistration;
    private readonly Action<ExecutionSessionCheckpoint> _executionCheckpointAppender;

    public AskCommand(
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration,
        Action<ExecutionSessionCheckpoint> executionCheckpointAppender)
    {
        _promptExecutor = promptExecutor;
        _cancelSignalRegistration = cancelSignalRegistration;
        _executionCheckpointAppender = executionCheckpointAppender;
    }

    public override CommandParseResult ParseArguments(string[] args)
    {
        var commandArguments = args.Skip(1).ToArray();
        var optionError = TryExtractModelOption(
            commandArguments,
            out var selectedModel,
            out var remainingArguments);

        if (optionError is not null)
        {
            return Failure(optionError);
        }

        if (remainingArguments.Count == 0)
        {
            return Failure(CliFriendlyError.InvalidArguments(
                detail: "Voce precisa informar um prompt para o comando 'ask'.",
                suggestion: $"Exemplo: {CliName} ask \"seu prompt\"."));
        }

        var prompt = string.Join(' ', remainingArguments).Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Failure(CliFriendlyError.InvalidArguments(
                detail: "O prompt informado para o comando 'ask' esta vazio.",
                suggestion: $"Exemplo: {CliName} ask \"seu prompt\"."));
        }

        var parameters = new Dictionary<string, object>
        {
            { "prompt", prompt },
            { "model", selectedModel ?? (object?)null }
        };

        return Success(parameters);
    }

    public override async Task<int> ExecuteAsync(CommandParseResult parseResult, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        if (parseResult.HasError)
        {
            WriteFriendlyError(parseResult.Error ?? CliFriendlyError.Runtime("Unknown error"));
            return (int)(parseResult.Error?.ExitCode ?? CliExitCode.RuntimeError);
        }

        var prompt = GetStringParameter(parseResult.Parameters, "prompt");
        var model = GetStringParameter(parseResult.Parameters, "model");

        ArgumentNullException.ThrowIfNull(prompt);

        ConsoleLogger.Info("Executando comando unico 'ask'.");
        var checkpointContext = CreatePromptCheckpointContext(
            command: "ask",
            prompt: prompt,
            model: model,
            skillName: null,
            executionCheckpointAppender: _executionCheckpointAppender);

        var wasCancelled = await ExecutePromptAsync(
            prompt,
            model,
            _promptExecutor,
            _cancelSignalRegistration,
            checkpointContext,
            cancellationToken);

        return wasCancelled
            ? (int)CliExitCode.Cancelled
            : (int)CliExitCode.Success;
    }

    private CliFriendlyError? TryExtractModelOption(
        string[] arguments,
        out string? selectedModel,
        out List<string> remainingArguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        selectedModel = null;
        remainingArguments = [];

        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index].Trim();

            if (string.Equals(argument, ModelFlag, StringComparison.OrdinalIgnoreCase))
            {
                if (selectedModel is not null)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{ModelFlag}' foi informada mais de uma vez no comando '{Name}'.",
                        suggestion: $"Exemplo: {CliName} ask --model {OllamaModelDefaults.DefaultModel} \"seu prompt\".");
                }

                if (index + 1 >= arguments.Length)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{ModelFlag}' exige um valor.",
                        suggestion: $"Exemplo: {CliName} ask --model {OllamaModelDefaults.DefaultModel} \"seu prompt\".");
                }

                selectedModel = arguments[index + 1].Trim();
                index++; // Skip the next argument as it's the value
            }
            else if (argument.StartsWith($"{ModelFlag}=", StringComparison.OrdinalIgnoreCase))
            {
                if (selectedModel is not null)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{ModelFlag}' foi informada mais de uma vez no comando '{Name}'.",
                        suggestion: $"Exemplo: {CliName} ask --model {OllamaModelDefaults.DefaultModel} \"seu prompt\".");
                }

                var candidate = argument[(ModelFlag.Length + 1)..].Trim();
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{ModelFlag}' exige um valor.",
                        suggestion: $"Exemplo: {CliName} ask --model {OllamaModelDefaults.DefaultModel} \"seu prompt\".");
                }

                selectedModel = candidate;
            }
            else
            {
                remainingArguments.Add(argument);
            }
        }

        return null;
    }

    private static PromptCheckpointContext CreatePromptCheckpointContext(
        string command,
        string prompt,
        string? model,
        string? skillName,
        Action<ExecutionSessionCheckpoint> executionCheckpointAppender)
    {
        ArgumentNullException.ThrowIfNull(executionCheckpointAppender);

        var sessionId = Guid.NewGuid().ToString("N");
        var stage = "prompt-execution";
        var checkpoint = new ExecutionSessionCheckpoint(
            TimestampUtc: DateTimeOffset.UtcNow,
            SessionId: sessionId,
            Command: command,
            Stage: stage,
            Status: ExecutionCheckpointStatus.InProgress,
            Prompt: prompt,
            Model: model,
            SkillName: skillName,
            Detail: string.Empty);

        executionCheckpointAppender(checkpoint);
        return new PromptCheckpointContext(sessionId, stage);
    }

    private static async Task<bool> ExecutePromptAsync(
        string prompt,
        string? model,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration,
        PromptCheckpointContext checkpointContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(promptExecutor);
        ArgumentNullException.ThrowIfNull(cancelSignalRegistration);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var wasCancelled = false;

        using var _ = cancelSignalRegistration(cts, () =>
        {
            wasCancelled = true;
            ConsoleLogger.Info("Execucao cancelada pelo usuario.");
        });

        try
        {
            await foreach (var chunk in promptExecutor(prompt, model, cts.Token)
                .WithCancellation(cts.Token)
                .ConfigureAwait(false))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                Console.Write(chunk);
            }

            Console.WriteLine();
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            ConsoleLogger.Info("Execucao cancelada pelo usuario.");
            wasCancelled = true;
        }

        return wasCancelled;
    }

    private static void WriteFriendlyError(CliFriendlyError error)
    {
        Program.WriteFriendlyError(error);
    }

    private record PromptCheckpointContext(string SessionId, string Stage);
}
