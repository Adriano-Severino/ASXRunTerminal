using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class AgentObjectivePlannerTests
{
    [Fact]
    public void Build_WithCompositeObjective_SplitsActionPairIntoExecutionStages()
    {
        var plan = AgentObjectivePlanner.Build(
            "  Planejar   e executar migracao incremental de banco com rollback seguro. ");

        Assert.Equal(
            "Planejar e executar migracao incremental de banco com rollback seguro.",
            plan.Objective);
        Assert.Contains(
            plan.Steps,
            static step => step.Action.Contains("Planejar.", StringComparison.Ordinal));
        Assert.Contains(
            plan.Steps,
            static step => step.Action.Contains(
                "Executar migracao incremental de banco com rollback seguro.",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Build_WithSequentialConnectors_ExtractsDistinctExecutionStages()
    {
        var plan = AgentObjectivePlanner.Build(
            "Mapear riscos; depois implementar plano de rollout => validar testes de regressao");

        Assert.Contains(
            plan.Steps,
            static step => step.Action.Contains("Mapear riscos.", StringComparison.Ordinal));
        Assert.Contains(
            plan.Steps,
            static step => step.Action.Contains(
                "Implementar plano de rollout.",
                StringComparison.Ordinal));
        Assert.Contains(
            plan.Steps,
            static step => step.Action.Contains(
                "Validar testes de regressao.",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Build_WithRepeatedStages_DeduplicatesExecutionSubobjectives()
    {
        var plan = AgentObjectivePlanner.Build(
            "Planejar e planejar e planejar e planejar e planejar");

        var executionSteps = plan.Steps
            .Where(static step => step.Stage.StartsWith(
                "Executar subobjetivo ",
                StringComparison.Ordinal))
            .ToArray();

        Assert.Single(executionSteps);
        Assert.Equal("Planejar.", executionSteps[0].Action);
    }

    [Fact]
    public void Build_WithEmptyObjective_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => AgentObjectivePlanner.Build("   "));

        Assert.Equal("objective", exception.ParamName);
    }
}
