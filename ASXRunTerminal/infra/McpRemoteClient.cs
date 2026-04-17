using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Infra;

internal sealed class McpRemoteClient : IMcpClient
{
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly MediaTypeWithQualityHeaderValue SseAcceptHeader = new("text/event-stream");

    private readonly McpServerRemoteOptions _serverOptions;
    private readonly HttpClient _httpClient;
    private readonly bool _disposeHttpClient;
    private readonly TimeSpan _defaultRequestTimeout;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, PendingRequest> _pendingRequests =
        new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _disposeTokenSource = new();
    private readonly TaskCompletionSource<Uri> _messageEndpointSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private long _requestIdCounter;
    private Uri? _resolvedMessageEndpoint;
    private HttpResponseMessage? _sseConnectionResponse;
    private Task? _receiveLoopTask;
    private Exception? _transportFailure;
    private int _isDisposed;
    private bool _isConnected;

    public McpRemoteClient(
        McpServerRemoteOptions serverOptions,
        HttpClient? httpClient = null,
        TimeSpan? defaultRequestTimeout = null)
    {
        _serverOptions = serverOptions ?? throw new ArgumentNullException(nameof(serverOptions));
        _httpClient = httpClient ?? new HttpClient();
        _disposeHttpClient = httpClient is null;
        _defaultRequestTimeout = defaultRequestTimeout ?? DefaultRequestTimeout;

        if (_defaultRequestTimeout <= TimeSpan.Zero && _defaultRequestTimeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                nameof(defaultRequestTimeout),
                "O timeout padrao do cliente MCP remoto deve ser maior que zero.");
        }

        if (_serverOptions.TransportKind == McpRemoteTransportKind.Http)
        {
            _resolvedMessageEndpoint = _serverOptions.MessageEndpoint ?? _serverOptions.Endpoint;
            _messageEndpointSource.TrySetResult(_resolvedMessageEndpoint);
            return;
        }

