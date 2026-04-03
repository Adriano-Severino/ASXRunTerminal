namespace ASXRunTerminal.Core;

internal static class SkillCatalog
{
    private static readonly IReadOnlyList<SkillDefinition> BuiltInSkills =
    [
        new SkillDefinition(
            Name: "code-review",
            Description: "Revisa codigo com foco em bugs, riscos e testes faltantes.",
            Instruction:
                """
                Atue como um engenheiro de software senior em code review.
                Priorize corretude, regressao, seguranca e cobertura de testes.
                Liste os problemas por severidade, com recomendacoes objetivas.
                """),
        new SkillDefinition(
            Name: "bugfix",
            Description: "Foca em diagnosticar causa raiz e corrigir defeitos.",
            Instruction:
                """
                Atue como um especialista em correcoes de bugs.
                Identifique a causa raiz, proponha a menor mudanca segura e valide com testes.
                Explique o risco de regressao e como mitiga-lo.
                """),
        new SkillDefinition(
            Name: "refactor",
            Description: "Refatora mantendo comportamento e melhorando estrutura.",
            Instruction:
                """
                Atue como um engenheiro senior em refatoracao.
                Preserve comportamento observavel, reduza complexidade e melhore legibilidade.
                Inclua ajustes de testes quando necessario para manter confiabilidade.
                """),
        new SkillDefinition(
            Name: "test-writer",
            Description: "Gera testes unitarios e de integracao para cenarios criticos.",
            Instruction:
                """
                Atue como especialista em testes automatizados.
                Crie testes focados em comportamento, casos de borda e regressao.
                Prefira casos deterministas, rapidos e de facil manutencao.
                """),
        new SkillDefinition(
            Name: "docs-writer",
            Description: "Escreve documentacao tecnica e de uso para features.",
            Instruction:
                """
                Atue como redator tecnico para engenharia de software.
                Produza documentacao clara com objetivo, pre-requisitos, exemplos e limites.
                Priorize instrucoes acionaveis e consistentes com o codigo.
                """)
    ];

    public static IReadOnlyList<SkillDefinition> List()
    {
        return BuiltInSkills;
    }

    public static bool TryFind(string skillName, out SkillDefinition skill)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            skill = default;
            return false;
        }

        var trimmedName = skillName.Trim();
        foreach (var candidate in BuiltInSkills)
        {
            if (string.Equals(candidate.Name, trimmedName, StringComparison.OrdinalIgnoreCase))
            {
                skill = candidate;
                return true;
            }
        }

        skill = default;
        return false;
    }
}
