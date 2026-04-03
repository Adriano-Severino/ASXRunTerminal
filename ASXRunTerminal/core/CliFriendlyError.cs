namespace ASXRunTerminal.Core;

internal enum CliErrorCategory
{
    InvalidArguments,
    RuntimeFailure
}

internal readonly record struct CliErrorTemplate(string Prefix, string DefaultSuggestion)
{
    public static implicit operator CliErrorTemplate(CliErrorCategory category)
    {
        return category switch
        {
            CliErrorCategory.InvalidArguments => new CliErrorTemplate(
                Prefix: "Nao foi possivel executar o comando.",
                DefaultSuggestion: "Use 'asxrun --help' para ver as opcoes disponiveis."),
            CliErrorCategory.RuntimeFailure => new CliErrorTemplate(
                Prefix: "Nao foi possivel concluir a execucao.",
                DefaultSuggestion: "Tente novamente. Se o problema persistir, revise os logs acima."),
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Categoria de erro invalida.")
        };
    }
}

internal readonly record struct CliFriendlyError(
    CliExitCode ExitCode,
    CliErrorCategory Category,
    string Detail,
    string? Suggestion = null)
{
    public string BuildPrimaryMessage()
    {
        CliErrorTemplate template = Category;
        return $"{template.Prefix} {Detail}";
    }

    public string BuildSuggestionMessage()
    {
        CliErrorTemplate template = Category;
        var suggestion = string.IsNullOrWhiteSpace(Suggestion)
            ? template.DefaultSuggestion
            : Suggestion;

        return $"Sugestao: {suggestion}";
    }

    public static CliFriendlyError InvalidArguments(string detail, string? suggestion = null)
    {
        return new CliFriendlyError(
            ExitCode: CliExitCode.InvalidArguments,
            Category: CliErrorCategory.InvalidArguments,
            Detail: detail,
            Suggestion: suggestion);
    }

    public static CliFriendlyError Runtime(string detail, string? suggestion = null)
    {
        return new CliFriendlyError(
            ExitCode: CliExitCode.RuntimeError,
            Category: CliErrorCategory.RuntimeFailure,
            Detail: detail,
            Suggestion: suggestion);
    }
}
