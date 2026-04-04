using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class ChatAutocompleteEngineTests
{
    [Fact]
    public void ApplyNextCompletion_WithInteractiveCommandPrefix_CompletesCommand()
    {
        var engine = CreateEngine();

        var completedInput = engine.ApplyNextCompletion("/he");

        Assert.Equal("/help", completedInput);
    }

    [Fact]
    public void ApplyNextCompletion_WithRepeatedTab_CyclesInteractiveCommandCandidates()
    {
        var engine = CreateEngine();

        var firstCompletion = engine.ApplyNextCompletion("/");
        var secondCompletion = engine.ApplyNextCompletion(firstCompletion);
        var thirdCompletion = engine.ApplyNextCompletion(secondCompletion);

        Assert.Equal("/help", firstCompletion);
        Assert.Equal("/clear", secondCompletion);
        Assert.Equal("/models", thirdCompletion);
    }

    [Fact]
    public void ApplyNextCompletion_WithCliCommandPrefix_CompletesCommand()
    {
        var engine = CreateEngine();

        var completedInput = engine.ApplyNextCompletion("asxrun mo");

        Assert.Equal("asxrun models", completedInput);
    }

    [Fact]
    public void ApplyNextCompletion_WithCliOptionPrefix_CompletesOption()
    {
        var engine = CreateEngine();

        var completedInput = engine.ApplyNextCompletion("asxrun ask --m");

        Assert.Equal("asxrun ask --model", completedInput);
    }

    [Fact]
    public void ApplyNextCompletion_WithModelFlagSeparatedBySpace_CompletesModelName()
    {
        var engine = CreateEngine();

        var completedInput = engine.ApplyNextCompletion("asxrun ask --model qw");

        Assert.Equal("asxrun ask --model qwen3.5:4b", completedInput);
    }

    [Fact]
    public void ApplyNextCompletion_WithModelFlagUsingEqualsSyntax_CompletesModelName()
    {
        var engine = CreateEngine();

        var completedInput = engine.ApplyNextCompletion("asxrun ask --model=ll");

        Assert.Equal("asxrun ask --model=llama3.2:latest", completedInput);
    }

    [Fact]
    public void ApplyNextCompletion_WhenNoCandidateMatches_ReturnsOriginalInput()
    {
        var engine = CreateEngine();

        var completedInput = engine.ApplyNextCompletion("/nao-existe");

        Assert.Equal("/nao-existe", completedInput);
    }

    [Fact]
    public void Reset_AfterCompletion_StopsCurrentCycle()
    {
        var engine = CreateEngine();

        var firstCompletion = engine.ApplyNextCompletion("asxrun ask --");
        engine.Reset();
        var nextCompletion = engine.ApplyNextCompletion(firstCompletion);

        Assert.Equal("asxrun ask --model", firstCompletion);
        Assert.Equal("asxrun ask --model", nextCompletion);
    }

    private static ChatAutocompleteEngine CreateEngine()
    {
        return new ChatAutocompleteEngine(
            interactiveCommandCandidates:
            [
                "/help",
                "/clear",
                "/models",
                "/tools",
                "/exit"
            ],
            cliCommandCandidates:
            [
                "ask",
                "chat",
                "doctor",
                "models",
                "history",
                "config",
                "skills",
                "skill"
            ],
            optionCandidates:
            [
                "--model",
                "--help",
                "--version",
                "--clear"
            ],
            modelNameProvider: static () =>
            [
                "qwen3.5:4b",
                "llama3.2:latest"
            ]);
    }
}
