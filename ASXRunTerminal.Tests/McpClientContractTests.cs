using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Text.Json;
using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;

namespace ASXRunTerminal.Tests;

public sealed class McpClientContractTests
{
    public static TheoryData<string> HarnessKinds =>
    [
        "stdio",
        "remote-http"
    ];

    [Theory]
    [MemberData(nameof(HarnessKinds))]
    public async Task SendRequestAsync_WritesJsonRpcRequest_AndReturnsResult(
        string harnessKind)
    {
        await using var harness = CreateHarness(harnessKind);
        using var parameters = JsonDocument.Parse(
            """
            {
              "query": "contracts",
              "limit": 3
            }
            """);

        var requestTask = harness.Client.SendRequestAsync(
            method: "tools/list",
            parameters: parameters.RootElement);

        var requestPayload = await harness.ReadRequestAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("2.0", requestPayload.GetProperty("jsonrpc").GetString());
        Assert.Equal("tools/list", requestPayload.GetProperty("method").GetString());
        Assert.True(requestPayload.TryGetProperty("id", out var requestIdElement));
        Assert.Equal(JsonValueKind.String, requestIdElement.ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(requestIdElement.GetString()));
        Assert.Equal("contracts", requestPayload.GetProperty("params").GetProperty("query").GetString());
        Assert.Equal(3, requestPayload.GetProperty("params").GetProperty("limit").GetInt32());

        var responsePayload = $$"""
        {
          "jsonrpc": "2.0",
          "id": {{requestIdElement.GetRawText()}},
          "result": {
            "tools": [
              {
                "name": "contract-echo"
              }
            ]
          }
        }
        """;

        await harness.SendResponseAsync(HttpStatusCode.OK, responsePayload);
        var result = await requestTask;

        Assert.Equal("contract-echo", result.GetProperty("tools")[0].GetProperty("name").GetString());
    }

    [Theory]
    [MemberData(nameof(HarnessKinds))]
    public async Task SendRequestAsync_MapsJsonRpcError_ToMcpRequestException(
        string harnessKind)
    {
        await using var harness = CreateHarness(harnessKind);

        var requestTask = harness.Client.SendRequestAsync("tools/call");

        var requestPayload = await harness.ReadRequestAsync(TimeSpan.FromSeconds(2));
        var responsePayload = $$"""
        {
          "jsonrpc": "2.0",
          "id": {{requestPayload.GetProperty("id").GetRawText()}},
          "error": {
            "code": -32001,
            "message": "Falha de contrato",
            "data": {
              "detail": "invalid-arguments"
            }
          }
        }
        """;

        await harness.SendResponseAsync(HttpStatusCode.OK, responsePayload);
        var exception = await Assert.ThrowsAsync<McpRequestException>(() => requestTask);

        Assert.Equal("tools/call", exception.Method);
        Assert.Equal(-32001, exception.Code);
        Assert.Contains("Falha de contrato", exception.Message, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(exception.RawData));
        using var rawData = JsonDocument.Parse(exception.RawData!);
        Assert.Equal("invalid-arguments", rawData.RootElement.GetProperty("detail").GetString());
    }

    [Theory]
    [MemberData(nameof(HarnessKinds))]
    public async Task SendNotificationAsync_WritesJsonRpcNotification_WithoutId(
        string harnessKind)
    {
        await using var harness = CreateHarness(harnessKind);
        using var parameters = JsonDocument.Parse(
            """
            {
              "session": "contract-session"
            }
            """);

        var notificationTask = harness.Client.SendNotificationAsync(
            method: "notifications/initialized",
            parameters: parameters.RootElement);

        var requestPayload = await harness.ReadRequestAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("2.0", requestPayload.GetProperty("jsonrpc").GetString());
        Assert.Equal("notifications/initialized", requestPayload.GetProperty("method").GetString());
        Assert.Equal("contract-session", requestPayload.GetProperty("params").GetProperty("session").GetString());
        Assert.False(requestPayload.TryGetProperty("id", out _));

        await harness.SendResponseAsync(HttpStatusCode.NoContent);
        await notificationTask;
    }

    private static McpClientContractHarness CreateHarness(string harnessKind)
    {
        return harnessKind switch
        {
            "stdio" => new StdioMcpClientContractHarness(),
            "remote-http" => new RemoteHttpMcpClientContractHarness(),
            _ => throw new InvalidOperationException(
                $"Harness MCP de contrato nao suportado: '{harnessKind}'.")
        };
    }

    private abstract class McpClientContractHarness : IAsyncDisposable
    {
        public abstract IMcpClient Client { get; }

        public abstract Task<JsonElement> ReadRequestAsync(TimeSpan timeout);

        public abstract Task SendResponseAsync(HttpStatusCode statusCode, string? body = null);

        public abstract ValueTask DisposeAsync();
    }

    private sealed class StdioMcpClientContractHarness : McpClientContractHarness
    {
        private readonly Stream _clientInput;
        private readonly Stream _clientOutput;
        private readonly Stream _serverInput;
        private readonly Stream _serverOutput;

