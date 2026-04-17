using System.Text.Json;
using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;

namespace ASXRunTerminal.Tests;

public sealed class McpToolDiscoveryTests
{
    [Fact]
    public async Task DiscoverAsync_SendsToolsListAndParsesSchema()
    {
        var toolsListResult = ParseJson(
            """
            {
              "tools": [
                {
                  "name": "search",
                  "description": "Busca no indice",
                  "inputSchema": {
                    "type": "object",
                    "properties": {
                      "query": {
                        "type": "string",
                        "description": "Termo de busca."
                      },
                      "limit": {
                        "type": "integer"
                      }
                    },
                    "required": ["query"]
                  }
                }
              ]
            }
            """);

        await using var client = new StubMcpClient(toolsListResult);
        var tools = await McpToolDiscovery.DiscoverAsync(client);

        Assert.Equal("tools/list", client.LastMethod);
        Assert.Equal(TimeSpan.FromSeconds(15), client.LastTimeout);

        var tool = Assert.Single(tools);
        Assert.Equal("search", tool.Name);
        Assert.Equal("Busca no indice", tool.Description);
        Assert.Equal(2, tool.Parameters.Count);

        var queryParameter = Assert.Single(
            tool.Parameters,
            static parameter => string.Equals(parameter.Name, "query", StringComparison.Ordinal));
        Assert.True(queryParameter.IsRequired);
        Assert.Equal("Termo de busca.", queryParameter.Description);

        var limitParameter = Assert.Single(
            tool.Parameters,
            static parameter => string.Equals(parameter.Name, "limit", StringComparison.Ordinal));
        Assert.False(limitParameter.IsRequired);
        Assert.Contains("limit", limitParameter.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseToolsWithSchema_TracksAdditionalPropertiesRules()
    {
        var toolsListResult = ParseJson(
            """
            {
              "tools": [
                {
                  "name": "strict-tool",
                  "inputSchema": {
                    "type": "object",
                    "properties": {
                      "query": {
                        "type": "string"
                      }
                    },
                    "required": ["query"],
                    "additionalProperties": false
                  }
                },
                {
                  "name": "loose-tool",
                  "inputSchema": {
                    "type": "object",
                    "properties": {
                      "query": {
                        "type": "string"
                      }
                    }
                  }
                }
              ]
            }
            """);

        var discoveredTools = McpToolDiscovery.ParseToolsWithSchema(toolsListResult);
        Assert.Equal(2, discoveredTools.Count);

        var strictTool = Assert.Single(
            discoveredTools,
            static tool => string.Equals(tool.Descriptor.Name, "strict-tool", StringComparison.Ordinal));
        Assert.True(strictTool.HasInputSchema);
        Assert.False(strictTool.AllowsAdditionalProperties);

        var looseTool = Assert.Single(
            discoveredTools,
            static tool => string.Equals(tool.Descriptor.Name, "loose-tool", StringComparison.Ordinal));
        Assert.True(looseTool.HasInputSchema);
        Assert.True(looseTool.AllowsAdditionalProperties);
    }

    [Fact]
    public void ParseToolsWithSchema_WithoutInputSchema_MarksSchemaAsOptional()
    {
        var toolsListResult = ParseJson(
            """
            {
              "tools": [
                {
                  "name": "ping",
                  "description": "Teste"
                }
              ]
            }
            """);

        var discoveredTool = Assert.Single(McpToolDiscovery.ParseToolsWithSchema(toolsListResult));

        Assert.False(discoveredTool.HasInputSchema);
        Assert.True(discoveredTool.AllowsAdditionalProperties);
        Assert.Empty(discoveredTool.Descriptor.Parameters);
    }

    [Fact]
    public void ParseTools_SupportsSnakeCaseInputSchemaAlias()
    {
        var toolsListResult = ParseJson(
            """
            {
              "tools": [
                {
                  "name": "echo",
                  "input_schema": {
                    "type": "object",
                    "properties": {
                      "text": {
                        "description": "Texto a ser ecoado."
                      }
                    },
                    "required": ["text"]
                  }
                }
              ]
            }
            """);

        var tools = McpToolDiscovery.ParseTools(toolsListResult);

        var tool = Assert.Single(tools);
        Assert.Equal("echo", tool.Name);
        var parameter = Assert.Single(tool.Parameters);
        Assert.Equal("text", parameter.Name);
        Assert.True(parameter.IsRequired);
    }

    [Fact]
    public void ParseTools_ReturnsEmptyParameters_WhenSchemaIsNotInformed()
    {
        var toolsListResult = ParseJson(
            """
            {
              "tools": [
                {
                  "name": "ping",
                  "description": "Teste basico."
                }
              ]
            }
            """);

        var tools = McpToolDiscovery.ParseTools(toolsListResult);

        var tool = Assert.Single(tools);
        Assert.Equal("ping", tool.Name);
        Assert.Empty(tool.Parameters);
    }

    [Fact]
    public void ParseTools_Throws_WhenToolsFieldIsMissing()
    {
        var toolsListResult = ParseJson(
            """
            {
              "result": []
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(
            () => McpToolDiscovery.ParseTools(toolsListResult));

        Assert.Contains("campo 'tools'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseTools_Throws_WhenInputSchemaIsNotObject()
    {
        var toolsListResult = ParseJson(
            """
            {
              "tools": [
                {
                  "name": "invalid",
                  "inputSchema": "invalid"
                }
              ]
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(
            () => McpToolDiscovery.ParseTools(toolsListResult));

        Assert.Contains("inputSchema", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseTools_Throws_WhenRequiredContainsUnknownParameter()
    {
        var toolsListResult = ParseJson(
            """
            {
              "tools": [
                {
                  "name": "search",
                  "inputSchema": {
                    "type": "object",
                    "properties": {
                      "query": {
                        "type": "string"
                      }
                    },
                    "required": ["missing"]
                  }
                }
              ]
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(
            () => McpToolDiscovery.ParseTools(toolsListResult));

        Assert.Contains("missing", exception.Message, StringComparison.Ordinal);
        Assert.Contains("properties", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseTools_Throws_WhenRequiredContainsDuplicateParameter()
    {
        var toolsListResult = ParseJson(
            """
            {
              "tools": [
                {
                  "name": "search",
                  "inputSchema": {
                    "type": "object",
                    "properties": {
                      "query": {
                        "type": "string"
                      }
                    },
                    "required": ["query", "query"]
                  }
                }
              ]
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(
            () => McpToolDiscovery.ParseTools(toolsListResult));

        Assert.Contains("repetido", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class StubMcpClient(JsonElement sendRequestResponse) : IMcpClient
    {
        private readonly JsonElement _sendRequestResponse = sendRequestResponse.Clone();

        public bool IsConnected => true;

        public string? LastMethod { get; private set; }

        public TimeSpan? LastTimeout { get; private set; }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public Task<JsonElement> SendRequestAsync(
            string method,
            JsonElement? parameters = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastMethod = method;
            LastTimeout = timeout;
            return Task.FromResult(_sendRequestResponse.Clone());
        }

        public Task SendNotificationAsync(
            string method,
            JsonElement? parameters = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
