using System.IO.Pipelines;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;

namespace ASXRunTerminal.Tests;

public sealed class McpRemoteClientTests
{
    [Fact]
    public void McpServerRemoteOptions_ImplicitOperator_MapsHttpEndpoint()
    {
        McpServerRemoteOptions options = "https://mcp.example.com/rpc";

        Assert.Equal(new Uri("https://mcp.example.com/rpc"), options.Endpoint);
        Assert.Equal(McpRemoteTransportKind.Http, options.TransportKind);
        Assert.Equal(McpAuthenticationOptions.None, options.Authentication);
    }

    [Fact]
    public void McpAuthenticationOptions_ImplicitOperator_MapsBearerToken()
    {
        McpAuthenticationOptions authentication = "token-abc";
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://mcp.example.com");

        authentication.ApplyTo(request.Headers);

        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("token-abc", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public void McpAuthenticationOptions_Header_Throws_WhenAuthorizationHeaderIsProvided()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => McpAuthenticationOptions.Header("Authorization", "Bearer token"));

        Assert.Contains("Bearer", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendRequestAsync_WithHttpTransport_ReturnsResult_AndAppliesAuthentication()
    {
        using var handler = new JsonRpcHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var options = new McpServerRemoteOptions(
            endpoint: new Uri("https://mcp.example.com/rpc"),
            authentication: McpAuthenticationOptions.Bearer("token-123"));
        await using IMcpClient client = new McpRemoteClient(options, httpClient);

        var result = await client.SendRequestAsync("tools/list");

        Assert.Equal("echo", result.GetProperty("tools")[0].GetProperty("name").GetString());
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://mcp.example.com/rpc", handler.LastRequest.Url);
        Assert.Equal("Bearer", handler.LastRequest.AuthorizationScheme);
        Assert.Equal("token-123", handler.LastRequest.AuthorizationParameter);

        using var requestPayload = JsonDocument.Parse(handler.LastRequest.Body!);
        Assert.Equal("2.0", requestPayload.RootElement.GetProperty("jsonrpc").GetString());
        Assert.Equal("tools/list", requestPayload.RootElement.GetProperty("method").GetString());
        Assert.True(requestPayload.RootElement.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task SendRequestAsync_WithHttpTransport_AppliesCustomAuthenticationHeader()
    {
        using var handler = new JsonRpcHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var options = new McpServerRemoteOptions(
            endpoint: new Uri("https://mcp.example.com/rpc"),
            authentication: McpAuthenticationOptions.Header("X-Api-Key", "abc-123"));
        await using IMcpClient client = new McpRemoteClient(options, httpClient);

        _ = await client.SendRequestAsync("tools/list");

        Assert.NotNull(handler.LastRequest);
        Assert.True(handler.LastRequest!.Headers.TryGetValue("X-Api-Key", out var headerValues));
        Assert.Contains("abc-123", headerValues!);
        Assert.Null(handler.LastRequest.AuthorizationScheme);
    }

    [Fact]
    public async Task SendNotificationAsync_WithHttpTransport_WritesJsonRpcNotificationWithoutId()
    {
        using var handler = new JsonRpcHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var options = new McpServerRemoteOptions(
            endpoint: new Uri("https://mcp.example.com/rpc"));
        await using IMcpClient client = new McpRemoteClient(options, httpClient);
        using var payload = JsonDocument.Parse(
            """
            {
              "session": "abc-123"
            }
            """);

        await client.SendNotificationAsync(
            method: "notifications/initialized",
            parameters: payload.RootElement);

        Assert.NotNull(handler.LastRequest);
        using var requestPayload = JsonDocument.Parse(handler.LastRequest!.Body!);
        Assert.Equal("2.0", requestPayload.RootElement.GetProperty("jsonrpc").GetString());
        Assert.Equal("notifications/initialized", requestPayload.RootElement.GetProperty("method").GetString());
        Assert.False(requestPayload.RootElement.TryGetProperty("id", out _));
        Assert.Equal("abc-123", requestPayload.RootElement.GetProperty("params").GetProperty("session").GetString());
    }

    [Fact]
    public async Task SendRequestAsync_WithSseTransport_ResolvesEndpointEvent_AndReturnsResult()
    {
        await using var handler = new SseMcpMessageHandler(sendEndpointOnConnect: true);
        using var httpClient = new HttpClient(handler);
        var options = new McpServerRemoteOptions(
            endpoint: new Uri("https://mcp.example.com/sse"),
            transportKind: McpRemoteTransportKind.Sse,
            authentication: McpAuthenticationOptions.Basic("user", "secret"));
        await using IMcpClient client = new McpRemoteClient(options, httpClient);

        var requestTask = client.SendRequestAsync("tools/list");

        var postRequest = await handler.WaitForPostAsync(TimeSpan.FromSeconds(2));
        using var requestPayload = JsonDocument.Parse(postRequest.Body!);
        var requestId = requestPayload.RootElement.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(requestId));

        var responsePayload = $$"""
        {
          "jsonrpc": "2.0",
          "id": "{{requestId}}",
          "result": {
            "tools": [
              {
                "name": "remote-echo"
              }
            ]
          }
        }
        """;

        await handler.WriteMessageAsync(responsePayload);
        var result = await requestTask;

        Assert.Equal("remote-echo", result.GetProperty("tools")[0].GetProperty("name").GetString());

        var getRequest = Assert.Single(handler.GetRequests);
        Assert.Equal("Basic", getRequest.AuthorizationScheme);
        Assert.Equal("user:secret", DecodeBasicCredentials(getRequest.AuthorizationParameter));

        var capturedPostRequest = Assert.Single(handler.PostRequests);
        Assert.Equal("https://mcp.example.com/messages", capturedPostRequest.Url);
        Assert.Equal("Basic", capturedPostRequest.AuthorizationScheme);
        Assert.Equal("user:secret", DecodeBasicCredentials(capturedPostRequest.AuthorizationParameter));
    }

    [Fact]
    public async Task SendRequestAsync_WithSseTransport_ThrowsTimeout_WhenEndpointEventDoesNotArrive()
    {
        await using var handler = new SseMcpMessageHandler(sendEndpointOnConnect: false);
        using var httpClient = new HttpClient(handler);
        var options = new McpServerRemoteOptions(
            endpoint: new Uri("https://mcp.example.com/sse"),
            transportKind: McpRemoteTransportKind.Sse);
        await using IMcpClient client = new McpRemoteClient(
            options,
            httpClient,
            defaultRequestTimeout: TimeSpan.FromMilliseconds(150));

        var exception = await Assert.ThrowsAsync<TimeoutException>(
            () => client.SendRequestAsync("tools/list"));

        Assert.Contains("endpoint", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendRequestAsync_WithSseTransport_ReusesExistingConnection_AfterInitialEndpointTimeout()
    {
        await using var handler = new SseMcpMessageHandler(sendEndpointOnConnect: false);
        using var httpClient = new HttpClient(handler);
        var options = new McpServerRemoteOptions(
            endpoint: new Uri("https://mcp.example.com/sse"),
            transportKind: McpRemoteTransportKind.Sse);
        await using IMcpClient client = new McpRemoteClient(
            options,
            httpClient,
            defaultRequestTimeout: TimeSpan.FromMilliseconds(120));

        await Assert.ThrowsAsync<TimeoutException>(
            () => client.SendRequestAsync("tools/list"));
        Assert.Single(handler.GetRequests);

        await handler.WriteEndpointAsync("/messages");
        var requestTask = client.SendRequestAsync(
            method: "tools/list",
            timeout: TimeSpan.FromSeconds(2));

        var postRequest = await handler.WaitForPostAsync(TimeSpan.FromSeconds(2));
        using var requestPayload = JsonDocument.Parse(postRequest.Body!);
        var requestId = requestPayload.RootElement.GetProperty("id").GetString();

        var responsePayload = $$"""
        {
          "jsonrpc": "2.0",
          "id": "{{requestId}}",
          "result": {
            "tools": [
              {
                "name": "reused-connection"
              }
            ]
          }
        }
        """;

        await handler.WriteMessageAsync(responsePayload);
        var result = await requestTask;

        Assert.Equal("reused-connection", result.GetProperty("tools")[0].GetProperty("name").GetString());
        Assert.Single(handler.GetRequests);
    }

    private static string DecodeBasicCredentials(string? authorizationParameter)
    {
        Assert.False(string.IsNullOrWhiteSpace(authorizationParameter));
        var bytes = Convert.FromBase64String(authorizationParameter!);
        return Encoding.UTF8.GetString(bytes);
    }

    private static HttpResponseMessage BuildJsonResponse(HttpStatusCode statusCode, string body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private sealed class JsonRpcHttpMessageHandler : HttpMessageHandler
    {
        public CapturedHttpRequest? LastRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            LastRequest = CapturedHttpRequest.From(request, body);

            if (request.Method != HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            using var payload = JsonDocument.Parse(body);
            if (!payload.RootElement.TryGetProperty("id", out var requestId))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            var responseBody = $$"""
            {
              "jsonrpc": "2.0",
              "id": {{requestId.GetRawText()}},
              "result": {
                "tools": [
                  {
                    "name": "echo"
                  }
                ]
              }
            }
            """;

            return BuildJsonResponse(HttpStatusCode.OK, responseBody);
        }
    }

    private sealed class SseMcpMessageHandler : HttpMessageHandler, IAsyncDisposable
    {
        private readonly Stream _sseInput;
        private readonly Stream _sseOutput;
        private readonly bool _sendEndpointOnConnect;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly TaskCompletionSource<CapturedHttpRequest> _postRequestSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _isDisposed;

        public SseMcpMessageHandler(bool sendEndpointOnConnect)
        {
            var pipe = new Pipe();
            _sseInput = pipe.Reader.AsStream();
            _sseOutput = pipe.Writer.AsStream();
            _sendEndpointOnConnect = sendEndpointOnConnect;
        }

        public List<CapturedHttpRequest> GetRequests { get; } = [];

        public List<CapturedHttpRequest> PostRequests { get; } = [];

        public async Task<CapturedHttpRequest> WaitForPostAsync(TimeSpan timeout)
        {
            return await _postRequestSource.Task.WaitAsync(timeout);
        }

        public async Task WriteMessageAsync(string payload)
        {
            await WriteEventAsync("message", payload);
        }

        public async Task WriteEndpointAsync(string endpoint)
        {
            await WriteEventAsync("endpoint", endpoint);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            {
                return;
            }

            await _sseOutput.DisposeAsync();
            await _sseInput.DisposeAsync();
            _writeLock.Dispose();
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var capturedRequest = CapturedHttpRequest.From(request, body);

            if (request.Method == HttpMethod.Get)
            {
                GetRequests.Add(capturedRequest);

                if (_sendEndpointOnConnect)
                {
                    _ = WriteEventAsync("endpoint", "/messages");
                }

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(_sseInput)
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
                return response;
            }

            if (request.Method == HttpMethod.Post)
            {
                PostRequests.Add(capturedRequest);
                _postRequestSource.TrySetResult(capturedRequest);
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }

            return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }

        private async Task WriteEventAsync(string eventName, string data)
        {
            var normalizedData = data.Replace("\r\n", "\n", StringComparison.Ordinal);
            var builder = new StringBuilder();
            builder.Append("event: ").Append(eventName).Append('\n');

            foreach (var line in normalizedData.Split('\n'))
            {
                builder.Append("data: ").Append(line).Append('\n');
            }

            builder.Append('\n');
            var bytes = Encoding.UTF8.GetBytes(builder.ToString());

            await _writeLock.WaitAsync();
            try
            {
                await _sseOutput.WriteAsync(bytes);
                await _sseOutput.FlushAsync();
            }
            finally
            {
                _writeLock.Release();
            }
        }
    }

    private sealed record CapturedHttpRequest(
        HttpMethod Method,
        string Url,
        string? Body,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        IReadOnlyDictionary<string, IReadOnlyList<string>> Headers)
    {
        public static CapturedHttpRequest From(HttpRequestMessage request, string? body)
        {
            var headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in request.Headers)
            {
                headers[header.Key] = header.Value.ToArray();
            }

            if (request.Content is not null)
            {
                foreach (var header in request.Content.Headers)
                {
                    headers[header.Key] = header.Value.ToArray();
                }
            }

            return new CapturedHttpRequest(
                Method: request.Method,
                Url: request.RequestUri?.ToString() ?? string.Empty,
                Body: body,
                AuthorizationScheme: request.Headers.Authorization?.Scheme,
                AuthorizationParameter: request.Headers.Authorization?.Parameter,
                Headers: headers);
        }
    }
}
