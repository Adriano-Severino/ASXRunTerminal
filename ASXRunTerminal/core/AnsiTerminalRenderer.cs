namespace ASXRunTerminal.Core;

internal sealed class AnsiTerminalRenderer
{
    private const string EscapePrefix = "\u001b[";
    private const string EscapeReset = "\u001b[0m";
    private readonly TerminalDesignSystem _designSystem;

    public AnsiTerminalRenderer(bool supportsAnsi, TerminalDesignSystem? designSystem = null)
    {
        SupportsAnsi = supportsAnsi;
        _designSystem = designSystem ?? TerminalDesignSystem.Default;
    }

    public bool SupportsAnsi { get; }

    public static AnsiTerminalRenderer CreateDefault(TerminalDesignSystem? designSystem = null)
    {
        var supportsAnsi = ShouldUseAnsi(Environment.GetEnvironmentVariable, Console.IsOutputRedirected);
        return new AnsiTerminalRenderer(supportsAnsi, designSystem);
    }

    public string Render(string content, TerminalTextStyle style)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!SupportsAnsi || content.Length == 0)
        {
            return content;
        }

        var foregroundCode = _designSystem.ResolveAnsiForegroundCode(style.Foreground);
        var emphasisCode = ResolveAnsiEmphasisCode(style.Emphasis);
        var openCode = string.IsNullOrEmpty(emphasisCode)
            ? $"{EscapePrefix}{foregroundCode}m"
            : $"{EscapePrefix}{emphasisCode};{foregroundCode}m";

        return $"{openCode}{content}{EscapeReset}";
    }

    internal static bool ShouldUseAnsi(
        Func<string, string?> environmentVariableReader,
        bool isOutputRedirected)
    {
        ArgumentNullException.ThrowIfNull(environmentVariableReader);

        if (isOutputRedirected)
        {
            return false;
        }

        if (IsAnsiOptOutEnabled(environmentVariableReader, "ASXRUN_NO_COLOR")
            || IsAnsiOptOutEnabled(environmentVariableReader, "NO_COLOR"))
        {
            return false;
        }

        var terminalKind = environmentVariableReader("TERM");
        return !string.Equals(terminalKind?.Trim(), "dumb", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAnsiOptOutEnabled(
        Func<string, string?> environmentVariableReader,
        string variableName)
    {
        var variableValue = environmentVariableReader(variableName);
        return !string.IsNullOrWhiteSpace(variableValue);
    }

    private static string ResolveAnsiEmphasisCode(TerminalTextEmphasis emphasis)
    {
        return emphasis switch
        {
            TerminalTextEmphasis.Normal => string.Empty,
            TerminalTextEmphasis.Bold => "1",
            TerminalTextEmphasis.Dim => "2",
            _ => throw new ArgumentOutOfRangeException(nameof(emphasis), emphasis, "Enfase de terminal invalida.")
        };
    }
}