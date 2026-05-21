using System.Globalization;

namespace ASXRunTerminal.Core;

internal enum AgentBenchmarkSessionStatus
{
    InProgress,
    Completed,
    Failed,
    Cancelled
}

internal readonly record struct AgentBenchmarkSessionResult(
    string SessionId,
    string Objective,
    string WorkspaceRootDirectory,
    string? Model,
    string AutonomyLevel,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset LastEventAtUtc,
    int IterationCount,
    int ValidationCommandCount,
    int FailedValidationCommandCount,
    int ChangeCount,
    AgentBenchmarkSessionStatus Status,
    bool RequiredHumanIntervention,
    IReadOnlyList<string> HumanInterventionReasons)
{
    public bool IsCompleted =>
        Status is AgentBenchmarkSessionStatus.Completed;

    public bool IsAutonomousSuccess =>
        IsCompleted && !RequiredHumanIntervention;
}

internal readonly record struct AgentSuccessBenchmarkReport(
    int TotalSessions,
    int CompletedSessions,
    int AutonomousCompletedSessions,
    int HumanInterventionSessions,
    int FailedSessions,
    int CancelledSessions,
    decimal AutonomousSuccessRatePercent,
    decimal CompletionRatePercent,
    IReadOnlyList<AgentBenchmarkSessionResult> Sessions)
{
    public static AgentSuccessBenchmarkReport Empty => new(
        TotalSessions: 0,
        CompletedSessions: 0,
        AutonomousCompletedSessions: 0,
        HumanInterventionSessions: 0,
        FailedSessions: 0,
        CancelledSessions: 0,
        AutonomousSuccessRatePercent: 0m,
        CompletionRatePercent: 0m,
        Sessions: Array.Empty<AgentBenchmarkSessionResult>());

    public string ToSummary(decimal? minimumAutonomousSuccessRatePercent = null)
    {
        var summary =
            "benchmark_sucesso_agente: " +
            $"sessoes={TotalSessions}; " +
            $"concluidas={CompletedSessions}; " +
            $"autonomas_sem_intervencao={AutonomousCompletedSessions}; " +
            $"com_intervencao={HumanInterventionSessions}; " +
            $"falhas={FailedSessions}; " +
            $"canceladas={CancelledSessions}; " +
            $"taxa_sucesso_autonomo={FormatPercent(AutonomousSuccessRatePercent)}; " +
            $"taxa_conclusao={FormatPercent(CompletionRatePercent)}";

        if (minimumAutonomousSuccessRatePercent is not { } minimum)
        {
            return $"{summary}.";
        }

        var normalizedMinimum = NormalizePercent(minimum);
        var status = AutonomousSuccessRatePercent >= normalizedMinimum
            ? "passed"
            : "failed";
        return
            $"{summary}; " +
            $"minimo={FormatPercent(normalizedMinimum)}; " +
            $"status={status}.";
    }

    internal static decimal NormalizePercent(decimal value)
    {
        return value is >= 0m and <= 1m
            ? decimal.Round(value * 100m, 2, MidpointRounding.AwayFromZero)
            : decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    internal static string FormatPercent(decimal value)
    {
        return $"{NormalizePercent(value).ToString("0.00", CultureInfo.InvariantCulture)}%";
    }
}

internal static class AgentSuccessBenchmark
{
    public static AgentSuccessBenchmarkReport Evaluate(
        IReadOnlyList<AgentAuditEntry> auditEntries)
    {
        if (auditEntries is null || auditEntries.Count == 0)
        {
            return AgentSuccessBenchmarkReport.Empty;
        }

        var sessions = auditEntries
            .Where(static entry =>
                string.Equals(entry.Command, "agent", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(entry.SessionId))
            .GroupBy(static entry => entry.SessionId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(static group => BuildSessionResult(group.ToArray()))
            .OrderByDescending(static session => session.LastEventAtUtc)
            .ThenBy(static session => session.SessionId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (sessions.Length == 0)
        {
            return AgentSuccessBenchmarkReport.Empty;
        }

        var completedSessions = sessions.Count(static session => session.IsCompleted);
        var autonomousCompletedSessions = sessions.Count(static session => session.IsAutonomousSuccess);
        var humanInterventionSessions = sessions.Count(static session => session.RequiredHumanIntervention);
        var failedSessions = sessions.Count(static session => session.Status is AgentBenchmarkSessionStatus.Failed);
        var cancelledSessions = sessions.Count(static session => session.Status is AgentBenchmarkSessionStatus.Cancelled);

        return new AgentSuccessBenchmarkReport(
            TotalSessions: sessions.Length,
            CompletedSessions: completedSessions,
            AutonomousCompletedSessions: autonomousCompletedSessions,
            HumanInterventionSessions: humanInterventionSessions,
            FailedSessions: failedSessions,
            CancelledSessions: cancelledSessions,
            AutonomousSuccessRatePercent: CalculateRate(autonomousCompletedSessions, sessions.Length),
            CompletionRatePercent: CalculateRate(completedSessions, sessions.Length),
            Sessions: sessions);
    }

    private static AgentBenchmarkSessionResult BuildSessionResult(
        IReadOnlyList<AgentAuditEntry> entries)
    {
        var orderedEntries = entries
            .OrderBy(static entry => entry.TimestampUtc)
            .ThenBy(static entry => entry.SessionSequence)
            .ToArray();
        var firstEntry = orderedEntries[0];
        var lastEntry = orderedEntries[^1];
        var status = ResolveSessionStatus(orderedEntries);
        var interventionReasons = ResolveHumanInterventionReasons(orderedEntries);
        var validationCommandCount = orderedEntries.Count(IsValidationCommandEntry);
        var failedValidationCommandCount = orderedEntries.Count(static entry =>
            IsValidationCommandEntry(entry)
            && (entry.IsSuccess == false
                || IsAnyStatus(entry.Status, "failed", "timed-out", "cancelled")));

        return new AgentBenchmarkSessionResult(
            SessionId: firstEntry.SessionId.Trim(),
            Objective: firstEntry.Objective.Trim(),
            WorkspaceRootDirectory: firstEntry.WorkspaceRootDirectory.Trim(),
            Model: string.IsNullOrWhiteSpace(firstEntry.Model) ? null : firstEntry.Model.Trim(),
            AutonomyLevel: firstEntry.AutonomyLevel.Trim(),
            StartedAtUtc: firstEntry.TimestampUtc,
            LastEventAtUtc: lastEntry.TimestampUtc,
            IterationCount: orderedEntries.Max(static entry => Math.Max(0, entry.Iteration)),
            ValidationCommandCount: validationCommandCount,
            FailedValidationCommandCount: failedValidationCommandCount,
            ChangeCount: CountDistinctChanges(orderedEntries),
            Status: status,
            RequiredHumanIntervention: interventionReasons.Count > 0,
            HumanInterventionReasons: interventionReasons);
    }

    private static AgentBenchmarkSessionStatus ResolveSessionStatus(
        IReadOnlyList<AgentAuditEntry> orderedEntries)
    {
        var sessionEvents = orderedEntries
            .Where(static entry => string.Equals(entry.Phase, "session", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var finalSessionEvent = sessionEvents.Length == 0
            ? null
            : (AgentAuditEntry?)sessionEvents[^1];

        if (finalSessionEvent is { } finalEvent)
        {
            if (IsAnyStatus(finalEvent.Status, "completed"))
            {
                return AgentBenchmarkSessionStatus.Completed;
            }

            if (IsAnyStatus(finalEvent.Status, "cancelled"))
            {
                return AgentBenchmarkSessionStatus.Cancelled;
            }

            if (IsAnyStatus(finalEvent.Status, "failed", "timed-out"))
            {
                return AgentBenchmarkSessionStatus.Failed;
            }
        }

        if (orderedEntries.Any(static entry =>
            entry.IsCancelled || IsAnyStatus(entry.Status, "cancelled")))
        {
            return AgentBenchmarkSessionStatus.Cancelled;
        }

        if (sessionEvents.Any(static entry => IsAnyStatus(entry.Status, "failed", "timed-out")))
        {
            return AgentBenchmarkSessionStatus.Failed;
        }

        return AgentBenchmarkSessionStatus.InProgress;
    }

    private static IReadOnlyList<string> ResolveHumanInterventionReasons(
        IReadOnlyList<AgentAuditEntry> orderedEntries)
    {
        var reasons = new List<string>();

        if (orderedEntries.Any(static entry => entry.HasExplicitSensitiveOperationApproval))
        {
            reasons.Add("aprovacao sensivel/destrutiva explicita");
        }

        if (orderedEntries.Any(static entry =>
            IsAnyText(entry.AutonomyLevel, "assistido", "assisted")))
        {
            reasons.Add("nivel assistido exige supervisao humana");
        }

        if (orderedEntries.Any(static entry =>
            string.Equals(entry.Phase, "session", StringComparison.OrdinalIgnoreCase)
            && IsAnyStatus(entry.Status, "resumed")))
        {
            reasons.Add("sessao retomada manualmente");
        }

        if (orderedEntries.Any(IsHumanInterventionEvent))
        {
            reasons.Add("evento de aprovacao, bloqueio ou validacao manual");
        }

        if (orderedEntries.Any(static entry =>
            entry.IsCancelled || IsAnyStatus(entry.Status, "cancelled")))
        {
            reasons.Add("cancelamento solicitado");
        }

        return reasons
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsHumanInterventionEvent(AgentAuditEntry entry)
    {
        if (string.Equals(
            entry.CommandLine,
            "agent-sensitive-operation-approval",
            StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IsAnyStatus(entry.Status, "blocked")
            && IsManualInterventionText(entry.Summary, entry.Detail))
        {
            return true;
        }

        return IsManualInterventionText(entry.Summary, entry.Detail)
            && !IsAutomaticRefinementEvent(entry);
    }

    private static bool IsAutomaticRefinementEvent(AgentAuditEntry entry)
    {
        return IsAnyText(entry.Phase, "verify", "auto-review", "refine")
            && IsAnyStatus(entry.Status, "refine", "forced-refine");
    }

    private static bool IsManualInterventionText(params string?[] values)
    {
        return values.Any(static value =>
            IsAnyText(
                value,
                "aprovacao manual",
                "aprovacao explicita",
                "supervisao humana",
                "intervencao humana",
                "validacao manual",
                "reexecute",
                "--approve-sensitive"));
    }

    private static bool IsValidationCommandEntry(AgentAuditEntry entry)
    {
        return string.Equals(entry.EventType, "command", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(entry.Phase, "validation", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Phase, "auto-correct-validation", StringComparison.OrdinalIgnoreCase));
    }

    private static int CountDistinctChanges(IReadOnlyList<AgentAuditEntry> orderedEntries)
    {
        return orderedEntries
            .SelectMany(static entry => entry.Changes ?? Array.Empty<AgentAuditChangeEntry>())
            .Where(static change => !string.IsNullOrWhiteSpace(change.Path))
            .Select(static change => $"{change.Kind.Trim()}:{change.Path.Trim()}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    private static decimal CalculateRate(int numerator, int denominator)
    {
        if (denominator <= 0)
        {
            return 0m;
        }

        return decimal.Round(
            numerator / (decimal)denominator * 100m,
            2,
            MidpointRounding.AwayFromZero);
    }

    private static bool IsAnyStatus(string value, params string[] expectedStatuses)
    {
        return expectedStatuses.Any(expected =>
            string.Equals(value, expected, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAnyText(string? value, params string[] fragments)
    {
        return !string.IsNullOrWhiteSpace(value)
            && fragments.Any(fragment =>
                value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}
