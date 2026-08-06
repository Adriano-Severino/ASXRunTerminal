using System.Collections.Generic;

namespace ASXRunTerminal.Subagents;

/// <summary>
/// Resultado de uma revisao de codigo.
/// Contem problemas encontrados, metricas e recomendacoes.
/// </summary>
internal sealed class CodeReviewResult
{
    /// <summary>
    /// Status geral da revisao.
    /// </summary>
    public CodeReviewStatus Status { get; set; }

    /// <summary>
    /// Lista de problemas encontrados por arquivo.
    /// </summary>
    public IReadOnlyList<CodeReviewIssue> Issues { get; set; } = System.Array.Empty<CodeReviewIssue>();

    /// <summary>
    /// Metricas gerais da revisao.
    /// </summary>
    public CodeReviewMetrics Metrics { get; set; } = new();

    /// <summary>
    /// Recomendacoes gerais.
    /// </summary>
    public IReadOnlyList<string> Recommendations { get; set; } = System.Array.Empty<string>();

    /// <summary>
    /// Contexto RAG usado na revisao (se aplicavel).
    /// </summary>
    public RagContextInfo? RagContext { get; set; }

    /// <summary>
    /// Tempo total gasto na revisao em milissegundos.
    /// </summary>
    public long DurationMs { get; set; }
}

/// <summary>
/// Status geral de uma revisao de codigo.
/// </summary>
internal enum CodeReviewStatus
{
    /// <summary>
    /// Revisao concluida com sucesso.
    /// </summary>
    Completed,

    /// <summary>
    /// Revisao concluida com erros.
    /// </summary>
    CompletedWithErrors,

    /// <summary>
    /// Revisao cancelada.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Revisao falhou por erro interno.
    /// </summary>
    Failed
}

/// <summary>
/// Problema encontrado durante a revisao de codigo.
/// </summary>
internal sealed class CodeReviewIssue
{
    /// <summary>
    /// Identificador unico do problema.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Arquivo onde o problema foi encontrado.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Linha onde o problema foi encontrado (opcional).
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>
    /// Severidade do problema.
    /// </summary>
    public CodeReviewSeverity Severity { get; set; }

    /// <summary>
    /// Categoria do problema.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Titulo descritivo do problema.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Descricao detalhada do problema.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Sugestao de correcao ou melhoria.
    /// </summary>
    public string? Suggestion { get; set; }

    /// <summary>
    /// Codigo de exemplo para correcao (opcional).
    /// </summary>
    public string? ExampleCode { get; set; }

    /// <summary>
    /// Regra do projeto violada (se aplicavel).
    /// </summary>
    public string? ProjectRule { get; set; }

    /// <summary>
    /// Tags adicionais para categorizacao.
    /// </summary>
    public IReadOnlyList<string> Tags { get; set; } = System.Array.Empty<string>();
}

/// <summary>
/// Metricas de uma revisao de codigo.
/// </summary>
internal sealed class CodeReviewMetrics
{
    /// <summary>
    /// Numero total de arquivos revisados.
    /// </summary>
    public int TotalFiles { get; set; }

    /// <summary>
    /// Numero total de linhas analisadas.
    /// </summary>
    public int TotalLines { get; set; }

    /// <summary>
    /// Numero de problemas por severidade.
    /// </summary>
    public Dictionary<CodeReviewSeverity, int> IssuesBySeverity { get; set; } = new();

    /// <summary>
    /// Numero de problemas por categoria.
    /// </summary>
    public Dictionary<string, int> IssuesByCategory { get; set; } = new();

    /// <summary>
    /// Score geral de qualidade (0-100).
    /// </summary>
    public int QualityScore { get; set; }

    /// <summary>
    /// Indice de conformidade com regras do projeto (0-100).
    /// </summary>
    public int ProjectRuleCompliance { get; set; }
}

/// <summary>
/// Informacoes sobre o contexto RAG usado na revisao.
/// </summary>
internal sealed class RagContextInfo
{
    /// <summary>
    /// Numero de documentos recuperados do contexto.
    /// </summary>
    public int DocumentsRetrieved { get; set; }

    /// <summary>
    /// Similaridade media dos documentos recuperados.
    /// </summary>
    public double AverageSimilarity { get; set; }

    /// <summary>
    /// Tempo gasto nas operacoes RAG em milissegundos.
    /// </summary>
    public long RagDurationMs { get; set; }

    /// <summary>
    /// Lista de arquivos usados como contexto.
    /// </summary>
    public IReadOnlyList<string> ContextFiles { get; set; } = System.Array.Empty<string>();
}

/// <summary>
/// Resultado de uma busca de codigo relevante.
/// </summary>
internal sealed class CodeSearchResult
{
    /// <summary>
    /// ID do documento.
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Conteudo do trecho de codigo encontrado.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Caminho do arquivo.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Similaridade com a consulta (0-1).
    /// </summary>
    public float Similarity { get; set; }
}
