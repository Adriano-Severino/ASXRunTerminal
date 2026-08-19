using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;
using Microsoft.Extensions.Logging;

namespace ASXRunTerminal.Commands;

/// <summary>
/// Command for code review with RAG (Retrieval-Augmented Generation).
/// </summary>
internal sealed class CodeReviewCommand : CommandBase
{
    private const string CliName = "asxrun";
    private readonly ILogger<CodeReviewCommand> _logger;

    public CodeReviewCommand(ILogger<CodeReviewCommand>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CodeReviewCommand>.Instance;
    }

    public override string Name => "code-review";
    public override string Description => "Revisao de codigo com RAG para analise contextual.";

    public override CommandParseResult ParseArguments(string[] args)
    {
        var files = new List<string>();
        string? severity = null;
        string? focus = null;
        var useRag = true;

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];

            if (string.Equals(argument, "--no-rag", StringComparison.OrdinalIgnoreCase))
            {
                useRag = false;
            }
            else if (argument.StartsWith("--severity=", StringComparison.OrdinalIgnoreCase))
            {
                severity = argument.Substring("--severity=".Length);
            }
            else if (argument.StartsWith("--focus=", StringComparison.OrdinalIgnoreCase))
            {
                focus = argument.Substring("--focus=".Length);
            }
            else if (argument.StartsWith("--", StringComparison.OrdinalIgnoreCase))
            {
                return Failure(CliFriendlyError.InvalidArguments(
                    detail: $"Opcao desconhecida: {argument}",
                    suggestion: $"Opcoes validas: --no-rag, --severity=<level>, --focus=<area>"));
            }
            else
            {
                files.Add(argument);
            }
        }

        if (files.Count == 0)
        {
            return Failure(CliFriendlyError.InvalidArguments(
                detail: "O comando 'code-review' exige pelo menos um arquivo.",
                suggestion: $"Exemplo: {CliName} code-review src/Program.cs"));
        }

        var parameters = new Dictionary<string, object>
        {
            { "files", files.ToArray() },
            { "severity", severity },
            { "focus", focus },
            { "useRag", useRag }
        };

        return Success(parameters);
    }

    public override Task<int> ExecuteAsync(CommandParseResult parseResult, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        if (parseResult.HasError)
        {
            WriteFriendlyError(parseResult.Error ?? CliFriendlyError.Runtime("Unknown error"));
            return Task.FromResult((int)(parseResult.Error?.ExitCode ?? CliExitCode.RuntimeError));
        }

        var files = parseResult.Parameters.TryGetValue("files", out var filesValue) && filesValue is string[] fileArray ? fileArray : Array.Empty<string>();
        var severity = GetStringParameter(parseResult.Parameters, "severity");
        var focus = GetStringParameter(parseResult.Parameters, "focus");
        var useRag = GetBoolParameter(parseResult.Parameters, "useRag", true);

        ConsoleLogger.Info($"Code review com RAG: {useRag}, Arquivos: {files.Length}, Severity: {severity ?? "default"}, Focus: {focus ?? "default"}");
        _logger.LogInformation("Code review com RAG: {UseRag}, Arquivos: {FileCount}, Severity: {Severity}, Focus: {Focus}",
            useRag, files.Length, severity ?? "default", focus ?? "default");
        ConsoleLogger.Info("Code review (implementacao parcial - use Program.cs por enquanto)");
        _logger.LogInformation("Code review (implementacao parcial - use Program.cs por enquanto)");

        // For now, delegate to the existing Program.cs ExecuteCodeReview method
        // This will be refactored further in subsequent steps
        return Task.FromResult((int)CliExitCode.Success);
    }

    private static void WriteFriendlyError(CliFriendlyError error)
    {
        Program.WriteFriendlyError(error);
    }
}
