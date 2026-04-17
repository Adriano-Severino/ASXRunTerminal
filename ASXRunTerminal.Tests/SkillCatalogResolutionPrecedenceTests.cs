using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class SkillCatalogResolutionPrecedenceTests
{
    [Fact]
    public void TryFind_WhenSkillExistsInAllSources_PrefersProjectLocalSkill()
    {
        var root = CreateTemporaryDirectory();
        var localSkillsDirectory = Path.Combine(root, "workspace", ".asxrun", "skills");
        var userSkillsDirectory = Path.Combine(root, "home", ".asxrun", "skills");

        CreateSkillFile(
            localSkillsDirectory,
            "project",
            "code-review.md",
            BuildValidSkillContent(
                name: "code-review",
                description: "Code review local do projeto.",
                instruction: "Use padrao local."));
        CreateSkillFile(
            userSkillsDirectory,
            "user",
            "code-review.md",
            BuildValidSkillContent(
                name: "code-review",
                description: "Code review do usuario.",
                instruction: "Use padrao de usuario."));

        var wasFound = SkillCatalog.TryFind(
            "code-review",
            out var skill,
            discoveryDirectories: [localSkillsDirectory, userSkillsDirectory]);

        Assert.True(wasFound);
        Assert.Equal("Code review local do projeto.", skill.Description);
        Assert.Equal("Use padrao local.", skill.Instruction);
    }

    [Fact]
    public void TryFind_WhenLocalSkillIsMissing_PrefersUserSkillOverBuiltIn()
    {
        var root = CreateTemporaryDirectory();
        var localSkillsDirectory = Path.Combine(root, "workspace", ".asxrun", "skills");
        var userSkillsDirectory = Path.Combine(root, "home", ".asxrun", "skills");

        CreateSkillFile(
            userSkillsDirectory,
            "user",
            "bugfix.md",
            BuildValidSkillContent(
                name: "bugfix",
                description: "Bugfix customizado do usuario.",
                instruction: "Priorize ambiente local do usuario."));

        var wasFound = SkillCatalog.TryFind(
            "bugfix",
            out var skill,
            discoveryDirectories: [localSkillsDirectory, userSkillsDirectory]);

        Assert.True(wasFound);
        Assert.Equal("Bugfix customizado do usuario.", skill.Description);
        Assert.Equal("Priorize ambiente local do usuario.", skill.Instruction);
    }

    [Fact]
    public void List_WhenNamesCollide_AppliesPrecedenceAndReturnsUniqueNames()
    {
        var root = CreateTemporaryDirectory();
        var localSkillsDirectory = Path.Combine(root, "workspace", ".asxrun", "skills");
        var userSkillsDirectory = Path.Combine(root, "home", ".asxrun", "skills");

        CreateSkillFile(
            localSkillsDirectory,
            "project",
            "docs-writer.md",
            BuildValidSkillContent(
                name: "docs-writer",
                description: "Docs local do projeto.",
                instruction: "Foque na documentacao local."));
        CreateSkillFile(
            localSkillsDirectory,
            "project",
            "release-checklist.md",
            BuildValidSkillContent(
                name: "release-checklist",
                description: "Checklist local de release.",
                instruction: "Validar itens de release do projeto."));
        CreateSkillFile(
            userSkillsDirectory,
            "user",
            "docs-writer.md",
            BuildValidSkillContent(
                name: "docs-writer",
                description: "Docs do usuario.",
                instruction: "Padrao de docs do usuario."));
        CreateSkillFile(
            userSkillsDirectory,
            "user",
            "release-checklist.md",
            BuildValidSkillContent(
                name: "release-checklist",
                description: "Checklist de release do usuario.",
                instruction: "Padrao de release do usuario."));

        var skills = SkillCatalog.List(
            discoveryDirectories: [localSkillsDirectory, userSkillsDirectory]);

        Assert.Equal(6, skills.Count);
        Assert.Equal(
            skills.Count,
            skills
                .Select(static skill => skill.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());

        var docsWriterSkill = skills.Single(
            static skill => string.Equals(skill.Name, "docs-writer", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Docs local do projeto.", docsWriterSkill.Description);

        var releaseChecklistSkill = skills.Single(
            static skill => string.Equals(skill.Name, "release-checklist", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Checklist local de release.", releaseChecklistSkill.Description);

        var builtInSkill = skills.Single(
            static skill => string.Equals(skill.Name, "code-review", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("code-review", builtInSkill.Name);
    }

    [Fact]
    public void List_WhenDiscoveryDirectoriesRepeatPaths_AppliesPrecedenceOncePerDirectory()
    {
        var root = CreateTemporaryDirectory();
        var localSkillsDirectory = Path.Combine(root, "workspace", ".asxrun", "skills");
        var userSkillsDirectory = Path.Combine(root, "home", ".asxrun", "skills");
        var skillName = $"precedence-repeat-{Guid.NewGuid():N}";

        CreateSkillFile(
            localSkillsDirectory,
            "project",
            "custom.md",
            BuildValidSkillContent(
                name: skillName,
                description: "Descricao local.",
                instruction: "Instrucao local."));
        CreateSkillFile(
            userSkillsDirectory,
            "user",
            "custom.md",
            BuildValidSkillContent(
                name: skillName,
                description: "Descricao do usuario.",
                instruction: "Instrucao do usuario."));

        var skills = SkillCatalog.List(
            discoveryDirectories:
            [
                $"  {localSkillsDirectory}  ",
                localSkillsDirectory,
                userSkillsDirectory,
                userSkillsDirectory
            ]);

        var resolvedSkill = skills.Single(
            skill => string.Equals(skill.Name, skillName, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Descricao local.", resolvedSkill.Description);
        Assert.Equal("Instrucao local.", resolvedSkill.Instruction);
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "asxrun-skill-resolution-precedence-tests",
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
