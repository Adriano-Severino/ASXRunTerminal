using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class SkillCatalogDiscoveryDirectoriesTests
{
    [Fact]
    public void GetDiscoveryDirectories_ReturnsLocalThenUserDirectory()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "asxrun-skill-discovery-tests",
            Guid.NewGuid().ToString("N"));
        var currentDirectory = Path.Combine(root, "workspace");
        var userHomeDirectory = Path.Combine(root, "home");

        var discoveredDirectories = SkillCatalog.GetDiscoveryDirectories(
            currentDirectoryResolver: () => currentDirectory,
            userHomeResolver: () => userHomeDirectory);

        var expectedLocalDirectory = Path.GetFullPath(Path.Combine(currentDirectory, ".asxrun", "skills"));
        var expectedUserDirectory = Path.GetFullPath(Path.Combine(userHomeDirectory, ".asxrun", "skills"));

        Assert.Equal(2, discoveredDirectories.Count);
        Assert.Equal(expectedLocalDirectory, discoveredDirectories[0]);
        Assert.Equal(expectedUserDirectory, discoveredDirectories[1]);
    }

    [Fact]
    public void GetDiscoveryDirectories_TrimsResolvedPaths()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "asxrun-skill-discovery-tests",
            Guid.NewGuid().ToString("N"));
        var currentDirectory = Path.Combine(root, "workspace");
        var userHomeDirectory = Path.Combine(root, "home");

        var discoveredDirectories = SkillCatalog.GetDiscoveryDirectories(
            currentDirectoryResolver: () => $"  {currentDirectory}  ",
            userHomeResolver: () => $"  {userHomeDirectory}  ");

        var expectedLocalDirectory = Path.GetFullPath(Path.Combine(currentDirectory, ".asxrun", "skills"));
        var expectedUserDirectory = Path.GetFullPath(Path.Combine(userHomeDirectory, ".asxrun", "skills"));

        Assert.Equal(expectedLocalDirectory, discoveredDirectories[0]);
        Assert.Equal(expectedUserDirectory, discoveredDirectories[1]);
    }

    [Fact]
    public void GetDiscoveryDirectories_WhenCurrentDirectoryIsMissing_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => SkillCatalog.GetDiscoveryDirectories(
                currentDirectoryResolver: static () => " ",
                userHomeResolver: static () => "/tmp/home"));

        Assert.Equal(
            "Nao foi possivel resolver o diretorio atual para descobrir skills.",
            exception.Message);
    }

    [Fact]
    public void GetDiscoveryDirectories_WhenUserHomeIsMissing_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => SkillCatalog.GetDiscoveryDirectories(
                currentDirectoryResolver: static () => "/tmp/workspace",
                userHomeResolver: static () => null));

        Assert.Equal(
            "Nao foi possivel resolver o diretorio home do usuario para descobrir skills.",
            exception.Message);
    }

    [Fact]
    public void GetDiscoveryDirectories_WhenLocalAndUserPathsMatch_ReturnsUniquePath()
    {
        var sharedBaseDirectory = Path.Combine(
            Path.GetTempPath(),
            "asxrun-skill-discovery-tests",
            Guid.NewGuid().ToString("N"),
            "shared");

        var discoveredDirectories = SkillCatalog.GetDiscoveryDirectories(
            currentDirectoryResolver: () => sharedBaseDirectory,
            userHomeResolver: () => sharedBaseDirectory);

        var expectedDirectory = Path.GetFullPath(
            Path.Combine(sharedBaseDirectory, ".asxrun", "skills"));

        var onlyDirectory = Assert.Single(discoveredDirectories);
        Assert.Equal(expectedDirectory, onlyDirectory);
    }

    [Fact]
    public void GetDiscoveryDirectories_UsesDetectedWorkspaceRoot_ForLocalSkillsDirectory()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "asxrun-skill-discovery-tests",
            Guid.NewGuid().ToString("N"));
        var solutionDirectory = Path.Combine(root, "src", "Backend");
        var nestedCurrentDirectory = Path.Combine(solutionDirectory, "Api", "Controllers");
        var userHomeDirectory = Path.Combine(root, "home");

        Directory.CreateDirectory(nestedCurrentDirectory);
        Directory.CreateDirectory(userHomeDirectory);
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        File.WriteAllText(Path.Combine(solutionDirectory, "backend.sln"), string.Empty);

        var discoveredDirectories = SkillCatalog.GetDiscoveryDirectories(
            currentDirectoryResolver: () => nestedCurrentDirectory,
            userHomeResolver: () => userHomeDirectory);

        var expectedLocalDirectory = Path.GetFullPath(
            Path.Combine(solutionDirectory, ".asxrun", "skills"));
        var expectedUserDirectory = Path.GetFullPath(
            Path.Combine(userHomeDirectory, ".asxrun", "skills"));

        Assert.Equal(2, discoveredDirectories.Count);
        Assert.Equal(expectedLocalDirectory, discoveredDirectories[0]);
        Assert.Equal(expectedUserDirectory, discoveredDirectories[1]);
    }
}
