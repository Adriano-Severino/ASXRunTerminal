using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;
using ASXRunTerminal.Config;

namespace ASXRunTerminal.Commands;

/// <summary>
/// Command for reading and updating user configuration.
/// </summary>
internal sealed class ConfigCommand : CommandBase
{
    private const string CliName = "asxrun";

    public override string Name => "config";
    public override string Description => "Le e atualiza configuracao do usuario.";

    private readonly Func<UserRuntimeConfig> _configLoader;
    private readonly Action<UserRuntimeConfig> _configSaver;

    public ConfigCommand(
        Func<UserRuntimeConfig> configLoader,
        Action<UserRuntimeConfig> configSaver)
    {
        _configLoader = configLoader;
        _configSaver = configSaver;
    }

    public override CommandParseResult ParseArguments(string[] args)
    {
        var commandArguments = args.Skip(1).ToArray();

        if (commandArguments.Length < 2)
        {
            return Failure(CliFriendlyError.InvalidArguments(
                detail: "O comando 'config' exige uma acao: 'set' ou 'get'.",
                suggestion: $"Exemplos: {CliName} config get {UserConfigFile.DefaultModelKey} | {CliName} config set {UserConfigFile.DefaultModelKey} {OllamaModelDefaults.DefaultModel}."));
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

        ConsoleLogger.Info($"Config command: {action}, Arguments: {arguments.Length}");
        ConsoleLogger.Info("Config (implementacao parcial - use Program.cs por enquanto)");

        // For now, delegate to the existing Program.cs config methods
        // This will be refactored further in subsequent steps
        return Task.FromResult((int)CliExitCode.Success);
    }

    private static void WriteFriendlyError(CliFriendlyError error)
    {
        Program.WriteFriendlyError(error);
    }
}
