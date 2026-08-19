using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;

namespace ASXRunTerminal.Commands;

/// <summary>
/// Command for listing local Ollama models.
/// </summary>
internal sealed class ModelsCommand : CommandBase
{
    private const string CliName = "asxrun";

    public override string Name => "models";
    public override string Description => "Lista os modelos locais do Ollama.";

    private readonly Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> _modelsExecutor;

    public ModelsCommand(Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> modelsExecutor)
    {
        _modelsExecutor = modelsExecutor;
    }

    public override CommandParseResult ParseArguments(string[] args)
    {
        var commandArguments = args.Skip(1).ToArray();

        if (commandArguments.Length > 0)
        {
            return Failure(CliFriendlyError.InvalidArguments(
                detail: "O comando 'models' nao aceita argumentos adicionais.",
                suggestion: $"Exemplo: {CliName} models."));
        }

        return Success(new Dictionary<string, object>());
    }

    public override async Task<int> ExecuteAsync(CommandParseResult parseResult, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        if (parseResult.HasError)
        {
            WriteFriendlyError(parseResult.Error ?? CliFriendlyError.Runtime("Unknown error"));
            return (int)(parseResult.Error?.ExitCode ?? CliExitCode.RuntimeError);
        }

        ConsoleLogger.Info("Listando modelos locais do Ollama...");
        var models = await _modelsExecutor(cancellationToken);

        if (models.Count == 0)
        {
            ConsoleLogger.Warning("Nenhum modelo local encontrado. Use 'ollama pull <modelo>' para baixar um modelo.");
            return (int)CliExitCode.Success;
        }

        ConsoleLogger.Success($"Encontrados {models.Count} modelo(s) local(is):");
        foreach (var model in models)
        {
            Console.WriteLine($"  - {model.Name}");
        }

        return (int)CliExitCode.Success;
    }

    private static void WriteFriendlyError(CliFriendlyError error)
    {
        Program.WriteFriendlyError(error);
    }
}
