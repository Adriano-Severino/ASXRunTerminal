using ASXRunTerminal.Core;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace ASXRunTerminal.Infra;

internal sealed class OllamaHttpClient : IOllamaHttpClient
{
    private const int MaxAttempts = 2;
    private static readonly Uri DefaultBaseAddress = new("http://127.0.0.1:11434/", UriKind.Absolute);
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMilliseconds(200);

    private readonly IOllamaApiClient _ollamaApiClient;
    private readonly TimeSpan _retryDelay;

    public OllamaHttpClient(
        HttpClient httpClient,
        Uri? baseAddress = null,
        string? defaultModel = null,
        TimeSpan? retryDelay = null,
        Func<string, string?>? environmentVariableReader = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        httpClient.BaseAddress ??= baseAddress ?? DefaultBaseAddress;

        _ollamaApiClient = new OllamaApiClient(
            client: httpClient,
            defaultModel: OllamaModelDefaults.Resolve(defaultModel, environmentVariableReader),
            jsonSerializerContext: null);

        _retryDelay = retryDelay ?? DefaultRetryDelay;
        ArgumentOutOfRangeException.ThrowIfLessThan(_retryDelay, TimeSpan.Zero);
    }

    public Uri BaseAddress => _ollamaApiClient.Uri;
    public IChatClient ChatClient => (IChatClient)_ollamaApiClient;

