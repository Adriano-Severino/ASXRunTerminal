using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;
using Microsoft.Extensions.Logging;

namespace ASXRunTerminal.Commands;

/// <summary>
/// Command for executing a prompt using a built-in skill.
/// </summary>
internal sealed class SkillCommand : CommandBase
{
    private const string CliName = "asxrun";
    private const string ModelFlag = "--model";

    public override string Name => "skill";
    public override string Description => "Executa um prompt usando uma skill padrao.";

    private readonly Func<string, string?, CancellationToken, IAsyncEnumerable<string>> _promptExecutor;
    private readonly Func<CancellationTokenSource, Action, IDisposable> _cancelSignalRegistration;
    private readonly Action<ExecutionSessionCheckpoint> _executionCheckpointAppender;
    private readonly ILogger<SkillCommand> _logger;

    public SkillCommand(
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration,
        Action<ExecutionSessionCheckpoint> executionCheckpointAppender,
        ILogger<SkillCommand>? logger = null)
    {
        _promptExecutor = promptExecutor;
        _cancelSignalRegistration = cancelSignalRegistration;
        _executionCheckpointAppender = executionCheckpointAppender;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SkillCommand>.Instance;
    }

    public override CommandParseResult ParseArguments(string[] args)
    {
        var commandArguments = args.Skip(1).ToArray();

        if (commandArguments.Length == 0)
        {
            return Failure(CliFriendlyError.InvalidArguments(
                detail: "O comando 'skill' exige um nome de skill.",
                suggestion: $"Exemplo: {CliName} skill code-review \"seu prompt\"."));
        }

        var skillName = commandArguments[0].Trim();
        var optionError = TryExtractModelOption(
            commandArguments[1..],
            out var selectedModel,
            out var remainingArguments);

        if (optionError is not null)
        {
            return Failure(optionError);
        }

        if (remainingArguments.Count == 0)
        {
            return Failure(CliFriendlyError.InvalidArguments(
                detail: "O comando 'skill' exige um prompt.",
                suggestion: $"Exemplo: {CliName} skill {skillName} \"seu prompt\"."));
        }

        var prompt = string.Join(' ', remainingArguments).Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Failure(CliFriendlyError.InvalidArguments(
                detail: "O prompt informado para o comando 'skill' esta vazio.",
                suggestion: $"Exemplo: {CliName} skill {skillName} \"seu prompt\"."));
        }

        var parameters = new Dictionary<string, object>
        {
            { "skillName", skillName },
            { "prompt", prompt },
            { "model", selectedModel }
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

        var skillName = GetStringParameter(parseResult.Parameters, "skillName") ?? string.Empty;
        var prompt = GetStringParameter(parseResult.Parameters, "prompt") ?? string.Empty;
        var model = GetStringParameter(parseResult.Parameters, "model") ?? string.Empty;

        ConsoleLogger.Info($"Skill command: {skillName}, Prompt: {prompt}, Model: {model}");
        _logger.LogInformation("Skill command: {SkillName}, Prompt: {Prompt}, Model: {Model}", skillName, prompt, model);
        ConsoleLogger.Info("Skill (implementacao parcial - use Program.cs por enquanto)");
        _logger.LogInformation("Skill (implementacao parcial - use Program.cs por enquanto)");

        // For now, delegate to the existing Program.cs skill methods
        // This will be refactored further in subsequent steps
        return Task.FromResult((int)CliExitCode.Success);
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
                        suggestion: $"Exemplo: {CliName} skill code-review --model {OllamaModelDefaults.DefaultModel} \"seu prompt\".");
                }

                if (index + 1 >= arguments.Length)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{ModelFlag}' exige um valor.",
                        suggestion: $"Exemplo: {CliName} skill code-review --model {OllamaModelDefaults.DefaultModel} \"seu prompt\".");
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
                        suggestion: $"Exemplo: {CliName} skill code-review --model {OllamaModelDefaults.DefaultModel} \"seu prompt\".");
                }

                var candidate = argument[(ModelFlag.Length + 1)..].Trim();
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{ModelFlag}' exige um valor.",
                        suggestion: $"Exemplo: {CliName} skill code-review --model {OllamaModelDefaults.DefaultModel} \"seu prompt\".");
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

    private static void WriteFriendlyError(CliFriendlyError error)
    {
        Program.WriteFriendlyError(error);
    }
}
