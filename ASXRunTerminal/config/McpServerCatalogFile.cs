using System.Text;
using System.Text.Json;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Config;

internal static class McpServerCatalogFile
{
    internal const string McpServersFileName = "mcp-servers.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string EnsureExists(Func<string?>? userHomeResolver = null)
    {
        var catalogPath = GetCatalogPath(userHomeResolver);
        var catalogDirectory = Path.GetDirectoryName(catalogPath)
            ?? throw new InvalidOperationException(
                "Nao foi possivel resolver o diretorio de servidores MCP do usuario.");

        Directory.CreateDirectory(catalogDirectory);
        if (File.Exists(catalogPath))
        {
            return catalogPath;
        }

        File.WriteAllText(catalogPath, BuildDefaultCatalogContent());
        return catalogPath;
    }

    public static IReadOnlyList<McpServerDefinition> Load(Func<string?>? userHomeResolver = null)
    {
        var catalogPath = EnsureExists(userHomeResolver);
        var rawContent = File.ReadAllText(catalogPath);
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return [];
        }

        PersistedCatalog? persistedCatalog;
        try
        {
            persistedCatalog = JsonSerializer.Deserialize<PersistedCatalog>(rawContent, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Arquivo de servidores MCP invalido em '{catalogPath}'.",
                ex);
        }

        if (persistedCatalog?.Servers is null || persistedCatalog.Servers.Count == 0)
        {
            return [];
        }

        var servers = new List<McpServerDefinition>(persistedCatalog.Servers.Count);
        foreach (var persistedServer in persistedCatalog.Servers)
        {
            if (persistedServer is null)
            {
                throw new InvalidOperationException(
                    $"Arquivo de servidores MCP invalido em '{catalogPath}'. Um item nulo foi encontrado.");
            }

            try
            {
                McpServerDefinition server = persistedServer;
                servers.Add(server);
            }
            catch (Exception ex) when (
                ex is InvalidOperationException or ArgumentException or FormatException)
            {
                throw new InvalidOperationException(
                    $"Arquivo de servidores MCP invalido em '{catalogPath}'. {ex.Message}",
                    ex);
            }
        }

