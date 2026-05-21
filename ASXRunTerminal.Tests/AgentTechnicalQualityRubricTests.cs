using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class AgentTechnicalQualityRubricTests
{
    [Fact]
    public void Default_IncludesRequiredSeniorQualityDimensions()
    {
        var rubric = AgentTechnicalQualityRubric.Default;

        var criterionKeys = rubric.Criteria
            .Select(static criterion => criterion.Key)
            .ToArray();

        Assert.Equal(
            [
                "correctness",
                "readability",
                "tests",
                "security",
                "performance"
            ],
            criterionKeys);
        Assert.All(rubric.Criteria, static criterion =>
            Assert.NotEmpty(criterion.AcceptanceCriteria));
    }

    [Fact]
    public void ToPromptSection_RendersGateAndAcceptanceCriteria()
    {
        var promptSection = AgentTechnicalQualityRubric.Default.ToPromptSection();

        Assert.Contains("Use esta rubrica como gate de qualidade", promptSection);
        Assert.Contains("marque verify como refine", promptSection);
        Assert.Contains("Corretude (correctness)", promptSection);
        Assert.Contains("Legibilidade (readability)", promptSection);
        Assert.Contains("Testes (tests)", promptSection);
        Assert.Contains("Seguranca (security)", promptSection);
        Assert.Contains("Performance (performance)", promptSection);
        Assert.Contains("Cobertura minima de testes", promptSection);
        Assert.Contains("Build, testes e lint/format", promptSection);
        Assert.Contains("segredos nao sao expostos", promptSection);
    }

    [Fact]
    public void CriterionImplicitOperator_NormalizesTupleValues()
    {
        AgentQualityRubricCriterion criterion = (
            " risk ",
            " Risco ",
            " Avalia risco residual. ",
            [" Evidencia documentada. "]);

        Assert.Equal("risk", criterion.Key);
        Assert.Equal("Risco", criterion.Name);
        Assert.Equal("Avalia risco residual.", criterion.Description);
        Assert.Equal("Evidencia documentada.", Assert.Single(criterion.AcceptanceCriteria));
    }
}
