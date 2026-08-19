using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;
using Microsoft.Extensions.Logging;

namespace ASXRunTerminal.Commands;

/// <summary>
/// Command for displaying version information.
/// </summary>
internal sealed class VersionCommand : CommandBase
{
    private const string CliName = "asxrun";
    private readonly ILogger<VersionCommand> _logger;

    public VersionCommand(ILogger<VersionCommand>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<VersionCommand>.Instance;
    }

    public override string Name => "version";
    public override string Description => "Exibe a versao do CLI.";

    public override CommandParseResult ParseArguments(string[] args)
    {
        // Version command doesn't require any arguments
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

        WriteVersion();
        return Task.FromResult((int)CliExitCode.Success);
    }

    private static void WriteVersion()
    {
        Console.WriteLine($"{CliName} {GetVersion()}");
    }

    private static string GetVersion()
    {
        return typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0";
    }

    private static void WriteFriendlyError(CliFriendlyError error)
    {
        Program.WriteFriendlyError(error);
    }
}
