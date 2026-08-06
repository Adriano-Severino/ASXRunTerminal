using System;

namespace ASXRunTerminal.Infra;

/// <summary>
/// Configuracao para o armazenamento de vetores e operacoes RAG.
/// Define parametros para SQLite, modelo de embedding e estrategia de indexacao.
/// </summary>
internal sealed class VectorStoreConfiguration
{
    /// <summary>
    /// Caminho para o arquivo SQLite que armazena os vetores.
    /// Se null, usa armazenamento em memoria.
    /// </summary>
    public string? DatabasePath { get; set; }

    /// <summary>
    /// Modelo de embedding do Ollama a ser usado (padrao: nomic-embed-text).
    /// </summary>
    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>
    /// Dimensao esperada dos vetores de embedding.
    /// Para nomic-embed-text, geralmente 768 ou 1024 dependendo da versao.
    /// </summary>
    public int EmbeddingDimension { get; set; } = 768;

    /// <summary>
    /// Numero maximo de resultados a retornar em buscas de similaridade.
    /// </summary>
    public int MaxSearchResults { get; set; } = 10;

    /// <summary>
    /// Limiar de similaridade (0 a 1) para filtrar resultados.
    /// Resultados abaixo deste limiar sao descartados.
    /// </summary>
    public double SimilarityThreshold { get; set; } = 0.7;

    /// <summary>
    /// Estrategia de indexacao a ser usada.
    /// </summary>
    public IndexingStrategy IndexingStrategy { get; set; } = IndexingStrategy.SmartIncremental;

    /// <summary>
    /// Indica se deve reindexar arquivos mesmo que ja estejam no cache.
    /// </summary>
    public bool ForceReindex { get; set; } = false;

    /// <summary>
    /// Tempo de cache para embeddings em minutos.
    /// Apos este periodo, embeddings sao recalculados.
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Cria uma configuracao padrao para armazenamento em memoria.
    /// </summary>
    public static VectorStoreConfiguration CreateInMemory()
    {
        return new VectorStoreConfiguration
        {
            DatabasePath = null,
            IndexingStrategy = IndexingStrategy.InMemory
        };
    }

    /// <summary>
    /// Cria uma configuracao padrao para armazenamento SQLite persistente.
    /// </summary>
    public static VectorStoreConfiguration CreatePersistent(string databasePath)
    {
        return new VectorStoreConfiguration
        {
            DatabasePath = databasePath,
            IndexingStrategy = IndexingStrategy.SmartIncremental
        };
    }
}

/// <summary>
/// Estrategias de indexacao para o armazenamento de vetores.
/// </summary>
internal enum IndexingStrategy
{
    /// <summary>
    /// Indexacao completa de todos os arquivos no primeiro uso.
    /// Lento no setup, mas busca completa.
    /// </summary>
    Full,

    /// <summary>
    /// Indexacao sob demanda: arquivos sao indexados apenas quando necessarios.
    /// Rapido no setup, mas busca pode perder contexto.
    /// </summary>
    OnDemand,

    /// <summary>
    /// Indexacao inteligente incremental: indexa no primeiro uso e cacheia resultados.
    /// Balance entre velocidade e completude.
    /// </summary>
    SmartIncremental,

    /// <summary>
    /// Armazenamento puramente em memoria sem persistencia.
    /// Dados sao perdidos ao reiniciar.
    /// </summary>
    InMemory
}
