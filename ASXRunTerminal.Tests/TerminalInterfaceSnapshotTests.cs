using System.Text;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class TerminalInterfaceSnapshotTests
{
    [Theory]
    [InlineData(nameof(TerminalThemeMode.Dark), "interface-dark-ansi")]
    [InlineData(nameof(TerminalThemeMode.Light), "interface-light-ansi")]
    [InlineData(nameof(TerminalThemeMode.HighContrast), "interface-high-contrast-ansi")]
    public void BuildInterfacePreview_WithAnsiEnabled_MatchesSnapshot(
        string themeModeName,
        string snapshotName)
    {
        var themeMode = Enum.Parse<TerminalThemeMode>(themeModeName);
        var output = BuildInterfacePreview(themeMode, supportsAnsi: true);

        SnapshotAssert.Match(snapshotName, SnapshotAssert.NormalizeForSnapshot(output));
    }

    [Fact]
    public void BuildInterfacePreview_WithAnsiDisabled_MatchesSnapshot()
    {
        var output = BuildInterfacePreview(TerminalThemeMode.Dark, supportsAnsi: false);

        SnapshotAssert.Match("interface-no-ansi", SnapshotAssert.NormalizeForSnapshot(output));
    }

    [Theory]
    [InlineData(nameof(TerminalThemeMode.Dark), "palette-dark-ansi")]
    [InlineData(nameof(TerminalThemeMode.Light), "palette-light-ansi")]
    [InlineData(nameof(TerminalThemeMode.HighContrast), "palette-high-contrast-ansi")]
    public void BuildThemePalettePreview_WithAnsiEnabled_MatchesSnapshot(
        string themeModeName,
        string snapshotName)
    {
        var themeMode = Enum.Parse<TerminalThemeMode>(themeModeName);
        var output = BuildThemePalettePreview(themeMode);

        SnapshotAssert.Match(snapshotName, SnapshotAssert.NormalizeForSnapshot(output));
    }

    private static string BuildInterfacePreview(
        TerminalThemeMode themeMode,
        bool supportsAnsi)
    {
        var designSystem = TerminalDesignSystem.Create(themeMode);
        var ansiRenderer = new AnsiTerminalRenderer(supportsAnsi, designSystem);
        var responseRenderer = new TerminalResponseRenderer(ansiRenderer, designSystem);
        var builder = new StringBuilder();

        TerminalTheme theme = themeMode;
        TerminalHeader header = TerminalVisualComponents.BuildHeader(
            "ASXRunTerminal CLI",
            $"preview | theme={(string)theme} | ansi={(supportsAnsi ? "on" : "off")}",
            width: 48);
        builder.AppendLine(ansiRenderer.Render(header.Value, designSystem.PrimaryTextStyle));

        TerminalSeparator statusSeparator = TerminalVisualComponents.BuildSeparator("STATUS", width: 48);
        builder.AppendLine(ansiRenderer.Render(statusSeparator.Value, designSystem.BorderStyle));

        var executionStates = Enum.GetValues<ExecutionState>();
        for (var index = 0; index < executionStates.Length; index++)
        {
            var state = executionStates[index];
            TerminalExecutionStateToken token = state;
            var suffix = TerminalVisualComponents.BuildExecutionSuffix(state, index);
            var stateLine = $"{token.Badge} {token.Label} | {suffix}";
            builder.AppendLine(ansiRenderer.Render(stateLine, token.Style));
        }

        TerminalSeparator responseSeparator = TerminalVisualComponents.BuildSeparator("RESPONSE", width: 48);
        builder.AppendLine(ansiRenderer.Render(responseSeparator.Value, designSystem.BorderStyle));
        builder.Append(responseRenderer.RenderChunk("Antes\n```csharp\nvar total = 1;\n```\nDepois\n"));
        builder.Append(responseRenderer.RenderChunk("```python\nprint('ok')\n```\n"));
        builder.Append(responseRenderer.Flush());

        return builder.ToString();
    }

    private static string BuildThemePalettePreview(TerminalThemeMode themeMode)
    {
        var designSystem = TerminalDesignSystem.Create(themeMode);
        var ansiRenderer = new AnsiTerminalRenderer(supportsAnsi: true, designSystem);
        var builder = new StringBuilder();

        TerminalTheme theme = themeMode;
        builder.AppendLine($"theme={(string)theme}");
        builder.AppendLine(
            $"typography={designSystem.Typography.PreferredFontStack} | tab={designSystem.Typography.TabWidth}");

        foreach (var colorRole in Enum.GetValues<TerminalColorRole>())
        {
            var code = designSystem.ResolveAnsiForegroundCode(colorRole);
            var normal = ansiRenderer.Render(
                colorRole.ToString(),
                new TerminalTextStyle(colorRole, TerminalTextEmphasis.Normal));
            var bold = ansiRenderer.Render(
                colorRole.ToString(),
                new TerminalTextStyle(colorRole, TerminalTextEmphasis.Bold));
            var dim = ansiRenderer.Render(
                colorRole.ToString(),
                new TerminalTextStyle(colorRole, TerminalTextEmphasis.Dim));

            builder.AppendLine(
                $"{colorRole} | code={code} | normal={normal} | bold={bold} | dim={dim}");
        }

        return builder.ToString();
    }
}
