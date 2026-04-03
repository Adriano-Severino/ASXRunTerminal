using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class CliFriendlyErrorTests
{
    [Fact]
    public void CliErrorCategory_ToTemplate_UsesImplicitOperator()
    {
        CliErrorTemplate template = CliErrorCategory.InvalidArguments;

        Assert.Equal("Nao foi possivel executar o comando.", template.Prefix);
        Assert.Equal("Use 'asxrun --help' para ver as opcoes disponiveis.", template.DefaultSuggestion);
    }

    [Fact]
    public void BuildSuggestionMessage_UsesDefaultSuggestion_WhenSuggestionIsMissing()
    {
        var error = CliFriendlyError.Runtime("Falha interna.");

        Assert.Equal(
            "Sugestao: Tente novamente. Se o problema persistir, revise os logs acima.",
            error.BuildSuggestionMessage());
    }
}
