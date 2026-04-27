using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class AgentValidationCommandCatalogTests
{
    [Fact]
    public void Discover_WhenDotNetSolutionExists_ReturnsBuildTestAndLintCommands()
    {
        var directoryPath = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directoryPath, "Sample.slnx"), string.Empty);

            var commands = AgentValidationCommandCatalog.Discover(directoryPath);

            Assert.Collection(
                commands,
                static command =>
                {
                    Assert.Equal("build", command.Name);
                    Assert.Contains("dotnet build Sample.slnx", command.CommandLine);
                },
                static command =>
                {
                    Assert.Equal("test", command.Name);
                    Assert.Contains("dotnet test Sample.slnx", command.CommandLine);
                },
                static command =>
                {
                    Assert.Equal("lint", command.Name);
                    Assert.Contains("dotnet format Sample.slnx --verify-no-changes", command.CommandLine);
                });
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void Discover_WhenNoKnownProjectFilesExist_ReturnsEmpty()
    {
        var directoryPath = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directoryPath, "README.md"), "# docs");

            var commands = AgentValidationCommandCatalog.Discover(directoryPath);

            Assert.Empty(commands);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            $"asxrun-validation-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }
}
