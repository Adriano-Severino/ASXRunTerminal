namespace ASXRunTerminal.Core;

internal readonly record struct ToolDescriptor(
    string Name,
    string Description,
    IReadOnlyList<ToolParameter> Parameters);
