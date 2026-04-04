using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class TerminalVisualComponentsTests
{
    [Fact]
    public void BuildHeader_WithSubtitle_RendersTitleAndBottomBorder()
    {
        TerminalHeader header = TerminalVisualComponents.BuildHeader(
            "ASXRunTerminal CLI",
            "Terminal local para IA",
            width: 40);

        var lines = header.Value.Split(Environment.NewLine);

        Assert.Equal(3, lines.Length);
        Assert.Equal(40, lines[0].Length);
        Assert.Contains(" ASXRunTerminal CLI ", lines[0]);
        Assert.Equal("Terminal local para IA", lines[1]);
        Assert.Equal(new string('=', 40), lines[2]);
    }

    [Fact]
    public void BuildSeparator_WithLabel_CentralizesLabelWithinWidth()
    {
        TerminalSeparator separator = TerminalVisualComponents.BuildSeparator(
            "COMANDOS",
            width: 30,
            fill: '-');

        Assert.Equal(30, separator.Value.Length);
        Assert.Contains(" COMANDOS ", separator.Value);
        Assert.StartsWith("-", separator.Value);
        Assert.EndsWith("-", separator.Value);
    }

    [Fact]
    public void BuildSeparator_WithoutLabel_ReturnsFilledLine()
    {
        TerminalSeparator separator = TerminalVisualComponents.BuildSeparator(width: 8, fill: '=');

        Assert.Equal("========", separator.Value);
    }

    [Theory]
    [InlineData(nameof(ExecutionState.Connecting), "[CONN]")]
    [InlineData(nameof(ExecutionState.Processing), "[WORK]")]
    [InlineData(nameof(ExecutionState.Completed), "[DONE]")]
    [InlineData(nameof(ExecutionState.Error), "[FAIL]")]
    public void TerminalStatusBadge_ImplicitOperator_MapsExpectedBadge(
        string stateName,
        string expectedBadge)
    {
        var state = Enum.Parse<ExecutionState>(stateName);
        TerminalStatusBadge badge = state;

        Assert.Equal(expectedBadge, badge.Value);
    }

    [Theory]
    [InlineData(0, "|")]
    [InlineData(1, "/")]
    [InlineData(2, "-")]
    [InlineData(3, "\\")]
    [InlineData(4, "|")]
    [InlineData(-1, "\\")]
    public void BuildSpinnerFrame_ForProcessing_CyclesByStep(int step, string expectedFrame)
    {
        TerminalSpinnerFrame frame = TerminalVisualComponents.BuildSpinnerFrame(
            ExecutionState.Processing,
            step);

        Assert.Equal(expectedFrame, frame.Value);
    }

    [Fact]
    public void BuildExecutionSuffix_ComposesBadgeAndSpinner()
    {
        var suffix = TerminalVisualComponents.BuildExecutionSuffix(ExecutionState.Completed, step: 7);

        Assert.Equal("[DONE] *", suffix);
    }
}