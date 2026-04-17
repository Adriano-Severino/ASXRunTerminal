using System.Text.Json;

namespace ASXRunTerminal.Core;

internal interface IMcpClient : IAsyncDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task<JsonElement> SendRequestAsync(
        string method,
        JsonElement? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    Task SendNotificationAsync(
        string method,
        JsonElement? parameters = null,
        CancellationToken cancellationToken = default);
}
