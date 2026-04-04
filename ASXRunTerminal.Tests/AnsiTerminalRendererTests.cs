using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class AnsiTerminalRendererTests
{
    [Fact]
    public void Render_WhenAnsiIsEnabled_WrapsContentWithEscapeCodes()
    {
        var renderer = new AnsiTerminalRenderer(supportsAnsi: true);

        var rendered = renderer.Render(
            "mensagem",
            new TerminalTextStyle(TerminalColorRole.Accent, TerminalTextEmphasis.Bold));

        Assert.Equal("\u001b[1;96mmensagem\u001b[0m", rendered);
    }

    [Fact]
    public void Render_WhenAnsiIsDisabled_ReturnsPlainContent()
    {
        var renderer = new AnsiTerminalRenderer(supportsAnsi: false);

        var rendered = renderer.Render(
            "mensagem",
            new TerminalTextStyle(TerminalColorRole.Accent, TerminalTextEmphasis.Bold));

        Assert.Equal("mensagem", rendered);
    }

    [Fact]
    public void ShouldUseAnsi_WhenOutputIsRedirected_ReturnsFalse()
    {
        var result = AnsiTerminalRenderer.ShouldUseAnsi(_ => null, isOutputRedirected: true);

        Assert.False(result);
    }

    [Fact]
    public void ShouldUseAnsi_WhenNoColorVariableIsSet_ReturnsFalse()
    {
        var result = AnsiTerminalRenderer.ShouldUseAnsi(
            CreateEnvironmentReader(("NO_COLOR", "1")),
            isOutputRedirected: false);

        Assert.False(result);
    }

    [Fact]
    public void ShouldUseAnsi_WhenAsxRunNoColorVariableIsSet_ReturnsFalse()
    {
        var result = AnsiTerminalRenderer.ShouldUseAnsi(
            CreateEnvironmentReader(("ASXRUN_NO_COLOR", "true")),
            isOutputRedirected: false);

        Assert.False(result);
    }

    [Fact]
    public void ShouldUseAnsi_WhenTerminalIsDumb_ReturnsFalse()
    {
        var result = AnsiTerminalRenderer.ShouldUseAnsi(
            CreateEnvironmentReader(("TERM", "dumb")),
            isOutputRedirected: false);

        Assert.False(result);
    }

    [Fact]
    public void ShouldUseAnsi_WhenTerminalIsNotDumb_AndNoOptOutExists_ReturnsTrue()
    {
        var result = AnsiTerminalRenderer.ShouldUseAnsi(
            CreateEnvironmentReader(("TERM", "xterm-256color")),
            isOutputRedirected: false);

        Assert.True(result);
    }

    private static Func<string, string?> CreateEnvironmentReader(params (string Key, string? Value)[] variables)
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in variables)
        {
            map[key] = value;
        }

        return key => map.TryGetValue(key, out var value) ? value : null;
    }
}