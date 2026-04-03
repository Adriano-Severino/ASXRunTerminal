namespace ASXRunTerminal.Core;

using Microsoft.Extensions.AI;

internal interface IOllamaHttpClient
{
    Uri BaseAddress { get; }
    IChatClient ChatClient { get; }

    IAsyncEnumerable<string> GenerateStreamAsync(string prompt, CancellationToken cancellationToken = default);
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
    Task<OllamaHealthcheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OllamaLocalModel>> ListLocalModelsAsync(CancellationToken cancellationToken = default);
}
