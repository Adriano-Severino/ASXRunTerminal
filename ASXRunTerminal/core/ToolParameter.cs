namespace ASXRunTerminal.Core;

internal readonly record struct ToolParameter(
    string Name,
    string Description,
    bool IsRequired);
