using ASXRunTerminal.Config;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class AgentAutonomyPolicyFileTests
{
    [Fact]
    public void Load_WhenPolicyFileIsMissing_ReturnsDefaultPolicy()
    {
        var workspaceRoot = CreateWorkspaceRoot();

        var policy = AgentAutonomyPolicyFile.Load(workspaceRoot);

        Assert.Equal(AgentAutonomyLevel.Autonomous, policy.Level);
        Assert.Equal("autonomo", policy.LevelName);
        Assert.True(policy.AllowsAutomaticValidation);
    }

    [Theory]
    [InlineData("assistido", "assistido", false)]
    [InlineData("semi-autonomo", "semi-autonomo", true)]
    [InlineData("autonomo", "autonomo", true)]
    public void Load_WhenPolicyFileIsValid_ReturnsConfiguredPolicy(
        string configuredLevel,
        string expectedLevelName,
        bool expectedAutomaticValidation)
    {
        var workspaceRoot = CreateWorkspaceRoot();
        var policyDirectory = Path.Combine(workspaceRoot, UserConfigFile.ConfigDirectoryName);
        var policyPath = Path.Combine(policyDirectory, AgentAutonomyPolicyFile.AgentAutonomyPolicyFileName);

        Directory.CreateDirectory(policyDirectory);
        File.WriteAllText(
            policyPath,
            $$"""
            {
              "autonomyLevel": "{{configuredLevel}}"
            }
            """);

        var policy = AgentAutonomyPolicyFile.Load(workspaceRoot);

        Assert.Equal(expectedLevelName, policy.LevelName);
        Assert.Equal(expectedAutomaticValidation, policy.AllowsAutomaticValidation);
        Assert.Equal(
            string.Equals(expectedLevelName, "autonomo", StringComparison.Ordinal),
            policy.AllowsAutoCorrection);
    }

    [Fact]
    public void Load_WhenSnakeCaseLevelIsConfigured_ReturnsConfiguredPolicy()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        var policyDirectory = Path.Combine(workspaceRoot, UserConfigFile.ConfigDirectoryName);
        var policyPath = Path.Combine(policyDirectory, AgentAutonomyPolicyFile.AgentAutonomyPolicyFileName);

        Directory.CreateDirectory(policyDirectory);
        File.WriteAllText(
            policyPath,
            """
            {
              "autonomy_level": "semi_autonomo"
            }
            """);

        var policy = AgentAutonomyPolicyFile.Load(workspaceRoot);

        Assert.Equal(AgentAutonomyLevel.SemiAutonomous, policy.Level);
        Assert.Equal("semi-autonomo", policy.LevelName);
    }

    [Fact]
    public void Load_WhenLevelIsInvalid_ThrowsInvalidOperationException()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        var policyDirectory = Path.Combine(workspaceRoot, UserConfigFile.ConfigDirectoryName);
        var policyPath = Path.Combine(policyDirectory, AgentAutonomyPolicyFile.AgentAutonomyPolicyFileName);

        Directory.CreateDirectory(policyDirectory);
        File.WriteAllText(
            policyPath,
            """
            {
              "autonomyLevel": "irrestrito"
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(
            () => AgentAutonomyPolicyFile.Load(workspaceRoot));

        Assert.Contains("autonomyLevel", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("assistido", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentAutonomyLevelName_WhenConvertedFromEnum_ReturnsCanonicalName()
    {
        AgentAutonomyLevelName levelName = AgentAutonomyLevel.SemiAutonomous;

        Assert.Equal("semi-autonomo", (string)levelName);
    }

    private static string CreateWorkspaceRoot()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "asxrun-agent-autonomy-policy-file-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);
        return workspaceRoot;
    }
}
