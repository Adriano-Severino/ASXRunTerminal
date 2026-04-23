using ASXRunTerminal.Core;

namespace ASXRunTerminal.Infra;

internal static class ConsoleLogger
{
    private static readonly object SyncRoot = new();
    private static TerminalDesignSystem _designSystem = TerminalDesignSystem.Default;
    private static AnsiTerminalRenderer _renderer = AnsiTerminalRenderer.CreateDefault(_designSystem);

    public static TerminalDesignSystem CurrentDesignSystem
    {
        get
        {
            lock (SyncRoot)
            {
                return _designSystem;
            }
        }
    }

    public static void ConfigureTheme(TerminalThemeMode themeMode)
    {
        var designSystem = TerminalDesignSystem.Create(themeMode);

        lock (SyncRoot)
        {
            _designSystem = designSystem;
            _renderer = AnsiTerminalRenderer.CreateDefault(designSystem);
        }
    }

    public static void Info(string message)
    {
        Write(Console.Out, "INFO", message, isError: false);
    }

    public static void Error(string message)
    {
        Write(Console.Error, "ERROR", message, isError: true);
    }

    private static void Write(
        TextWriter output,
        string level,
        string message,
        bool isError)
    {
        TerminalDesignSystem designSystem;
        AnsiTerminalRenderer renderer;
        lock (SyncRoot)
        {
            designSystem = _designSystem;
            renderer = _renderer;
        }

        var style = isError ? designSystem.ErrorStyle : designSystem.InfoStyle;
        var sanitizedMessage = SecretMasker.Mask(message);
        var formattedMessage = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {sanitizedMessage}";
        output.WriteLine(renderer.Render(formattedMessage, style));
    }
}
