namespace ASXRunTerminal.Core;

internal sealed class ChatAutocompleteEngine
{
    private const string CliName = "asxrun";
    private const string ModelFlag = "--model";

    private readonly IReadOnlyList<string> _interactiveCommandCandidates;
    private readonly IReadOnlyList<string> _cliCommandCandidates;
    private readonly IReadOnlyList<string> _optionCandidates;
    private readonly Func<IReadOnlyList<string>> _modelNameProvider;

    private IReadOnlyList<string>? _cachedModelNames;
    private CompletionSession? _activeSession;

    public ChatAutocompleteEngine(
        IEnumerable<string> interactiveCommandCandidates,
        IEnumerable<string> cliCommandCandidates,
        IEnumerable<string> optionCandidates,
        Func<IReadOnlyList<string>> modelNameProvider)
    {
        ArgumentNullException.ThrowIfNull(interactiveCommandCandidates);
        ArgumentNullException.ThrowIfNull(cliCommandCandidates);
        ArgumentNullException.ThrowIfNull(optionCandidates);
        ArgumentNullException.ThrowIfNull(modelNameProvider);

        _interactiveCommandCandidates = NormalizeCandidates(interactiveCommandCandidates);
        _cliCommandCandidates = NormalizeCandidates(cliCommandCandidates);
        _optionCandidates = NormalizeCandidates(optionCandidates);
        _modelNameProvider = modelNameProvider;
    }

