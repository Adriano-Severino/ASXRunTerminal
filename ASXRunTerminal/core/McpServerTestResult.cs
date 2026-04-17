namespace ASXRunTerminal.Core;

internal readonly record struct McpServerTestResult(
    bool IsSuccess,
    string Detail)
{
    public static McpServerTestResult Success(string detail)
    {
        return new McpServerTestResult(
            IsSuccess: true,
            Detail: string.IsNullOrWhiteSpace(detail)
                ? "Servidor MCP respondeu ao teste."
                : detail.Trim());
    }

    public static McpServerTestResult Failure(string detail)
    {
        return new McpServerTestResult(
            IsSuccess: false,
            Detail: string.IsNullOrWhiteSpace(detail)
                ? "Falha ao validar servidor MCP."
                : detail.Trim());
    }
}
