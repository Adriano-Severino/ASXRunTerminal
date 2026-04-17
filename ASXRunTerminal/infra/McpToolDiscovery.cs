using System.Text.Json;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Infra;

internal static class McpToolDiscovery
{
    private static readonly TimeSpan DefaultToolsListTimeout = TimeSpan.FromSeconds(15);

    public static async Task<IReadOnlyList<ToolDescriptor>> DiscoverAsync(
        IMcpClient client,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var discoveredTools = await DiscoverWithSchemaAsync(
            client,
            timeout,
            cancellationToken);

        return discoveredTools
            .Select(static tool => tool.Descriptor)
            .ToArray();
    }

    internal static async Task<IReadOnlyList<McpDiscoveredTool>> DiscoverWithSchemaAsync(
        IMcpClient client,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        var toolsListResult = await client.SendRequestAsync(
            method: "tools/list",
            parameters: null,
            timeout: timeout ?? DefaultToolsListTimeout,
            cancellationToken: cancellationToken);

        return ParseToolsWithSchema(toolsListResult);
    }

    internal static IReadOnlyList<ToolDescriptor> ParseTools(JsonElement toolsListResult)
    {
        return ParseToolsWithSchema(toolsListResult)
            .Select(static tool => tool.Descriptor)
            .ToArray();
    }

    internal static IReadOnlyList<McpDiscoveredTool> ParseToolsWithSchema(JsonElement toolsListResult)
    {
        if (toolsListResult.ValueKind != JsonValueKind.Object
            || !toolsListResult.TryGetProperty("tools", out var toolsElement)
            || toolsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "Resposta 'tools/list' invalida: o campo 'tools' deve ser um array.");
        }

