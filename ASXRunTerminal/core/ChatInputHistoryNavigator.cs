namespace ASXRunTerminal.Core;

internal sealed class ChatInputHistoryNavigator
{
    private readonly List<string> _historyEntries;
    private readonly List<int> _searchMatches = [];

    private string _draftInput = string.Empty;
    private int _historyIndex = -1;

    private string _searchSnapshotInput = string.Empty;
    private int _searchSnapshotHistoryIndex = -1;
    private int _searchMatchIndex = -1;

    public ChatInputHistoryNavigator(IEnumerable<string> historyPrompts)
    {
        ArgumentNullException.ThrowIfNull(historyPrompts);

        _historyEntries = historyPrompts
            .Where(static prompt => !string.IsNullOrWhiteSpace(prompt))
            .Select(static prompt => prompt.Trim())
            .ToList();
    }

    public string CurrentInput { get; private set; } = string.Empty;

    public bool IsIncrementalSearchActive { get; private set; }

    public string SearchQuery { get; private set; } = string.Empty;

    public bool HasIncrementalSearchMatch => _searchMatchIndex >= 0 && _searchMatchIndex < _searchMatches.Count;

    public void AppendInputCharacter(char character)
    {
        if (char.IsControl(character))
        {
            return;
        }

        EnsureDraftMode();
        CurrentInput += character;
        _draftInput = CurrentInput;
    }

    public void RemoveLastInputCharacter()
    {
        if (CurrentInput.Length == 0)
        {
            return;
        }

        EnsureDraftMode();
        CurrentInput = CurrentInput[..^1];
        _draftInput = CurrentInput;
    }

    public void ClearInput()
    {
        EnsureDraftMode();
        CurrentInput = string.Empty;
        _draftInput = CurrentInput;
    }

    public void ReplaceInput(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        EnsureDraftMode();
        CurrentInput = input;
        _draftInput = CurrentInput;
    }

    public void MovePrevious()
    {
        if (_historyEntries.Count == 0)
        {
            return;
        }

        if (_historyIndex < 0)
        {
            _draftInput = CurrentInput;
            _historyIndex = 0;
            CurrentInput = _historyEntries[_historyIndex];
            return;
        }

        if (_historyIndex >= _historyEntries.Count - 1)
        {
            return;
        }

        _historyIndex++;
        CurrentInput = _historyEntries[_historyIndex];
    }

    public void MoveNext()
    {
        if (_historyIndex < 0)
        {
            return;
        }

        _historyIndex--;
        CurrentInput = _historyIndex >= 0
            ? _historyEntries[_historyIndex]
            : _draftInput;
    }

    public void RememberPrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        _historyEntries.Insert(0, prompt.Trim());
        _historyIndex = -1;
        _draftInput = string.Empty;
        CurrentInput = string.Empty;
    }

    public void StartIncrementalSearch()
    {
        if (IsIncrementalSearchActive)
        {
            CycleIncrementalSearch(olderMatch: true);
            return;
        }

        IsIncrementalSearchActive = true;
        SearchQuery = string.Empty;
        _searchSnapshotInput = CurrentInput;
        _searchSnapshotHistoryIndex = _historyIndex;
        _searchMatchIndex = -1;
        RefreshSearchMatches(selectMostRecentMatch: false);
    }

    public void AppendSearchCharacter(char character)
    {
        if (!IsIncrementalSearchActive || char.IsControl(character))
        {
            return;
        }

        SearchQuery += character;
        RefreshSearchMatches(selectMostRecentMatch: true);
    }

    public void RemoveSearchCharacter()
    {
        if (!IsIncrementalSearchActive)
        {
            return;
        }

        if (SearchQuery.Length > 0)
        {
            SearchQuery = SearchQuery[..^1];
        }

        RefreshSearchMatches(selectMostRecentMatch: true);
    }

    public void CycleIncrementalSearch(bool olderMatch)
    {
        if (!IsIncrementalSearchActive || _searchMatches.Count == 0)
        {
            return;
        }

        var delta = olderMatch ? 1 : -1;
        _searchMatchIndex = (_searchMatchIndex + delta + _searchMatches.Count) % _searchMatches.Count;
        ApplyActiveSearchMatch();
    }

    public void AcceptIncrementalSearch()
    {
        if (!IsIncrementalSearchActive)
        {
            return;
        }

        IsIncrementalSearchActive = false;
        SearchQuery = string.Empty;
        _searchMatchIndex = -1;
        _searchMatches.Clear();
    }

    public void CancelIncrementalSearch()
    {
        if (!IsIncrementalSearchActive)
        {
            return;
        }

        CurrentInput = _searchSnapshotInput;
        _historyIndex = _searchSnapshotHistoryIndex;
        if (_historyIndex < 0)
        {
            _draftInput = CurrentInput;
        }

        IsIncrementalSearchActive = false;
        SearchQuery = string.Empty;
        _searchMatchIndex = -1;
        _searchMatches.Clear();
    }

    private void EnsureDraftMode()
    {
        if (_historyIndex < 0)
        {
            return;
        }

        _historyIndex = -1;
        _draftInput = CurrentInput;
    }

    private void RefreshSearchMatches(bool selectMostRecentMatch)
    {
        _searchMatches.Clear();

        if (_historyEntries.Count == 0)
        {
            RestoreSearchSnapshotWhenNoMatch();
            return;
        }

        if (SearchQuery.Length == 0)
        {
            for (var index = 0; index < _historyEntries.Count; index++)
            {
                _searchMatches.Add(index);
            }
        }
        else
        {
            for (var index = 0; index < _historyEntries.Count; index++)
            {
                if (_historyEntries[index].Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                {
                    _searchMatches.Add(index);
                }
            }
        }

        if (_searchMatches.Count == 0)
        {
            RestoreSearchSnapshotWhenNoMatch();
            return;
        }

        if (selectMostRecentMatch || _searchMatchIndex < 0 || _searchMatchIndex >= _searchMatches.Count)
        {
            _searchMatchIndex = 0;
        }

        ApplyActiveSearchMatch();
    }

    private void ApplyActiveSearchMatch()
    {
        if (_searchMatchIndex < 0 || _searchMatchIndex >= _searchMatches.Count)
        {
            return;
        }

        _historyIndex = _searchMatches[_searchMatchIndex];
        CurrentInput = _historyEntries[_historyIndex];
    }

    private void RestoreSearchSnapshotWhenNoMatch()
    {
        _historyIndex = _searchSnapshotHistoryIndex;
        CurrentInput = _searchSnapshotInput;
        _searchMatchIndex = -1;
    }
}
