using System.Text;
using System.Text.Json;

namespace ASXRunTerminal.Infra;

internal static class McpMessageFraming
{
    private const string ContentLengthHeaderName = "Content-Length";
    private const int MaxHeaderLineLength = 8 * 1024;

    public static async Task WriteMessageAsync(
        Stream output,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        var headerBytes = Encoding.ASCII.GetBytes(
            $"{ContentLengthHeaderName}: {payload.Length}\r\n\r\n");

        await output.WriteAsync(headerBytes, cancellationToken);
        await output.WriteAsync(payload, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }

    public static async Task<JsonDocument?> ReadMessageAsync(
        Stream input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        int? contentLength = null;

        while (true)
        {
            var line = await ReadHeaderLineAsync(input, cancellationToken);
            if (line is null)
            {
                return null;
            }

            if (line.Length == 0)
            {
                break;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                throw new InvalidOperationException(
                    $"Cabecalho MCP invalido: '{line}'.");
            }

            var headerName = line[..separatorIndex].Trim();
            var headerValue = line[(separatorIndex + 1)..].Trim();

            if (!string.Equals(
                    headerName,
                    ContentLengthHeaderName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!int.TryParse(headerValue, out var parsedContentLength) || parsedContentLength <= 0)
            {
                throw new InvalidOperationException(
                    "Cabecalho Content-Length invalido no protocolo MCP.");
            }

            contentLength = parsedContentLength;
        }

        if (contentLength is null)
        {
            throw new InvalidOperationException(
                "Cabecalho Content-Length ausente na mensagem MCP.");
        }

        var payload = new byte[contentLength.Value];
        await ReadExactlyAsync(input, payload, cancellationToken);
        return JsonDocument.Parse(payload);
    }

    private static async Task<string?> ReadHeaderLineAsync(
        Stream input,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var oneByte = new byte[1];

        while (true)
        {
            var bytesRead = await input.ReadAsync(
                oneByte.AsMemory(0, 1),
                cancellationToken);

            if (bytesRead == 0)
            {
                if (buffer.Length == 0)
                {
                    return null;
                }

                throw new EndOfStreamException(
                    "Stream MCP encerrado durante leitura de cabecalho.");
            }

            buffer.WriteByte(oneByte[0]);
            if (buffer.Length > MaxHeaderLineLength)
            {
                throw new InvalidOperationException(
                    "Cabecalho MCP excedeu o limite maximo suportado.");
            }

            if (buffer.Length >= 2)
            {
                var bytes = buffer.GetBuffer();
                var length = (int)buffer.Length;

                if (bytes[length - 2] == '\r' && bytes[length - 1] == '\n')
                {
                    return Encoding.ASCII.GetString(bytes, 0, length - 2);
                }
            }
        }
    }

    private static async Task ReadExactlyAsync(
        Stream input,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var bytesRead = await input.ReadAsync(
                buffer.AsMemory(offset),
                cancellationToken);

            if (bytesRead == 0)
            {
                throw new EndOfStreamException(
                    "Stream MCP encerrado antes de concluir o payload.");
            }

            offset += bytesRead;
        }
    }
}
