using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.AI;

namespace ASXRunTerminal.Infra;

/// <summary>
/// Armazenamento de vetores usando SQLite para operacoes RAG.
/// Implementa persistencia de embeddings e busca por similaridade usando cosine similarity.
/// </summary>
internal sealed class SqliteVectorStore : IDisposable
{
    private readonly VectorStoreConfiguration _configuration;
    private readonly SqliteConnection? _connection;
    private readonly bool _ownsConnection;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SqliteVectorStore(VectorStoreConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        if (string.IsNullOrEmpty(_configuration.DatabasePath))
        {
            // In-memory mode
            _connection = new SqliteConnection("Data Source=:memory:");
            _ownsConnection = true;
        }
        else
        {
            _connection = new SqliteConnection($"Data Source={_configuration.DatabasePath}");
            _ownsConnection = true;
        }

        InitializeDatabase();
    }

    /// <summary>
    /// Adiciona um documento ao armazenamento de vetores.
    /// </summary>
    public async Task AddDocumentAsync(
        string documentId,
        string content,
        string filePath,
        IReadOnlyList<float> embedding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(embedding);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnectionOpen();

            var embeddingJson = SerializeEmbedding(embedding);
            var now = DateTime.UtcNow;

            var command = _connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO documents 
                (id, content, file_path, embedding, created_at, updated_at)
                VALUES (@id, @content, @file_path, @embedding, @created_at, @updated_at)";

            command.Parameters.AddWithValue("@id", documentId);
            command.Parameters.AddWithValue("@content", content);
            command.Parameters.AddWithValue("@file_path", filePath);
            command.Parameters.AddWithValue("@embedding", embeddingJson);
            command.Parameters.AddWithValue("@created_at", now);
            command.Parameters.AddWithValue("@updated_at", now);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Busca documentos similares usando cosine similarity.
    /// </summary>
    public async Task<IReadOnlyList<VectorSearchResult>> SearchSimilarAsync(
        IReadOnlyList<float> queryEmbedding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryEmbedding);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnectionOpen();

            var command = _connection.CreateCommand();
            command.CommandText = @"
                SELECT id, content, file_path, embedding, created_at, updated_at
                FROM documents";

            var results = new List<VectorSearchResult>();

            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = reader.GetString(0);
                var content = reader.GetString(1);
                var filePath = reader.GetString(2);
                var embeddingJson = reader.GetString(3);
                var createdAt = reader.GetDateTime(4);
                var updatedAt = reader.GetDateTime(5);

                var storedEmbedding = DeserializeEmbedding(embeddingJson);
                var similarity = ComputeCosineSimilarity(queryEmbedding, storedEmbedding);

                if (similarity >= _configuration.SimilarityThreshold)
                {
                    results.Add(new VectorSearchResult(
                        id,
                        content,
                        filePath,
                        similarity,
                        createdAt,
                        updatedAt));
                }
            }

            return results
                .OrderByDescending(r => r.Similarity)
                .Take(_configuration.MaxSearchResults)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Verifica se um documento ja esta indexado.
    /// </summary>
    public async Task<bool> IsDocumentIndexedAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentId);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnectionOpen();

            var command = _connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM documents WHERE id = @id";
            command.Parameters.AddWithValue("@id", documentId);

            var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            return count > 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Obtém todos os documentos indexados.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetIndexedDocumentIdsAsync(
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnectionOpen();

            var command = _connection.CreateCommand();
            command.CommandText = "SELECT id FROM documents";

            var ids = new List<string>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ids.Add(reader.GetString(0));
            }

            return ids;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Remove um documento do armazenamento.
    /// </summary>
    public async Task RemoveDocumentAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentId);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnectionOpen();

            var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM documents WHERE id = @id";
            command.Parameters.AddWithValue("@id", documentId);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Limpa todos os documentos do armazenamento.
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnectionOpen();

            var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM documents";

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private void InitializeDatabase()
    {
        EnsureConnectionOpen();

        var command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS documents (
                id TEXT PRIMARY KEY,
                content TEXT NOT NULL,
                file_path TEXT NOT NULL,
                embedding TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_file_path ON documents(file_path);
            CREATE INDEX IF NOT EXISTS idx_updated_at ON documents(updated_at);
        ";

        command.ExecuteNonQuery();
    }

    private void EnsureConnectionOpen()
    {
        if (_connection?.State != System.Data.ConnectionState.Open)
        {
            _connection?.Open();
        }
    }

    private string SerializeEmbedding(IReadOnlyList<float> embedding)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < embedding.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(embedding[i].ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
        }
        sb.Append(']');
        return sb.ToString();
    }

    private float[] DeserializeEmbedding(string json)
    {
        // Simple JSON array parsing for performance
        if (string.IsNullOrWhiteSpace(json) || json[0] != '[' || json[^1] != ']')
        {
            return Array.Empty<float>();
        }

        var content = json.Substring(1, json.Length - 2);
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<float>();
        }

        var parts = content.Split(',');
        var result = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (float.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                result[i] = value;
            }
        }

        return result;
    }

    private float ComputeCosineSimilarity(IReadOnlyList<float> a, IReadOnlyList<float> b)
    {
        if (a.Count != b.Count || a.Count == 0)
        {
            return 0f;
        }

        float dotProduct = 0;
        float magnitudeA = 0;
        float magnitudeB = 0;

        for (int i = 0; i < a.Count; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = (float)Math.Sqrt(magnitudeA);
        magnitudeB = (float)Math.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
        {
            return 0f;
        }

        return dotProduct / (magnitudeA * magnitudeB);
    }

    public void Dispose()
    {
        _lock?.Dispose();
        if (_ownsConnection)
        {
            _connection?.Dispose();
        }
    }
}

/// <summary>
/// Resultado de uma busca de similaridade no armazenamento de vetores.
/// </summary>
internal sealed record VectorSearchResult(
    string DocumentId,
    string Content,
    string FilePath,
    float Similarity,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
