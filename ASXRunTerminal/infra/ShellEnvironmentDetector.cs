using System.Runtime.InteropServices;

namespace ASXRunTerminal.Infra;

internal static class ShellEnvironmentDetector
{
    public static string? ResolveDefaultShell(Func<OSPlatform, bool>? isOSPlatform = null)
    {
        isOSPlatform ??= RuntimeInformation.IsOSPlatform;

        if (isOSPlatform(OSPlatform.Windows))
        {
            return "powershell";
        }

        if (isOSPlatform(OSPlatform.OSX))
        {
            return "zsh";
        }

        if (isOSPlatform(OSPlatform.Linux))
        {
            return "bash";
        }

        return null;
    }
}
