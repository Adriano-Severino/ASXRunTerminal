using ASXRunTerminal.Core;

namespace ASXRunTerminal.Commands;

/// <summary>
/// Interface for CLI commands in the ASXRunTerminal.
/// </summary>
internal interface ICommand
{
    /// <summary>
    /// Gets the name of the command.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the description of the command.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Parses the command arguments and returns a parse result.
    /// </summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>A parse result containing the parsed command data or an error.</returns>
    CommandParseResult ParseArguments(string[] args);

    /// <summary>
    /// Executes the command with the given parse result.
    /// </summary>
    /// <param name="parseResult">The parse result from ParseArguments.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The exit code of the command execution.</returns>
    Task<int> ExecuteAsync(CommandParseResult parseResult, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of parsing command arguments.
/// </summary>
internal sealed class CommandParseResult
{
    public CommandParseResult(
        string commandName,
        Dictionary<string, object> parameters,
        CliFriendlyError? error = null)
    {
        CommandName = commandName;
        Parameters = parameters;
        Error = error;
    }

    public string CommandName { get; }
    public Dictionary<string, object> Parameters { get; }
    public CliFriendlyError? Error { get; }
    public bool HasError => Error is not null;
}
