namespace ASXRunTerminal.Config;

/// <summary>
/// Configuration for structured logging in the ASXRunTerminal application.
/// </summary>
public sealed class LoggingConfiguration
{
    /// <summary>
    /// Gets or sets the minimum log level to capture.
    /// </summary>
    public Microsoft.Extensions.Logging.LogLevel MinimumLevel { get; set; } = Microsoft.Extensions.Logging.LogLevel.Information;

    /// <summary>
    /// Gets or sets whether to log to console.
    /// </summary>
    public bool LogToConsole { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include timestamps in log output.
    /// </summary>
    public bool IncludeTimestamps { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include correlation IDs for request tracking.
    /// </summary>
    public bool IncludeCorrelationIds { get; set; } = true;

    /// <summary>
    /// Gets or sets the log output format.
    /// </summary>
    public LogOutputFormat OutputFormat { get; set; } = LogOutputFormat.Structured;

    /// <summary>
    /// Gets or sets the file path for log file output (optional).
    /// </summary>
    public string? LogFilePath { get; set; }

    /// <summary>
    /// Gets or sets specific category log levels for fine-grained control.
    /// </summary>
    public Dictionary<string, Microsoft.Extensions.Logging.LogLevel> CategoryLogLevels { get; set; } = new();

    /// <summary>
    /// Creates a default logging configuration.
    /// </summary>
    public static LoggingConfiguration Default => new();
}

/// <summary>
/// Log output format options.
/// </summary>
public enum LogOutputFormat
{
    /// <summary>
    /// Structured JSON-like format.
    /// </summary>
    Structured,

    /// <summary>
    /// Simple text format.
    /// </summary>
    Simple,

    /// <summary>
    /// Compact single-line format.
    /// </summary>
    Compact
}
