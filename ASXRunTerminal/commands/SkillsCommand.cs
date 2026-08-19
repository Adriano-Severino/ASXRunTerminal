using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;
using Microsoft.Extensions.Logging;

namespace ASXRunTerminal.Commands;

/// <summary>
/// Command for listing available skills.
/// </summary>
internal sealed class SkillsCommand : CommandBase
{
    private const string CliName = "asxrun";
    private readonly ILogger<SkillsCommand> _logger;

    public SkillsCommand(ILogger<SkillsCommand>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SkillsCommand>.Instance;
    }

    public override string Name => "skills";
    public override string Description => "Lista as skills disponiveis.";

    public override CommandParseResult ParseArguments(string[] args)
    {
        var commandArguments = args.Skip(1).ToArray();

        if (commandArguments.Length == 0)
        {
            var listParams = new Dictionary<string, object>
            {
                { "action", "list" },
                { "arguments", Array.Empty<string>() }
            };

            return Success(listParams);
        }

        var action = commandArguments[0].Trim();
        var actionParams = new Dictionary<string, object>
        {
            { "action", action },
            { "arguments", commandArguments[1..] }
        };

        return Success(actionParams);
    }

    public override Task<int> ExecuteAsync(CommandParseResult parseResult, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        if (parseResult.HasError)
        {
            WriteFriendlyError(parseResult.Error ?? CliFriendlyError.Runtime("Unknown error"));
            return Task.FromResult((int)(parseResult.Error?.ExitCode ?? CliExitCode.RuntimeError));
        }

        var action = GetStringParameter(parseResult.Parameters, "action");
        var arguments = parseResult.Parameters.TryGetValue("arguments", out var argsValue) && argsValue is string[] argArray ? argArray : Array.Empty<string>();

        ConsoleLogger.Info($"Skills command: {action}, Arguments: {arguments.Length}");
        _logger.LogInformation("Skills command: {Action}, Arguments: {ArgumentCount}", action, arguments.Length);
        ConsoleLogger.Info("Skills (implementacao parcial - use Program.cs por enquanto)");
        _logger.LogInformation("Skills (implementacao parcial - use Program.cs por enquanto)");

        // For now, delegate to the existing Program.cs skills methods
        // This will be refactored further in subsequent steps
        return Task.FromResult((int)CliExitCode.Success);
    }

    private static void WriteFriendlyError(CliFriendlyError error)
    {
        Program.WriteFriendlyError(error);
    }
}
