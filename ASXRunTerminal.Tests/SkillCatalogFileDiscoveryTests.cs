using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class SkillCatalogFileDiscoveryTests
{
    [Fact]
    public void DiscoverSkillFiles_SearchesRecursivelyAndFiltersBySupportedExtension()
    {
        var root = CreateTemporaryDirectory();
        var localSkillsDirectory = Path.Combine(root, "workspace", ".asxrun", "skills");
        var userSkillsDirectory = Path.Combine(root, "home", ".asxrun", "skills");

        var localSkillFile = CreateFile(localSkillsDirectory, "backend", "api-review.md");
        var localSkillTemplateFile = CreateFile(localSkillsDirectory, "templates", "SKILL.MD");
        var ignoredTextFile = CreateFile(localSkillsDirectory, "templates", "README.txt");
        var userSkillFile = CreateFile(userSkillsDirectory, "ops", "incident-response.md");
        var ignoredYamlFile = CreateFile(userSkillsDirectory, "ops", "incident-response.yaml");

        var discoveredFiles = SkillCatalog.DiscoverSkillFiles(
            discoveryDirectories: [localSkillsDirectory, userSkillsDirectory]);

        Assert.Equal(3, discoveredFiles.Count);
        Assert.Contains(Path.GetFullPath(localSkillFile), discoveredFiles);
        Assert.Contains(Path.GetFullPath(localSkillTemplateFile), discoveredFiles);
        Assert.Contains(Path.GetFullPath(userSkillFile), discoveredFiles);
        Assert.DoesNotContain(Path.GetFullPath(ignoredTextFile), discoveredFiles);
        Assert.DoesNotContain(Path.GetFullPath(ignoredYamlFile), discoveredFiles);
    }

    [Fact]
    public void DiscoverSkillFiles_WhenDirectoryDoesNotExist_ReturnsEmptyCollection()
    {
        var missingSkillsDirectory = Path.Combine(
            Path.GetTempPath(),
            "asxrun-skill-file-discovery-tests",
            Guid.NewGuid().ToString("N"),
            "missing",
            ".asxrun",
            "skills");

        var discoveredFiles = SkillCatalog.DiscoverSkillFiles(
            discoveryDirectories: [missingSkillsDirectory]);

        Assert.Empty(discoveredFiles);
    }

    [Fact]
    public void DiscoverSkillFiles_AcceptsCustomSupportedExtensions()
    {
        var root = CreateTemporaryDirectory();
        var skillsDirectory = Path.Combine(root, ".asxrun", "skills");

        var yamlFile = CreateFile(skillsDirectory, "custom", "api-security.yaml");
        var ymlFile = CreateFile(skillsDirectory, "custom", "api-security.YML");
        var ignoredMarkdownFile = CreateFile(skillsDirectory, "custom", "api-security.md");

        var discoveredFiles = SkillCatalog.DiscoverSkillFiles(
            discoveryDirectories: [skillsDirectory],
            supportedFileExtensions: ["yaml", ".yml"]);

        Assert.Equal(2, discoveredFiles.Count);
        Assert.Contains(Path.GetFullPath(yamlFile), discoveredFiles);
        Assert.Contains(Path.GetFullPath(ymlFile), discoveredFiles);
        Assert.DoesNotContain(Path.GetFullPath(ignoredMarkdownFile), discoveredFiles);
    }

    [Fact]
    public void SupportedSkillFileExtensions_UsesMarkdownAsDefault()
    {
        Assert.Equal([".md"], SkillCatalog.SupportedSkillFileExtensions);
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "asxrun-skill-file-discovery-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string CreateFile(string rootDirectory, params string[] pathSegments)
    {
        var relativeFilePath = Path.Combine(pathSegments);
        var filePath = Path.Combine(rootDirectory, relativeFilePath);
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException("O diretorio do arquivo nao pode ser nulo.");

        Directory.CreateDirectory(directory);
        File.WriteAllText(filePath, "skill content");
        return filePath;
    }
}
