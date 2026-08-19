using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;
using Microsoft.Extensions.Logging;

namespace ASXRunTerminal.Commands;

/// <summary>
/// Command for applying file changes via JSON with diff display.
/// </summary>
internal sealed class PatchCommand : CommandBase
{
    private const string CliName = "asxrun";
    private readonly ILogger<PatchCommand> _logger;

    public PatchCommand(ILogger<PatchCommand>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PatchCommand>.Instance;
    }

    public override string Name => "patch";
    public override string Description => "Aplica mudancas de arquivo por JSON e exibe diff unificado.";

    public override CommandParseResult ParseArguments(string[] args)
    {
        var commandArguments = args.Skip(1).ToArray();
        var dryRun = false;
        string? patchRequestFilePath = null;

        for (var index = 0; index < commandArguments.Length; index++)
        {
            var argument = commandArguments[index];

            if (string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                dryRun = true;
            }
            else if (argument.StartsWith("--", StringComparison.OrdinalIgnoreCase))
            {
                return Failure(CliFriendlyError.InvalidArguments(
                    detail: $"Opcao desconhecida: {argument}",
                    suggestion: $"Opcao valida: --dry-run"));
            }
            else if (patchRequestFilePath is null)
            {
                patchRequestFilePath = argument;
            }
            else
            {
                return Failure(CliFriendlyError.InvalidArguments(
                    detail: "O comando 'patch' aceita apenas um arquivo JSON.",
                    suggestion: $"Exemplo: {CliName} patch patch.json"));
            }
        }

        if (patchRequestFilePath is null)
        {
            return Failure(CliFriendlyError.InvalidArguments(
                detail: "O comando 'patch' exige um arquivo JSON.",
                suggestion: $"Exemplo: {CliName} patch patch.json"));
        }

        var parameters = new Dictionary<string, object>
        {
            { "patchFilePath", patchRequestFilePath },
            { "dryRun", dryRun }
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

        var patchFilePath = GetStringParameter(parseResult.Parameters, "patchFilePath");
        var dryRun = GetBoolParameter(parseResult.Parameters, "dryRun");

        ConsoleLogger.Info($"Patch command: {patchFilePath}, Dry-run: {dryRun}");
        _logger.LogInformation("Patch command: {PatchFilePath}, Dry-run: {DryRun}", patchFilePath, dryRun);
        ConsoleLogger.Info("Patch (implementacao parcial - use Program.cs por enquanto)");
        _logger.LogInformation("Patch (implementacao parcial - use Program.cs por enquanto)");

        return Task.FromResult((int)CliExitCode.Success);
    }

    private static void WriteFriendlyError(CliFriendlyError error)
    {
        Program.WriteFriendlyError(error);
    }
}