        EnsureUniqueServerNames(servers);
        return servers;
    }

    public static void Save(
        IReadOnlyList<McpServerDefinition> servers,
        Func<string?>? userHomeResolver = null)
    {
        ArgumentNullException.ThrowIfNull(servers);
        EnsureUniqueServerNames(servers);

        var catalogPath = EnsureExists(userHomeResolver);
        var persistedServers = servers
            .Select(static server => (PersistedServer)server)
            .ToArray();
        var payload = new PersistedCatalog(persistedServers);
        var content = JsonSerializer.Serialize(payload, JsonOptions);
        File.WriteAllText(catalogPath, $"{content}{Environment.NewLine}");
    }

    internal static string GetCatalogPath(Func<string?>? userHomeResolver = null)
    {
        var userHome = ResolveUserHome(userHomeResolver);
        return Path.Combine(userHome, UserConfigFile.ConfigDirectoryName, McpServersFileName);
    }

    private static string ResolveUserHome(Func<string?>? userHomeResolver)
    {
        var resolver = userHomeResolver ?? ResolveUserHomeFromEnvironment;
        var userHome = resolver();
        if (string.IsNullOrWhiteSpace(userHome))
        {
            throw new InvalidOperationException(
                "Nao foi possivel resolver o diretorio home do usuario para criar o catalogo MCP.");
        }

        return userHome.Trim();
    }

    private static string? ResolveUserHomeFromEnvironment()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static string BuildDefaultCatalogContent()
    {
        var defaultCatalog = new PersistedCatalog([]);
        var content = JsonSerializer.Serialize(defaultCatalog, JsonOptions);
        return $"{content}{Environment.NewLine}";
    }

    private static void EnsureUniqueServerNames(IEnumerable<McpServerDefinition> servers)
    {
        var duplicateName = servers
            .GroupBy(static server => server.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1)?
            .Key;

        if (string.IsNullOrWhiteSpace(duplicateName))
        {
            return;
        }

        throw new InvalidOperationException(
            $"A lista de servidores MCP contem nome duplicado: '{duplicateName}'.");
    }

    private static string ValidateNonEmptyValue(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(message);
        }

        return value.Trim();
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static Uri ParseAbsoluteUri(string? rawValue, string valueLabel)
    {
        var value = ValidateNonEmptyValue(
            rawValue,
            $"O campo '{valueLabel}' do servidor MCP remoto nao pode ser vazio.");

        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsedUri))
        {
            throw new InvalidOperationException(
                $"O campo '{valueLabel}' do servidor MCP remoto deve ser uma URL absoluta.");
        }

        return parsedUri;
    }

    private static Uri? ParseOptionalAbsoluteUri(string? rawValue, string valueLabel)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return ParseAbsoluteUri(rawValue, valueLabel);
    }

    private static IReadOnlyList<string> NormalizeStringList(
        IReadOnlyList<string>? values,
        string fieldLabel)
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        var normalized = new List<string>(values.Count);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"O campo '{fieldLabel}' do servidor MCP nao pode conter valores vazios.");
            }

            normalized.Add(value.Trim());
        }

        return normalized;
    }

    private static IReadOnlyDictionary<string, string> NormalizeDictionary(
        IReadOnlyDictionary<string, string>? values,
        string fieldLabel,
        StringComparer comparer)
    {
        if (values is null || values.Count == 0)
        {
            return new Dictionary<string, string>(comparer);
        }

        var normalized = new Dictionary<string, string>(comparer);
        foreach (var pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new InvalidOperationException(
                    $"O campo '{fieldLabel}' do servidor MCP contem uma chave vazia.");
            }

            if (pair.Value is null)
            {
                throw new InvalidOperationException(
                    $"O campo '{fieldLabel}' do servidor MCP contem valor nulo para a chave '{pair.Key}'.");
            }

            normalized[pair.Key.Trim()] = pair.Value.Trim();
        }

        return normalized;
    }

    private static PersistedTransport ParseTransport(string? transport)
    {
        var normalizedTransport = ValidateNonEmptyValue(
            transport,
            "O campo 'transport' do servidor MCP nao pode ser vazio.");

        return normalizedTransport.ToLowerInvariant() switch
        {
            "stdio" => PersistedTransport.Stdio,
            "http" => PersistedTransport.Http,
            "sse" => PersistedTransport.Sse,
            _ => throw new InvalidOperationException(
                "O campo 'transport' do servidor MCP deve ser um entre: stdio, http, sse.")
        };
    }

    private static McpAuthenticationOptions ResolveAuthentication(PersistedServer server)
    {
        var hasAuthorizationScheme = !string.IsNullOrWhiteSpace(server.AuthorizationScheme);
        var hasCustomHeaderName = !string.IsNullOrWhiteSpace(server.CustomHeaderName);
        var hasCustomHeaderValue = !string.IsNullOrWhiteSpace(server.CustomHeaderValue);

        if (!hasAuthorizationScheme && !hasCustomHeaderName && !hasCustomHeaderValue)
        {
            return McpAuthenticationOptions.None;
        }

        if (hasAuthorizationScheme && (hasCustomHeaderName || hasCustomHeaderValue))
        {
            throw new InvalidOperationException(
                "A autenticacao MCP remota nao pode combinar cabecalho customizado com Authorization.");
        }

        if (hasCustomHeaderName || hasCustomHeaderValue)
        {
            var headerName = ValidateNonEmptyValue(
                server.CustomHeaderName,
                "O nome do cabecalho de autenticacao MCP nao pode ser vazio.");
            var headerValue = ValidateNonEmptyValue(
                server.CustomHeaderValue,
                "O valor do cabecalho de autenticacao MCP nao pode ser vazio.");
            return McpAuthenticationOptions.Header(headerName, headerValue);
        }

        var scheme = ValidateNonEmptyValue(
            server.AuthorizationScheme,
            "O esquema de autenticacao MCP remoto nao pode ser vazio.");
        var parameter = ValidateNonEmptyValue(
            server.AuthorizationParameter,
            "O parametro de autenticacao MCP remoto nao pode ser vazio.");

        if (string.Equals(scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return McpAuthenticationOptions.Bearer(parameter);
        }

        if (string.Equals(scheme, "Basic", StringComparison.OrdinalIgnoreCase))
        {
            string decodedCredentials;
            try
            {
                decodedCredentials = Encoding.UTF8.GetString(
                    Convert.FromBase64String(parameter));
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException(
                    "O parametro de autenticacao Basic MCP deve ser um Base64 valido.",
                    ex);
            }

            var separatorIndex = decodedCredentials.IndexOf(':');
            if (separatorIndex <= 0 || separatorIndex == decodedCredentials.Length - 1)
            {
                throw new InvalidOperationException(
                    "As credenciais Basic MCP devem seguir o formato usuario:senha.");
            }

            var username = decodedCredentials[..separatorIndex];
            var password = decodedCredentials[(separatorIndex + 1)..];
            return McpAuthenticationOptions.Basic(username, password);
        }

        throw new InvalidOperationException(
            $"Esquema de autenticacao MCP remoto nao suportado: '{scheme}'.");
    }

    private sealed record PersistedCatalog(IReadOnlyList<PersistedServer>? Servers);

    private sealed record PersistedServer(
        string? Name,
        string? Transport,
        string? Command,
        IReadOnlyList<string>? Arguments,
        string? WorkingDirectory,
        IReadOnlyDictionary<string, string>? EnvironmentVariables,
        string? Endpoint,
        string? MessageEndpoint,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string? CustomHeaderName,
        string? CustomHeaderValue,
        IReadOnlyDictionary<string, string>? Headers)
    {
        public static implicit operator PersistedServer(McpServerDefinition server)
        {
            if (server.ProcessOptions is McpServerProcessOptions processOptions)
            {
                return new PersistedServer(
                    Name: server.Name,
                    Transport: "stdio",
                    Command: processOptions.Command,
                    Arguments: processOptions.Arguments.ToArray(),
                    WorkingDirectory: processOptions.WorkingDirectory,
                    EnvironmentVariables: processOptions.EnvironmentVariables.Count == 0
                        ? null
                        : new Dictionary<string, string>(
                            processOptions.EnvironmentVariables,
                            StringComparer.Ordinal),
                    Endpoint: null,
                    MessageEndpoint: null,
                    AuthorizationScheme: null,
                    AuthorizationParameter: null,
                    CustomHeaderName: null,
                    CustomHeaderValue: null,
                    Headers: null);
            }

            var remoteOptions = server.RemoteOptions
                ?? throw new InvalidOperationException(
                    $"Servidor MCP '{server.Name}' nao possui opcoes remotas.");

            var transport = remoteOptions.TransportKind == McpRemoteTransportKind.Sse
                ? "sse"
                : "http";

            return new PersistedServer(
                Name: server.Name,
                Transport: transport,
                Command: null,
                Arguments: null,
                WorkingDirectory: null,
                EnvironmentVariables: null,
                Endpoint: remoteOptions.Endpoint.AbsoluteUri,
                MessageEndpoint: remoteOptions.MessageEndpoint?.AbsoluteUri,
                AuthorizationScheme: remoteOptions.Authentication.AuthorizationScheme,
                AuthorizationParameter: remoteOptions.Authentication.AuthorizationParameter,
                CustomHeaderName: remoteOptions.Authentication.CustomHeaderName,
                CustomHeaderValue: remoteOptions.Authentication.CustomHeaderValue,
                Headers: remoteOptions.Headers.Count == 0
                    ? null
                    : new Dictionary<string, string>(
                        remoteOptions.Headers,
                        StringComparer.OrdinalIgnoreCase));
        }

        public static implicit operator McpServerDefinition(PersistedServer server)
        {
            var name = ValidateNonEmptyValue(
                server.Name,
                "O nome do servidor MCP nao pode ser vazio.");
            var transport = ParseTransport(server.Transport);

            if (transport == PersistedTransport.Stdio)
            {
                var processOptions = new McpServerProcessOptions(
                    command: ValidateNonEmptyValue(
                        server.Command,
                        "O comando do servidor MCP stdio nao pode ser vazio."),
                    arguments: NormalizeStringList(
                        server.Arguments,
                        "arguments"),
                    workingDirectory: NormalizeOptionalValue(server.WorkingDirectory),
                    environmentVariables: NormalizeDictionary(
                        server.EnvironmentVariables,
                        "environmentVariables",
                        StringComparer.Ordinal));

                return McpServerDefinition.Stdio(name, processOptions);
            }

            var remoteOptions = new McpServerRemoteOptions(
                endpoint: ParseAbsoluteUri(server.Endpoint, "endpoint"),
                transportKind: transport == PersistedTransport.Sse
                    ? McpRemoteTransportKind.Sse
                    : McpRemoteTransportKind.Http,
                messageEndpoint: ParseOptionalAbsoluteUri(server.MessageEndpoint, "messageEndpoint"),
                authentication: ResolveAuthentication(server),
                headers: NormalizeDictionary(
                    server.Headers,
                    "headers",
                    StringComparer.OrdinalIgnoreCase));

            return McpServerDefinition.Remote(name, remoteOptions);
        }
    }

    private enum PersistedTransport
    {
        Stdio,
        Http,
        Sse
    }
}
