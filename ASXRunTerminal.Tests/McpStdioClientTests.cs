using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;

namespace ASXRunTerminal.Tests;

public sealed class McpStdioClientTests
{
    [Fact]
    public void McpServerProcessOptions_ImplicitOperator_MapsCommand()
    {
        McpServerProcessOptions options = "node";

        Assert.Equal("node", options.Command);
        Assert.Empty(options.Arguments);
        Assert.Null(options.WorkingDirectory);
        Assert.Empty(options.EnvironmentVariables);
    }

    [Fact]
    public async Task SendRequestAsync_ReturnsResult_WhenServerRespondsWithSuccess()
    {
        await using var context = new InMemoryMcpConnectionContext();
        await using IMcpClient client = CreateClient(context);

        var serverTask = Task.Run(async () =>
        {
            using var request = await McpMessageFraming.ReadMessageAsync(context.ServerInput);
            Assert.NotNull(request);
            Assert.Equal("2.0", request!.RootElement.GetProperty("jsonrpc").GetString());
            Assert.Equal("tools/list", request.RootElement.GetProperty("method").GetString());
            var requestId = request.RootElement.GetProperty("id").GetString();
            Assert.False(string.IsNullOrWhiteSpace(requestId));

            var responsePayload = $$"""
            {
              "jsonrpc": "2.0",
              "id": "{{requestId}}",
              "result": {
                "tools": [
                  {
                    "name": "echo"
                  }
                ]
              }
            }
            """;

            await McpMessageFraming.WriteMessageAsync(
                context.ServerOutput,
                Encoding.UTF8.GetBytes(responsePayload));
        });

        var result = await client.SendRequestAsync("tools/list");

        Assert.Equal(JsonValueKind.Array, result.GetProperty("tools").ValueKind);
        Assert.Equal("echo", result.GetProperty("tools")[0].GetProperty("name").GetString());
        await serverTask;
    }

    [Fact]
    public async Task SendRequestAsync_ThrowsMcpRequestException_WhenServerReturnsJsonRpcError()
    {
        await using var context = new InMemoryMcpConnectionContext();
        await using IMcpClient client = CreateClient(context);

        var serverTask = Task.Run(async () =>
        {
            using var request = await McpMessageFraming.ReadMessageAsync(context.ServerInput);
            Assert.NotNull(request);
            var requestId = request!.RootElement.GetProperty("id").GetString();

            var responsePayload = $$"""
            {
              "jsonrpc": "2.0",
              "id": "{{requestId}}",
              "error": {
                "code": -32601,
                "message": "Method not found"
              }
            }
            """;

            await McpMessageFraming.WriteMessageAsync(
                context.ServerOutput,
                Encoding.UTF8.GetBytes(responsePayload));
        });

        var exception = await Assert.ThrowsAsync<McpRequestException>(
            () => client.SendRequestAsync("tools/unknown"));

        Assert.Equal("tools/unknown", exception.Method);
        Assert.Equal(-32601, exception.Code);
        Assert.Contains("Method not found", exception.Message);
        await serverTask;
    }

    [Fact]
    public async Task SendRequestAsync_ThrowsTimeoutException_WhenResponseDoesNotArrive()
    {
        await using var context = new InMemoryMcpConnectionContext();
        await using IMcpClient client = CreateClient(context);

        var exception = await Assert.ThrowsAsync<TimeoutException>(
            () => client.SendRequestAsync(
                method: "tools/list",
                timeout: TimeSpan.FromMilliseconds(100)));

        Assert.Contains("timeout", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendRequestAsync_ThrowsInvalidOperationException_WhenServerClosesStreamBeforeResponse()
    {
        await using var context = new InMemoryMcpConnectionContext(
            standardError: "server crashed");
        await using IMcpClient client = CreateClient(context);

        var serverTask = Task.Run(async () =>
        {
            using var request = await McpMessageFraming.ReadMessageAsync(context.ServerInput);
            Assert.NotNull(request);
            await context.ServerOutput.DisposeAsync();
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SendRequestAsync(
                method: "tools/list",
                timeout: TimeSpan.FromSeconds(5)));

        Assert.Contains("encerrada", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("server crashed", exception.Message, StringComparison.OrdinalIgnoreCase);
        await serverTask;
    }

    [Fact]
    public async Task SendNotificationAsync_WritesJsonRpcNotificationWithoutId()
    {
        await using var context = new InMemoryMcpConnectionContext();
        await using IMcpClient client = CreateClient(context);

        var payload = JsonDocument.Parse(
            """
            {
              "session": "abc-123"
            }
            """).RootElement;

        var serverTask = Task.Run(async () =>
        {
            using var request = await McpMessageFraming.ReadMessageAsync(context.ServerInput);
            Assert.NotNull(request);
            Assert.Equal("2.0", request!.RootElement.GetProperty("jsonrpc").GetString());
            Assert.Equal("notifications/initialized", request.RootElement.GetProperty("method").GetString());
            Assert.False(request.RootElement.TryGetProperty("id", out _));
            Assert.Equal("abc-123", request.RootElement.GetProperty("params").GetProperty("session").GetString());
        });

        await client.SendNotificationAsync(
            method: "notifications/initialized",
            parameters: payload);

        await serverTask;
    }

    private static IMcpClient CreateClient(InMemoryMcpConnectionContext context)
    {
        return new McpStdioClient(
            new McpServerProcessOptions("fake-server"),
            defaultRequestTimeout: TimeSpan.FromSeconds(2),
            connectionFactory: (_, _) => ValueTask.FromResult(context.ClientConnection));
    }

    private sealed class InMemoryMcpConnectionContext : IAsyncDisposable
    {
        private readonly Stream _clientInput;
        private readonly Stream _clientOutput;

        public InMemoryMcpConnectionContext(string? standardError = null)
        {
            var serverToClientPipe = new Pipe();
            var clientToServerPipe = new Pipe();

            _clientInput = serverToClientPipe.Reader.AsStream();
            _clientOutput = clientToServerPipe.Writer.AsStream();

            ServerInput = clientToServerPipe.Reader.AsStream();
            ServerOutput = serverToClientPipe.Writer.AsStream();

            ClientConnection = new McpStdioConnection(
                input: _clientInput,
                output: _clientOutput,
                standardErrorProvider: () => standardError ?? string.Empty,
                disposeAsync: DisposeClientSideAsync);
        }

        public McpStdioConnection ClientConnection { get; }

        public Stream ServerInput { get; }

        public Stream ServerOutput { get; }

        public async ValueTask DisposeAsync()
        {
            await ClientConnection.DisposeAsync();
            await ServerInput.DisposeAsync();
            await ServerOutput.DisposeAsync();
        }

        private async ValueTask DisposeClientSideAsync()
        {
            await _clientOutput.DisposeAsync();
            await _clientInput.DisposeAsync();
        }
    }
}