    public async IAsyncEnumerable<string> GenerateStreamAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("O prompt informado nao pode estar vazio.", nameof(prompt));
        }

        var request = new GenerateRequest
        {
            Model = _ollamaApiClient.SelectedModel,
            Prompt = prompt.Trim(),
            Stream = true
        };

        var hasContent = false;
        await using var enumerator = CreateGenerateResponseEnumerator(request, cancellationToken);
        while (true)
        {
            GenerateResponseStream? current;

            try
            {
                if (!await enumerator.MoveNextAsync())
                {
                    break;
                }

                current = enumerator.Current;
            }
            catch (Exception ex)
            {
                if (hasContent && IsRecoverablePartialGenerateError(ex))
                {
                    // Preserve already streamed text when the remaining payload is malformed/truncated.
                    break;
                }

                throw MapGenerateException(ex, cancellationToken);
            }

            if (current is null)
            {
                if (hasContent)
                {
                    break;
                }

                throw new InvalidOperationException(
                    "O payload de geracao retornado pelo Ollama e invalido.");
            }

            if (string.IsNullOrEmpty(current.Response))
            {
                continue;
            }

            hasContent = true;
            yield return current.Response;
        }

        if (!hasContent)
        {
            throw new InvalidOperationException(
                "O Ollama retornou uma resposta vazia para o prompt informado.");
        }
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var responseBuilder = new StringBuilder();

        await foreach (var chunk in GenerateStreamAsync(prompt, cancellationToken))
        {
            responseBuilder.Append(chunk);
        }

        return ValidateGeneratedResponse(responseBuilder.ToString());
    }

    public async Task<OllamaHealthcheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var version = await _ollamaApiClient.GetVersionAsync(cancellationToken);
                OllamaHealthcheckResult result = version;
                return result;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (CanRetry(attempt))
                {
                    await WaitBeforeRetryAsync(cancellationToken);
                    continue;
                }

                return OllamaHealthcheckResult.Unhealthy(
                    $"O tempo limite para conectar ao Ollama em '{BaseAddress}' foi excedido apos {MaxAttempts} tentativas.");
            }
            catch (HttpRequestException ex) when (ShouldRetry(ex.StatusCode, attempt))
            {
                await WaitBeforeRetryAsync(cancellationToken);
                continue;
            }
            catch (HttpRequestException ex)
            {
                return OllamaHealthcheckResult.Unhealthy(
                    BuildHttpRequestError(ex));
            }
            catch (ArgumentException)
            {
                return OllamaHealthcheckResult.Unhealthy(
                    "O payload de versao retornado pelo Ollama e invalido.");
            }
            catch (FormatException)
            {
                return OllamaHealthcheckResult.Unhealthy(
                    "O payload de versao retornado pelo Ollama e invalido.");
            }
            catch (NotSupportedException ex)
            {
                return OllamaHealthcheckResult.Unhealthy(
                    $"O formato da resposta do Ollama nao e suportado. {ex.Message}");
            }
            catch (JsonException ex) when (IsVersionPayloadError(ex))
            {
                return OllamaHealthcheckResult.Unhealthy(
                    "O payload de versao retornado pelo Ollama e invalido.");
            }
            catch (JsonException ex)
            {
                return OllamaHealthcheckResult.Unhealthy(
                    $"Nao foi possivel interpretar a resposta do Ollama. {ex.Message}");
            }
        }

        return OllamaHealthcheckResult.Unhealthy(
            "Nao foi possivel validar a disponibilidade do Ollama.");
    }

    public async Task<IReadOnlyList<OllamaLocalModel>> ListLocalModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var models = await _ollamaApiClient.ListLocalModelsAsync(cancellationToken);
            if (models is null)
            {
                return [];
            }

            return [.. models.Select(static model => (OllamaLocalModel)model)];
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"O tempo limite para listar modelos locais no Ollama em '{BaseAddress}' foi excedido.");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(BuildHttpRequestError(ex), ex);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(
                "O payload de modelos retornado pelo Ollama e invalido.",
                ex);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "O payload de modelos retornado pelo Ollama e invalido.",
                ex);
        }
        catch (NotSupportedException ex)
        {
            throw new InvalidOperationException(
                $"O formato da resposta do Ollama nao e suportado. {ex.Message}",
                ex);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Nao foi possivel interpretar a resposta de modelos do Ollama. {ex.Message}",
                ex);
        }
    }

    private IAsyncEnumerator<GenerateResponseStream?> CreateGenerateResponseEnumerator(
        GenerateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return _ollamaApiClient.GenerateAsync(request, cancellationToken).GetAsyncEnumerator(cancellationToken);
        }
        catch (Exception ex)
        {
            throw MapGenerateException(ex, cancellationToken);
        }
    }

    private Exception MapGenerateException(Exception exception, CancellationToken cancellationToken)
    {
        return exception switch
        {
            InvalidOperationException => exception,
            TimeoutException => exception,
            OperationCanceledException when !cancellationToken.IsCancellationRequested
                => new TimeoutException(
                    $"O tempo limite para gerar resposta no Ollama em '{BaseAddress}' foi excedido."),
            HttpRequestException httpException
                => new InvalidOperationException(BuildHttpRequestError(httpException), httpException),
            ArgumentException argumentException when argumentException.ParamName is "prompt"
                => argumentException,
            ArgumentException argumentException
                => new InvalidOperationException(
                    "O payload de geracao retornado pelo Ollama e invalido.",
                    argumentException),
            FormatException formatException
                => new InvalidOperationException(
                    "O payload de geracao retornado pelo Ollama e invalido.",
                    formatException),
            NotSupportedException notSupportedException
                => new InvalidOperationException(
                    $"O formato da resposta do Ollama nao e suportado. {notSupportedException.Message}",
                    notSupportedException),
            JsonException jsonException when IsGeneratePayloadError(jsonException)
                => new InvalidOperationException(
                    "O payload de geracao retornado pelo Ollama e invalido.",
                    jsonException),
            JsonException jsonException
                => new InvalidOperationException(
                    $"Nao foi possivel interpretar a resposta de geracao do Ollama. {jsonException.Message}",
                    jsonException),
            _ => exception
        };
    }

    private static bool ShouldRetry(HttpStatusCode? statusCode, int attempt)
    {
        if (!CanRetry(attempt))
        {
            return false;
        }

        if (statusCode is null)
        {
            return true;
        }

        return statusCode == HttpStatusCode.RequestTimeout
            || statusCode == HttpStatusCode.TooManyRequests
            || (int)statusCode >= 500;
    }

    private static bool CanRetry(int attempt)
    {
        return attempt < MaxAttempts;
    }

    private async Task WaitBeforeRetryAsync(CancellationToken cancellationToken)
    {
        if (_retryDelay == TimeSpan.Zero)
        {
            return;
        }

        await Task.Delay(_retryDelay, cancellationToken);
    }

    private string BuildHttpRequestError(HttpRequestException exception)
    {
        if (exception.StatusCode is HttpStatusCode statusCode)
        {
            return $"O Ollama respondeu com HTTP {(int)statusCode} ({statusCode}).";
        }

        return $"Nao foi possivel conectar ao Ollama em '{BaseAddress}'. {exception.Message}";
    }

    private static bool IsVersionPayloadError(JsonException exception)
    {
        return exception.Path?.Contains("version", StringComparison.OrdinalIgnoreCase) == true
            || exception.Message.Contains("System.Version", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGeneratePayloadError(JsonException exception)
    {
        return exception.Path?.Contains("response", StringComparison.OrdinalIgnoreCase) == true
            || exception.Message.Contains("GenerateResponseStream", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRecoverablePartialGenerateError(Exception exception)
    {
        return exception switch
        {
            JsonException => true,
            FormatException => true,
            NotSupportedException => true,
            ArgumentException argumentException when argumentException.ParamName is not "prompt" => true,
            InvalidOperationException invalidOperationException
                when IsRecoverablePartialGenerateErrorFromInnerException(invalidOperationException.InnerException) => true,
            _ => false
        };
    }

    private static bool IsRecoverablePartialGenerateErrorFromInnerException(Exception? exception)
    {
        return exception switch
        {
            null => false,
            JsonException => true,
            FormatException => true,
            NotSupportedException => true,
            ArgumentException argumentException when argumentException.ParamName is not "prompt" => true,
            _ => false
        };
    }

    private static string ValidateGeneratedResponse(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new InvalidOperationException(
                "O Ollama retornou uma resposta vazia para o prompt informado.");
        }

        return response.Trim();
    }

}
