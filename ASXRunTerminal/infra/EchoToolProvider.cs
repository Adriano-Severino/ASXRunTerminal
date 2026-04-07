using System.Diagnostics;

using ASXRunTerminal.Core;

namespace ASXRunTerminal.Infra;

internal sealed class EchoToolProvider : IToolProvider
{
    private const string ToolName = "echo";

    private static readonly ToolDescriptor Descriptor = new(
        Name: ToolName,
        Description: "Retorna o texto de entrada como saida. Util para testes de pipeline.",
        Parameters:
        [
            new ToolParameter(
                Name: "text",
                Description: "Texto a ser ecoado.",
                IsRequired: true)
        ]);

    public string ProviderName => "built-in";

    public IReadOnlyList<ToolDescriptor> ListTools()
    {
        return [Descriptor];
    }

    public bool CanHandle(string toolName)
    {
        return string.Equals(toolName, ToolName, StringComparison.OrdinalIgnoreCase);
    }

    public Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();

        if (!request.Arguments.TryGetValue("text", out var text)
            || string.IsNullOrWhiteSpace(text))
        {
            stopwatch.Stop();
            return Task.FromResult(
                ToolExecutionResult.Failure(
                    error: "Parametro obrigatorio 'text' nao foi informado.",
                    exitCode: 1,
                    duration: stopwatch.Elapsed));
        }

        stopwatch.Stop();
        return Task.FromResult(
            ToolExecutionResult.Success(
                output: text.Trim(),
                duration: stopwatch.Elapsed));
    }
}
