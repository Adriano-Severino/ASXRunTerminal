using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ASXRunTerminal.Subagents;

/// <summary>
/// Implementacao do subagente de revisao de codigo usando RAG.
/// Combina embeddings, armazenamento de vetores e analise de IA para revisoes contextuais.
/// </summary>
internal sealed class CodeReviewerSubagent : ICodeReviewerSubagent
{
    private readonly OllamaEmbeddingGenerator _embeddingGenerator;
    private readonly SqliteVectorStore _vectorStore;
    private readonly IChatClient _chatClient;
    private readonly VectorStoreConfiguration _configuration;
    private readonly IOllamaHttpClient _ollamaHttpClient;
    private readonly ILogger<CodeReviewerSubagent> _logger;

    private static readonly string[] ProjectSpecificRules = new[]
    {
        "Usar implicit operator em vez de AutoMapper para mapeamento de objetos",
        "Criar testes para novos comportamentos e funcionalidades",
        "Seguir melhores praticas de C#/.NET",
        "Evitar exposicao de segredos e credenciais",
        "Validar entrada de dados adequadamente",
        "Tratar excecoes de forma apropriada",
        "Manter legibilidade e manutenibilidade do codigo"
    };

    public CodeReviewerSubagent(
        OllamaEmbeddingGenerator embeddingGenerator,
        SqliteVectorStore vectorStore,
        IChatClient chatClient,
        VectorStoreConfiguration configuration,
        IOllamaHttpClient ollamaHttpClient,
        ILogger<CodeReviewerSubagent>? logger = null)
    {
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _ollamaHttpClient = ollamaHttpClient ?? throw new ArgumentNullException(nameof(ollamaHttpClient));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CodeReviewerSubagent>.Instance;
    }

