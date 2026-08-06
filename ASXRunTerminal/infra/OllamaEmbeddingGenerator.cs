using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ASXRunTerminal.Core;
using Microsoft.Extensions.AI;

namespace ASXRunTerminal.Infra;

/// <summary>
/// Adaptador de <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> (Microsoft.Extensions.AI) sobre
/// <see cref="OllamaHttpClient"/>, expondo o Ollama local atraves do contrato
/// oficial recomendado pelo Microsoft Agent Framework.
///
/// Usa o endpoint <c>/api/embeddings</c> do Ollama para gerar embeddings de texto,
/// com suporte ao modelo nomic-embed-text por padrao.
/// </summary>
internal sealed class OllamaEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private static readonly EmbeddingGeneratorMetadata DefaultMetadata = new("ollama-embeddings");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly string? _defaultModel;
    private readonly Uri _baseAddress;

    public OllamaEmbeddingGenerator(
        HttpClient httpClient,
        string? defaultModel = null,
        Uri? baseAddress = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
        _defaultModel = defaultModel ?? "nomic-embed-text";
        _baseAddress = baseAddress ?? OllamaModelDefaults.DefaultEndpoint;
    }

    public EmbeddingGeneratorMetadata Metadata => DefaultMetadata;

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        var valueList = values.ToList();
        if (valueList.Count == 0)
        {
            return new GeneratedEmbeddings<Embedding<float>>();
        }

        var model = options?.ModelId ?? _defaultModel;
        var embeddings = new List<Embedding<float>>(valueList.Count);

        foreach (var value in valueList)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var embedding = await GenerateSingleEmbeddingAsync(value, model, cancellationToken)
                .ConfigureAwait(false);
            embeddings.Add(embedding);
        }

        return new GeneratedEmbeddings<Embedding<float>>(embeddings);
    }

    public async IAsyncEnumerable<Embedding<float>> GenerateStreamingAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        var model = options?.ModelId ?? _defaultModel;

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var embedding = await GenerateSingleEmbeddingAsync(value, model, cancellationToken)
                .ConfigureAwait(false);
            yield return embedding;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
        // O ciclo de vida do HttpClient e gerenciado pelo DI host.
    }

    private async Task<Embedding<float>> GenerateSingleEmbeddingAsync(
        string text,
        string model,
        CancellationToken cancellationToken)
    {
        var payload = new EmbeddingRequest(model, text);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseAddress, "api/embeddings"))
            {
                Content = JsonContent.Create(payload, options: JsonOptions),
            };

            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"O Ollama retornou status HTTP {(int)response.StatusCode} ao gerar embedding.");
            }

            var result = await response.Content
                .ReadFromJsonAsync<EmbeddingResponse>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (result?.Embedding is null || result.Embedding.Count == 0)
            {
                throw new InvalidOperationException(
                    "O Ollama retornou um embedding vazio para o texto informado.");
            }

            return new Embedding<float>(result.Embedding.ToArray());
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                "O Ollama nao respondeu dentro do tempo limite configurado ao gerar embedding.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            var statusCode = (int)(exception.StatusCode ?? HttpStatusCode.InternalServerError);
            throw new InvalidOperationException(
                $"O Ollama retornou status HTTP {statusCode} ao gerar embedding.",
                exception);
        }
    }

    private sealed record EmbeddingRequest(string Model, string Input);

    private sealed record EmbeddingResponse(IReadOnlyList<float> Embedding);
}
