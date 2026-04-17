using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Infra;

internal sealed class McpStdioClient : IMcpClient
{
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);

    private readonly McpServerProcessOptions _serverOptions;
    private readonly Func<McpServerProcessOptions, CancellationToken, ValueTask<McpStdioConnection>> _connectionFactory;
    private readonly TimeSpan _defaultRequestTimeout;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, PendingRequest> _pendingRequests =
        new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _disposeTokenSource = new();

    private long _requestIdCounter;
    private McpStdioConnection? _connection;
    private Task? _receiveLoopTask;
    private Exception? _transportFailure;
    private int _isDisposed;

    public McpStdioClient(
        McpServerProcessOptions serverOptions,
        TimeSpan? defaultRequestTimeout = null,
        Func<McpServerProcessOptions, CancellationToken, ValueTask<McpStdioConnection>>? connectionFactory = null)
    {
        _serverOptions = serverOptions ?? throw new ArgumentNullException(nameof(serverOptions));
        _defaultRequestTimeout = defaultRequestTimeout ?? DefaultRequestTimeout;

        if (_defaultRequestTimeout <= TimeSpan.Zero && _defaultRequestTimeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                nameof(defaultRequestTimeout),
                "O timeout padrao do cliente MCP deve ser maior que zero.");
        }

        _connectionFactory = connectionFactory ?? CreateProcessConnectionAsync;
    }

    public bool IsConnected =>
        _connection is not null &&
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

            if (_connection is not null)
            {
                return;
            }

            var connection = await _connectionFactory(_serverOptions, cancellationToken);
            _connection = connection;
            _receiveLoopTask = Task.Run(
                () => ReceiveLoopAsync(connection, _disposeTokenSource.Token),
                CancellationToken.None);
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
        var connection = _connection ?? throw new InvalidOperationException(
            "Conexao MCP nao inicializada.");

        try
        {
            await WritePayloadAsync(connection, payload, cancellationToken);
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
        var connection = _connection ?? throw new InvalidOperationException(
            "Conexao MCP nao inicializada.");

        await WritePayloadAsync(connection, payload, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        _disposeTokenSource.Cancel();

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
            new ObjectDisposedException(nameof(McpStdioClient)));

        _disposeTokenSource.Dispose();
        _connectionLock.Dispose();
        _writeLock.Dispose();
    }

    private async Task ReceiveLoopAsync(
        McpStdioConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                JsonDocument? message;
                try
                {
                    message = await McpMessageFraming.ReadMessageAsync(
                        connection.Input,
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (message is null)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    throw BuildTransportException(
                        "A conexao MCP via stdio foi encerrada pelo servidor.",
                        connection);
                }

                using (message)
                {
                    HandleIncomingMessage(message.RootElement);
                }
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _transportFailure = ex;
            FailAllPendingRequests(ex);
        }
        finally
        {
            if (ReferenceEquals(_connection, connection))
            {
                _connection = null;
            }

            await connection.DisposeAsync();
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

    private async Task WritePayloadAsync(
        McpStdioConnection connection,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await McpMessageFraming.WriteMessageAsync(
                connection.Output,
                payload,
                cancellationToken);
        }
        catch (Exception ex)
        {
            throw BuildTransportException(
                "Falha ao enviar mensagem para o servidor MCP.",
                connection,
                ex);
        }
        finally
        {
            _writeLock.Release();
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
            throw new ObjectDisposedException(nameof(McpStdioClient));
        }
        finally
        {
            waitTokenSource?.Dispose();
            timeoutTokenSource?.Dispose();
        }
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

    private static InvalidOperationException BuildTransportException(
        string message,
        McpStdioConnection connection,
        Exception? innerException = null)
    {
        var standardError = connection.GetStandardError();
        var messageWithStandardError = string.IsNullOrWhiteSpace(standardError)
            ? message
            : $"{message} stderr: {standardError.Trim()}";

        return innerException is null
            ? new InvalidOperationException(messageWithStandardError)
            : new InvalidOperationException(messageWithStandardError, innerException);
    }

    private void ThrowIfTransportFailed()
    {
        if (_transportFailure is not null)
        {
            throw new InvalidOperationException(
                "O transporte MCP nao esta mais disponivel.",
                _transportFailure);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed != 0 || _disposeTokenSource.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(McpStdioClient));
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

    private static ValueTask<McpStdioConnection> CreateProcessConnectionAsync(
        McpServerProcessOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = options.Command,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in options.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (!string.IsNullOrWhiteSpace(options.WorkingDirectory))
        {
            startInfo.WorkingDirectory = options.WorkingDirectory;
        }

        foreach (var environmentVariable in options.EnvironmentVariables)
        {
            startInfo.Environment[environmentVariable.Key] = environmentVariable.Value;
        }

        var process = new Process
        {
            StartInfo = startInfo
        };

        try
        {
            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException(
                    $"Nao foi possivel iniciar o servidor MCP '{options.Command}'.");
            }
        }
        catch (Exception ex)
        {
            process.Dispose();
            throw new InvalidOperationException(
                $"Falha ao iniciar o servidor MCP '{options.Command}'. {ex.Message}",
                ex);
        }

        var standardErrorBuffer = new StringBuilder();
        var standardErrorLock = new object();
        var standardErrorPumpTask = Task.Run(
            async () =>
            {
                var buffer = new char[512];
                while (true)
                {
                    var read = await process.StandardError.ReadAsync(
                        buffer.AsMemory(0, buffer.Length));

                    if (read == 0)
                    {
                        break;
                    }

                    lock (standardErrorLock)
                    {
                        standardErrorBuffer.Append(buffer, 0, read);
                    }
                }
            },
            CancellationToken.None);

        string ReadStandardError()
        {
            lock (standardErrorLock)
            {
                return standardErrorBuffer.ToString();
            }
        }

        async ValueTask DisposeConnectionAsync()
        {
            try
            {
                process.StandardInput.Close();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }

            try
            {
                await process.WaitForExitAsync(CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
            }

            try
            {
                await standardErrorPumpTask;
            }
            catch (ObjectDisposedException)
            {
            }

            process.Dispose();
        }

        var connection = new McpStdioConnection(
            input: process.StandardOutput.BaseStream,
            output: process.StandardInput.BaseStream,
            standardErrorProvider: ReadStandardError,
            disposeAsync: DisposeConnectionAsync);

        return ValueTask.FromResult(connection);
    }

    private sealed class PendingRequest(
        string method,
        TaskCompletionSource<JsonElement> completion)
    {
        public string Method { get; } = method;

        public TaskCompletionSource<JsonElement> Completion { get; } = completion;
    }
}
