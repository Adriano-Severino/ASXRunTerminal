using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using ASXRunTerminal.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ASXRunTerminal.Infra;

/// <summary>
/// Cliente HTTP de baixo nivel para a API local do Ollama. Encapsula
/// <c>POST /api/generate</c>, <c>GET /api/version</c> e <c>GET /api/tags</c>,
/// expondo tambem um adapter <see cref="IChatClient"/> para integracao com o
/// Microsoft Agent Framework.
/// </summary>
internal sealed class OllamaHttpClient : IOllamaHttpClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly string? _defaultModel;
    private readonly Func<string, string?>? _environmentVariableReader;
    private readonly TimeSpan _retryDelay;
    private readonly Lazy<IChatClient> _chatClient;
    private readonly ILogger<OllamaHttpClient> _logger;

    public OllamaHttpClient(
        HttpClient httpClient,
        string? defaultModel = null,
        Func<string, string?>? environmentVariableReader = null,
        Uri? baseAddress = null,
        TimeSpan? retryDelay = null,
        ILogger<OllamaHttpClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
        _defaultModel = defaultModel;
        _environmentVariableReader = environmentVariableReader;
        _retryDelay = retryDelay ?? TimeSpan.FromMilliseconds(200);
        BaseAddress = baseAddress ?? OllamaModelDefaults.DefaultEndpoint;
        _chatClient = new Lazy<IChatClient>(CreateChatClientAdapter);
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<OllamaHttpClient>.Instance;
    }

    public Uri BaseAddress { get; }

    public IChatClient ChatClient => _chatClient.Value;

    public Task<OllamaHealthcheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        return CheckHealthWithRetryAsync(cancellationToken);
    }

    public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        return GenerateAsync(prompt, model: null, cancellationToken);
    }

    public async Task<string> GenerateAsync(string prompt, string? model, CancellationToken cancellationToken = default)
    {
        ValidatePrompt(prompt);

        var resolvedModel = ResolveModel(model);
        _logger.LogDebug("Starting Ollama generate request with model: {Model}", resolvedModel);

        var payload = new GenerateRequest(resolvedModel, prompt, Stream: false);
        var aggregatedContent = new System.Text.StringBuilder();
        var anyChunkParsed = false;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseAddress, "api/generate"))
            {
                Content = JsonContent.Create(payload, options: JsonOptions),
            };

            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Ollama returned HTTP status {StatusCode} for generate request", (int)response.StatusCode);
                throw new InvalidOperationException(
                    $"O Ollama retornou status HTTP {(int)response.StatusCode} ao gerar a resposta.");
            }

            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            await foreach (var chunk in JsonSerializer
                .DeserializeAsyncEnumerable<GenerateChunk>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false))
            {
                if (chunk is null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(chunk.Response))
                {
                    aggregatedContent.Append(chunk.Response);
                    anyChunkParsed = true;
                }
            }
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                "O Ollama nao respondeu dentro do tempo limite configurado.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            var statusCode = (int)(exception.StatusCode ?? HttpStatusCode.InternalServerError);
            throw new InvalidOperationException(
                $"O Ollama retornou status HTTP {statusCode} ao gerar a resposta.",
                exception);
        }

        if (!anyChunkParsed)
        {
            throw new InvalidOperationException(
                "O Ollama retornou uma resposta vazia para o prompt informado.");
        }

        return aggregatedContent.ToString().Trim();
    }

    public async IAsyncEnumerable<string> GenerateStreamAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ValidatePrompt(prompt);

        var resolvedModel = ResolveModel();
        var payload = new GenerateRequest(resolvedModel, prompt, Stream: true);

        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseAddress, "api/generate"))
            {
                Content = JsonContent.Create(payload, options: JsonOptions),
            };

            response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                "O Ollama nao respondeu dentro do tempo limite configurado.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            var statusCode = (int)(exception.StatusCode ?? HttpStatusCode.InternalServerError);
            throw new InvalidOperationException(
                $"O Ollama retornou status HTTP {statusCode} ao iniciar o streaming.",
                exception);
        }

        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        await foreach (var chunk in JsonSerializer
            .DeserializeAsyncEnumerable<GenerateChunk>(stream, JsonOptions, cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            if (chunk is null || string.IsNullOrEmpty(chunk.Response))
            {
                continue;
            }

            yield return chunk.Response;
        }

        response.Dispose();
    }

    public async Task<IReadOnlyList<OllamaLocalModel>> ListLocalModelsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseAddress, "api/tags"));
            using var response = await _httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"O Ollama retornou status HTTP {(int)response.StatusCode} ao listar os modelos locais.");
            }

            var payload = await response.Content
                .ReadFromJsonAsync<TagsResponse>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (payload?.Models is null)
            {
                throw new InvalidOperationException(
                    "O payload de modelos retornado pelo Ollama e invalido.");
            }

            var result = new List<OllamaLocalModel>(payload.Models.Count);
            foreach (var entry in payload.Models)
            {
                var resolvedName = string.IsNullOrWhiteSpace(entry.Name)
                    ? entry.Model
                    : entry.Name;

                if (string.IsNullOrWhiteSpace(resolvedName))
                {
                    throw new InvalidOperationException(
                        "O payload de modelos retornado pelo Ollama e invalido.");
                }

                result.Add(new OllamaLocalModel(resolvedName.Trim()));
            }

            return result;
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                "O Ollama nao respondeu dentro do tempo limite configurado.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            var statusCode = (int)(exception.StatusCode ?? HttpStatusCode.InternalServerError);
            throw new InvalidOperationException(
                $"O Ollama retornou status HTTP {statusCode} ao listar os modelos locais.",
                exception);
        }
    }

    private IChatClient CreateChatClientAdapter() => new OllamaChatClient(this);

    private string ResolveModel(string? overrideModel = null)
        => OllamaModelDefaults.Resolve(overrideModel ?? _defaultModel, _environmentVariableReader);

    private static void ValidatePrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException(
                "O prompt informado para o Ollama esta vazio.",
                paramName: nameof(prompt));
        }
    }

    private async Task<OllamaHealthcheckResult> CheckHealthWithRetryAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 2;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseAddress, "api/version"));
                using var response = await _httpClient
                    .SendAsync(request, cancellationToken)
                    .ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var payload = await response.Content
                        .ReadFromJsonAsync<VersionResponse>(JsonOptions, cancellationToken)
                        .ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(payload?.Version))
                    {
                        return OllamaHealthcheckResult.Unhealthy(
                            "O payload de versao retornado pelo Ollama e invalido.");
                    }

                    return OllamaHealthcheckResult.Healthy(payload.Version.Trim());
                }

                if (!IsTransient(response.StatusCode) || attempt == maxAttempts)
                {
                    return OllamaHealthcheckResult.Unhealthy(
                        $"O Ollama retornou status HTTP {(int)response.StatusCode} ao consultar a versao.");
                }
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                return OllamaHealthcheckResult.Unhealthy(
                    "O Ollama excedeu o tempo limite ao consultar a versao.");
            }
            catch (HttpRequestException exception)
            {
                if (attempt == maxAttempts)
                {
                    return OllamaHealthcheckResult.Unhealthy(
                        $"Nao foi possivel conectar ao Ollama: {exception.Message}");
                }
            }

            if (_retryDelay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            }
        }

        return OllamaHealthcheckResult.Unhealthy("O Ollama nao respondeu a verificacao de saude.");
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout ||
        (int)statusCode >= 500;

    private sealed record GenerateRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record GenerateChunk(
        [property: JsonPropertyName("response")] string? Response,
        [property: JsonPropertyName("done")] bool Done);

    private sealed record TagsResponse(
        [property: JsonPropertyName("models")] List<TagEntry>? Models);

    private sealed record TagEntry(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("model")] string? Model);

    private sealed record VersionResponse(
        [property: JsonPropertyName("version")] string? Version);
}
