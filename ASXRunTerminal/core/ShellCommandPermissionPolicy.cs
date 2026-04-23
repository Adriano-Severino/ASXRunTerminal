using System.Text;

namespace ASXRunTerminal.Core;

internal sealed class ShellCommandPermissionPolicy
{
    public const int BlockedCommandExitCode = 126;
    public const string DestructiveApprovalArgumentName = "destructive_approval";

    private static readonly IReadOnlySet<string> DefaultBlockedCommands = new HashSet<string>(
        [
            "clear-disk",
            "dd",
            "del",
            "diskpart",
            "erase",
            "fdisk",
            "format",
            "format-volume",
            "halt",
            "iex",
            "init",
            "invoke-expression",
            "mkfs",
            "parted",
            "poweroff",
            "rd",
            "reboot",
            "remove-item",
            "remove-partition",
            "restart-computer",
            "rm",
            "rmdir",
            "set-disk",
            "shutdown",
            "stop-computer"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> WrapperCommands = new HashSet<string>(
        [
            "&",
            ".",
            "builtin",
            "command",
            "env",
            "exec",
            "nohup",
            "start",
            "sudo",
            "time"
        ],
        StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlySet<string> _allowedCommands;
    private readonly IReadOnlySet<string> _blockedCommands;

    public static ShellCommandPermissionPolicy Default { get; } = new();

    public ShellCommandPermissionPolicy(
        IReadOnlyList<string>? allowedCommands = null,
        IReadOnlyList<string>? blockedCommands = null)
    {
        _allowedCommands = NormalizeCommands(
            allowedCommands,
            nameof(allowedCommands));
        _blockedCommands = BuildBlockedCommands(blockedCommands);
    }

    public void EnsureAllowed(
        string shellToolName,
        string script,
        bool isDestructiveCommandApproved = false)
    {
        if (string.IsNullOrWhiteSpace(shellToolName))
        {
            throw new ArgumentException(
                "O argumento 'shellToolName' e obrigatorio.",
                nameof(shellToolName));
        }

        if (string.IsNullOrWhiteSpace(script))
        {
            throw new ArgumentException(
                "O argumento 'script' e obrigatorio.",
                nameof(script));
        }

        var commandNames = ExtractCommandNames(script);
        foreach (var commandName in commandNames)
        {
            if (!_blockedCommands.Contains(commandName))
            {
                continue;
            }

            if (_allowedCommands.Contains(commandName))
            {
                if (isDestructiveCommandApproved)
                {
                    continue;
                }

                throw new UnauthorizedAccessException(
                    $"Comando destrutivo bloqueado por padrao: '{commandName}' na ferramenta '{shellToolName}'. " +
                    $"Forneca aprovacao explicita informando o argumento '{DestructiveApprovalArgumentName}=sim' para esta execucao.");
            }

            throw new UnauthorizedAccessException(
                $"Comando de shell de alto risco bloqueado: '{commandName}' na ferramenta '{shellToolName}'. " +
                $"Inclua o comando em '.asxrun/shell-command-policy.json' na lista 'allow' para liberar explicitamente.");
        }
    }

    internal static IReadOnlyList<string> ExtractCommandNames(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return [];
        }

        var commands = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var segment in SplitSegments(script))
        {
            if (!TryExtractLeadingCommand(segment, out var commandName))
            {
                continue;
            }

            if (!seen.Add(commandName))
            {
                continue;
            }

            commands.Add(commandName);
        }

        return commands;
    }

    private static IReadOnlySet<string> BuildBlockedCommands(IReadOnlyList<string>? blockedCommands)
    {
        var combinedBlockedCommands = new HashSet<string>(
            DefaultBlockedCommands,
            StringComparer.OrdinalIgnoreCase);

        foreach (var blockedCommand in NormalizeCommands(
                     blockedCommands,
                     nameof(blockedCommands)))
        {
            combinedBlockedCommands.Add(blockedCommand);
        }

        return combinedBlockedCommands;
    }

    private static IReadOnlySet<string> NormalizeCommands(
        IReadOnlyList<string>? commands,
        string argumentName)
    {
        if (commands is null || commands.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var normalizedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var command in commands)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new InvalidOperationException(
                    $"A lista '{argumentName}' nao pode conter comandos vazios.");
            }

            var normalized = NormalizeCommandToken(command);
            if (normalized.Length == 0)
            {
                throw new InvalidOperationException(
                    $"A lista '{argumentName}' contem um comando invalido: '{command}'.");
            }

            normalizedCommands.Add(normalized);
        }

