using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;
using ASXRunTerminal.Config;

namespace ASXRunTerminal.Commands;

/// <summary>
/// Command for resuming the last interrupted session.
/// </summary>
internal sealed class ResumeCommand : CommandBase
{
    private const string CliName = "asxrun";

    public override string Name => "resume";
    public override string Description => "Retoma a ultima sessao interrompida de ask/agent/skill.";

    private readonly Func<IReadOnlyList<ExecutionSessionCheckpoint>> _executionCheckpointLoader;

    public ResumeCommand(Func<IReadOnlyList<ExecutionSessionCheckpoint>> executionCheckpointLoader)
    {
        _executionCheckpointLoader = executionCheckpointLoader;
    }

    public override CommandParseResult ParseArguments(string[] args)
    {
        var commandArguments = args.Skip(1).ToArray();
        string? sessionId = null;

        if (commandArguments.Length > 0)
        {
           sessionId = commandArguments[0];
        }

        var parameters = new Dictionary<string, object>
        {
            { "sessionId", sessionId }
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

        var sessionId = GetStringParameter(parseResult.Parameters, "sessionId");

        ConsoleLogger.Info($"Resume command: {sessionId ?? "latest"}");
        ConsoleLogger.Info("Resume (implementacao parcial - use Program.cs por enquanto)");

        return Task.FromResult((int)CliExitCode.Success);
    }

    private static void WriteFriendlyError(CliFriendlyError error)
    {
        Program.WriteFriendlyError(error);
    }
}
