using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;

namespace ASXRunTerminal.Tests;

public sealed class ShellCommandGuardrailEvaluatorTests
{
    [Fact]
    public void HasExplicitDestructiveCommandApproval_WhenApprovalArgumentIsMissing_ReturnsFalse()
    {
        var arguments = new Dictionary<string, string>
        {
            ["script"] = "rm -rf ./tmp"
        };

        var hasApproval = ShellCommandGuardrailEvaluator.HasExplicitDestructiveCommandApproval(arguments);

        Assert.False(hasApproval);
    }

    [Fact]
    public void HasExplicitDestructiveCommandApproval_WhenApprovalArgumentIsAffirmativeAndCaseInsensitive_ReturnsTrue()
    {
        var arguments = new Dictionary<string, string>
        {
            ["script"] = "rm -rf ./tmp",
            [ShellCommandPermissionPolicy.DestructiveApprovalArgumentName.ToUpperInvariant()] = "YES"
        };

        var hasApproval = ShellCommandGuardrailEvaluator.HasExplicitDestructiveCommandApproval(arguments);

        Assert.True(hasApproval);
    }

    [Fact]
    public void HasExplicitDestructiveCommandApproval_WhenApprovalArgumentIsNotAffirmative_ReturnsFalse()
    {
        var arguments = new Dictionary<string, string>
        {
            ["script"] = "rm -rf ./tmp",
            [ShellCommandPermissionPolicy.DestructiveApprovalArgumentName] = "nao"
        };

        var hasApproval = ShellCommandGuardrailEvaluator.HasExplicitDestructiveCommandApproval(arguments);

        Assert.False(hasApproval);
    }
}
