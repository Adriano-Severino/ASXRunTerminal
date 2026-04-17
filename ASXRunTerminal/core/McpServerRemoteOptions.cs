namespace ASXRunTerminal.Core;

internal sealed record McpServerRemoteOptions
{
    public McpServerRemoteOptions(
        Uri endpoint,
        McpRemoteTransportKind transportKind = McpRemoteTransportKind.Http,
        Uri? messageEndpoint = null,
        McpAuthenticationOptions? authentication = null,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        Endpoint = ValidateEndpoint(endpoint, nameof(endpoint));
        TransportKind = transportKind;
        MessageEndpoint = messageEndpoint is null
            ? null
            : ValidateEndpoint(messageEndpoint, nameof(messageEndpoint));
        Authentication = authentication ?? McpAuthenticationOptions.None;
        Headers = NormalizeHeaders(headers);
    }

    public Uri Endpoint { get; }

    public McpRemoteTransportKind TransportKind { get; }

    public Uri? MessageEndpoint { get; }

    public McpAuthenticationOptions Authentication { get; }

    public IReadOnlyDictionary<string, string> Headers { get; }

    public static implicit operator McpServerRemoteOptions(string endpoint)
    {
        return new McpServerRemoteOptions(ParseEndpoint(endpoint));
    }

    private static Uri ParseEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException(
                "A URL do servidor MCP remoto nao pode ser vazia.",
                nameof(endpoint));
        }

        if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var parsedEndpoint))
        {
            throw new ArgumentException(
                "A URL do servidor MCP remoto deve ser absoluta.",
                nameof(endpoint));
        }

        return parsedEndpoint;
    }

    private static Uri ValidateEndpoint(Uri endpoint, string parameterName)
    {
        if (!endpoint.IsAbsoluteUri)
        {
            throw new ArgumentException(
                "A URL do servidor MCP remoto deve ser absoluta.",
                parameterName);
        }

        var isHttp = string.Equals(
            endpoint.Scheme,
            Uri.UriSchemeHttp,
            StringComparison.OrdinalIgnoreCase);
        var isHttps = string.Equals(
            endpoint.Scheme,
            Uri.UriSchemeHttps,
            StringComparison.OrdinalIgnoreCase);

        if (!isHttp && !isHttps)
        {
            throw new ArgumentException(
                "O servidor MCP remoto deve usar os esquemas 'http' ou 'https'.",
                parameterName);
        }

        return endpoint;
    }

    private static IReadOnlyDictionary<string, string> NormalizeHeaders(
        IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null || headers.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var normalizedHeaders = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            if (string.IsNullOrWhiteSpace(header.Key))
            {
                throw new ArgumentException(
                    "O nome de um cabecalho MCP remoto nao pode ser vazio.",
                    nameof(headers));
            }

            if (header.Value is null)
            {
                throw new ArgumentException(
                    $"O valor do cabecalho '{header.Key}' nao pode ser nulo.",
                    nameof(headers));
            }

            normalizedHeaders[header.Key.Trim()] = header.Value.Trim();
        }

        return normalizedHeaders;
    }
}
