using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASXRunTerminal.Subagents;

/// <summary>
/// Interface para o subagente de revisao de codigo usando RAG.
/// Fornece analise de codigo aprimorada com contexto do projeto e melhores praticas.
/// </summary>
internal interface ICodeReviewerSubagent
{
    /// <summary>
    /// Executa uma revisao de codigo abrangente usando RAG para contexto.
    /// </summary>
    /// <param name="context">Contexto da revisao de codigo.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Resultado da revisao de codigo.</returns>
    Task<CodeReviewResult> ReviewCodeAsync(
        CodeReviewContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexa arquivos de codigo para uso em buscas RAG.
    /// </summary>
    /// <param name="filePaths">Caminhos dos arquivos para indexar.</param>
    /// <param name="forceReindex">Se true, reindexa mesmo que ja esteja no cache.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Numero de arquivos indexados.</returns>
    Task<int> IndexCodeFilesAsync(
        IReadOnlyList<string> filePaths,
        bool forceReindex = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca contexto relevante de codigo similar usando RAG.
    /// </summary>
    /// <param name="query">Consulta de busca.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Lista de resultados de busca.</returns>
    Task<IReadOnlyList<CodeSearchResult>> SearchRelevantCodeAsync(
        string query,
        CancellationToken cancellationToken = default);
}
