using ASXRunTerminal.Core;

namespace ASXRunTerminal.Infra;

internal static class ShellCommandGuardrailEvaluator
{
    public static ToolExecutionResult? ValidateScript(
        string toolName,
        string script,
        Func<ShellCommandPermissionPolicy> policyResolver,
        bool isDestructiveCommandApproved = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        ArgumentNullException.ThrowIfNull(policyResolver);

        ShellCommandPermissionPolicy policy;
        try
        {
            policy = policyResolver();
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or DirectoryNotFoundException
            or IOException
            or UnauthorizedAccessException)
        {
            return ToolExecutionResult.Failure(
                error: $"Nao foi possivel carregar a politica de comandos de shell: {ex.Message}",
                exitCode: 1,
                duration: TimeSpan.Zero);
        }

        try
        {
            policy.EnsureAllowed(
                shellToolName: toolName,
                script: script,
                isDestructiveCommandApproved: isDestructiveCommandApproved);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolExecutionResult.Failure(
                error: ex.Message,
                exitCode: ShellCommandPermissionPolicy.BlockedCommandExitCode,
                duration: TimeSpan.Zero);
        }
    }

    public static bool HasExplicitDestructiveCommandApproval(
        IReadOnlyDictionary<string, string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!TryGetApprovalValue(arguments, out var approvalValue))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(approvalValue))
        {
            return false;
        }

        var normalizedApproval = approvalValue.Trim();
        return string.Equals(normalizedApproval, "sim", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalizedApproval, "s", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalizedApproval, "yes", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalizedApproval, "y", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalizedApproval, "true", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalizedApproval, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetApprovalValue(
        IReadOnlyDictionary<string, string> arguments,
        out string? approvalValue)
    {
        if (arguments.TryGetValue(
                ShellCommandPermissionPolicy.DestructiveApprovalArgumentName,
                out approvalValue))
        {
            return true;
        }

        foreach (var entry in arguments)
        {
            if (!string.Equals(
                    entry.Key,
                    ShellCommandPermissionPolicy.DestructiveApprovalArgumentName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            approvalValue = entry.Value;
            return true;
        }

        approvalValue = null;
        return false;
    }
}
