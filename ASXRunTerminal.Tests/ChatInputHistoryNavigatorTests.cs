using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class ChatInputHistoryNavigatorTests
{
    [Fact]
    public void MovePrevious_UsesMostRecentPromptFromHistory()
    {
        var navigator = new ChatInputHistoryNavigator([
            "prompt mais recente",
            "prompt mais antigo"
        ]);

        navigator.MovePrevious();

        Assert.Equal("prompt mais recente", navigator.CurrentInput);
    }

    [Fact]
    public void MovePrevious_ThenMoveNext_RestoresDraftInput()
    {
        var navigator = new ChatInputHistoryNavigator(["prompt antigo"]);

        navigator.AppendInputCharacter('n');
        navigator.AppendInputCharacter('o');
        navigator.AppendInputCharacter('v');
        navigator.AppendInputCharacter('o');

        navigator.MovePrevious();
        navigator.MoveNext();

        Assert.Equal("novo", navigator.CurrentInput);
    }

    [Fact]
    public void StartIncrementalSearch_AndAppendQuery_SelectsMatchingPrompt()
    {
        var navigator = new ChatInputHistoryNavigator([
            "gerar controller",
            "revisar testes"
        ]);

        navigator.StartIncrementalSearch();
        navigator.AppendSearchCharacter('t');
        navigator.AppendSearchCharacter('e');
        navigator.AppendSearchCharacter('s');
        navigator.AppendSearchCharacter('t');

        Assert.True(navigator.IsIncrementalSearchActive);
        Assert.True(navigator.HasIncrementalSearchMatch);
        Assert.Equal("revisar testes", navigator.CurrentInput);
    }

    [Fact]
    public void CycleIncrementalSearch_WithMultipleMatches_NavigatesBetweenMatchedPrompts()
    {
        var navigator = new ChatInputHistoryNavigator([
            "gerar test de api",
            "ajustar readme",
            "revisar testes unitarios"
        ]);

        navigator.StartIncrementalSearch();
        navigator.AppendSearchCharacter('t');
        navigator.AppendSearchCharacter('e');
        navigator.AppendSearchCharacter('s');
        navigator.AppendSearchCharacter('t');

        Assert.Equal("gerar test de api", navigator.CurrentInput);

        navigator.CycleIncrementalSearch(olderMatch: true);

        Assert.Equal("revisar testes unitarios", navigator.CurrentInput);

        navigator.CycleIncrementalSearch(olderMatch: false);

        Assert.Equal("gerar test de api", navigator.CurrentInput);
    }

    [Fact]
    public void CancelIncrementalSearch_RestoresInputBeforeSearch()
    {
        var navigator = new ChatInputHistoryNavigator(["historico 1"]);

        navigator.AppendInputCharacter('d');
        navigator.AppendInputCharacter('r');
        navigator.AppendInputCharacter('a');
        navigator.AppendInputCharacter('f');
        navigator.AppendInputCharacter('t');

        navigator.StartIncrementalSearch();
        navigator.AppendSearchCharacter('h');
        navigator.CancelIncrementalSearch();

        Assert.False(navigator.IsIncrementalSearchActive);
        Assert.Equal("draft", navigator.CurrentInput);
    }

    [Fact]
    public void RememberPrompt_AddsPromptToTopOfHistory()
    {
        var navigator = new ChatInputHistoryNavigator(["prompt antigo"]);

        navigator.RememberPrompt("prompt novo");
        navigator.MovePrevious();

        Assert.Equal("prompt novo", navigator.CurrentInput);
    }
}
