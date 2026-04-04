using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class TerminalResponseRendererTests
{
    [Fact]
    public void RenderChunk_WithCSharpCodeFence_AppliesLanguageSpecificAnsiStyle()
    {
        var renderer = CreateAnsiRenderer();

        var output = renderer.RenderChunk(
            "Antes\n```csharp\nvar total = 1;\n```\nDepois\n");

        Assert.Contains("Antes\n", output);
        Assert.Contains("\u001b[90m```csharp\u001b[0m\n", output);
        Assert.Contains("\u001b[1;36mvar total = 1;\u001b[0m\n", output);
        Assert.Contains("\u001b[90m```\u001b[0m\n", output);
        Assert.Contains("Depois\n", output);
    }

    [Fact]
    public void RenderChunk_WithPythonCodeFence_AppliesPythonAnsiStyle()
    {
        var renderer = CreateAnsiRenderer();

        var output = renderer.RenderChunk(
            "```python\nprint('ok')\n```\n");

        Assert.Contains("\u001b[1;32mprint('ok')\u001b[0m\n", output);
    }

    [Fact]
    public void RenderChunk_WithFenceSplitAcrossChunks_PreservesFenceState()
    {
        var renderer = CreateAnsiRenderer();

        var first = renderer.RenderChunk("```pyt");
        var second = renderer.RenderChunk("hon\nprint('chunk')\n```\n");

        Assert.Equal(string.Empty, first);
        Assert.Contains("\u001b[90m```python\u001b[0m\n", second);
        Assert.Contains("\u001b[1;32mprint('chunk')\u001b[0m\n", second);
        Assert.Contains("\u001b[90m```\u001b[0m\n", second);
    }

    [Fact]
    public void Flush_WithTrailingCodeLineWithoutNewLine_RendersRemainingBufferedContent()
    {
        var renderer = CreateAnsiRenderer();

        var chunkOutput = renderer.RenderChunk("```sql\nselect 1;");
        var flushOutput = renderer.Flush();

        Assert.Contains("\u001b[90m```sql\u001b[0m\n", chunkOutput);
        Assert.Equal("\u001b[1;31mselect 1;\u001b[0m", flushOutput);
    }

    [Fact]
    public void RenderChunk_WhenAnsiIsDisabled_KeepsOriginalMarkdownCodeBlock()
    {
        var renderer = CreatePlainRenderer();

        var output = renderer.RenderChunk("```js\nconsole.log('ok');\n```\n");

        Assert.Equal("```js\nconsole.log('ok');\n```\n", output);
    }

    private static TerminalResponseRenderer CreateAnsiRenderer()
    {
        return new TerminalResponseRenderer(
            new AnsiTerminalRenderer(supportsAnsi: true),
            TerminalDesignSystem.Default);
    }

    private static TerminalResponseRenderer CreatePlainRenderer()
    {
        return new TerminalResponseRenderer(
            new AnsiTerminalRenderer(supportsAnsi: false),
            TerminalDesignSystem.Default);
    }
}
