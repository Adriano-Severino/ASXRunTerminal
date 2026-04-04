using System.Text;

namespace ASXRunTerminal.Core;

internal sealed class TerminalResponseRenderer
{
    private readonly AnsiTerminalRenderer _ansiRenderer;
    private readonly TerminalDesignSystem _designSystem;
    private readonly StringBuilder _pending = new();

    private bool _isInsideCodeFence;
    private string? _activeCodeLanguage;

    public TerminalResponseRenderer()
        : this(AnsiTerminalRenderer.CreateDefault(), TerminalDesignSystem.Default)
    {
    }

    internal TerminalResponseRenderer(
        AnsiTerminalRenderer ansiRenderer,
        TerminalDesignSystem designSystem)
    {
        _ansiRenderer = ansiRenderer ?? throw new ArgumentNullException(nameof(ansiRenderer));
        _designSystem = designSystem;
    }

    public string RenderChunk(string chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        if (chunk.Length == 0)
        {
            return string.Empty;
        }

        _pending.Append(chunk);
        return DrainBufferedText(processTrailingLine: false);
    }

    public string Flush()
    {
        return DrainBufferedText(processTrailingLine: true);
    }

    private string DrainBufferedText(bool processTrailingLine)
    {
        if (_pending.Length == 0)
        {
            return string.Empty;
        }

        var bufferedText = _pending.ToString();
        var output = new StringBuilder(bufferedText.Length);
        var cursor = 0;

        while (cursor < bufferedText.Length)
        {
            var lineFeedIndex = bufferedText.IndexOf('\n', cursor);
            if (lineFeedIndex < 0)
            {
                if (!processTrailingLine)
                {
                    break;
                }

                output.Append(RenderLine(bufferedText[cursor..]));
                cursor = bufferedText.Length;
                break;
            }

            output.Append(RenderLine(bufferedText[cursor..(lineFeedIndex + 1)]));
            cursor = lineFeedIndex + 1;
        }

        _pending.Clear();
        if (cursor < bufferedText.Length)
        {
            _pending.Append(bufferedText.AsSpan(cursor));
        }

        return output.ToString();
    }

    private string RenderLine(string line)
    {
        var (content, lineEnding) = SplitLineEnding(line);
        var trimmedStart = content.TrimStart();

        if (IsCodeFence(trimmedStart))
        {
            if (_isInsideCodeFence)
            {
                _isInsideCodeFence = false;
                _activeCodeLanguage = null;
            }
            else
            {
                _isInsideCodeFence = true;
                _activeCodeLanguage = ExtractFenceLanguage(trimmedStart);
            }

            return $"{_ansiRenderer.Render(content, _designSystem.BorderStyle)}{lineEnding}";
        }

        if (_isInsideCodeFence)
        {
            var codeStyle = ResolveCodeStyle(_activeCodeLanguage);
            return $"{_ansiRenderer.Render(content, codeStyle)}{lineEnding}";
        }

        return $"{content}{lineEnding}";
    }

    private TerminalTextStyle ResolveCodeStyle(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return _designSystem.HighlightStyle;
        }

        var normalizedLanguage = language.Trim().ToLowerInvariant();

        return normalizedLanguage switch
        {
            "csharp" or "c#" or "cs" or "dotnet" or "fsharp" or "vbnet" => _designSystem.InfoStyle,
            "python" or "py" => _designSystem.SuccessStyle,
            "javascript" or "js" or "typescript" or "ts" or "jsx" or "tsx" => _designSystem.WarningStyle,
            "powershell" or "pwsh" or "ps1" or "shell" or "sh" or "bash" or "zsh" => _designSystem.AccentStyle,
            "sql" or "postgresql" or "mysql" or "sqlite" or "plsql" => _designSystem.ErrorStyle,
            "json" or "yaml" or "yml" or "xml" or "toml" => _designSystem.AccentStyle,
            _ => _designSystem.HighlightStyle
        };
    }

    private static bool IsCodeFence(string trimmedStartLine)
    {
        return trimmedStartLine.StartsWith("```", StringComparison.Ordinal);
    }

    private static string? ExtractFenceLanguage(string trimmedFenceLine)
    {
        var metadata = trimmedFenceLine[3..].Trim();
        if (metadata.Length == 0)
        {
            return null;
        }

        var separatorIndex = metadata.IndexOfAny([' ', '\t', '{']);
        if (separatorIndex < 0)
        {
            return metadata;
        }

        var language = metadata[..separatorIndex].Trim();
        return language.Length == 0 ? null : language;
    }

    private static (string Content, string LineEnding) SplitLineEnding(string line)
    {
        if (line.EndsWith("\r\n", StringComparison.Ordinal))
        {
            return (line[..^2], "\r\n");
        }

        if (line.EndsWith('\n'))
        {
            return (line[..^1], "\n");
        }

        if (line.EndsWith('\r'))
        {
            return (line[..^1], "\r");
        }

        return (line, string.Empty);
    }
}