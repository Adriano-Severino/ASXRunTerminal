using System.Net.Http.Headers;
using System.Text;

namespace ASXRunTerminal.Core;

internal sealed record McpAuthenticationOptions
{
    private McpAuthenticationOptions(
        string? authorizationScheme,
        string? authorizationParameter,
        string? customHeaderName,
        string? customHeaderValue)
    {
        AuthorizationScheme = authorizationScheme;
        AuthorizationParameter = authorizationParameter;
        CustomHeaderName = customHeaderName;
        CustomHeaderValue = customHeaderValue;
    }

    public static McpAuthenticationOptions None { get; } =
        new(
            authorizationScheme: null,
            authorizationParameter: null,
            customHeaderName: null,
            customHeaderValue: null);

    public string? AuthorizationScheme { get; }

    public string? AuthorizationParameter { get; }

    public string? CustomHeaderName { get; }

    public string? CustomHeaderValue { get; }

    public static McpAuthenticationOptions Bearer(string token)
    {
        var sanitizedToken = ValidateNonEmpty(
            token,
            nameof(token),
            "O token Bearer de autenticacao MCP nao pode ser vazio.");

        return new McpAuthenticationOptions(
            authorizationScheme: "Bearer",
            authorizationParameter: sanitizedToken,
            customHeaderName: null,
            customHeaderValue: null);
    }

    public static McpAuthenticationOptions Basic(string username, string password)
    {
        var sanitizedUsername = ValidateNonEmpty(
            username,
            nameof(username),
            "O usuario da autenticacao Basic MCP nao pode ser vazio.");
        var sanitizedPassword = ValidateNonEmpty(
            password,
            nameof(password),
            "A senha da autenticacao Basic MCP nao pode ser vazia.");
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{sanitizedUsername}:{sanitizedPassword}"));

        return new McpAuthenticationOptions(
            authorizationScheme: "Basic",
            authorizationParameter: credentials,
            customHeaderName: null,
            customHeaderValue: null);
    }

    public static McpAuthenticationOptions Header(string headerName, string headerValue)
    {
        var sanitizedHeaderName = ValidateNonEmpty(
            headerName,
            nameof(headerName),
            "O nome do cabecalho de autenticacao MCP nao pode ser vazio.");
        var sanitizedHeaderValue = ValidateNonEmpty(
            headerValue,
            nameof(headerValue),
            "O valor do cabecalho de autenticacao MCP nao pode ser vazio.");

        if (string.Equals(
                sanitizedHeaderName,
                "Authorization",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Use os metodos Bearer ou Basic para configurar o cabecalho Authorization.",
                nameof(headerName));
        }

        return new McpAuthenticationOptions(
            authorizationScheme: null,
            authorizationParameter: null,
            customHeaderName: sanitizedHeaderName,
            customHeaderValue: sanitizedHeaderValue);
    }

    public void ApplyTo(HttpRequestHeaders headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        if (!string.IsNullOrWhiteSpace(AuthorizationScheme))
        {
            headers.Authorization = new AuthenticationHeaderValue(
                AuthorizationScheme,
                AuthorizationParameter);
        }

        if (!string.IsNullOrWhiteSpace(CustomHeaderName))
        {
            headers.Remove(CustomHeaderName);
            headers.TryAddWithoutValidation(CustomHeaderName, CustomHeaderValue);
        }
    }

    public static implicit operator McpAuthenticationOptions(string bearerToken)
    {
        return Bearer(bearerToken);
    }

    private static string ValidateNonEmpty(
        string value,
        string parameterName,
        string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, parameterName);
        }

        return value.Trim();
    }
}
