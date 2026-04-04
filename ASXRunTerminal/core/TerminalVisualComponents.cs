namespace ASXRunTerminal.Core;

internal readonly record struct TerminalHeader(string Value)
{
    public static implicit operator string(TerminalHeader header)
    {
        return header.Value;
    }
}

internal readonly record struct TerminalSeparator(string Value)
{
    public static implicit operator string(TerminalSeparator separator)
    {
        return separator.Value;
    }
}

internal readonly record struct TerminalStatusBadge(string Value)
{
    public static implicit operator string(TerminalStatusBadge badge)
    {
        return badge.Value;
    }

    public static implicit operator TerminalStatusBadge(ExecutionState state)
    {
        TerminalExecutionStateToken token = state;
        return new TerminalStatusBadge(token.Badge);
    }
}

internal readonly record struct TerminalSpinnerFrame(string Value)
{
    public static implicit operator string(TerminalSpinnerFrame frame)
    {
        return frame.Value;
    }
}

internal static class TerminalVisualComponents
{
    private static readonly string[] ConnectingFrames = [".", "..", "..."];
    private static readonly string[] ProcessingFrames = ["|", "/", "-", "\\"];
    private const int MinimumSeparatorWidth = 3;
    private const int DefaultSeparatorWidth = 48;

    public static TerminalHeader BuildHeader(
        string title,
        string? subtitle = null,
        int width = DefaultSeparatorWidth)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("O titulo do header nao pode ser vazio.", nameof(title));
        }

        var topLine = (string)BuildSeparator(title.Trim(), width, '=');

        if (string.IsNullOrWhiteSpace(subtitle))
        {
            return new TerminalHeader(topLine);
        }

        var normalizedSubtitle = subtitle.Trim();
        var bottomLine = (string)BuildSeparator(width: width, fill: '=');

        return new TerminalHeader(
            $"{topLine}{Environment.NewLine}{normalizedSubtitle}{Environment.NewLine}{bottomLine}");
    }

    public static TerminalSeparator BuildSeparator(
        string? label = null,
        int width = DefaultSeparatorWidth,
        char fill = '-')
    {
        if (width < MinimumSeparatorWidth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                width,
                $"A largura minima do separador e {MinimumSeparatorWidth}.");
        }

        if (char.IsWhiteSpace(fill))
        {
            throw new ArgumentException(
                "O caractere de preenchimento do separador nao pode ser espaco em branco.",
                nameof(fill));
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            return new TerminalSeparator(new string(fill, width));
        }

        var normalizedLabel = $" {label.Trim()} ";
        if (normalizedLabel.Length >= width)
        {
            return new TerminalSeparator(normalizedLabel);
        }

        var totalFill = width - normalizedLabel.Length;
        var leftFill = totalFill / 2;
        var rightFill = totalFill - leftFill;

        return new TerminalSeparator(
            $"{new string(fill, leftFill)}{normalizedLabel}{new string(fill, rightFill)}");
    }

    public static TerminalSpinnerFrame BuildSpinnerFrame(ExecutionState state, int step)
    {
        return state switch
        {
            ExecutionState.Connecting => new TerminalSpinnerFrame(GetFrame(ConnectingFrames, step)),
            ExecutionState.Processing => new TerminalSpinnerFrame(GetFrame(ProcessingFrames, step)),
            ExecutionState.Completed => new TerminalSpinnerFrame("*"),
            ExecutionState.Error => new TerminalSpinnerFrame("x"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(state),
                state,
                "Estado de execucao invalido para spinner.")
        };
    }

    public static string BuildExecutionSuffix(ExecutionState state, int step)
    {
        TerminalStatusBadge badge = state;
        TerminalSpinnerFrame spinnerFrame = BuildSpinnerFrame(state, step);
        return $"{(string)badge} {(string)spinnerFrame}";
    }

    private static string GetFrame(IReadOnlyList<string> frames, int step)
    {
        if (frames.Count == 0)
        {
            throw new ArgumentException(
                "A colecao de frames do spinner nao pode estar vazia.",
                nameof(frames));
        }

        var normalizedStep = (int)((uint)step % (uint)frames.Count);
        return frames[normalizedStep];
    }
}