using System;
using ASXRunTerminal.Infra;
using Xunit;

namespace ASXRunTerminal.Tests;

public class VectorStoreConfigurationTests
{
    [Fact]
    public void CreateInMemory_ShouldReturnConfigurationWithNullDatabasePath()
    {
        // Act
        var config = VectorStoreConfiguration.CreateInMemory();

        // Assert
        Assert.Null(config.DatabasePath);
        Assert.Equal(IndexingStrategy.InMemory, config.IndexingStrategy);
    }

    [Fact]
    public void CreatePersistent_ShouldReturnConfigurationWithDatabasePath()
    {
        // Arrange
        var dbPath = "/test/path/vector.db";

        // Act
        var config = VectorStoreConfiguration.CreatePersistent(dbPath);

        // Assert
        Assert.Equal(dbPath, config.DatabasePath);
        Assert.Equal(IndexingStrategy.SmartIncremental, config.IndexingStrategy);
    }

    [Fact]
    public void DefaultConfiguration_ShouldHaveExpectedDefaults()
    {
        // Arrange & Act
        var config = new VectorStoreConfiguration();

        // Assert
        Assert.Equal("nomic-embed-text", config.EmbeddingModel);
        Assert.Equal(768, config.EmbeddingDimension);
        Assert.Equal(10, config.MaxSearchResults);
        Assert.Equal(0.7, config.SimilarityThreshold);
        Assert.Equal(IndexingStrategy.SmartIncremental, config.IndexingStrategy);
        Assert.False(config.ForceReindex);
        Assert.Equal(60, config.CacheExpirationMinutes);
    }

    [Fact]
    public void Configuration_ShouldAllowCustomValues()
    {
        // Arrange & Act
        var config = new VectorStoreConfiguration
        {
            DatabasePath = "/custom/path.db",
            EmbeddingModel = "custom-model",
            EmbeddingDimension = 1024,
            MaxSearchResults = 20,
            SimilarityThreshold = 0.8,
            IndexingStrategy = IndexingStrategy.Full,
            ForceReindex = true,
            CacheExpirationMinutes = 120
        };

        // Assert
        Assert.Equal("/custom/path.db", config.DatabasePath);
        Assert.Equal("custom-model", config.EmbeddingModel);
        Assert.Equal(1024, config.EmbeddingDimension);
        Assert.Equal(20, config.MaxSearchResults);
        Assert.Equal(0.8, config.SimilarityThreshold);
        Assert.Equal(IndexingStrategy.Full, config.IndexingStrategy);
        Assert.True(config.ForceReindex);
        Assert.Equal(120, config.CacheExpirationMinutes);
    }
}
