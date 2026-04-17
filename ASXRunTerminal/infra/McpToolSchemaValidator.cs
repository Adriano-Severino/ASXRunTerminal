using ASXRunTerminal.Core;

namespace ASXRunTerminal.Infra;

internal static class McpToolSchemaValidator
{
    public static string? ValidateArguments(
        McpDiscoveredTool discoveredTool,
        IReadOnlyDictionary<string, string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var descriptor = discoveredTool.Descriptor;

        var missingRequiredParameters = descriptor.Parameters
            .Where(static parameter => parameter.IsRequired)
            .Where(parameter =>
                !arguments.TryGetValue(parameter.Name, out var value)
                || string.IsNullOrWhiteSpace(value))
            .Select(static parameter => parameter.Name)
            .ToArray();

        if (missingRequiredParameters.Length > 0)
        {
            var missingList = string.Join(", ", missingRequiredParameters);
            return
                $"Schema de parametros invalido para a tool '{descriptor.Name}': parametro(s) obrigatorio(s) ausente(s) ou vazio(s): {missingList}.";
        }

        if (!discoveredTool.HasInputSchema || discoveredTool.AllowsAdditionalProperties)
        {
            return null;
        }

        if (descriptor.Parameters.Count == 0)
        {
            if (arguments.Count == 0)
            {
                return null;
            }

            var unexpectedWithoutProperties = string.Join(", ", arguments.Keys.OrderBy(static key => key, StringComparer.Ordinal));
            return
                $"Schema de parametros invalido para a tool '{descriptor.Name}': parametros nao definidos: {unexpectedWithoutProperties}.";
        }

        var definedParameterNames = new HashSet<string>(
            descriptor.Parameters.Select(static parameter => parameter.Name),
            StringComparer.Ordinal);

        var unexpectedParameters = arguments.Keys
            .Where(argumentName => !definedParameterNames.Contains(argumentName))
            .OrderBy(static argumentName => argumentName, StringComparer.Ordinal)
            .ToArray();

        if (unexpectedParameters.Length == 0)
        {
            return null;
        }

        var unexpectedList = string.Join(", ", unexpectedParameters);
        return
            $"Schema de parametros invalido para a tool '{descriptor.Name}': parametros nao definidos: {unexpectedList}.";
    }
}
