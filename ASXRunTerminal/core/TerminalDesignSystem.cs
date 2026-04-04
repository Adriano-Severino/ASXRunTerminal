namespace ASXRunTerminal.Core;

internal enum TerminalThemeMode
{
    Auto,
    Light,
    Dark,
    HighContrast
}

internal readonly record struct TerminalTheme(string Value)
{
    private const string SupportedValuesLabel = "auto, light, dark, high-contrast";

    public static implicit operator string(TerminalTheme theme)
    {
        return theme.Value;
    }

    public static implicit operator TerminalTheme(TerminalThemeMode themeMode)
    {
        return themeMode switch
        {
            TerminalThemeMode.Auto => new TerminalTheme("auto"),
            TerminalThemeMode.Light => new TerminalTheme("light"),
            TerminalThemeMode.Dark => new TerminalTheme("dark"),
            TerminalThemeMode.HighContrast => new TerminalTheme("high-contrast"),
            _ => throw new ArgumentOutOfRangeException(nameof(themeMode), themeMode, "Tema de terminal invalido.")
        };
    }

    public static implicit operator TerminalThemeMode(TerminalTheme theme)
    {
        if (string.IsNullOrWhiteSpace(theme.Value))
        {
            throw new InvalidOperationException(
                $"O valor 'theme' no arquivo de configuracao deve ser um entre: {SupportedValuesLabel}.");
        }

        var normalized = theme.Value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "auto" => TerminalThemeMode.Auto,
            "light" => TerminalThemeMode.Light,
            "dark" => TerminalThemeMode.Dark,
            "high-contrast" or "highcontrast" or "high_contrast" => TerminalThemeMode.HighContrast,
            _ => throw new InvalidOperationException(
                $"O valor 'theme' no arquivo de configuracao deve ser um entre: {SupportedValuesLabel}.")
        };
    }
}

internal enum TerminalColorRole
{
    TextPrimary,
    TextMuted,
    Accent,
    Info,
    Success,
    Warning,
    Error,
    Border
}

internal enum TerminalTextEmphasis
{
    Normal,
    Bold,
    Dim
}

internal readonly record struct TerminalTypography(
    string PreferredFontStack,
    bool IsMonospace,
    int TabWidth)
{
    public static TerminalTypography Monospace =>
        new(
            PreferredFontStack: "Cascadia Mono, Consolas, Menlo, monospace",
            IsMonospace: true,
            TabWidth: 4);
}

internal readonly record struct TerminalTextStyle(
    TerminalColorRole Foreground,
    TerminalTextEmphasis Emphasis = TerminalTextEmphasis.Normal);

internal readonly record struct TerminalExecutionStateToken(
    string Label,
    string Badge,
    TerminalTextStyle Style)
{
    public static implicit operator TerminalExecutionStateToken(ExecutionState state)
    {
        return state switch
        {
            ExecutionState.Connecting => new(
                Label: "conectando",
                Badge: "[CONN]",
                Style: TerminalDesignSystem.Default.InfoStyle),
            ExecutionState.ToolCall => new(
                Label: "tool call",
                Badge: "[TOOL]",
                Style: TerminalDesignSystem.Default.AccentStyle),
            ExecutionState.Processing => new(
                Label: "processando",
                Badge: "[WORK]",
                Style: TerminalDesignSystem.Default.AccentStyle),
            ExecutionState.Diff => new(
                Label: "diff",
                Badge: "[DIFF]",
                Style: TerminalDesignSystem.Default.WarningStyle),
            ExecutionState.Completed => new(
                Label: "concluido",
                Badge: "[DONE]",
                Style: TerminalDesignSystem.Default.SuccessStyle),
            ExecutionState.Error => new(
                Label: "erro",
                Badge: "[FAIL]",
                Style: TerminalDesignSystem.Default.ErrorStyle),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Estado de execucao invalido.")
        };
    }
}

