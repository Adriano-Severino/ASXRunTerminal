using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASXRunTerminal.Infra;
using Xunit;

namespace ASXRunTerminal.Tests;

public class SqliteVectorStoreTests
{
    [Fact]
    public async Task AddDocumentAsync_ShouldStoreDocumentSuccessfully()
    {
        // Arrange
        var config = VectorStoreConfiguration.CreateInMemory();
        using var store = new SqliteVectorStore(config);
        var documentId = "test-doc-1";
        var content = "Test content for document";
        var filePath = "/test/path.cs";
        var embedding = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };

        // Act
        await store.AddDocumentAsync(documentId, content, filePath, embedding);

        // Assert
        var isIndexed = await store.IsDocumentIndexedAsync(documentId);
        Assert.True(isIndexed);
    }

    [Fact]
    public async Task SearchSimilarAsync_ShouldReturnSimilarDocuments()
    {
        // Arrange
        var config = VectorStoreConfiguration.CreateInMemory();
        using var store = new SqliteVectorStore(config);

        // Add documents with different embeddings
        await store.AddDocumentAsync("doc1", "Content 1", "/path1.cs", new float[] { 1.0f, 0.0f, 0.0f });
        await store.AddDocumentAsync("doc2", "Content 2", "/path2.cs", new float[] { 0.0f, 1.0f, 0.0f });
        await store.AddDocumentAsync("doc3", "Content 3", "/path3.cs", new float[] { 0.9f, 0.1f, 0.0f });

        var queryEmbedding = new float[] { 1.0f, 0.0f, 0.0f };

        // Act
        var results = await store.SearchSimilarAsync(queryEmbedding);

        // Assert
        Assert.NotNull(results);
        Assert.NotEmpty(results);
        
        // The most similar should be doc1 (identical embedding)
        var mostSimilar = results.OrderByDescending(r => r.Similarity).First();
        Assert.Equal("doc1", mostSimilar.DocumentId);
    }

    [Fact]
    public async Task IsDocumentIndexedAsync_ShouldReturnFalseForNonExistentDocument()
    {
        // Arrange
        var config = VectorStoreConfiguration.CreateInMemory();
        using var store = new SqliteVectorStore(config);

        // Act
        var isIndexed = await store.IsDocumentIndexedAsync("non-existent-doc");

        // Assert
        Assert.False(isIndexed);
    }

    [Fact]
    public async Task RemoveDocumentAsync_ShouldRemoveDocumentSuccessfully()
    {
        // Arrange
        var config = VectorStoreConfiguration.CreateInMemory();
        using var store = new SqliteVectorStore(config);
        var documentId = "test-doc-remove";
        
        await store.AddDocumentAsync(documentId, "Content", "/path.cs", new float[] { 0.1f, 0.2f, 0.3f });
        Assert.True(await store.IsDocumentIndexedAsync(documentId));

        // Act
        await store.RemoveDocumentAsync(documentId);

        // Assert
        Assert.False(await store.IsDocumentIndexedAsync(documentId));
    }

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllDocuments()
    {
        // Arrange
        var config = VectorStoreConfiguration.CreateInMemory();
        using var store = new SqliteVectorStore(config);

        await store.AddDocumentAsync("doc1", "Content 1", "/path1.cs", new float[] { 0.1f, 0.2f, 0.3f });
        await store.AddDocumentAsync("doc2", "Content 2", "/path2.cs", new float[] { 0.4f, 0.5f, 0.6f });

        var idsBefore = await store.GetIndexedDocumentIdsAsync();
        Assert.Equal(2, idsBefore.Count);

        // Act
        await store.ClearAsync();

        // Assert
        var idsAfter = await store.GetIndexedDocumentIdsAsync();
        Assert.Empty(idsAfter);
    }

    [Fact]
    public async Task GetIndexedDocumentIdsAsync_ShouldReturnAllIndexedIds()
    {
        // Arrange
        var config = VectorStoreConfiguration.CreateInMemory();
        using var store = new SqliteVectorStore(config);

        await store.AddDocumentAsync("doc1", "Content 1", "/path1.cs", new float[] { 0.1f, 0.2f, 0.3f });
        await store.AddDocumentAsync("doc2", "Content 2", "/path2.cs", new float[] { 0.4f, 0.5f, 0.6f });
        await store.AddDocumentAsync("doc3", "Content 3", "/path3.cs", new float[] { 0.7f, 0.8f, 0.9f });

        // Act
        var ids = await store.GetIndexedDocumentIdsAsync();

        // Assert
        Assert.Equal(3, ids.Count);
        Assert.Contains("doc1", ids);
        Assert.Contains("doc2", ids);
        Assert.Contains("doc3", ids);
    }

    [Fact]
    public async Task SearchSimilarAsync_ShouldRespectSimilarityThreshold()
    {
        // Arrange
        var config = new VectorStoreConfiguration
        {
            DatabasePath = null,
            SimilarityThreshold = 0.9 // High threshold
        };
        using var store = new SqliteVectorStore(config);

        await store.AddDocumentAsync("doc1", "Content 1", "/path1.cs", new float[] { 1.0f, 0.0f, 0.0f });
        await store.AddDocumentAsync("doc2", "Content 2", "/path2.cs", new float[] { 0.5f, 0.5f, 0.0f });

        var queryEmbedding = new float[] { 1.0f, 0.0f, 0.0f };

        // Act
        var results = await store.SearchSimilarAsync(queryEmbedding);

        // Assert
        // Only doc1 should be returned (high similarity)
        Assert.Single(results);
        Assert.Equal("doc1", results[0].DocumentId);
    }

    [Fact]
    public async Task AddDocumentAsync_ShouldUpdateExistingDocument()
    {
        // Arrange
        var config = VectorStoreConfiguration.CreateInMemory();
        using var store = new SqliteVectorStore(config);
        var documentId = "test-doc-update";

        await store.AddDocumentAsync(documentId, "Original content", "/path.cs", new float[] { 0.1f, 0.2f, 0.3f });

        // Act
        await store.AddDocumentAsync(documentId, "Updated content", "/path.cs", new float[] { 0.4f, 0.5f, 0.6f });

        // Assert
        var isIndexed = await store.IsDocumentIndexedAsync(documentId);
        Assert.True(isIndexed);

        var ids = await store.GetIndexedDocumentIdsAsync();
        Assert.Single(ids); // Should still be only one document
    }
}
