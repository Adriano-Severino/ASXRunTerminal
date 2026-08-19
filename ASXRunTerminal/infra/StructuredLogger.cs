using ASXRunTerminal.Config;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ASXRunTerminal.Infra;

/// <summary>
/// Structured logger implementation for ASXRunTerminal with correlation ID support.
/// </summary>
internal sealed class StructuredLogger : ILogger
{
    private readonly string _categoryName;
    private readonly LoggingConfiguration _configuration;
    private readonly string? _correlationId;

    public StructuredLogger(
        string categoryName,
        LoggingConfiguration configuration,
        string? correlationId = null)
    {
        _categoryName = categoryName;
        _configuration = configuration;
        _correlationId = correlationId;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return new Scope(state?.ToString());
    }

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        return logLevel >= _configuration.MinimumLevel;
    }

    public void Log<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var timestamp = _configuration.IncludeTimestamps ? DateTime.UtcNow.ToString("o") : null;
        var correlationId = _configuration.IncludeCorrelationIds ? _correlationId : null;

        var logEntry = new LogEntry
        {
            Timestamp = timestamp,
            Level = logLevel.ToString(),
            Category = _categoryName,
            EventId = eventId.Id,
            Message = message,
            Exception = exception?.Message,
            CorrelationId = correlationId,
            State = state?.ToString()
        };

        WriteLogEntry(logEntry, _configuration.OutputFormat);
    }

    private void WriteLogEntry(LogEntry entry, LogOutputFormat format)
    {
        var output = format switch
        {
            LogOutputFormat.Structured => FormatStructured(entry),
            LogOutputFormat.Simple => FormatSimple(entry),
            LogOutputFormat.Compact => FormatCompact(entry),
            _ => FormatStructured(entry)
        };

        if (_configuration.LogToConsole)
        {
            Console.WriteLine(output);
        }

        if (!string.IsNullOrEmpty(_configuration.LogFilePath))
        {
            try
            {
                File.AppendAllText(_configuration.LogFilePath, output + Environment.NewLine);
            }
            catch
            {
                // Silently fail if file logging fails to avoid disrupting application
            }
        }
    }

    private string FormatStructured(LogEntry entry)
    {
        var parts = new List<string>();
        if (entry.Timestamp != null) parts.Add($"\"timestamp\":\"{entry.Timestamp}\"");
        parts.Add($"\"level\":\"{entry.Level}\"");
        parts.Add($"\"category\":\"{entry.Category}\"");
        if (entry.EventId > 0) parts.Add($"\"eventId\":{entry.EventId}");
        parts.Add($"\"message\":\"{EscapeJson(entry.Message)}\"");
        if (entry.Exception != null) parts.Add($"\"exception\":\"{EscapeJson(entry.Exception)}\"");
        if (entry.CorrelationId != null) parts.Add($"\"correlationId\":\"{entry.CorrelationId}\"");
        if (entry.State != null) parts.Add($"\"state\":\"{EscapeJson(entry.State)}\"");

        return $"{{{string.Join(",", parts)}}}";
    }

    private string FormatSimple(LogEntry entry)
    {
        var parts = new List<string>();
        if (entry.Timestamp != null) parts.Add($"[{entry.Timestamp}]");
        parts.Add($"[{entry.Level}]");
        parts.Add($"[{entry.Category}]");
        if (entry.EventId > 0) parts.Add($"[Event:{entry.EventId}]");
        if (entry.CorrelationId != null) parts.Add($"[Corr:{entry.CorrelationId}]");
        parts.Add(entry.Message);
        if (entry.Exception != null) parts.Add($"Exception: {entry.Exception}");

        return string.Join(" ", parts);
    }

    private string FormatCompact(LogEntry entry)
    {
        var parts = new List<string>();
        if (entry.Timestamp != null) parts.Add(entry.Timestamp);
        parts.Add($"{entry.Level.Substring(0, 1).ToUpper()}");
        parts.Add(entry.Message);

        return string.Join(" | ", parts);
    }

    private string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private sealed class Scope : IDisposable
    {
        private readonly string? _scope;
        public Scope(string? scope) => _scope = scope;
        public void Dispose() { /* Scope cleanup if needed */ }
    }

    private sealed class LogEntry
    {
        public string? Timestamp { get; set; }
        public string? Level { get; set; }
        public string? Category { get; set; }
        public int EventId { get; set; }
        public string? Message { get; set; }
        public string? Exception { get; set; }
        public string? CorrelationId { get; set; }
        public string? State { get; set; }
    }
}

/// <summary>
/// Logger provider for creating structured loggers.
/// </summary>
internal sealed class StructuredLoggerProvider : ILoggerProvider
{
    private readonly LoggingConfiguration _configuration;
    private readonly string? _correlationId;
    private readonly List<StructuredLogger> _loggers = new();

    public StructuredLoggerProvider(
        LoggingConfiguration configuration,
        string? correlationId = null)
    {
        _configuration = configuration;
        _correlationId = correlationId;
    }

    public ILogger CreateLogger(string categoryName)
    {
        var logger = new StructuredLogger(categoryName, _configuration, _correlationId);
        _loggers.Add(logger);
        return logger;
    }

    public void Dispose()
    {
        _loggers.Clear();
    }
}
