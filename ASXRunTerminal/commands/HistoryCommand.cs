using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;
using ASXRunTerminal.Config;

namespace ASXRunTerminal.Commands;

/// <summary>
/// Command for showing and clearing local history.
/// </summary>
internal sealed class HistoryCommand : CommandBase
{
    private const string CliName = "asxrun";

    public override string Name => "history";
    public override string Description => "Mostra e limpa historico local.";

    private readonly Func<IReadOnlyList<PromptHistoryEntry>> _historyLoader;
    private readonly Action _historyClearer;

    public HistoryCommand(
        Func<IReadOnlyList<PromptHistoryEntry>> historyLoader,
        Action historyClearer)
    {
        _historyLoader = historyLoader;
        _historyClearer = historyClearer;
    }

    public override CommandParseResult ParseArguments(string[] args)
    {
        var commandArguments = args.Skip(1).ToArray();
        var shouldClear = false;

        if (commandArguments.Length > 0)
        {
            if (string.Equals(commandArguments[0], "--clear", StringComparison.OrdinalIgnoreCase))
            {
                shouldClear = true;
            }
            else
            {
                return Failure(CliFriendlyError.InvalidArguments(
                    detail: $"Opcao desconhecida: {commandArguments[0]}",
                    suggestion: $"Opcao valida: --clear"));
            }
        }

        var parameters = new Dictionary<string, object>
        {
            { "shouldClear", shouldClear }
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

        var shouldClear = GetBoolParameter(parseResult.Parameters, "shouldClear");

        if (shouldClear)
        {
            ConsoleLogger.Info("Limpando historico local...");
            _historyClearer();
            ConsoleLogger.Success("Historico local limpo.");
        }
        else
        {
            ConsoleLogger.Info("Listando historico local...");
            var history = _historyLoader();

            if (history.Count == 0)
            {
                ConsoleLogger.Warning("Nenhum historico encontrado.");
            }
            else
            {
                ConsoleLogger.Success($"Encontrados {history.Count} entrada(s) no historico:");
                foreach (var entry in history)
                {
                    Console.WriteLine($"  [{entry.TimestampUtc:yyyy-MM-dd HH:mm:ss}] {entry.Prompt}");
                }
            }
        }

        return Task.FromResult((int)CliExitCode.Success);
    }

    private static void WriteFriendlyError(CliFriendlyError error)
    {
        Program.WriteFriendlyError(error);
    }
}
