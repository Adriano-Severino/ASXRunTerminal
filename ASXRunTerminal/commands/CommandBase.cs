using ASXRunTerminal.Core;

namespace ASXRunTerminal.Commands;

/// <summary>
/// Base class for CLI commands providing common functionality.
/// </summary>
internal abstract class CommandBase : ICommand
{
    public abstract string Name { get; }
    public abstract string Description { get; }

    public abstract CommandParseResult ParseArguments(string[] args);

    public abstract Task<int> ExecuteAsync(CommandParseResult parseResult, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a successful parse result with the given parameters.
    /// </summary>
    protected CommandParseResult Success(Dictionary<string, object> parameters)
    {
        return new CommandParseResult(Name, parameters);
    }

    /// <summary>
    /// Creates a failed parse result with the given error.
    /// </summary>
    protected CommandParseResult Failure(CliFriendlyError? error)
    {
        return new CommandParseResult(Name, new Dictionary<string, object>(), error);
    }

    /// <summary>
    /// Gets a string parameter from the parameters dictionary.
    /// </summary>
    protected string? GetStringParameter(Dictionary<string, object> parameters, string key, string? defaultValue = null)
    {
        if (parameters.TryGetValue(key, out var value) && value is string stringValue)
        {
            return stringValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Gets a boolean parameter from the parameters dictionary.
    /// </summary>
    protected bool GetBoolParameter(Dictionary<string, object> parameters, string key, bool defaultValue = false)
    {
        if (parameters.TryGetValue(key, out var value) && value is bool boolValue)
        {
            return boolValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Gets an integer parameter from the parameters dictionary.
    /// </summary>
    protected int GetIntParameter(Dictionary<string, object> parameters, string key, int defaultValue = 0)
    {
        if (parameters.TryGetValue(key, out var value) && value is int intValue)
        {
            return intValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Gets a TimeSpan parameter from the parameters dictionary.
    /// </summary>
    protected TimeSpan? GetTimeSpanParameter(Dictionary<string, object> parameters, string key)
    {
        if (parameters.TryGetValue(key, out var value))
        {
            if (value is TimeSpan timeSpanValue)
            {
                return timeSpanValue;
            }
            if (value is string stringValue && TimeSpan.TryParse(stringValue, out var parsedTimeSpan))
            {
                return parsedTimeSpan;
            }
        }
        return null;
    }
}
