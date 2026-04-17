namespace ASXRunTerminal.Core;

internal readonly record struct McpDiscoveredTool(
    ToolDescriptor Descriptor,
    bool HasInputSchema,
    bool AllowsAdditionalProperties);
