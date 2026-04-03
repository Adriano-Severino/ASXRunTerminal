namespace ASXRunTerminal.Infra;

internal static class ConsoleLogger
{
    public static void Info(string message)
    {
        Write(Console.Out, "INFO", message);
    }

    public static void Error(string message)
    {
        Write(Console.Error, "ERROR", message);
    }

    private static void Write(TextWriter output, string level, string message)
    {
        output.WriteLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}");
    }
}
