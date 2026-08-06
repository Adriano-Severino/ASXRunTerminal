using System.Collections.Generic;

namespace ASXRunTerminal.Subagents;

/// <summary>
/// Contexto para uma revisao de codigo.
/// Contem informacoes sobre os arquivos a serem revisados e opcoes de configuracao.
/// </summary>
internal sealed class CodeReviewContext
{
    /// <summary>
    /// Lista de caminhos de arquivos para revisar.
    /// </summary>
    public IReadOnlyList<string> FilePaths { get; set; } = System.Array.Empty<string>();

    /// <summary>
    /// Conteudo dos arquivos a serem revisados (opcional, se fornecido evita leitura de disco).
    /// </summary>
    public IReadOnlyDictionary<string, string> FileContents { get; set; } = 
        new System.Collections.Generic.Dictionary<string, string>();

    /// <summary>
    /// Nivel de severidade minima para reportar problemas.
    /// </summary>
    public CodeReviewSeverity MinSeverity { get; set; } = CodeReviewSeverity.Low;

    /// <summary>
    /// Foco especifico da revisao (ex: seguranca, performance, manutenibilidade).
    /// </summary>
    public CodeReviewFocus Focus { get; set; } = CodeReviewFocus.Comprehensive;

    /// <summary>
    /// Se true, usa RAG para contexto do projeto; caso contrario, revisao simples.
    /// </summary>
    public bool UseRag { get; set; } = true;

    /// <summary>
    /// Modelo do Ollama a ser usado para a revisao.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Se true, inclui sugestoes de refatoracao automatica.
    /// </summary>
    public bool IncludeRefactoringSuggestions { get; set; } = false;

    /// <summary>
    /// Se true, verifica conformidade com regras especificas do ASXRunTerminal.
    /// </summary>
    public bool CheckProjectRules { get; set; } = true;
}

/// <summary>
/// Niveis de severidade para problemas de codigo.
/// </summary>
internal enum CodeReviewSeverity
{
    /// <summary>
    /// Problemas criticos que devem ser corrigidos imediatamente (bugs de seguranca, crashes).
    /// </summary>
    Critical,

    /// <summary>
    /// Problemas importantes que afetam funcionalidade ou performance.
    /// </summary>
    High,

    /// <summary>
    /// Problemas moderados que devem ser corrigidos mas nao bloqueiam.
    /// </summary>
    Medium,

    /// <summary>
    /// Problemas menores, sugestoes de melhoria.
    /// </summary>
    Low,

    /// <summary>
    /// Apenas informacional, sugestoes opcionais.
    /// </summary>
    Info
}

/// <summary>
/// Foco da revisao de codigo.
/// </summary>
internal enum CodeReviewFocus
{
    /// <summary>
    /// Revisao abrangente cobrindo todos os aspectos.
    /// </summary>
    Comprehensive,

    /// <summary>
    /// Foco em seguranca e vulnerabilidades.
    /// </summary>
    Security,

    /// <summary>
    /// Foco em performance e otimizacao.
    /// </summary>
    Performance,

    /// <summary>
    /// Foco em manutenibilidade e legibilidade.
    /// </summary>
    Maintainability,

    /// <summary>
    /// Foco em cobertura e qualidade de testes.
    /// </summary>
    Testing,

    /// <summary>
    /// Foco em conformidade com regras do projeto (implicit operator, etc).
    /// </summary>
    ProjectRules
}
