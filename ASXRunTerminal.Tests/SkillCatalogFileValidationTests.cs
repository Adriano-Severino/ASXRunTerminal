using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class SkillCatalogFileValidationTests
{
    [Fact]
    public void LoadFileSkills_WithValidSkillFile_ReturnsParsedSkill()
    {
        var root = CreateTemporaryDirectory();
        var skillsDirectory = Path.Combine(root, ".asxrun", "skills");
        CreateSkillFile(
            skillsDirectory,
            "backend",
            "api-review.md",
            BuildValidSkillContent(
                name: "api-review",
                description: "Revisa contratos e erros de API.",
                instruction:
                """
                Atue como revisor tecnico.
                Priorize corretude e regressao.
                """));

        var loadedSkills = SkillCatalog.LoadFileSkills(
            discoveryDirectories: [skillsDirectory]);

        var loadedSkill = Assert.Single(loadedSkills);
        Assert.Equal("api-review", loadedSkill.Name);
        Assert.Equal("Revisa contratos e erros de API.", loadedSkill.Description);
        Assert.Equal(
            """
            Atue como revisor tecnico.
            Priorize corretude e regressao.
            """,
            loadedSkill.Instruction);
    }

    [Fact]
    public void LoadFileSkills_WhenSchemaIsInvalid_ThrowsFriendlyError()
    {
        var root = CreateTemporaryDirectory();
        var skillsDirectory = Path.Combine(root, ".asxrun", "skills");
        var invalidSkillPath = CreateSkillFile(
            skillsDirectory,
            "ops",
            "incident-response.md",
            """
            ---
            name: incident-response
            description: Guia de resposta a incidentes.
            ---
            """);

        var exception = Assert.Throws<InvalidOperationException>(
            () => SkillCatalog.LoadFileSkills(
                discoveryDirectories: [skillsDirectory]));

        Assert.Contains("Arquivo de skill invalido", exception.Message);
        Assert.Contains(Path.GetFullPath(invalidSkillPath), exception.Message);
        Assert.Contains("metadado obrigatorio 'instruction'", exception.Message);
        Assert.Contains("Schema esperado", exception.Message);
    }

    [Fact]
    public void LoadFileSkills_WhenFileCannotBeRead_ThrowsFriendlyError()
    {
        var root = CreateTemporaryDirectory();
        var skillsDirectory = Path.Combine(root, ".asxrun", "skills");
        var skillFilePath = CreateSkillFile(
            skillsDirectory,
            "docs",
            "docs-writer.md",
            BuildValidSkillContent(
                name: "docs-writer-plus",
                description: "Escreve guias tecnicos claros.",
                instruction: "Produza passos acionaveis."));

        var exception = Assert.Throws<InvalidOperationException>(
            () => SkillCatalog.LoadFileSkills(
                discoveryDirectories: [skillsDirectory],
                fileContentReader: static _ => throw new IOException("Acesso negado ao arquivo.")));

        Assert.Contains("Arquivo de skill invalido", exception.Message);
        Assert.Contains(Path.GetFullPath(skillFilePath), exception.Message);
        Assert.Contains("Nao foi possivel ler o arquivo", exception.Message);
        Assert.Contains("Acesso negado ao arquivo.", exception.Message);
    }

    [Fact]
    public void LoadFileSkills_WhenNameIsDuplicatedAcrossFiles_ThrowsFriendlyError()
    {
        var root = CreateTemporaryDirectory();
        var skillsDirectory = Path.Combine(root, ".asxrun", "skills");
        var firstSkillPath = CreateSkillFile(
            skillsDirectory,
            "backend",
            "api-review.md",
            BuildValidSkillContent(
                name: "api-review",
                description: "Revisa APIs.",
                instruction: "Priorize corretude."));
        var secondSkillPath = CreateSkillFile(
            skillsDirectory,
            "platform",
            "api-review-v2.md",
            BuildValidSkillContent(
                name: "api-review",
                description: "Revisa APIs da plataforma.",
                instruction: "Priorize contratos e testes."));

        var exception = Assert.Throws<InvalidOperationException>(
            () => SkillCatalog.LoadFileSkills(
                discoveryDirectories: [skillsDirectory]));

        Assert.Contains("Arquivo de skill invalido", exception.Message);
        Assert.Contains(Path.GetFullPath(secondSkillPath), exception.Message);
        Assert.Contains(Path.GetFullPath(firstSkillPath), exception.Message);
        Assert.Contains("metadado obrigatorio 'name' esta duplicado", exception.Message);
    }

    [Fact]
    public void LoadFileSkills_WhenNoFilesAreDiscovered_ReturnsEmptyCollection()
    {
        var missingSkillsDirectory = Path.Combine(
            Path.GetTempPath(),
            "asxrun-skill-file-validation-tests",
            Guid.NewGuid().ToString("N"),
            "missing",
            ".asxrun",
            "skills");

        var loadedSkills = SkillCatalog.LoadFileSkills(
            discoveryDirectories: [missingSkillsDirectory]);

        Assert.Empty(loadedSkills);
    }

    [Fact]
    public void LoadFileSkills_UsesInjectedFileContentReaderForDiscoveredFiles()
    {
        var root = CreateTemporaryDirectory();
        var skillsDirectory = Path.Combine(root, ".asxrun", "skills");
        var firstFile = CreateSkillFile(skillsDirectory, "backend", "api.md", "placeholder");
        var secondFile = CreateSkillFile(skillsDirectory, "docs", "docs.md", "placeholder");
        var firstResolvedPath = Path.GetFullPath(firstFile);
        var secondResolvedPath = Path.GetFullPath(secondFile);
        var contentByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [firstResolvedPath] = BuildValidSkillContent(
                name: "api-review-injected",
                description: "Revisa API por conteudo injetado.",
                instruction: "Use contratos e testes."),
            [secondResolvedPath] = BuildValidSkillContent(
                name: "docs-review-injected",
                description: "Revisa docs por conteudo injetado.",
                instruction: "Use clareza e exemplos.")
        };
        var readPaths = new List<string>();

        var loadedSkills = SkillCatalog.LoadFileSkills(
            discoveryDirectories: [skillsDirectory],
            fileContentReader: path =>
            {
                readPaths.Add(path);
                return contentByPath[path];
            });

        Assert.Equal(2, readPaths.Count);
        Assert.Contains(firstResolvedPath, readPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(secondResolvedPath, readPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(
            loadedSkills,
            static skill => string.Equals(skill.Name, "api-review-injected", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            loadedSkills,
            static skill => string.Equals(skill.Name, "docs-review-injected", StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "asxrun-skill-file-validation-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string CreateSkillFile(
        string rootDirectory,
        string subDirectory,
        string fileName,
        string content)
    {
        var directory = Path.Combine(rootDirectory, subDirectory);
        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private static string BuildValidSkillContent(
        string name,
        string description,
        string instruction)
    {
        var normalizedInstructionLines = instruction
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        var lines = new List<string>
        {
            "---",
            $"name: {name}",
            $"description: {description}",
            "instruction: |"
        };

        foreach (var line in normalizedInstructionLines)
        {
            lines.Add($"  {line}");
        }

        lines.Add("---");
        return string.Join(Environment.NewLine, lines);
    }
}
