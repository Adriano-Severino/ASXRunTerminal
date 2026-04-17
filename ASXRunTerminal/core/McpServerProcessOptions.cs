namespace ASXRunTerminal.Core;

internal sealed record McpServerProcessOptions
{
    public McpServerProcessOptions(
        string command,
        IReadOnlyList<string>? arguments = null,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException(
                "O comando do servidor MCP nao pode ser vazio.",
                nameof(command));
        }

        Command = command.Trim();
        Arguments = arguments?.ToArray() ?? [];
        WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? null
            : workingDirectory.Trim();
        EnvironmentVariables = environmentVariables is null
            ? []
            : new Dictionary<string, string>(environmentVariables, StringComparer.Ordinal);
    }

    public string Command { get; }

    public IReadOnlyList<string> Arguments { get; }

    public string? WorkingDirectory { get; }

    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; }

    public static implicit operator McpServerProcessOptions(string command)
    {
        return new McpServerProcessOptions(command);
    }
}
