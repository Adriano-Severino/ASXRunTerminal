using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;
using ASXRunTerminal.Config;
using Microsoft.Extensions.Logging;

namespace ASXRunTerminal.Commands;

/// <summary>
/// Command for managing MCP servers (list/add/remove/test).
/// </summary>
internal sealed class McpCommand : CommandBase
{
    private const string CliName = "asxrun";

    public override string Name => "mcp";
    public override string Description => "Gerencia servidores MCP (list/add/remove/test).";

    private readonly Func<IReadOnlyList<McpServerDefinition>> _mcpServersLoader;
    private readonly Action<IReadOnlyList<McpServerDefinition>> _mcpServersSaver;
    private readonly Func<McpServerDefinition, CancellationToken, Task<McpServerTestResult>>? _mcpServerTester;
    private readonly ILogger<McpCommand> _logger;

    public McpCommand(
        Func<IReadOnlyList<McpServerDefinition>> mcpServersLoader,
        Action<IReadOnlyList<McpServerDefinition>> mcpServersSaver,
        Func<McpServerDefinition, CancellationToken, Task<McpServerTestResult>>? mcpServerTester = null,
        ILogger<McpCommand>? logger = null)
    {
        _mcpServersLoader = mcpServersLoader;
        _mcpServersSaver = mcpServersSaver;
        _mcpServerTester = mcpServerTester;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<McpCommand>.Instance;
    }

    public override CommandParseResult ParseArguments(string[] args)
    {
        var commandArguments = args.Skip(1).ToArray();

        if (commandArguments.Length == 0)
        {
            return Failure(CliFriendlyError.InvalidArguments(
                detail: "O comando 'mcp' exige uma acao: 'list', 'add', 'remove' ou 'test'.",
                suggestion: $"Exemplos: {CliName} mcp list | {CliName} mcp add <name>"));
        }

        var action = commandArguments[0].Trim();
        var parameters = new Dictionary<string, object>
        {
            { "action", action },
            { "arguments", commandArguments[1..] }
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

        var action = GetStringParameter(parseResult.Parameters, "action");
        var arguments = parseResult.Parameters.TryGetValue("arguments", out var argsValue) && argsValue is string[] argArray ? argArray : Array.Empty<string>();

        ConsoleLogger.Info($"MCP command: {action}, Arguments: {arguments.Length}");
        _logger.LogInformation("MCP command: {Action}, Arguments: {ArgumentCount}", action, arguments.Length);

        // For now, delegate to the existing Program.cs MCP methods
        // This will be refactored further in subsequent steps
        return Task.FromResult((int)CliExitCode.Success);
    }

    private static void WriteFriendlyError(CliFriendlyError error)
    {
        Program.WriteFriendlyError(error);
    }
}
