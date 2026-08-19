using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;
using Microsoft.Extensions.Logging;

namespace ASXRunTerminal.Commands;

/// <summary>
/// Command for inspecting the current workspace context.
/// </summary>
internal sealed class ContextCommand : CommandBase
{
    private const string CliName = "asxrun";
    private readonly ILogger<ContextCommand> _logger;

    public ContextCommand(ILogger<ContextCommand>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ContextCommand>.Instance;
    }

    public override string Name => "context";
    public override string Description => "Inspeciona o resumo do workspace atual.";

    public override CommandParseResult ParseArguments(string[] args)
    {
        var commandArguments = args.Skip(1).ToArray();

        if (commandArguments.Length > 0)
        {
            return Failure(CliFriendlyError.InvalidArguments(
                detail: "O comando 'context' nao aceita argumentos adicionais.",
                suggestion: $"Exemplo: {CliName} context."));
        }

        return Success(new Dictionary<string, object>());
    }

    public override Task<int> ExecuteAsync(CommandParseResult parseResult, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        if (parseResult.HasError)
        {
            WriteFriendlyError(parseResult.Error ?? CliFriendlyError.Runtime("Unknown error"));
            return Task.FromResult((int)(parseResult.Error?.ExitCode ?? CliExitCode.RuntimeError));
        }

        ConsoleLogger.Info("Inspecionando contexto do workspace...");
        _logger.LogInformation("Inspecionando contexto do workspace...");

        try
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var workspaceRoot = WorkspaceRootDetector.Resolve(() => currentDirectory);

            ConsoleLogger.Success($"Diretorio atual: {currentDirectory}");
            ConsoleLogger.Success($"Raiz do workspace: {workspaceRoot.DirectoryPath}");
            ConsoleLogger.Success($"Tipo de workspace: {workspaceRoot.Kind}");
            _logger.LogInformation("Diretorio atual: {CurrentDirectory}", currentDirectory);
            _logger.LogInformation("Raiz do workspace: {WorkspaceRoot}", workspaceRoot.DirectoryPath);
            _logger.LogInformation("Tipo de workspace: {WorkspaceKind}", workspaceRoot.Kind);

            // Get file count in workspace
            var fileCount = Directory.EnumerateFiles(workspaceRoot.DirectoryPath, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true
            }).Count();

            ConsoleLogger.Success($"Total de arquivos: {fileCount}");
            _logger.LogInformation("Total de arquivos: {FileCount}", fileCount);

            return Task.FromResult((int)CliExitCode.Success);
        }
        catch (Exception ex)
        {
            var error = CliFriendlyError.Runtime($"Erro ao inspecionar contexto: {ex.Message}");
            WriteFriendlyError(error);
            return Task.FromResult((int)error.ExitCode);
        }
    }

    private static void WriteFriendlyError(CliFriendlyError error)
    {
        Program.WriteFriendlyError(error);
    }
}
