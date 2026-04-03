using ASXRunTerminal.Infra;
using Microsoft.Extensions.AI;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ASXRunTerminal.Tests;

public sealed class OllamaHttpClientTests
{
    [Fact]
    public void ChatClient_UsesMicrosoftExtensionsAiAbstraction()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            BuildJsonResponse(HttpStatusCode.OK, """{"version":"0.6.5"}"""));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient);

        Assert.IsAssignableFrom<IChatClient>(client.ChatClient);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsResponse_AndCallsGenerationEndpoint()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            BuildJsonResponse(HttpStatusCode.OK, """{"model":"llama3.2","response":" resposta gerada ","done":true}"""));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient);

        var response = await client.GenerateAsync("  explique singleton  ");

        Assert.Equal("resposta gerada", response);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("http://127.0.0.1:11434/api/generate", handler.LastRequest.RequestUri!.ToString());

        using var payload = ReadRequestPayload(handler.LastRequestBody);
        Assert.Equal("qwen3.5:4b", payload.RootElement.GetProperty("model").GetString());
        Assert.Equal("explique singleton", payload.RootElement.GetProperty("prompt").GetString());
        Assert.True(payload.RootElement.GetProperty("stream").GetBoolean());
    }

    [Fact]
    public async Task GenerateAsync_UsesConfiguredDefaultModel_WhenProvided()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            BuildJsonResponse(HttpStatusCode.OK, """{"model":"qwen2.5-coder:7b","response":"ok","done":true}"""));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(
            httpClient,
            defaultModel: "qwen2.5-coder:7b",
            environmentVariableReader: _ => "phi4-mini");

        var response = await client.GenerateAsync("gerar classe");

        Assert.Equal("ok", response);
        Assert.NotNull(handler.LastRequest);
        using var payload = ReadRequestPayload(handler.LastRequestBody);
        Assert.Equal("qwen2.5-coder:7b", payload.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task GenerateAsync_UsesEnvironmentDefaultModel_WhenNoExplicitDefaultModelIsProvided()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            BuildJsonResponse(HttpStatusCode.OK, """{"model":"phi4-mini","response":"ok","done":true}"""));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient, environmentVariableReader: _ => "phi4-mini");

        var response = await client.GenerateAsync("gerar classe");

        Assert.Equal("ok", response);
        Assert.NotNull(handler.LastRequest);
        using var payload = ReadRequestPayload(handler.LastRequestBody);
        Assert.Equal("phi4-mini", payload.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task GenerateAsync_UsesFallbackDefaultModel_WhenEnvironmentDefaultModelIsBlank()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            BuildJsonResponse(HttpStatusCode.OK, """{"model":"qwen3.5:4b","response":"ok","done":true}"""));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient, environmentVariableReader: _ => "   ");

        var response = await client.GenerateAsync("gerar classe");

        Assert.Equal("ok", response);
        Assert.NotNull(handler.LastRequest);
        using var payload = ReadRequestPayload(handler.LastRequestBody);
        Assert.Equal("qwen3.5:4b", payload.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task GenerateStreamAsync_StreamsResponseChunks_AndCallsGenerationEndpoint()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            BuildJsonResponse(
                HttpStatusCode.OK,
                """
                {"model":"llama3.2","response":"resposta ","done":false}
                {"model":"llama3.2","response":"em ","done":false}
                {"model":"llama3.2","response":"stream","done":false}
                {"model":"llama3.2","response":"","done":true}
                """));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient);
        var chunks = new List<string>();

        await foreach (var chunk in client.GenerateStreamAsync("descreva streaming"))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(["resposta ", "em ", "stream"], chunks);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("http://127.0.0.1:11434/api/generate", handler.LastRequest.RequestUri!.ToString());

        using var payload = ReadRequestPayload(handler.LastRequestBody);
        Assert.Equal("qwen3.5:4b", payload.RootElement.GetProperty("model").GetString());
        Assert.Equal("descreva streaming", payload.RootElement.GetProperty("prompt").GetString());
        Assert.True(payload.RootElement.GetProperty("stream").GetBoolean());
    }

    [Fact]
    public async Task GenerateStreamAsync_ReturnsPartialChunks_WhenParsingFailsAfterContent()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            BuildJsonResponse(
                HttpStatusCode.OK,
                """
                {"model":"llama3.2","response":"resposta ","done":false}
                {"model":"llama3.2","response":"parcial","done":false}
                {"model":"llama3.2","response":"incompleta","done":false
                """));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient);
        var chunks = new List<string>();

        await foreach (var chunk in client.GenerateStreamAsync("retorne parcial"))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(["resposta ", "parcial"], chunks);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsPartialResponse_WhenParsingFailsAfterContent()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            BuildJsonResponse(
                HttpStatusCode.OK,
                """
                {"model":"llama3.2","response":"resposta ","done":false}
                {"model":"llama3.2","response":"parcial","done":false}
                {"model":"llama3.2","response":"incompleta","done":false
                """));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient);

        var response = await client.GenerateAsync("retorne parcial");

        Assert.Equal("resposta parcial", response);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsInvalidOperationException_WhenParsingFailsBeforeAnyContent()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            BuildJsonResponse(
                HttpStatusCode.OK,
                """
                {"model":"llama3.2","response":"incompleta","done":false
                """));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GenerateAsync("retorne parcial"));
    }

    [Fact]
    public async Task GenerateAsync_ThrowsArgumentException_WhenPromptIsEmpty()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            BuildJsonResponse(HttpStatusCode.OK, """{"model":"llama3.2","response":"ok","done":true}"""));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => client.GenerateAsync("   "));

        Assert.Equal("prompt", exception.ParamName);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsInvalidOperationException_WhenResponseIsEmpty()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            BuildJsonResponse(HttpStatusCode.OK, """{"model":"llama3.2","response":"   ","done":true}"""));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GenerateAsync("gere uma resposta"));

        Assert.Equal(
            "O Ollama retornou uma resposta vazia para o prompt informado.",
            exception.Message);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsInvalidOperationException_WhenHttpRequestFails()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            throw new HttpRequestException(
                "Servico indisponivel",
                inner: null,
                statusCode: HttpStatusCode.ServiceUnavailable));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GenerateAsync("gere uma resposta"));

        Assert.Contains("HTTP 503", exception.Message);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsTimeoutException_WhenRequestTimesOut()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            throw new TaskCanceledException("tempo limite excedido"));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient);

        var exception = await Assert.ThrowsAsync<TimeoutException>(
            () => client.GenerateAsync("gere uma resposta"));

        Assert.Contains("tempo limite", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenVersionEndpointReturnsSuccess()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            BuildJsonResponse(HttpStatusCode.OK, """{"version":"0.6.5"}"""));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient);

        var result = await client.CheckHealthAsync();

        Assert.True(result.IsHealthy);
        Assert.Equal("0.6.5", result.Version);
        Assert.Null(result.Error);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("http://127.0.0.1:11434/api/version", handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenStatusCodeIsNotSuccess()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            BuildJsonResponse(HttpStatusCode.ServiceUnavailable, """{"error":"temporariamente indisponivel"}"""));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient, retryDelay: TimeSpan.Zero);

        var result = await client.CheckHealthAsync();

        Assert.False(result.IsHealthy);
        Assert.Null(result.Version);
        Assert.Contains("HTTP 503", result.Error);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenVersionPayloadIsMissing()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            BuildJsonResponse(HttpStatusCode.OK, """{"build":"2026.03.26"}"""));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient);

        var result = await client.CheckHealthAsync();

        Assert.False(result.IsHealthy);
        Assert.Null(result.Version);
        Assert.Equal("O payload de versao retornado pelo Ollama e invalido.", result.Error);
    }

    [Fact]
    public async Task CheckHealthAsync_UsesDefaultBaseAddress_WhenHttpClientBaseAddressIsNotConfigured()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            BuildJsonResponse(HttpStatusCode.OK, """{"version":"0.6.5"}"""));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient);

        _ = await client.CheckHealthAsync();

        Assert.Equal(new Uri("http://127.0.0.1:11434/"), client.BaseAddress);
    }

    [Fact]
    public async Task CheckHealthAsync_UsesProvidedBaseAddress_WhenHttpClientBaseAddressIsNotConfigured()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            BuildJsonResponse(HttpStatusCode.OK, """{"version":"0.6.5"}"""));
        using var httpClient = new HttpClient(handler);
        var baseAddress = new Uri("http://localhost:12456/");
        var client = new OllamaHttpClient(httpClient, baseAddress: baseAddress);

        var result = await client.CheckHealthAsync();

        Assert.True(result.IsHealthy);
        Assert.Equal(baseAddress, client.BaseAddress);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("http://localhost:12456/api/version", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task CheckHealthAsync_ThrowsOperationCanceledException_WhenCancellationIsRequestedByCaller()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            BuildJsonResponse(HttpStatusCode.OK, """{"version":"0.6.5"}"""));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.CheckHealthAsync(cancellationTokenSource.Token));
    }

    [Fact]
    public async Task CheckHealthAsync_RetriesOnce_WhenTransientHttpRequestExceptionOccurs()
    {
        var attempt = 0;
        using var handler = new StubHttpMessageHandler(_ =>
        {
            attempt++;

            if (attempt == 1)
            {
                throw new HttpRequestException("Conexao recusada");
            }

            return BuildJsonResponse(HttpStatusCode.OK, """{"version":"0.6.5"}""");
        });
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient, retryDelay: TimeSpan.Zero);

        var result = await client.CheckHealthAsync();

        Assert.True(result.IsHealthy);
        Assert.Equal("0.6.5", result.Version);
        Assert.Null(result.Error);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckHealthAsync_RetriesOnce_WhenServiceIsTemporarilyUnavailable()
    {
        var attempt = 0;
        using var handler = new StubHttpMessageHandler(_ =>
        {
            attempt++;

            if (attempt == 1)
            {
                return BuildJsonResponse(HttpStatusCode.ServiceUnavailable, """{"error":"temporariamente indisponivel"}""");
            }

            return BuildJsonResponse(HttpStatusCode.OK, """{"version":"0.6.5"}""");
        });
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient, retryDelay: TimeSpan.Zero);

        var result = await client.CheckHealthAsync();

        Assert.True(result.IsHealthy);
        Assert.Equal("0.6.5", result.Version);
        Assert.Null(result.Error);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenTimeoutIsExceededAfterRetry()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            throw new TaskCanceledException("Tempo limite excedido"));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient, retryDelay: TimeSpan.Zero);

        var result = await client.CheckHealthAsync();

        Assert.False(result.IsHealthy);
        Assert.Null(result.Version);
        Assert.Contains("tempo limite", result.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenHttpRequestFails()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            throw new HttpRequestException("Conexao recusada"));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient, retryDelay: TimeSpan.Zero);

        var result = await client.CheckHealthAsync();

        Assert.False(result.IsHealthy);
        Assert.Null(result.Version);
        Assert.Contains("Nao foi possivel conectar ao Ollama", result.Error);
        Assert.Contains("Conexao recusada", result.Error);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task ListLocalModelsAsync_ReturnsModels_AndCallsTagsEndpoint()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            BuildJsonResponse(
                HttpStatusCode.OK,
                """
                {
                  "models": [
                    {
                      "name": "llama3.2:latest",
                      "model": "llama3.2:latest",
                      "size": 2147483648
                    },
                    {
                      "name": "qwen2.5-coder:7b",
                      "model": "qwen2.5-coder:7b",
                      "size": 3221225472
                    }
                  ]
                }
                """));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient);

        var models = await client.ListLocalModelsAsync();

        Assert.Equal(2, models.Count);
        Assert.Equal("llama3.2:latest", models[0].Name);
        Assert.Equal("qwen2.5-coder:7b", models[1].Name);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("http://127.0.0.1:11434/api/tags", handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task ListLocalModelsAsync_ReturnsEmptyList_WhenNoModelIsAvailable()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            BuildJsonResponse(HttpStatusCode.OK, """{"models": []}"""));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient);

        var models = await client.ListLocalModelsAsync();

        Assert.Empty(models);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ListLocalModelsAsync_ThrowsTimeoutException_WhenRequestTimesOut()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            throw new TaskCanceledException("tempo limite excedido"));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient);

        var exception = await Assert.ThrowsAsync<TimeoutException>(
            () => client.ListLocalModelsAsync());

        Assert.Contains("tempo limite", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListLocalModelsAsync_ThrowsInvalidOperationException_WhenHttpRequestFails()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            throw new HttpRequestException(
                "Falha de conexao",
                inner: null,
                statusCode: HttpStatusCode.BadGateway));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ListLocalModelsAsync());

        Assert.Contains("HTTP 502", exception.Message);
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task ListLocalModelsAsync_ThrowsInvalidOperationException_WhenPayloadIsInvalid()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            BuildJsonResponse(HttpStatusCode.OK, """{"models":[{"name":"   ","model":"   "}]}"""));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaHttpClient(httpClient);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ListLocalModelsAsync());

        Assert.Equal("O payload de modelos retornado pelo Ollama e invalido.", exception.Message);
    }

    private static HttpResponseMessage BuildJsonResponse(HttpStatusCode statusCode, string body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private static JsonDocument ReadRequestPayload(string? requestBody)
    {
        Assert.False(string.IsNullOrWhiteSpace(requestBody));
        return JsonDocument.Parse(requestBody);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory = responseFactory;

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            RequestCount++;
            return Task.FromResult(_responseFactory(request));
        }
    }
}
