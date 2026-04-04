using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class TerminalDesignSystemTests
{
    [Theory]
    [InlineData(nameof(TerminalThemeMode.Auto), "auto")]
    [InlineData(nameof(TerminalThemeMode.Light), "light")]
    [InlineData(nameof(TerminalThemeMode.Dark), "dark")]
    [InlineData(nameof(TerminalThemeMode.HighContrast), "high-contrast")]
    public void TerminalTheme_ImplicitOperator_ConvertsModeToCanonicalString(
        string themeModeName,
        string expected)
    {
        var themeMode = Enum.Parse<TerminalThemeMode>(themeModeName);
        var theme = (TerminalTheme)themeMode;

        Assert.Equal(expected, (string)theme);
    }

    [Theory]
    [InlineData("auto", nameof(TerminalThemeMode.Auto))]
    [InlineData("LIGHT", nameof(TerminalThemeMode.Light))]
    [InlineData("dark", nameof(TerminalThemeMode.Dark))]
    [InlineData("high-contrast", nameof(TerminalThemeMode.HighContrast))]
    [InlineData("high_contrast", nameof(TerminalThemeMode.HighContrast))]
    public void TerminalTheme_ImplicitOperator_ParsesStringToThemeMode(
        string rawValue,
        string expectedThemeModeName)
    {
        var expected = Enum.Parse<TerminalThemeMode>(expectedThemeModeName);
        TerminalTheme theme = new(rawValue);

        var parsed = (TerminalThemeMode)theme;

        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void Default_UsesMonospaceTypography_AndHighlightStyle()
    {
        var designSystem = TerminalDesignSystem.Default;

        Assert.True(designSystem.Typography.IsMonospace);
        Assert.True(
            designSystem.Typography.PreferredFontStack.Contains(
                "monospace",
                StringComparison.OrdinalIgnoreCase));
        Assert.Equal(4, designSystem.Typography.TabWidth);
        Assert.Equal(TerminalColorRole.Accent, designSystem.HighlightStyle.Foreground);
        Assert.Equal(TerminalTextEmphasis.Bold, designSystem.HighlightStyle.Emphasis);
    }

    [Theory]
    [InlineData(nameof(ExecutionState.Connecting), "conectando", "[CONN]", nameof(TerminalColorRole.Info))]
    [InlineData(nameof(ExecutionState.Processing), "processando", "[WORK]", nameof(TerminalColorRole.Accent))]
    [InlineData(nameof(ExecutionState.Completed), "concluido", "[DONE]", nameof(TerminalColorRole.Success))]
    [InlineData(nameof(ExecutionState.Error), "erro", "[FAIL]", nameof(TerminalColorRole.Error))]
    public void TerminalExecutionStateToken_ImplicitOperator_MapsExpectedValues(
        string stateName,
        string expectedLabel,
        string expectedBadge,
        string expectedForegroundName)
    {
        var state = Enum.Parse<ExecutionState>(stateName);
        var expectedForeground = Enum.Parse<TerminalColorRole>(expectedForegroundName);

        TerminalExecutionStateToken token = state;

        Assert.Equal(expectedLabel, token.Label);
        Assert.Equal(expectedBadge, token.Badge);
        Assert.Equal(expectedForeground, token.Style.Foreground);
    }

    [Fact]
    public void ExecutionStateLabel_ImplicitOperator_UsesTerminalExecutionTokenLabel()
    {
        string label = (ExecutionStateLabel)ExecutionState.Processing;

        Assert.Equal("processando", label);
    }

    [Theory]
    [InlineData(nameof(TerminalColorRole.TextPrimary), "37")]
    [InlineData(nameof(TerminalColorRole.TextMuted), "90")]
    [InlineData(nameof(TerminalColorRole.Accent), "96")]
    [InlineData(nameof(TerminalColorRole.Info), "36")]
    [InlineData(nameof(TerminalColorRole.Success), "32")]
    [InlineData(nameof(TerminalColorRole.Warning), "33")]
    [InlineData(nameof(TerminalColorRole.Error), "31")]
    [InlineData(nameof(TerminalColorRole.Border), "90")]
    public void ResolveAnsiForegroundCode_ReturnsExpectedCode(
        string colorRoleName,
        string expectedCode)
    {
        var colorRole = Enum.Parse<TerminalColorRole>(colorRoleName);
        var designSystem = TerminalDesignSystem.Default;

        var actualCode = designSystem.ResolveAnsiForegroundCode(colorRole);

        Assert.Equal(expectedCode, actualCode);
    }

    [Fact]
    public void Create_WithLightTheme_UsesLightPaletteCodes()
    {
        var designSystem = TerminalDesignSystem.Create(TerminalThemeMode.Light);

        Assert.Equal(TerminalThemeMode.Light, designSystem.ThemeMode);
        Assert.Equal("30", designSystem.ResolveAnsiForegroundCode(TerminalColorRole.TextPrimary));
        Assert.Equal("34", designSystem.ResolveAnsiForegroundCode(TerminalColorRole.Accent));
    }

    [Fact]
    public void Create_WithHighContrastTheme_UsesHighContrastPaletteCodes()
    {
        var designSystem = TerminalDesignSystem.Create(TerminalThemeMode.HighContrast);

        Assert.Equal(TerminalThemeMode.HighContrast, designSystem.ThemeMode);
        Assert.Equal("97", designSystem.ResolveAnsiForegroundCode(TerminalColorRole.TextPrimary));
        Assert.Equal("91", designSystem.ResolveAnsiForegroundCode(TerminalColorRole.Error));
    }
}
