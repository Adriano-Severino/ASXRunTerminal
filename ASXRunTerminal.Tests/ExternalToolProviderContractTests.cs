using System.Runtime.InteropServices;
using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;

namespace ASXRunTerminal.Tests;

public sealed class ExternalToolProviderContractTests
{
    [Fact]
    public void ListTools_ExposesScriptContract_OnSupportedPlatform()
    {
        var provider = CreateDefaultProviderForCurrentPlatform();
        if (provider is null)
        {
            return;
        }

        var tools = provider.ListTools();

        Assert.NotEmpty(tools);
        foreach (var tool in tools)
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Name));
            Assert.False(string.IsNullOrWhiteSpace(tool.Description));

            var scriptParameter = Assert.Single(
                tool.Parameters,
                static parameter => string.Equals(parameter.Name, "script", StringComparison.Ordinal));
            Assert.True(scriptParameter.IsRequired);

            var destructiveApprovalParameter = Assert.Single(
                tool.Parameters,
                static parameter => string.Equals(
                    parameter.Name,
                    ShellCommandPermissionPolicy.DestructiveApprovalArgumentName,
                    StringComparison.Ordinal));
            Assert.False(destructiveApprovalParameter.IsRequired);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationError_WhenScriptIsMissing()
    {
        var provider = CreateDefaultProviderForCurrentPlatform();
        if (provider is null)
        {
            return;
        }

        var toolName = ResolveExecutableToolName(provider);
        var request = new ToolExecutionRequest(
            ToolName: toolName,
            Arguments: new Dictionary<string, string>());

        var result = await provider.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("script", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBlockedExitCode_WhenCommandIsDeniedByPolicy()
    {
        var provider = CreateProviderWithDeniedCommandPolicy(out var blockedScript);
        if (provider is null)
        {
            return;
        }

        var toolName = ResolveExecutableToolName(provider);
        var request = new ToolExecutionRequest(
            ToolName: toolName,
            Arguments: new Dictionary<string, string>
            {
                ["script"] = blockedScript
            });

        var result = await provider.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ShellCommandPermissionPolicy.BlockedCommandExitCode, result.ExitCode);
        Assert.Contains("bloqueado", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccessResult_ForSimpleScript()
    {
        var provider = CreateDefaultProviderForCurrentPlatform();
        if (provider is null)
        {
            return;
        }

        var toolName = ResolveExecutableToolName(provider);
        var script = BuildSimpleSuccessScript(toolName);
        var request = new ToolExecutionRequest(
            ToolName: toolName,
            Arguments: new Dictionary<string, string>
            {
                ["script"] = script
            });

        var result = await provider.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("contract-ok", result.StdOut);
        Assert.Null(result.Error);
    }

    private static IToolProvider? CreateDefaultProviderForCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new PowerShellToolProvider();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new UnixShellToolProvider();
        }

        return null;
    }

    private static IToolProvider? CreateProviderWithDeniedCommandPolicy(out string blockedScript)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            blockedScript = "Write-Output 'blocked by contract'";
            return new PowerShellToolProvider(
                () => new ShellCommandPermissionPolicy(blockedCommands: ["write-output"]));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            blockedScript = "echo 'blocked by contract'";
            return new UnixShellToolProvider(
                () => new ShellCommandPermissionPolicy(blockedCommands: ["echo"]));
        }

        blockedScript = string.Empty;
        return null;
    }

    private static string ResolveExecutableToolName(IToolProvider provider)
    {
        var declaredTools = provider.ListTools();
        Assert.NotEmpty(declaredTools);

        if (declaredTools.Any(static tool => string.Equals(tool.Name, "powershell", StringComparison.OrdinalIgnoreCase)))
        {
            return "powershell";
        }

        if (declaredTools.Any(static tool => string.Equals(tool.Name, "bash", StringComparison.OrdinalIgnoreCase)))
        {
            return "bash";
        }

        return declaredTools[0].Name;
    }

    private static string BuildSimpleSuccessScript(string toolName)
    {
        return string.Equals(toolName, "powershell", StringComparison.OrdinalIgnoreCase)
            ? "Write-Output 'contract-ok'"
            : "echo 'contract-ok'";
    }
}