    public string ApplyNextCompletion(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (_activeSession is CompletionSession activeSession
            && string.Equals(input, activeSession.LastAppliedInput, StringComparison.Ordinal))
        {
            if (activeSession.Candidates.Count == 0)
            {
                Reset();
                return input;
            }

            var nextIndex = (activeSession.CurrentIndex + 1) % activeSession.Candidates.Count;
            var nextValue = activeSession.BaseInput + activeSession.Candidates[nextIndex];
            _activeSession = activeSession with
            {
                CurrentIndex = nextIndex,
                LastAppliedInput = nextValue
            };

            return nextValue;
        }

        var completionContext = BuildCompletionContext(input);
        if (completionContext is null)
        {
            Reset();
            return input;
        }

        var candidates = completionContext.Value.Candidates
            .Where(candidate => candidate.StartsWith(
                completionContext.Value.Prefix,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (candidates.Length == 0)
        {
            Reset();
            return input;
        }

        var completedInput = completionContext.Value.BaseInput + candidates[0];
        _activeSession = new CompletionSession(
            BaseInput: completionContext.Value.BaseInput,
            Candidates: candidates,
            CurrentIndex: 0,
            LastAppliedInput: completedInput);

        return completedInput;
    }

    public void Reset()
    {
        _activeSession = null;
    }

    private CompletionContext? BuildCompletionContext(string input)
    {
        var parsedInput = ParseInput(input);

        if (TryBuildModelCompletionContext(parsedInput, input, out var modelCompletionContext))
        {
            return modelCompletionContext;
        }

        if (parsedInput.CurrentToken.StartsWith("/", StringComparison.Ordinal))
        {
            return new CompletionContext(
                BaseInput: input[..parsedInput.CurrentTokenStart],
                Prefix: parsedInput.CurrentToken,
                Candidates: _interactiveCommandCandidates);
        }

        if (IsCliCommandContext(parsedInput))
        {
            return new CompletionContext(
                BaseInput: input[..parsedInput.CurrentTokenStart],
                Prefix: parsedInput.CurrentToken,
                Candidates: _cliCommandCandidates);
        }

        if (IsCliOptionContext(parsedInput))
        {
            return new CompletionContext(
                BaseInput: input[..parsedInput.CurrentTokenStart],
                Prefix: parsedInput.CurrentToken,
                Candidates: _optionCandidates);
        }

        return null;
    }

    private bool TryBuildModelCompletionContext(
        ParsedInput parsedInput,
        string input,
        out CompletionContext completionContext)
    {
        completionContext = default;

        if (parsedInput.CurrentToken.StartsWith(
            $"{ModelFlag}=",
            StringComparison.OrdinalIgnoreCase))
        {
            var valueStartIndex = parsedInput.CurrentTokenStart + ModelFlag.Length + 1;
            var prefix = parsedInput.CurrentToken[(ModelFlag.Length + 1)..];

            completionContext = new CompletionContext(
                BaseInput: input[..valueStartIndex],
                Prefix: prefix,
                Candidates: GetModelNameCandidates());
            return true;
        }

        if (string.Equals(parsedInput.PreviousToken, ModelFlag, StringComparison.OrdinalIgnoreCase))
        {
            completionContext = new CompletionContext(
                BaseInput: input[..parsedInput.CurrentTokenStart],
                Prefix: parsedInput.CurrentToken,
                Candidates: GetModelNameCandidates());
            return true;
        }

        return false;
    }

    private IReadOnlyList<string> GetModelNameCandidates()
    {
        if (_cachedModelNames is not null)
        {
            return _cachedModelNames;
        }

        try
        {
            _cachedModelNames = NormalizeCandidates(_modelNameProvider());
            return _cachedModelNames;
        }
        catch
        {
            _cachedModelNames = Array.Empty<string>();
            return _cachedModelNames;
        }
    }

    private static bool IsCliCommandContext(ParsedInput parsedInput)
    {
        if (!string.Equals(parsedInput.FirstToken, CliName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return parsedInput.CurrentTokenIndex == 1;
    }

    private static bool IsCliOptionContext(ParsedInput parsedInput)
    {
        if (!string.Equals(parsedInput.FirstToken, CliName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (parsedInput.CurrentTokenIndex < 2)
        {
            return false;
        }

        return string.IsNullOrEmpty(parsedInput.CurrentToken)
            || parsedInput.CurrentToken.StartsWith("--", StringComparison.OrdinalIgnoreCase);
    }

    private static ParsedInput ParseInput(string input)
    {
        var tokens = Tokenize(input);
        var endsWithWhitespace = input.Length > 0 && char.IsWhiteSpace(input[^1]);

        if (tokens.Count == 0)
        {
            return new ParsedInput(
                CurrentToken: string.Empty,
                CurrentTokenStart: 0,
                PreviousToken: null,
                FirstToken: null,
                CurrentTokenIndex: 0);
        }

        if (endsWithWhitespace)
        {
            var previousToken = tokens[^1].Value;
            return new ParsedInput(
                CurrentToken: string.Empty,
                CurrentTokenStart: input.Length,
                PreviousToken: previousToken,
                FirstToken: tokens[0].Value,
                CurrentTokenIndex: tokens.Count);
        }

        var currentToken = tokens[^1];
        var previous = tokens.Count > 1
            ? tokens[^2].Value
            : null;

        return new ParsedInput(
            CurrentToken: currentToken.Value,
            CurrentTokenStart: currentToken.Start,
            PreviousToken: previous,
            FirstToken: tokens[0].Value,
            CurrentTokenIndex: tokens.Count - 1);
    }

    private static List<TokenEntry> Tokenize(string input)
    {
        var entries = new List<TokenEntry>();

        var index = 0;
        while (index < input.Length)
        {
            while (index < input.Length && char.IsWhiteSpace(input[index]))
            {
                index++;
            }

            if (index >= input.Length)
            {
                break;
            }

            var start = index;
            while (index < input.Length && !char.IsWhiteSpace(input[index]))
            {
                index++;
            }

            entries.Add(new TokenEntry(
                Value: input[start..index],
                Start: start));
        }

        return entries;
    }

    private static IReadOnlyList<string> NormalizeCandidates(IEnumerable<string> rawCandidates)
    {
        var distinctCandidates = new List<string>();
        var seenCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawCandidate in rawCandidates)
        {
            if (string.IsNullOrWhiteSpace(rawCandidate))
            {
                continue;
            }

            var candidate = rawCandidate.Trim();
            if (!seenCandidates.Add(candidate))
            {
                continue;
            }

            distinctCandidates.Add(candidate);
        }

        return distinctCandidates;
    }

    private readonly record struct ParsedInput(
        string CurrentToken,
        int CurrentTokenStart,
        string? PreviousToken,
        string? FirstToken,
        int CurrentTokenIndex);

    private readonly record struct TokenEntry(
        string Value,
        int Start);

    private readonly record struct CompletionContext(
        string BaseInput,
        string Prefix,
        IReadOnlyList<string> Candidates);

    private readonly record struct CompletionSession(
        string BaseInput,
        IReadOnlyList<string> Candidates,
        int CurrentIndex,
        string LastAppliedInput);
}