    public async Task<CodeReviewResult> ReviewCodeAsync(
        CodeReviewContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogInformation("Starting code review for {FileCount} files with RAG: {UseRag}", 
            context.FilePaths.Count, context.UseRag);

        var stopwatch = Stopwatch.StartNew();
        var result = new CodeReviewResult();

        try
        {
            // Step 1: Index files if using RAG
            RagContextInfo? ragInfo = null;
            if (context.UseRag && context.FilePaths.Count > 0)
            {
                _logger.LogDebug("Starting RAG indexing for {FileCount} files", context.FilePaths.Count);
                var ragStopwatch = Stopwatch.StartNew();
                await IndexCodeFilesAsync(context.FilePaths, context.FileContents, forceReindex: false, cancellationToken)
                    .ConfigureAwait(false);
                ragStopwatch.Stop();

                ragInfo = new RagContextInfo
                {
                    RagDurationMs = ragStopwatch.ElapsedMilliseconds
                };

                _logger.LogDebug("RAG indexing completed in {DurationMs}ms", ragInfo.RagDurationMs);
            }

            // Step 2: Read file contents
            _logger.LogDebug("Reading file contents for review");
            var fileContents = await ReadFileContentsAsync(context, cancellationToken)
                .ConfigureAwait(false);

            // Step 3: Build review prompt with RAG context if enabled
            _logger.LogDebug("Building review prompt with context");
            var reviewPrompt = await BuildReviewPromptAsync(context, fileContents, ragInfo, cancellationToken)
                .ConfigureAwait(false);

            // Step 4: Execute review
            var model = context.Model ?? OllamaModelDefaults.DefaultModel;
            _logger.LogDebug("Executing code review with model: {Model}", model);
            var reviewResponse = await _ollamaHttpClient
                .GenerateAsync(reviewPrompt, model, cancellationToken)
                .ConfigureAwait(false);

            // Step 5: Parse response into structured result
            result = ParseReviewResponse(reviewResponse, context, fileContents);

            // Step 6: Add RAG context info if applicable
            if (ragInfo != null)
            {
                result.RagContext = ragInfo;
            }

            stopwatch.Stop();
            _logger.LogInformation("Code review completed in {DurationMs}ms with {IssueCount} issues found", 
                stopwatch.ElapsedMilliseconds, result.Issues.Count);

            result.Status = CodeReviewStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            result.Status = CodeReviewStatus.Cancelled;
        }
        catch (Exception ex)
        {
            result.Status = CodeReviewStatus.Failed;
            result.Issues = new List<CodeReviewIssue>
            {
                new()
                {
                    Id = "ERROR-001",
                    Severity = CodeReviewSeverity.Critical,
                    Category = "System",
                    Title = "Review Failed",
                    Description = $"A revisao falhou com erro: {ex.Message}"
                }
            };
        }
        finally
        {
            stopwatch.Stop();
            result.DurationMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    public async Task<int> IndexCodeFilesAsync(
        IReadOnlyList<string> filePaths,
        bool forceReindex = false,
        CancellationToken cancellationToken = default)
    {
        return await IndexCodeFilesAsync(filePaths, null, forceReindex, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<int> IndexCodeFilesAsync(
        IReadOnlyList<string> filePaths,
        IReadOnlyDictionary<string, string>? providedContents,
        bool forceReindex = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        var indexedCount = 0;

        foreach (var filePath in filePaths)
        {
            if (!File.Exists(filePath))
            {
                continue;
            }

            var documentId = GenerateDocumentId(filePath);

            // Check if already indexed (unless force reindex)
            if (!forceReindex && await _vectorStore.IsDocumentIndexedAsync(documentId, cancellationToken)
                .ConfigureAwait(false))
            {
                continue;
            }

            // Read content
            var content = providedContents != null && providedContents.TryGetValue(filePath, out var provided)
                ? provided
                : await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);

            // Generate embedding
            var embeddings = await _embeddingGenerator
                .GenerateAsync(new[] { content }, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (embeddings.Count > 0)
            {
                await _vectorStore.AddDocumentAsync(
                    documentId,
                    content,
                    filePath,
                    embeddings[0].Vector.ToArray(),
                    cancellationToken)
                    .ConfigureAwait(false);

                indexedCount++;
            }
        }

        return indexedCount;
    }

    public async Task<IReadOnlyList<CodeSearchResult>> SearchRelevantCodeAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Generate embedding for query
        var embeddings = await _embeddingGenerator
            .GenerateAsync(new[] { query }, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (embeddings.Count == 0)
        {
            return Array.Empty<CodeSearchResult>();
        }

        // Search in vector store
        var vectorResults = await _vectorStore
            .SearchSimilarAsync(embeddings[0].Vector.ToArray(), cancellationToken)
            .ConfigureAwait(false);

        return vectorResults.Select(r => new CodeSearchResult
        {
            DocumentId = r.DocumentId,
            Content = r.Content,
            FilePath = r.FilePath,
            Similarity = r.Similarity
        }).ToList();
    }

    private async Task<Dictionary<string, string>> ReadFileContentsAsync(
        CodeReviewContext context,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>();

        foreach (var filePath in context.FilePaths)
        {
            if (context.FileContents.TryGetValue(filePath, out var content))
            {
                result[filePath] = content;
            }
            else if (File.Exists(filePath))
            {
                result[filePath] = await File.ReadAllTextAsync(filePath, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return result;
    }

    private async Task<string> BuildReviewPromptAsync(
        CodeReviewContext context,
        Dictionary<string, string> fileContents,
        RagContextInfo? ragInfo,
        CancellationToken cancellationToken)
    {
        var prompt = new StringBuilder();

        // Add system instructions
        prompt.AppendLine("Atue como um engenheiro de software senior especializado em revisao de codigo.");
        prompt.AppendLine("Sua tarefa é revisar o codigo fornecido seguindo as diretrizes abaixo:");
        prompt.AppendLine();

        // Add project-specific rules if checking project rules
        if (context.CheckProjectRules)
        {
            prompt.AppendLine("Regras especificas do projeto ASXRunTerminal:");
            foreach (var rule in ProjectSpecificRules)
            {
                prompt.AppendLine($"- {rule}");
            }
            prompt.AppendLine();
        }

        // Add focus-specific instructions
        prompt.AppendLine($"Foco da revisao: {GetFocusDescription(context.Focus)}");
        prompt.AppendLine($"Severidade minima: {context.MinSeverity}");
        prompt.AppendLine();

        // Add RAG context if enabled
        if (context.UseRag && ragInfo != null)
        {
            var relevantCode = await SearchRelevantCodeAsync(
                "best practices patterns code quality",
                cancellationToken)
                .ConfigureAwait(false);

            if (relevantCode.Count > 0)
            {
                prompt.AppendLine("Contexto relevante do projeto (codigo similar):");
                foreach (var code in relevantCode.Take(3))
                {
                    prompt.AppendLine($"Arquivo: {code.FilePath} (similaridade: {code.Similarity:F2})");
                    prompt.AppendLine("```");
                    prompt.AppendLine(TruncateContent(code.Content, 500));
                    prompt.AppendLine("```");
                    prompt.AppendLine();
                }

                ragInfo.DocumentsRetrieved = relevantCode.Count;
                ragInfo.AverageSimilarity = relevantCode.Average(r => r.Similarity);
                ragInfo.ContextFiles = relevantCode.Select(r => r.FilePath).ToList();
            }
        }

        // Add files to review
        prompt.AppendLine("Arquivos para revisar:");
        foreach (var kvp in fileContents)
        {
            prompt.AppendLine($"### {kvp.Key}");
            prompt.AppendLine("```");
            prompt.AppendLine(kvp.Value);
            prompt.AppendLine("```");
            prompt.AppendLine();
        }

        // Add output format instructions
        prompt.AppendLine("Instrucoes de saida:");
        prompt.AppendLine("Forneca uma revisao estruturada no seguinte formato:");
        prompt.AppendLine("1. Resumo geral da qualidade do codigo");
        prompt.AppendLine("2. Lista de problemas encontrados (por severidade)");
        prompt.AppendLine("   - Para cada problema: arquivo, linha, categoria, descricao, sugestao");
        prompt.AppendLine("3. Metricas: numero de problemas por severidade/categoria");
        prompt.AppendLine("4. Recomendacoes gerais");
        prompt.AppendLine("5. Score de qualidade (0-100) e conformidade com regras do projeto (0-100)");

        return prompt.ToString();
    }

    private CodeReviewResult ParseReviewResponse(
        string response,
        CodeReviewContext context,
        Dictionary<string, string> fileContents)
    {
        var result = new CodeReviewResult
        {
            Status = CodeReviewStatus.Completed,
            Metrics = new CodeReviewMetrics
            {
                TotalFiles = fileContents.Count,
                TotalLines = fileContents.Values.Sum(c => c.Split('\n').Length)
            }
        };

        // Simple parsing - in production, you'd want more robust parsing
        var lines = response.Split('\n');
        var currentSection = string.Empty;
        var issues = new List<CodeReviewIssue>();
        var issueCounter = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("##") || trimmed.StartsWith("#"))
            {
                currentSection = trimmed.Replace("#", "").Trim().ToLowerInvariant();
                continue;
            }

            if (currentSection.Contains("problema") || currentSection.Contains("issue"))
            {
                // Try to parse issue lines
                if (trimmed.StartsWith("-") || trimmed.StartsWith("*"))
                {
                    var issue = new CodeReviewIssue
                    {
                        Id = $"ISSUE-{++issueCounter:D4}",
                        Severity = DetermineSeverityFromText(trimmed),
                        Category = DetermineCategoryFromText(trimmed),
                        Title = ExtractTitleFromText(trimmed),
                        Description = trimmed
                    };

                    if (issue.Severity >= context.MinSeverity)
                    {
                        issues.Add(issue);
                    }
                }
            }
        }

        result.Issues = issues;

        // Calculate metrics
        result.Metrics.IssuesBySeverity = issues
            .GroupBy(i => i.Severity)
            .ToDictionary(g => g.Key, g => g.Count());

        result.Metrics.IssuesByCategory = issues
            .GroupBy(i => i.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        // Calculate quality score (simple heuristic)
        var criticalIssues = issues.Count(i => i.Severity == CodeReviewSeverity.Critical);
        var highIssues = issues.Count(i => i.Severity == CodeReviewSeverity.High);
        result.Metrics.QualityScore = Math.Max(0, 100 - (criticalIssues * 20) - (highIssues * 10));

        // Calculate project rule compliance
        var ruleViolations = issues.Count(i => !string.IsNullOrEmpty(i.ProjectRule));
        result.Metrics.ProjectRuleCompliance = ruleViolations == 0 
            ? 100 
            : Math.Max(0, 100 - (ruleViolations * 15));

        return result;
    }

    private CodeReviewSeverity DetermineSeverityFromText(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("critic") || lower.Contains("security") || lower.Contains("vulnerab"))
            return CodeReviewSeverity.Critical;
        if (lower.Contains("high") || lower.Contains("error") || lower.Contains("fail"))
            return CodeReviewSeverity.High;
        if (lower.Contains("medium") || lower.Contains("warning"))
            return CodeReviewSeverity.Medium;
        if (lower.Contains("low") || lower.Contains("minor"))
            return CodeReviewSeverity.Low;
        return CodeReviewSeverity.Info;
    }

    private string DetermineCategoryFromText(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("security")) return "Security";
        if (lower.Contains("performance")) return "Performance";
        if (lower.Contains("test")) return "Testing";
        if (lower.Contains("style") || lower.Contains("format")) return "Style";
        if (lower.Contains("maintain")) return "Maintainability";
        if (lower.Contains("rule") || lower.Contains("project")) return "Project Rules";
        return "General";
    }

    private string ExtractTitleFromText(string text)
    {
        // Remove markdown markers and extract first meaningful part
        var cleaned = text.TrimStart('-', '*').Trim();
        if (cleaned.Length > 100)
            return cleaned.Substring(0, 100) + "...";
        return cleaned;
    }

    private string GetFocusDescription(CodeReviewFocus focus)
    {
        return focus switch
        {
            CodeReviewFocus.Comprehensive => "Revisao abrangente de todos os aspectos",
            CodeReviewFocus.Security => "Foco em seguranca e vulnerabilidades",
            CodeReviewFocus.Performance => "Foco em performance e otimizacao",
            CodeReviewFocus.Maintainability => "Foco em legibilidade e manutenibilidade",
            CodeReviewFocus.Testing => "Foco em cobertura e qualidade de testes",
            CodeReviewFocus.ProjectRules => "Foco em conformidade com regras do projeto",
            _ => "Revisao geral"
        };
    }

    private string GenerateDocumentId(string filePath)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes(filePath));
        return Convert.ToHexString(hash).Substring(0, 16);
    }

    private string TruncateContent(string content, int maxLength)
    {
        if (content.Length <= maxLength)
            return content;
        return content.Substring(0, maxLength) + "...";
    }
}