        return normalizedCommands;
    }

    private static IEnumerable<string> SplitSegments(string script)
    {
        var builder = new StringBuilder(script.Length);
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var isEscaped = false;

        for (var index = 0; index < script.Length; index++)
        {
            var current = script[index];

            if (!inSingleQuote && current == '\\' && !isEscaped)
            {
                isEscaped = true;
                builder.Append(current);
                continue;
            }

            if (!isEscaped)
            {
                if (current == '\'' && !inDoubleQuote)
                {
                    inSingleQuote = !inSingleQuote;
                    builder.Append(current);
                    continue;
                }

                if (current == '"' && !inSingleQuote)
                {
                    inDoubleQuote = !inDoubleQuote;
                    builder.Append(current);
                    continue;
                }
            }

            if (!inSingleQuote && !inDoubleQuote)
            {
                if (current == '#' && IsCommentStart(script, index))
                {
                    while (index + 1 < script.Length
                           && script[index + 1] is not '\r'
                           && script[index + 1] is not '\n')
                    {
                        index++;
                    }

                    continue;
                }

                if (IsSegmentSeparator(current))
                {
                    if (builder.Length > 0)
                    {
                        yield return builder.ToString();
                        builder.Clear();
                    }

                    if ((current == '&' || current == '|')
                        && index + 1 < script.Length
                        && script[index + 1] == current)
                    {
                        index++;
                    }

                    isEscaped = false;
                    continue;
                }
            }

            builder.Append(current);
            isEscaped = false;
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }

    private static bool TryExtractLeadingCommand(string segment, out string commandName)
    {
        commandName = string.Empty;

        var remaining = segment.AsSpan().Trim();
        var consumedWrapper = false;

        while (!remaining.IsEmpty)
        {
            var token = ReadNextToken(ref remaining);
            if (token.Length == 0)
            {
                return false;
            }

            if (token.StartsWith("--", StringComparison.Ordinal) && consumedWrapper)
            {
                continue;
            }

            if (token.StartsWith("-", StringComparison.Ordinal) && consumedWrapper)
            {
                continue;
            }

            if (IsAssignmentToken(token))
            {
                consumedWrapper = true;
                continue;
            }

            var normalizedToken = NormalizeCommandToken(token);
            if (normalizedToken.Length == 0)
            {
                continue;
            }

            if (WrapperCommands.Contains(normalizedToken))
            {
                consumedWrapper = true;
                continue;
            }

            commandName = normalizedToken;
            return true;
        }

        return false;
    }

    private static string ReadNextToken(ref ReadOnlySpan<char> remaining)
    {
        remaining = remaining.TrimStart();
        if (remaining.IsEmpty)
        {
            return string.Empty;
        }

        var tokenBuilder = new StringBuilder(remaining.Length);
        var inSingleQuote = false;
        var inDoubleQuote = false;

        var index = 0;
        for (; index < remaining.Length; index++)
        {
            var current = remaining[index];

            if (current == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                tokenBuilder.Append(current);
                continue;
            }

            if (current == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                tokenBuilder.Append(current);
                continue;
            }

            if (!inSingleQuote && !inDoubleQuote && char.IsWhiteSpace(current))
            {
                break;
            }

            tokenBuilder.Append(current);
        }

        remaining = index >= remaining.Length
            ? ReadOnlySpan<char>.Empty
            : remaining[index..];

        return tokenBuilder.ToString();
    }

    private static string NormalizeCommandToken(string token)
    {
        var trimmedToken = token.Trim()
            .Trim('\'', '"', '`')
            .Trim('(', ')', '[', ']', '{', '}');
        if (trimmedToken.Length == 0)
        {
            return string.Empty;
        }

        if (trimmedToken.StartsWith("&", StringComparison.Ordinal))
        {
            trimmedToken = trimmedToken[1..].TrimStart();
            trimmedToken = trimmedToken.Trim('\'', '"', '`');
        }

        if (trimmedToken.Length == 0)
        {
            return string.Empty;
        }

        if (trimmedToken.Contains('/') || trimmedToken.Contains('\\'))
        {
            trimmedToken = Path.GetFileName(trimmedToken);
        }

        var extension = Path.GetExtension(trimmedToken);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            trimmedToken = Path.GetFileNameWithoutExtension(trimmedToken);
        }

        return trimmedToken.Trim().ToLowerInvariant();
    }

    private static bool IsAssignmentToken(string token)
    {
        var separatorIndex = token.IndexOf('=');
        if (separatorIndex <= 0)
        {
            return false;
        }

        var variableName = token[..separatorIndex];
        foreach (var character in variableName)
        {
            if (!char.IsLetterOrDigit(character)
                && character is not '_'
                && character is not ':')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSegmentSeparator(char character)
    {
        return character is ';' or '\r' or '\n' or '|' or '&';
    }

    private static bool IsCommentStart(string script, int index)
    {
        if (index == 0)
        {
            return true;
        }

        var previous = script[index - 1];
        return char.IsWhiteSpace(previous) || IsSegmentSeparator(previous);
    }
}
