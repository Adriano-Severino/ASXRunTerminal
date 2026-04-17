using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;

namespace ASXRunTerminal.Tests;

public sealed class McpToolSchemaValidatorTests
{
    [Fact]
    public void ValidateArguments_ReturnsNull_WhenArgumentsMatchStrictSchema()
    {
        var discoveredTool = BuildDiscoveredTool(
            toolName: "search",
            hasInputSchema: true,
            allowsAdditionalProperties: false,
            parameters:
            [
                new ToolParameter("query", "Termo", true),
                new ToolParameter("limit", "Limite", false)
            ]);

        var error = McpToolSchemaValidator.ValidateArguments(
            discoveredTool,
            new Dictionary<string, string>
            {
                ["query"] = "terminal",
                ["limit"] = "10"
            });

        Assert.Null(error);
    }

    [Fact]
    public void ValidateArguments_ReturnsError_WhenRequiredParameterIsMissing()
    {
        var discoveredTool = BuildDiscoveredTool(
            toolName: "search",
            hasInputSchema: true,
            allowsAdditionalProperties: false,
            parameters:
            [
                new ToolParameter("query", "Termo", true)
            ]);

        var error = McpToolSchemaValidator.ValidateArguments(
            discoveredTool,
            new Dictionary<string, string>());

        Assert.NotNull(error);
        Assert.Contains("obrigatorio", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("query", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateArguments_ReturnsError_WhenRequiredParameterIsBlank()
    {
        var discoveredTool = BuildDiscoveredTool(
            toolName: "search",
            hasInputSchema: true,
            allowsAdditionalProperties: false,
            parameters:
            [
                new ToolParameter("query", "Termo", true)
            ]);

        var error = McpToolSchemaValidator.ValidateArguments(
            discoveredTool,
            new Dictionary<string, string>
            {
                ["query"] = "   "
            });

        Assert.NotNull(error);
        Assert.Contains("query", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateArguments_ReturnsError_WhenSchemaIsStrictAndContainsUnexpectedParameter()
    {
        var discoveredTool = BuildDiscoveredTool(
            toolName: "search",
            hasInputSchema: true,
            allowsAdditionalProperties: false,
            parameters:
            [
                new ToolParameter("query", "Termo", true)
            ]);

        var error = McpToolSchemaValidator.ValidateArguments(
            discoveredTool,
            new Dictionary<string, string>
            {
                ["query"] = "terminal",
                ["page"] = "2"
            });

        Assert.NotNull(error);
        Assert.Contains("nao definidos", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("page", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateArguments_AllowsUnexpectedParameters_WhenSchemaAllowsAdditionalProperties()
    {
        var discoveredTool = BuildDiscoveredTool(
            toolName: "search",
            hasInputSchema: true,
            allowsAdditionalProperties: true,
            parameters:
            [
                new ToolParameter("query", "Termo", true)
            ]);

        var error = McpToolSchemaValidator.ValidateArguments(
            discoveredTool,
            new Dictionary<string, string>
            {
                ["query"] = "terminal",
                ["page"] = "2"
            });

        Assert.Null(error);
    }

    [Fact]
    public void ValidateArguments_AllowsUnexpectedParameters_WhenToolDoesNotDeclareInputSchema()
    {
        var discoveredTool = BuildDiscoveredTool(
            toolName: "legacy-tool",
            hasInputSchema: false,
            allowsAdditionalProperties: true,
            parameters: []);

        var error = McpToolSchemaValidator.ValidateArguments(
            discoveredTool,
            new Dictionary<string, string>
            {
                ["any"] = "value"
            });

        Assert.Null(error);
    }

    private static McpDiscoveredTool BuildDiscoveredTool(
        string toolName,
        bool hasInputSchema,
        bool allowsAdditionalProperties,
        IReadOnlyList<ToolParameter> parameters)
    {
        var descriptor = new ToolDescriptor(
            Name: toolName,
            Description: $"Tool '{toolName}'.",
            Parameters: parameters);

        return new McpDiscoveredTool(
            Descriptor: descriptor,
            HasInputSchema: hasInputSchema,
            AllowsAdditionalProperties: allowsAdditionalProperties);
    }
}
