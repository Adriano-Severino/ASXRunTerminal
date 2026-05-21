using System.Text;

namespace ASXRunTerminal.Core;

internal readonly record struct AgentQualityRubricCriterion(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<string> AcceptanceCriteria)
{
    public static implicit operator AgentQualityRubricCriterion(
        (string Key, string Name, string Description, string[] AcceptanceCriteria) tuple)
    {
        return Create(
            tuple.Key,
            tuple.Name,
            tuple.Description,
            tuple.AcceptanceCriteria);
    }

    public static AgentQualityRubricCriterion Create(
        string key,
        string name,
        string description,
        IEnumerable<string> acceptanceCriteria)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(acceptanceCriteria);

        var normalizedAcceptanceCriteria = acceptanceCriteria
            .Where(static criterion => !string.IsNullOrWhiteSpace(criterion))
            .Select(static criterion => criterion.Trim())
            .ToArray();

        if (normalizedAcceptanceCriteria.Length == 0)
        {
            throw new ArgumentException(
                "A dimensao da rubrica deve conter pelo menos um criterio de aceite.",
                nameof(acceptanceCriteria));
        }

        return new AgentQualityRubricCriterion(
            Key: key.Trim(),
            Name: name.Trim(),
            Description: description.Trim(),
            AcceptanceCriteria: normalizedAcceptanceCriteria);
    }
}

internal sealed class AgentTechnicalQualityRubric
{
    public static AgentTechnicalQualityRubric Default { get; } = new(
        [
            (
                "correctness",
                "Corretude",
                "A entrega resolve o objetivo declarado, preserva contratos existentes e trata estados de erro conhecidos.",
                [
                    "Comportamento esperado esta rastreado ao objetivo e aos casos de borda relevantes.",
                    "Regressoes conhecidas foram evitadas ou justificadas com evidencia objetiva."
                ]),
            (
                "readability",
                "Legibilidade",
                "O codigo fica simples, idiomatico e localizado, com nomes claros e sem abstracao desnecessaria.",
                [
                    "A mudanca segue padroes do projeto e evita duplicacao significativa.",
                    "Comentarios existem apenas quando reduzem ambiguidade real."
                ]),
            (
                "tests",
                "Testes",
                "A validacao objetiva cobre o comportamento alterado e as falhas provaveis.",
                [
                    "Testes novos ou atualizados acompanham mudancas de comportamento.",
                    "Cobertura minima de testes e respeitada quando ha metricas de cobertura disponiveis.",
                    "Build, testes e lint/format foram executados quando aplicavel."
                ]),
            (
                "security",
                "Seguranca",
                "A mudanca preserva privacidade, permissoes, segredos e operacoes destrutivas sob guardrails.",
                [
                    "Entradas externas sao validadas, caminhos e comandos respeitam as politicas do workspace, e segredos nao sao expostos.",
                    "Operacoes destrutivas ou sensiveis exigem aprovacao explicita e auditoria."
                ]),
            (
                "performance",
                "Performance",
                "A solucao evita custo computacional desnecessario e escala de forma razoavel para o uso esperado.",
                [
                    "Nao introduz IO, chamadas externas, alocacoes ou loops caros sem necessidade.",
                    "Tradeoffs de desempenho foram avaliados quando a mudanca toca fluxo critico."
                ])
        ]);

    private AgentTechnicalQualityRubric(
        IReadOnlyList<AgentQualityRubricCriterion> criteria)
    {
        Criteria = criteria;
    }

    public IReadOnlyList<AgentQualityRubricCriterion> Criteria { get; }

    public string ToPromptSection()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Use esta rubrica como gate de qualidade em plan, execute, verify e refine.");
        builder.AppendLine("Se qualquer dimensao ficar sem evidencia suficiente, marque verify como refine.");

        foreach (var criterion in Criteria)
        {
            builder.AppendLine();
            builder.AppendLine($"- {criterion.Name} ({criterion.Key}): {criterion.Description}");

            foreach (var acceptanceCriterion in criterion.AcceptanceCriteria)
            {
                builder.AppendLine($"  - {acceptanceCriterion}");
            }
        }

        return builder.ToString().TrimEnd();
    }
}
