using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace ASXRunTerminal.Infra;

/// <summary>
/// Adaptador de <see cref="IChatClient"/> (Microsoft.Extensions.AI) sobre
/// <see cref="OllamaHttpClient"/>, expondo o Ollama local atraves do contrato
/// oficial recomendado pelo Microsoft Agent Framework.
///
/// Para o MVP, mensagens <see cref="ChatMessage"/> sao serializadas em um prompt
/// plano no formato <c>[role]: texto</c> e o conteudo textual das
/// <see cref="ChatResponseUpdate"/>s e derivado dos <c>TextContent</c>.
/// Modelos Ollama nao-instrucionalizados podem precisar de um template
/// diferente no futuro.
/// </summary>
internal sealed class OllamaChatClient : IChatClient
{
    private static readonly ChatClientMetadata DefaultMetadata = new("ollama");

    private readonly OllamaHttpClient _httpClient;

    public OllamaChatClient(OllamaHttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    public ChatClientMetadata Metadata => DefaultMetadata;

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var prompt = BuildPrompt(messages);
        var model = options?.ModelId;
        var text = await _httpClient
            .GenerateAsync(prompt, model, cancellationToken)
            .ConfigureAwait(false);

        return new ChatResponse(new ChatMessage(new ChatRole("assistant"), text));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var prompt = BuildPrompt(messages);
        var model = options?.ModelId;

        await foreach (var chunk in _httpClient
            .GenerateStreamAsync(prompt, cancellationToken)
            .ConfigureAwait(false))
        {
            if (string.IsNullOrEmpty(chunk))
            {
                continue;
            }

            yield return new ChatResponseUpdate(new ChatRole("assistant"), chunk);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
        // O ciclo de vida do OllamaHttpClient e gerenciado pelo DI host.
    }

    private static string BuildPrompt(IEnumerable<ChatMessage> messages)
    {
        var builder = new System.Text.StringBuilder();
        var emitted = 0;

        foreach (var message in messages)
        {
            if (message is null)
            {
                continue;
            }

            var text = ExtractText(message);
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            var role = message.Role.Value;
            builder.Append(role);
            builder.Append(": ");
            builder.AppendLine(text);
            builder.AppendLine();
            emitted++;
        }

        if (emitted == 0)
        {
            throw new ArgumentException(
                "O chat client nao pode ser invocado sem mensagens com conteudo textual.",
                nameof(messages));
        }

        return builder.ToString().TrimEnd();
    }

    private static string ExtractText(ChatMessage message)
    {
        if (!string.IsNullOrEmpty(message.Text))
        {
            return message.Text;
        }

        if (message.Contents is null || message.Contents.Count == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        foreach (var content in message.Contents.OfType<TextContent>())
        {
            if (!string.IsNullOrEmpty(content.Text))
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }
                builder.Append(content.Text);
            }
        }
        return builder.ToString();
    }
}