internal readonly record struct TerminalDesignSystem(
    TerminalThemeMode ThemeMode,
    TerminalTypography Typography,
    TerminalTextStyle PrimaryTextStyle,
    TerminalTextStyle MutedTextStyle,
    TerminalTextStyle AccentStyle,
    TerminalTextStyle HighlightStyle,
    TerminalTextStyle InfoStyle,
    TerminalTextStyle SuccessStyle,
    TerminalTextStyle WarningStyle,
    TerminalTextStyle ErrorStyle,
    TerminalTextStyle BorderStyle)
{
    public static TerminalDesignSystem Default =>
        Create(TerminalThemeMode.Dark);

    public static TerminalDesignSystem Create(TerminalThemeMode themeMode)
    {
        var resolvedTheme = themeMode == TerminalThemeMode.Auto
            ? ResolveAutoThemeMode()
            : themeMode;

        return resolvedTheme switch
        {
            TerminalThemeMode.Light => BuildLightThemeDesignSystem(),
            TerminalThemeMode.Dark => BuildDarkThemeDesignSystem(),
            TerminalThemeMode.HighContrast => BuildHighContrastThemeDesignSystem(),
            _ => throw new ArgumentOutOfRangeException(nameof(themeMode), themeMode, "Tema de terminal invalido.")
        };
    }

    public string ResolveAnsiForegroundCode(TerminalColorRole colorRole)
    {
        return ThemeMode switch
        {
            TerminalThemeMode.Light => ResolveLightThemeAnsiForegroundCode(colorRole),
            TerminalThemeMode.Dark => ResolveDarkThemeAnsiForegroundCode(colorRole),
            TerminalThemeMode.HighContrast => ResolveHighContrastThemeAnsiForegroundCode(colorRole),
            _ => throw new ArgumentOutOfRangeException(nameof(ThemeMode), ThemeMode, "Tema de terminal invalido.")
        };
    }

    private static TerminalDesignSystem BuildDarkThemeDesignSystem() =>
        new(
            ThemeMode: TerminalThemeMode.Dark,
            Typography: TerminalTypography.Monospace,
            PrimaryTextStyle: new TerminalTextStyle(TerminalColorRole.TextPrimary),
            MutedTextStyle: new TerminalTextStyle(TerminalColorRole.TextMuted, TerminalTextEmphasis.Dim),
            AccentStyle: new TerminalTextStyle(TerminalColorRole.Accent, TerminalTextEmphasis.Bold),
            HighlightStyle: new TerminalTextStyle(TerminalColorRole.Accent, TerminalTextEmphasis.Bold),
            InfoStyle: new TerminalTextStyle(TerminalColorRole.Info, TerminalTextEmphasis.Bold),
            SuccessStyle: new TerminalTextStyle(TerminalColorRole.Success, TerminalTextEmphasis.Bold),
            WarningStyle: new TerminalTextStyle(TerminalColorRole.Warning, TerminalTextEmphasis.Bold),
            ErrorStyle: new TerminalTextStyle(TerminalColorRole.Error, TerminalTextEmphasis.Bold),
            BorderStyle: new TerminalTextStyle(TerminalColorRole.Border));

    private static TerminalDesignSystem BuildLightThemeDesignSystem() =>
        new(
            ThemeMode: TerminalThemeMode.Light,
            Typography: TerminalTypography.Monospace,
            PrimaryTextStyle: new TerminalTextStyle(TerminalColorRole.TextPrimary),
            MutedTextStyle: new TerminalTextStyle(TerminalColorRole.TextMuted, TerminalTextEmphasis.Dim),
            AccentStyle: new TerminalTextStyle(TerminalColorRole.Accent, TerminalTextEmphasis.Bold),
            HighlightStyle: new TerminalTextStyle(TerminalColorRole.Accent, TerminalTextEmphasis.Bold),
            InfoStyle: new TerminalTextStyle(TerminalColorRole.Info, TerminalTextEmphasis.Bold),
            SuccessStyle: new TerminalTextStyle(TerminalColorRole.Success, TerminalTextEmphasis.Bold),
            WarningStyle: new TerminalTextStyle(TerminalColorRole.Warning, TerminalTextEmphasis.Bold),
            ErrorStyle: new TerminalTextStyle(TerminalColorRole.Error, TerminalTextEmphasis.Bold),
            BorderStyle: new TerminalTextStyle(TerminalColorRole.Border));

    private static TerminalDesignSystem BuildHighContrastThemeDesignSystem() =>
        new(
            ThemeMode: TerminalThemeMode.HighContrast,
            Typography: TerminalTypography.Monospace,
            PrimaryTextStyle: new TerminalTextStyle(TerminalColorRole.TextPrimary, TerminalTextEmphasis.Bold),
            MutedTextStyle: new TerminalTextStyle(TerminalColorRole.TextMuted),
            AccentStyle: new TerminalTextStyle(TerminalColorRole.Accent, TerminalTextEmphasis.Bold),
            HighlightStyle: new TerminalTextStyle(TerminalColorRole.Accent, TerminalTextEmphasis.Bold),
            InfoStyle: new TerminalTextStyle(TerminalColorRole.Info, TerminalTextEmphasis.Bold),
            SuccessStyle: new TerminalTextStyle(TerminalColorRole.Success, TerminalTextEmphasis.Bold),
            WarningStyle: new TerminalTextStyle(TerminalColorRole.Warning, TerminalTextEmphasis.Bold),
            ErrorStyle: new TerminalTextStyle(TerminalColorRole.Error, TerminalTextEmphasis.Bold),
            BorderStyle: new TerminalTextStyle(TerminalColorRole.Border, TerminalTextEmphasis.Bold));

    private static string ResolveDarkThemeAnsiForegroundCode(TerminalColorRole colorRole)
    {
        return colorRole switch
        {
            TerminalColorRole.TextPrimary => "37",
            TerminalColorRole.TextMuted => "90",
            TerminalColorRole.Accent => "96",
            TerminalColorRole.Info => "36",
            TerminalColorRole.Success => "32",
            TerminalColorRole.Warning => "33",
            TerminalColorRole.Error => "31",
            TerminalColorRole.Border => "90",
            _ => throw new ArgumentOutOfRangeException(nameof(colorRole), colorRole, "Cor de terminal invalida.")
        };
    }

    private static string ResolveLightThemeAnsiForegroundCode(TerminalColorRole colorRole)
    {
        return colorRole switch
        {
            TerminalColorRole.TextPrimary => "30",
            TerminalColorRole.TextMuted => "90",
            TerminalColorRole.Accent => "34",
            TerminalColorRole.Info => "36",
            TerminalColorRole.Success => "32",
            TerminalColorRole.Warning => "33",
            TerminalColorRole.Error => "31",
            TerminalColorRole.Border => "90",
            _ => throw new ArgumentOutOfRangeException(nameof(colorRole), colorRole, "Cor de terminal invalida.")
        };
    }

    private static string ResolveHighContrastThemeAnsiForegroundCode(TerminalColorRole colorRole)
    {
        return colorRole switch
        {
            TerminalColorRole.TextPrimary => "97",
            TerminalColorRole.TextMuted => "97",
            TerminalColorRole.Accent => "94",
            TerminalColorRole.Info => "96",
            TerminalColorRole.Success => "92",
            TerminalColorRole.Warning => "93",
            TerminalColorRole.Error => "91",
            TerminalColorRole.Border => "97",
            _ => throw new ArgumentOutOfRangeException(nameof(colorRole), colorRole, "Cor de terminal invalida.")
        };
    }

    private static TerminalThemeMode ResolveAutoThemeMode()
    {
        try
        {
            return IsLightBackground(Console.BackgroundColor)
                ? TerminalThemeMode.Light
                : TerminalThemeMode.Dark;
        }
        catch
        {
            return TerminalThemeMode.Dark;
        }
    }

    private static bool IsLightBackground(ConsoleColor backgroundColor)
    {
        return backgroundColor is ConsoleColor.White
            or ConsoleColor.Gray
            or ConsoleColor.Yellow
            or ConsoleColor.Cyan;
    }
}