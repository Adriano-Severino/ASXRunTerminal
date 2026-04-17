namespace ASXRunTerminal.Core;

internal sealed record McpServerDefinition
{
    public McpServerDefinition(
        string name,
        McpServerProcessOptions? processOptions = null,
        McpServerRemoteOptions? remoteOptions = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "O nome do servidor MCP nao pode ser vazio.",
                nameof(name));
        }

        var hasProcessOptions = processOptions is not null;
        var hasRemoteOptions = remoteOptions is not null;
        if (hasProcessOptions == hasRemoteOptions)
        {
            throw new ArgumentException(
                "Informe exatamente uma configuracao MCP: processo local (stdio) ou servidor remoto.");
        }

        Name = name.Trim();
        ProcessOptions = processOptions;
        RemoteOptions = remoteOptions;
    }

    public string Name { get; }

    public McpServerProcessOptions? ProcessOptions { get; }

    public McpServerRemoteOptions? RemoteOptions { get; }

    public bool IsStdio => ProcessOptions is not null;

    public static McpServerDefinition Stdio(
        string name,
        McpServerProcessOptions processOptions)
    {
        ArgumentNullException.ThrowIfNull(processOptions);
        return new McpServerDefinition(name, processOptions: processOptions);
    }

    public static McpServerDefinition Remote(
        string name,
        McpServerRemoteOptions remoteOptions)
    {
        ArgumentNullException.ThrowIfNull(remoteOptions);
        return new McpServerDefinition(name, remoteOptions: remoteOptions);
    }
}
