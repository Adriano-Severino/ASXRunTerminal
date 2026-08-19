using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;
using Microsoft.Extensions.Logging;

namespace ASXRunTerminal.Commands;

/// <summary>
/// Command for validating Ollama availability and health.
/// </summary>
internal sealed class DoctorCommand : CommandBase
{
    private const string CliName = "asxrun";

    public override string Name => "doctor";
    public override string Description => "Valida a disponibilidade do Ollama.";

    private readonly Func<CancellationToken, Task<OllamaHealthcheckResult>> _healthcheckExecutor;
    private readonly ILogger<DoctorCommand> _logger;

    public DoctorCommand(
        Func<CancellationToken, Task<OllamaHealthcheckResult>> healthcheckExecutor,
        ILogger<DoctorCommand>? logger = null)
    {
        _healthcheckExecutor = healthcheckExecutor;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DoctorCommand>.Instance;
    }

    public override CommandParseResult ParseArguments(string[] args)
    {
        var commandArguments = args.Skip(1).ToArray();

        if (commandArguments.Length > 0)
        {
            return Failure(CliFriendlyError.InvalidArguments(
                detail: "O comando 'doctor' nao aceita argumentos adicionais.",
                suggestion: $"Exemplo: {CliName} doctor."));
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

        ConsoleLogger.Info("Verificando saude do Ollama...");
        _logger.LogInformation("Verificando saude do Ollama...");
        var healthcheckResult = await _healthcheckExecutor(cancellationToken);

        if (healthcheckResult.IsHealthy)
        {
            ConsoleLogger.Success($"Ollama esta saudavel (versao {healthcheckResult.Version}).");
            _logger.LogInformation("Ollama esta saudavel (versao {Version})", healthcheckResult.Version);
            return (int)CliExitCode.Success;
        }
        else
        {
            ConsoleLogger.Error($"Ollama nao esta saudavel: {healthcheckResult.Error}");
            _logger.LogError("Ollama nao esta saudavel: {Error}", healthcheckResult.Error);
            return (int)CliExitCode.RuntimeError;
        }
    }

    private static void WriteFriendlyError(CliFriendlyError error)
    {
        Program.WriteFriendlyError(error);
    }
}