        public StdioMcpClientContractHarness()
        {
            var serverToClientPipe = new Pipe();
            var clientToServerPipe = new Pipe();

            _clientInput = serverToClientPipe.Reader.AsStream();
            _clientOutput = clientToServerPipe.Writer.AsStream();
            _serverInput = clientToServerPipe.Reader.AsStream();
            _serverOutput = serverToClientPipe.Writer.AsStream();

            var connection = new McpStdioConnection(
                input: _clientInput,
                output: _clientOutput,
                standardErrorProvider: static () => string.Empty,
                disposeAsync: DisposeClientSideAsync);

            Client = new McpStdioClient(
                new McpServerProcessOptions("contract-mcp-server"),
                defaultRequestTimeout: TimeSpan.FromSeconds(2),
                connectionFactory: (_, _) => ValueTask.FromResult(connection));
        }

        public override IMcpClient Client { get; }

        public override async Task<JsonElement> ReadRequestAsync(TimeSpan timeout)
        {
            using var timeoutTokenSource = new CancellationTokenSource(timeout);
            using var payloadDocument = await McpMessageFraming.ReadMessageAsync(
                _serverInput,
                timeoutTokenSource.Token);
            if (payloadDocument is null)
            {
                throw new TimeoutException("Nao foi possivel ler request MCP via stdio.");
            }

            return payloadDocument.RootElement.Clone();
        }

        public override Task SendResponseAsync(HttpStatusCode statusCode, string? body = null)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return Task.CompletedTask;
            }

            return McpMessageFraming.WriteMessageAsync(
                _serverOutput,
                Encoding.UTF8.GetBytes(body));
        }

        public override async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await _serverInput.DisposeAsync();
            await _serverOutput.DisposeAsync();
        }

        private async ValueTask DisposeClientSideAsync()
        {
            await _clientOutput.DisposeAsync();
            await _clientInput.DisposeAsync();
        }
    }

    private sealed class RemoteHttpMcpClientContractHarness : McpClientContractHarness
    {
        private readonly ContractHttpMessageHandler _handler = new();
        private readonly HttpClient _httpClient;
        private readonly IMcpClient _client;
        private PendingHttpRequest? _pendingRequest;

        public RemoteHttpMcpClientContractHarness()
        {
            _httpClient = new HttpClient(_handler);
            _client = new McpRemoteClient(
                new McpServerRemoteOptions(
                    endpoint: new Uri("https://mcp.example.com/rpc"),
                    transportKind: McpRemoteTransportKind.Http),
                _httpClient);
        }

        public override IMcpClient Client => _client;

        public override async Task<JsonElement> ReadRequestAsync(TimeSpan timeout)
        {
            _pendingRequest = await _handler.WaitForPostAsync(timeout);
            Assert.Equal(HttpMethod.Post, _pendingRequest.Method);
            Assert.Equal("https://mcp.example.com/rpc", _pendingRequest.Url);

            Assert.False(string.IsNullOrWhiteSpace(_pendingRequest.Body));
            using var payloadDocument = JsonDocument.Parse(_pendingRequest.Body!);
            return payloadDocument.RootElement.Clone();
        }

        public override Task SendResponseAsync(HttpStatusCode statusCode, string? body = null)
        {
            var pendingRequest = _pendingRequest ?? throw new InvalidOperationException(
                "Nenhum request HTTP MCP pendente para responder.");
            _pendingRequest = null;

            var response = string.IsNullOrWhiteSpace(body)
                ? new HttpResponseMessage(statusCode)
                : new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };

            pendingRequest.Complete(response);
            return Task.CompletedTask;
        }

        public override async ValueTask DisposeAsync()
        {
            await _client.DisposeAsync();
            _httpClient.Dispose();
            _handler.Dispose();
        }

        private sealed class ContractHttpMessageHandler : HttpMessageHandler
        {
            private readonly object _sync = new();
            private readonly Queue<PendingHttpRequest> _pendingRequests = [];
            private readonly SemaphoreSlim _pendingSignal = new(0);

            public async Task<PendingHttpRequest> WaitForPostAsync(TimeSpan timeout)
            {
                if (!await _pendingSignal.WaitAsync(timeout))
                {
                    throw new TimeoutException("Nenhum request HTTP MCP foi recebido dentro do timeout.");
                }

                lock (_sync)
                {
                    return _pendingRequests.Dequeue();
                }
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                if (request.Method != HttpMethod.Post)
                {
                    return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
                }

                var body = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken);
                var pendingRequest = new PendingHttpRequest(
                    request.Method,
                    request.RequestUri?.ToString() ?? string.Empty,
                    body);

                lock (_sync)
                {
                    _pendingRequests.Enqueue(pendingRequest);
                }

                _pendingSignal.Release();
                return await pendingRequest.Response.Task.WaitAsync(cancellationToken);
            }
        }

        private sealed class PendingHttpRequest(
            HttpMethod method,
            string url,
            string? body)
        {
            public HttpMethod Method { get; } = method;

            public string Url { get; } = url;

            public string? Body { get; } = body;

            public TaskCompletionSource<HttpResponseMessage> Response { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public void Complete(HttpResponseMessage response)
            {
                Response.TrySetResult(response);
            }
        }
    }
}