        if (_serverOptions.MessageEndpoint is not null)
        {
            _resolvedMessageEndpoint = _serverOptions.MessageEndpoint;
            _messageEndpointSource.TrySetResult(_resolvedMessageEndpoint);
        }
    }

    public bool IsConnected =>
        _isConnected &&
        _transportFailure is null &&
        !_disposeTokenSource.IsCancellationRequested;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            ThrowIfTransportFailed();

            if (_isConnected)
            {
                return;
            }

            if (_serverOptions.TransportKind == McpRemoteTransportKind.Http)
            {
                _isConnected = true;
                return;
            }

            await ConnectSseAsync(cancellationToken);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<JsonElement> SendRequestAsync(
        string method,
        JsonElement? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ValidateMethod(method);
        await ConnectAsync(cancellationToken);
        ThrowIfTransportFailed();

        var resolvedTimeout = timeout ?? _defaultRequestTimeout;
        if (resolvedTimeout <= TimeSpan.Zero && resolvedTimeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "O timeout da requisicao MCP deve ser maior que zero.");
        }

        var requestId = Interlocked.Increment(ref _requestIdCounter)
            .ToString(CultureInfo.InvariantCulture);
        var completion = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingRequest = new PendingRequest(method, completion);

        if (!_pendingRequests.TryAdd(requestId, pendingRequest))
        {
            throw new InvalidOperationException(
                $"Nao foi possivel registrar a requisicao MCP '{requestId}'.");
        }

        var payload = BuildRequestPayload(requestId, method, parameters);

        try
        {
            await WritePayloadAsync(
                payload,
                method,
                expectsResponse: true,
                timeout: resolvedTimeout,
                cancellationToken);
            return await WaitForResponseAsync(
                requestId,
                pendingRequest,
                resolvedTimeout,
                cancellationToken);
        }
        catch
        {
            _pendingRequests.TryRemove(requestId, out _);
            throw;
        }
    }

    public async Task SendNotificationAsync(
        string method,
        JsonElement? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ValidateMethod(method);
        await ConnectAsync(cancellationToken);
        ThrowIfTransportFailed();

        var payload = BuildNotificationPayload(method, parameters);
        await WritePayloadAsync(
            payload,
            method,
            expectsResponse: false,
            timeout: _defaultRequestTimeout,
            cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        _disposeTokenSource.Cancel();
        _messageEndpointSource.TrySetCanceled(_disposeTokenSource.Token);
        var sseConnectionResponse = Interlocked.Exchange(ref _sseConnectionResponse, null);
        sseConnectionResponse?.Dispose();

        if (_receiveLoopTask is not null)
        {
            try
            {
                await _receiveLoopTask;
            }
            catch
            {
                // No-op: o motivo da falha ja e propagado nas requests pendentes.
            }
        }

        FailAllPendingRequests(
            new ObjectDisposedException(nameof(McpRemoteClient)));

        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }

        _disposeTokenSource.Dispose();
        _connectionLock.Dispose();
        _writeLock.Dispose();
    }

    private async Task ConnectSseAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _serverOptions.Endpoint);
        request.Headers.Accept.Add(SseAcceptHeader);
        ApplyHeaders(request);

        var response = await SendWithTimeoutAsync(
            request,
            "abrir conexao SSE MCP",
            _defaultRequestTimeout,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = response.StatusCode;
            response.Dispose();
            throw BuildHttpStatusException(
                "abrir conexao SSE MCP",
                statusCode);
        }

        _sseConnectionResponse = response;
        var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        _receiveLoopTask = Task.Run(
            () => ReceiveSseLoopAsync(responseStream, response, _disposeTokenSource.Token),
            CancellationToken.None);
        _isConnected = true;

        if (_resolvedMessageEndpoint is null)
        {
            _ = await WaitForMessageEndpointAsync(
                _defaultRequestTimeout,
                cancellationToken);
        }
    }

    private async Task ReceiveSseLoopAsync(
        Stream input,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            input,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024,
            leaveOpen: true);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var sseEvent = await ReadSseEventAsync(reader, cancellationToken);
                if (sseEvent is null)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    throw new InvalidOperationException(
                        "A conexao MCP remota via SSE foi encerrada pelo servidor.");
                }

                HandleSseEvent(sseEvent);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // No-op: encerramento esperado.
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _transportFailure = ex;
            _messageEndpointSource.TrySetException(ex);
            FailAllPendingRequests(ex);
        }
        finally
        {
            _isConnected = false;
            if (ReferenceEquals(_sseConnectionResponse, response))
            {
                _sseConnectionResponse = null;
            }

            response.Dispose();
        }
    }

    private void HandleSseEvent(SseEvent sseEvent)
    {
        if (string.Equals(
                sseEvent.EventName,
                "endpoint",
                StringComparison.OrdinalIgnoreCase))
        {
            SetMessageEndpointFromSseEvent(sseEvent.Data);
            return;
        }

        if (string.IsNullOrWhiteSpace(sseEvent.Data))
        {
            return;
        }

        JsonDocument payload;
        try
        {
            payload = JsonDocument.Parse(sseEvent.Data);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "O servidor MCP remoto enviou um evento SSE invalido.",
                ex);
        }

        using (payload)
        {
            HandleIncomingPayload(payload.RootElement);
        }
    }

    private void SetMessageEndpointFromSseEvent(string rawData)
    {
        var endpointValue = ExtractEndpointValue(rawData);
        if (string.IsNullOrWhiteSpace(endpointValue))
        {
            throw new InvalidOperationException(
                "O evento 'endpoint' do servidor MCP remoto nao informou uma URL valida.");
        }

        var endpoint = ResolveEndpointUri(endpointValue);
        _resolvedMessageEndpoint = endpoint;
        _messageEndpointSource.TrySetResult(endpoint);
    }

    private Uri ResolveEndpointUri(string endpointValue)
    {
        if (Uri.TryCreate(endpointValue, UriKind.Absolute, out var absoluteEndpoint))
        {
            return EnsureHttpEndpoint(absoluteEndpoint, "endpoint do evento SSE");
        }

        var relativeEndpoint = new Uri(_serverOptions.Endpoint, endpointValue);
        return EnsureHttpEndpoint(relativeEndpoint, "endpoint do evento SSE");
    }

    private async Task WritePayloadAsync(
        ReadOnlyMemory<byte> payload,
        string method,
        bool expectsResponse,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var endpoint = await ResolveMessageEndpointAsync(
                timeout,
                cancellationToken);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Content = new ByteArrayContent(payload.ToArray());
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            ApplyHeaders(request);

            using var response = await SendWithTimeoutAsync(
                request,
                $"enviar '{method}' para servidor MCP remoto",
                timeout,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw BuildHttpStatusException(
                    $"executar '{method}' no servidor MCP remoto",
                    response.StatusCode);
            }

            await ProcessResponsePayloadAsync(
                response,
                method,
                expectsResponse,
                cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ProcessResponsePayloadAsync(
        HttpResponseMessage response,
        string method,
        bool expectsResponse,
        CancellationToken cancellationToken)
    {
        if (response.Content is null)
        {
            if (expectsResponse && _serverOptions.TransportKind == McpRemoteTransportKind.Http)
            {
                throw new InvalidOperationException(
                    $"O servidor MCP remoto nao retornou payload para o metodo '{method}'.");
            }

            return;
        }

        var payloadText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(payloadText))
        {
            if (expectsResponse && _serverOptions.TransportKind == McpRemoteTransportKind.Http)
            {
                throw new InvalidOperationException(
                    $"O servidor MCP remoto nao retornou payload JSON-RPC para o metodo '{method}'.");
            }

            return;
        }

        JsonDocument payloadDocument;
        try
        {
            payloadDocument = JsonDocument.Parse(payloadText);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"O servidor MCP remoto retornou payload JSON invalido para o metodo '{method}'.",
                ex);
        }

        using (payloadDocument)
        {
            HandleIncomingPayload(payloadDocument.RootElement);
        }
    }

    private void HandleIncomingPayload(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Object)
        {
            HandleIncomingMessage(payload);
            return;
        }

        if (payload.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var message in payload.EnumerateArray())
        {
            if (message.ValueKind == JsonValueKind.Object)
            {
                HandleIncomingMessage(message);
            }
        }
    }

    private void HandleIncomingMessage(JsonElement root)
    {
        if (!TryGetRequestId(root, out var requestId))
        {
            return;
        }

        if (!_pendingRequests.TryRemove(requestId, out var pendingRequest))
        {
            return;
        }

        if (root.TryGetProperty("error", out var errorElement))
        {
            var exception = CreateRequestException(
                pendingRequest.Method,
                errorElement);
            pendingRequest.Completion.TrySetException(exception);
            return;
        }

        if (!root.TryGetProperty("result", out var resultElement))
        {
            pendingRequest.Completion.TrySetException(
                new InvalidOperationException(
                    $"Resposta MCP invalida para o metodo '{pendingRequest.Method}'."));
            return;
        }

        pendingRequest.Completion.TrySetResult(resultElement.Clone());
    }

    private async Task<HttpResponseMessage> SendWithTimeoutAsync(
        HttpRequestMessage request,
        string operationDescription,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource? timeoutTokenSource = null;
        CancellationTokenSource? sendTokenSource = null;

        try
        {
            if (timeout == Timeout.InfiniteTimeSpan)
            {
                sendTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _disposeTokenSource.Token);
            }
            else
            {
                timeoutTokenSource = new CancellationTokenSource(timeout);
                sendTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutTokenSource.Token,
                    _disposeTokenSource.Token);
            }

            return await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                sendTokenSource.Token);
        }
        catch (OperationCanceledException) when (
            timeoutTokenSource is not null &&
            timeoutTokenSource.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"A operacao MCP remota '{operationDescription}' excedeu o timeout de {timeout.TotalMilliseconds:0} ms.");
        }
        catch (OperationCanceledException) when (_disposeTokenSource.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(McpRemoteClient));
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Falha de transporte ao comunicar com servidor MCP remoto durante '{operationDescription}'. {ex.Message}",
                ex);
        }
        finally
        {
            sendTokenSource?.Dispose();
            timeoutTokenSource?.Dispose();
        }
    }

    private async Task<Uri> ResolveMessageEndpointAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (_resolvedMessageEndpoint is not null)
        {
            return _resolvedMessageEndpoint;
        }

        if (_serverOptions.TransportKind == McpRemoteTransportKind.Http)
        {
            _resolvedMessageEndpoint = _serverOptions.MessageEndpoint ?? _serverOptions.Endpoint;
            _messageEndpointSource.TrySetResult(_resolvedMessageEndpoint);
            return _resolvedMessageEndpoint;
        }

        return await WaitForMessageEndpointAsync(timeout, cancellationToken);
    }

    private async Task<Uri> WaitForMessageEndpointAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource? timeoutTokenSource = null;
        CancellationTokenSource? waitTokenSource = null;

        try
        {
            if (timeout == Timeout.InfiniteTimeSpan)
            {
                waitTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _disposeTokenSource.Token);
            }
            else
            {
                timeoutTokenSource = new CancellationTokenSource(timeout);
                waitTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutTokenSource.Token,
                    _disposeTokenSource.Token);
            }

            var endpoint = await _messageEndpointSource.Task.WaitAsync(waitTokenSource.Token);
            _resolvedMessageEndpoint = endpoint;
            return endpoint;
        }
        catch (OperationCanceledException) when (
            timeoutTokenSource is not null &&
            timeoutTokenSource.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Nao foi possivel resolver o endpoint de mensagens do servidor MCP remoto em {timeout.TotalMilliseconds:0} ms.");
        }
        catch (OperationCanceledException) when (_disposeTokenSource.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(McpRemoteClient));
        }
        finally
        {
            waitTokenSource?.Dispose();
            timeoutTokenSource?.Dispose();
        }
    }

    private async Task<JsonElement> WaitForResponseAsync(
        string requestId,
        PendingRequest pendingRequest,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource? timeoutTokenSource = null;
        CancellationTokenSource? waitTokenSource = null;

        try
        {
            if (timeout == Timeout.InfiniteTimeSpan)
            {
                waitTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _disposeTokenSource.Token);
            }
            else
            {
                timeoutTokenSource = new CancellationTokenSource(timeout);
                waitTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutTokenSource.Token,
                    _disposeTokenSource.Token);
            }

            return await pendingRequest.Completion.Task.WaitAsync(waitTokenSource.Token);
        }
        catch (OperationCanceledException) when (
            timeoutTokenSource is not null &&
            timeoutTokenSource.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested)
        {
            _pendingRequests.TryRemove(requestId, out _);
            throw new TimeoutException(
                $"A requisicao MCP '{pendingRequest.Method}' excedeu o timeout de {timeout.TotalMilliseconds:0} ms.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _pendingRequests.TryRemove(requestId, out _);
            throw;
        }
        catch (OperationCanceledException) when (_disposeTokenSource.IsCancellationRequested)
        {
            _pendingRequests.TryRemove(requestId, out _);
            throw new ObjectDisposedException(nameof(McpRemoteClient));
        }
        finally
        {
            waitTokenSource?.Dispose();
            timeoutTokenSource?.Dispose();
        }
    }

    private void ApplyHeaders(HttpRequestMessage request)
    {
        foreach (var header in _serverOptions.Headers)
        {
            request.Headers.Remove(header.Key);
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        _serverOptions.Authentication.ApplyTo(request.Headers);
    }

    private void ThrowIfTransportFailed()
    {
        if (_transportFailure is not null)
        {
            throw new InvalidOperationException(
                "O transporte MCP remoto nao esta mais disponivel.",
                _transportFailure);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed != 0 || _disposeTokenSource.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(McpRemoteClient));
        }
    }

    private static void ValidateMethod(string method)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            throw new ArgumentException(
                "O metodo MCP nao pode ser vazio.",
                nameof(method));
        }
    }

    private void FailAllPendingRequests(Exception exception)
    {
        foreach (var pendingRequest in _pendingRequests.Values)
        {
            pendingRequest.Completion.TrySetException(exception);
        }

        _pendingRequests.Clear();
    }

    private static byte[] BuildRequestPayload(
        string requestId,
        string method,
        JsonElement? parameters)
    {
        using var buffer = new MemoryStream();
        using var writer = new Utf8JsonWriter(buffer);

        writer.WriteStartObject();
        writer.WriteString("jsonrpc", "2.0");
        writer.WriteString("id", requestId);
        writer.WriteString("method", method);

        if (parameters is JsonElement parameterElement)
        {
            writer.WritePropertyName("params");
            parameterElement.WriteTo(writer);
        }

        writer.WriteEndObject();
        writer.Flush();

        return buffer.ToArray();
    }

    private static byte[] BuildNotificationPayload(
        string method,
        JsonElement? parameters)
    {
        using var buffer = new MemoryStream();
        using var writer = new Utf8JsonWriter(buffer);

        writer.WriteStartObject();
        writer.WriteString("jsonrpc", "2.0");
        writer.WriteString("method", method);

        if (parameters is JsonElement parameterElement)
        {
            writer.WritePropertyName("params");
            parameterElement.WriteTo(writer);
        }

        writer.WriteEndObject();
        writer.Flush();

        return buffer.ToArray();
    }

    private static bool TryGetRequestId(JsonElement root, out string requestId)
    {
        requestId = string.Empty;
        if (!root.TryGetProperty("id", out var idElement))
        {
            return false;
        }

        switch (idElement.ValueKind)
        {
            case JsonValueKind.String:
                requestId = idElement.GetString() ?? string.Empty;
                return !string.IsNullOrEmpty(requestId);

            case JsonValueKind.Number:
                if (idElement.TryGetInt64(out var numericId))
                {
                    requestId = numericId.ToString(CultureInfo.InvariantCulture);
                    return true;
                }

                requestId = idElement.GetRawText();
                return true;

            default:
                requestId = idElement.GetRawText();
                return !string.IsNullOrWhiteSpace(requestId);
        }
    }

    private static McpRequestException CreateRequestException(
        string method,
        JsonElement errorElement)
    {
        var code = errorElement.TryGetProperty("code", out var codeElement) &&
                   codeElement.TryGetInt32(out var parsedCode)
            ? parsedCode
            : -1;

        var message = errorElement.TryGetProperty("message", out var messageElement) &&
                      messageElement.ValueKind == JsonValueKind.String
            ? messageElement.GetString() ?? "Erro MCP sem mensagem."
            : "Erro MCP sem mensagem.";

        var rawData = errorElement.TryGetProperty("data", out var dataElement)
            ? dataElement.GetRawText()
            : null;

        return new McpRequestException(
            method: method,
            code: code,
            message: $"Erro MCP no metodo '{method}': [{code}] {message}",
            rawData: rawData);
    }

    private static InvalidOperationException BuildHttpStatusException(
        string operationDescription,
        System.Net.HttpStatusCode statusCode)
    {
        return new InvalidOperationException(
            $"O servidor MCP remoto respondeu com HTTP {(int)statusCode} ({statusCode}) durante '{operationDescription}'.");
    }

    private static async Task<SseEvent?> ReadSseEventAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        string? eventName = null;
        var dataBuilder = new StringBuilder();

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                if (eventName is null && dataBuilder.Length == 0)
                {
                    return null;
                }

                break;
            }

            if (line.Length == 0)
            {
                if (eventName is null && dataBuilder.Length == 0)
                {
                    continue;
                }

                break;
            }

            if (line.StartsWith(":", StringComparison.Ordinal))
            {
                continue;
            }

            var separatorIndex = line.IndexOf(':');
            var field = separatorIndex < 0
                ? line
                : line[..separatorIndex];
            var value = separatorIndex < 0
                ? string.Empty
                : line[(separatorIndex + 1)..].TrimStart();

            if (string.Equals(field, "event", StringComparison.OrdinalIgnoreCase))
            {
                eventName = value;
                continue;
            }

            if (!string.Equals(field, "data", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (dataBuilder.Length > 0)
            {
                dataBuilder.Append('\n');
            }

            dataBuilder.Append(value);
        }

        return new SseEvent(
            EventName: string.IsNullOrWhiteSpace(eventName)
                ? "message"
                : eventName.Trim(),
            Data: dataBuilder.ToString());
    }

    private static string? ExtractEndpointValue(string rawData)
    {
        if (string.IsNullOrWhiteSpace(rawData))
        {
            return null;
        }

        var trimmedData = rawData.Trim();
        if (!trimmedData.StartsWith('{') && !trimmedData.StartsWith('"'))
        {
            return trimmedData;
        }

        try
        {
            using var payload = JsonDocument.Parse(trimmedData);
            var root = payload.RootElement;

            if (root.ValueKind == JsonValueKind.String)
            {
                return root.GetString();
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return trimmedData;
            }

            if (TryGetStringProperty(root, "endpoint", out var endpointValue))
            {
                return endpointValue;
            }

            if (TryGetStringProperty(root, "url", out var urlValue))
            {
                return urlValue;
            }

            if (TryGetStringProperty(root, "uri", out var uriValue))
            {
                return uriValue;
            }

            return trimmedData;
        }
        catch (JsonException)
        {
            return trimmedData;
        }
    }

    private static bool TryGetStringProperty(
        JsonElement root,
        string propertyName,
        out string? value)
    {
        value = null;
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static Uri EnsureHttpEndpoint(Uri endpoint, string source)
    {
        var isHttp = string.Equals(
            endpoint.Scheme,
            Uri.UriSchemeHttp,
            StringComparison.OrdinalIgnoreCase);
        var isHttps = string.Equals(
            endpoint.Scheme,
            Uri.UriSchemeHttps,
            StringComparison.OrdinalIgnoreCase);

        if (!isHttp && !isHttps)
        {
            throw new InvalidOperationException(
                $"O {source} deve usar os esquemas 'http' ou 'https'.");
        }

        return endpoint;
    }

    private sealed class PendingRequest(
        string method,
        TaskCompletionSource<JsonElement> completion)
    {
        public string Method { get; } = method;

        public TaskCompletionSource<JsonElement> Completion { get; } = completion;
    }

    private sealed record SseEvent(string EventName, string Data);
}
