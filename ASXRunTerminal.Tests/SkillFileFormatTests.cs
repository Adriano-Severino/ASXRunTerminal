using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class SkillFileFormatTests
{
    [Fact]
    public void Parse_WithValidMetadata_ReturnsSkillDefinition()
    {
        const string content =
            """
            ---
            name: api-review
            description: Revisa contratos e erros de API.
            instruction: |
              Atue como revisor tecnico de APIs.
              Priorize corretude e regressao.
            ---

            # Conteudo opcional
            """;

        var skill = SkillFileFormat.Parse(content, "skills/api-review/SKILL.md");

        Assert.Equal("api-review", skill.Name);
        Assert.Equal("Revisa contratos e erros de API.", skill.Description);
        Assert.Equal(
            """
            Atue como revisor tecnico de APIs.
            Priorize corretude e regressao.
            """,
            skill.Instruction);
    }

    [Fact]
    public void Parse_WithInlineInstructionMetadata_ReturnsSkillDefinition()
    {
        const string content =
            """
            ---
            name: test-writer-plus
            description: Gera testes para cenarios criticos.
            instruction: Foque em casos de borda e regressao.
            ---
            """;

        var skill = SkillFileFormat.Parse(content);

        Assert.Equal("test-writer-plus", skill.Name);
        Assert.Equal("Gera testes para cenarios criticos.", skill.Description);
        Assert.Equal("Foque em casos de borda e regressao.", skill.Instruction);
    }

    [Theory]
    [InlineData(SkillFileFormat.NameMetadataKey)]
    [InlineData(SkillFileFormat.DescriptionMetadataKey)]
    [InlineData(SkillFileFormat.InstructionMetadataKey)]
    public void Parse_WhenRequiredMetadataIsMissing_ThrowsInvalidOperationException(string missingKey)
    {
        var lines = new List<string>
        {
            "---"
        };

        if (!string.Equals(missingKey, SkillFileFormat.NameMetadataKey, StringComparison.Ordinal))
        {
            lines.Add("name: bugfix-pro");
        }

        if (!string.Equals(missingKey, SkillFileFormat.DescriptionMetadataKey, StringComparison.Ordinal))
        {
            lines.Add("description: Corrige bugs com menor mudanca segura.");
        }

        if (!string.Equals(missingKey, SkillFileFormat.InstructionMetadataKey, StringComparison.Ordinal))
        {
            lines.Add("instruction: Identifique causa raiz e valide com testes.");
        }

        lines.Add("---");
        var content = string.Join(Environment.NewLine, lines);

        var exception = Assert.Throws<InvalidOperationException>(
            () => SkillFileFormat.Parse(content, "SKILL.md"));

        Assert.Contains($"metadado obrigatorio '{missingKey}'", exception.Message);
    }

    [Fact]
    public void Parse_WhenMetadataHeaderDoesNotStartWithDelimiter_ThrowsInvalidOperationException()
    {
        const string content =
            """
            name: sem-front-matter
            description: invalido
            instruction: invalido
            """;

        var exception = Assert.Throws<InvalidOperationException>(
            () => SkillFileFormat.Parse(content));

        Assert.Contains("deve iniciar com '---'", exception.Message);
    }

    [Fact]
    public void Parse_WhenMetadataKeyIsDuplicated_ThrowsInvalidOperationException()
    {
        const string content =
            """
            ---
            name: docs-writer
            name: docs-writer-2
            description: Escreve documentacao com objetivo e limites.
            instruction: Produza guias acionaveis.
            ---
            """;

        var exception = Assert.Throws<InvalidOperationException>(
            () => SkillFileFormat.Parse(content));

        Assert.Contains("foi definido mais de uma vez", exception.Message);
    }

    [Fact]
    public void Parse_WhenMetadataKeyIsNotSupported_ThrowsInvalidOperationException()
    {
        const string content =
            """
            ---
            name: docs-writer
            description: Escreve documentacao com objetivo e limites.
            instruction: Produza guias acionaveis.
            owner: platform-team
            ---
            """;

        var exception = Assert.Throws<InvalidOperationException>(
            () => SkillFileFormat.Parse(content, "skills/docs-writer/SKILL.md"));

        Assert.Contains("nao faz parte do schema suportado", exception.Message);
        Assert.Contains("name, description, instruction", exception.Message);
        Assert.Contains("skills/docs-writer/SKILL.md", exception.Message);
    }

    [Fact]
    public void Parse_WhenNameMetadataIsNotKebabCase_ThrowsInvalidOperationException()
    {
        const string content =
            """
            ---
            name: Docs Writer
            description: Escreve documentacao com objetivo e limites.
            instruction: Produza guias acionaveis.
            ---
            """;

        var exception = Assert.Throws<InvalidOperationException>(
            () => SkillFileFormat.Parse(content, "skills/docs-writer/SKILL.md"));

        Assert.Contains("deve usar kebab-case", exception.Message);
        Assert.Contains("'name'", exception.Message);
    }

    [Fact]
    public void Parse_WhenMetadataHeaderIsInvalid_IncludesSchemaHint()
    {
        const string content =
            """
            name: sem-front-matter
            description: invalido
            instruction: invalido
            """;

        var exception = Assert.Throws<InvalidOperationException>(
            () => SkillFileFormat.Parse(content));

        Assert.Contains("Schema esperado", exception.Message);
    }

    [Fact]
    public void BuildTemplate_CreatesSkillMarkdownWithMandatoryMetadata()
    {
        var content = SkillFileFormat.BuildTemplate(
            name: "code-review-api",
            description: "Revisa APIs e contratos.",
            instruction:
            """
            Aja como code reviewer.
            Priorize bugs, riscos e testes faltantes.
            """);

        var skill = SkillFileFormat.Parse(content, "template/SKILL.md");

        Assert.Equal("code-review-api", skill.Name);
        Assert.Equal("Revisa APIs e contratos.", skill.Description);
        Assert.Equal(
            """
            Aja como code reviewer.
            Priorize bugs, riscos e testes faltantes.
            """,
            skill.Instruction);
    }

    [Fact]
    public void BuildTemplate_WhenNameMetadataIsNotKebabCase_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => SkillFileFormat.BuildTemplate(
                name: "Code Review",
                description: "Descricao valida.",
                instruction: "Instrucao valida."));

        Assert.Contains("deve usar kebab-case", exception.Message);
    }

    [Fact]
    public void RequiredMetadataKeys_ContainsExpectedSchema()
    {
        Assert.Equal(
            [
                SkillFileFormat.NameMetadataKey,
                SkillFileFormat.DescriptionMetadataKey,
                SkillFileFormat.InstructionMetadataKey
            ],
            SkillFileFormat.RequiredMetadataKeys);
    }
}