        var discoveredTools = new List<McpDiscoveredTool>();
        var discoveredToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var toolElement in toolsElement.EnumerateArray())
        {
            var descriptor = ParseToolDescriptor(toolElement);
            if (!discoveredToolNames.Add(descriptor.Descriptor.Name))
            {
                throw new InvalidOperationException(
                    $"Resposta 'tools/list' invalida: a tool '{descriptor.Descriptor.Name}' foi informada mais de uma vez.");
            }

            discoveredTools.Add(descriptor);
        }

        return discoveredTools;
    }

    private static McpDiscoveredTool ParseToolDescriptor(JsonElement toolElement)
    {
        if (toolElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                "Resposta 'tools/list' invalida: cada tool deve ser um objeto JSON.");
        }

        var toolName = ReadRequiredStringProperty(
            toolElement,
            propertyName: "name",
            errorContext: "Resposta 'tools/list' invalida");
        var toolDescription = ReadOptionalStringProperty(
                toolElement,
                propertyName: "description",
                errorContext: $"Schema de tool invalido para '{toolName}'")
            ?? $"Tool MCP '{toolName}'.";

        var hasInputSchema = false;
        var inputSchema = GetInputSchemaElement(toolElement);
        var parsedParameters = inputSchema is JsonElement schemaElement
            ? ParseToolParameters(toolName, schemaElement)
            : new ParsedToolParameters(
                Parameters: [],
                AllowsAdditionalProperties: true);
        if (inputSchema is JsonElement)
        {
            hasInputSchema = true;
        }

        return new McpDiscoveredTool(
            Descriptor: new ToolDescriptor(
                Name: toolName,
                Description: toolDescription,
                Parameters: parsedParameters.Parameters),
            HasInputSchema: hasInputSchema,
            AllowsAdditionalProperties: parsedParameters.AllowsAdditionalProperties);
    }

    private static JsonElement? GetInputSchemaElement(JsonElement toolElement)
    {
        if (toolElement.TryGetProperty("inputSchema", out var inputSchemaElement))
        {
            return inputSchemaElement;
        }

        if (toolElement.TryGetProperty("input_schema", out var snakeCaseSchemaElement))
        {
            return snakeCaseSchemaElement;
        }

        if (toolElement.TryGetProperty("parameters", out var legacyParametersElement))
        {
            return legacyParametersElement;
        }

        return null;
    }

    private static ParsedToolParameters ParseToolParameters(
        string toolName,
        JsonElement inputSchema)
    {
        if (inputSchema.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"Schema de parametros invalido para a tool '{toolName}': o campo 'inputSchema' deve ser um objeto JSON.");
        }

        if (inputSchema.TryGetProperty("type", out var schemaTypeElement))
        {
            if (schemaTypeElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    $"Schema de parametros invalido para a tool '{toolName}': o campo 'type' deve ser uma string.");
            }

            var schemaType = schemaTypeElement.GetString();
            if (!string.Equals(schemaType, "object", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Schema de parametros invalido para a tool '{toolName}': apenas schemas com type='object' sao suportados.");
            }
        }

        var allowsAdditionalProperties = true;
        if (inputSchema.TryGetProperty("additionalProperties", out var additionalPropertiesElement)
            && additionalPropertiesElement.ValueKind is not JsonValueKind.Object
            and not JsonValueKind.True
            and not JsonValueKind.False)
        {
            throw new InvalidOperationException(
                $"Schema de parametros invalido para a tool '{toolName}': o campo 'additionalProperties' deve ser booleano ou objeto.");
        }
        else if (inputSchema.TryGetProperty("additionalProperties", out additionalPropertiesElement)
                 && additionalPropertiesElement.ValueKind == JsonValueKind.False)
        {
            allowsAdditionalProperties = false;
        }

        var parameterDefinitions = ParseParameterDefinitions(toolName, inputSchema);
        MarkRequiredParameters(toolName, inputSchema, parameterDefinitions);
        return new ParsedToolParameters(
            Parameters: parameterDefinitions.Values.ToArray(),
            AllowsAdditionalProperties: allowsAdditionalProperties);
    }

    private static Dictionary<string, ToolParameter> ParseParameterDefinitions(
        string toolName,
        JsonElement inputSchema)
    {
        if (!inputSchema.TryGetProperty("properties", out var propertiesElement))
        {
            return new Dictionary<string, ToolParameter>(StringComparer.Ordinal);
        }

        if (propertiesElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"Schema de parametros invalido para a tool '{toolName}': o campo 'properties' deve ser um objeto.");
        }

        var parameterDefinitions = new Dictionary<string, ToolParameter>(StringComparer.Ordinal);
        foreach (var property in propertiesElement.EnumerateObject())
        {
            var parameterName = property.Name;
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                throw new InvalidOperationException(
                    $"Schema de parametros invalido para a tool '{toolName}': foi encontrado um parametro com nome vazio.");
            }

            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    $"Schema de parametros invalido para a tool '{toolName}': o parametro '{parameterName}' deve ser um objeto JSON.");
            }

            var parameterDescription = ReadOptionalStringProperty(
                    property.Value,
                    propertyName: "description",
                    errorContext: $"Schema de parametros invalido para a tool '{toolName}'")
                ?? $"Parametro '{parameterName}' da tool '{toolName}'.";

            parameterDefinitions[parameterName] = new ToolParameter(
                Name: parameterName,
                Description: parameterDescription,
                IsRequired: false);
        }

        return parameterDefinitions;
    }

    private static void MarkRequiredParameters(
        string toolName,
        JsonElement inputSchema,
        Dictionary<string, ToolParameter> parameterDefinitions)
    {
        if (!inputSchema.TryGetProperty("required", out var requiredElement))
        {
            return;
        }

        if (requiredElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                $"Schema de parametros invalido para a tool '{toolName}': o campo 'required' deve ser um array.");
        }

        var requiredParameterNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var requiredParameterElement in requiredElement.EnumerateArray())
        {
            if (requiredParameterElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    $"Schema de parametros invalido para a tool '{toolName}': o array 'required' deve conter apenas strings.");
            }

            var requiredParameterName = requiredParameterElement.GetString();
            if (string.IsNullOrWhiteSpace(requiredParameterName))
            {
                throw new InvalidOperationException(
                    $"Schema de parametros invalido para a tool '{toolName}': o array 'required' nao pode conter nomes vazios.");
            }

            if (!requiredParameterNames.Add(requiredParameterName))
            {
                throw new InvalidOperationException(
                    $"Schema de parametros invalido para a tool '{toolName}': o parametro '{requiredParameterName}' foi repetido no campo 'required'.");
            }

            if (!parameterDefinitions.TryGetValue(requiredParameterName, out var existingParameter))
            {
                throw new InvalidOperationException(
                    $"Schema de parametros invalido para a tool '{toolName}': o parametro obrigatorio '{requiredParameterName}' nao foi definido em 'properties'.");
            }

            parameterDefinitions[requiredParameterName] = existingParameter with
            {
                IsRequired = true
            };
        }
    }

    private static string ReadRequiredStringProperty(
        JsonElement sourceElement,
        string propertyName,
        string errorContext)
    {
        if (!sourceElement.TryGetProperty(propertyName, out var propertyElement)
            || propertyElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(
                $"{errorContext}: o campo '{propertyName}' deve ser uma string.");
        }

        var value = propertyElement.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{errorContext}: o campo '{propertyName}' nao pode ser vazio.");
        }

        return value.Trim();
    }

    private static string? ReadOptionalStringProperty(
        JsonElement sourceElement,
        string propertyName,
        string errorContext)
    {
        if (!sourceElement.TryGetProperty(propertyName, out var propertyElement))
        {
            return null;
        }

        if (propertyElement.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (propertyElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(
                $"{errorContext}: o campo '{propertyName}' deve ser uma string.");
        }

        var value = propertyElement.GetString();
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private readonly record struct ParsedToolParameters(
        IReadOnlyList<ToolParameter> Parameters,
        bool AllowsAdditionalProperties);
}
