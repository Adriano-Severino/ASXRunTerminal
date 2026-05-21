using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class AgentSuccessBenchmarkTests
{
    [Fact]
    public void Evaluate_WhenAuditHasCompletedFailedAndInterventionSessions_ComputesAutonomousSuccessRate()
    {
        var entries = new[]
        {
            CreateAuditEntry(
                sessionId: "session-a",
                sessionSequence: 1,
                timestampUtc: new DateTimeOffset(2026, 4, 29, 10, 0, 0, TimeSpan.Zero),
                phase: "session",
                eventType: "lifecycle",
                status: "started"),
            CreateAuditEntry(
                sessionId: "session-a",
                sessionSequence: 2,
                timestampUtc: new DateTimeOffset(2026, 4, 29, 10, 1, 0, TimeSpan.Zero),
                iteration: 1,
                phase: "validation",
                eventType: "command",
                status: "passed",
                isSuccess: true,
                changes:
                [
                    new AgentAuditChangeEntry(
                        Path: "src/App.cs",
                        Kind: "edit",
                        IsDestructive: false)
                ]),
            CreateAuditEntry(
                sessionId: "session-a",
                sessionSequence: 3,
                timestampUtc: new DateTimeOffset(2026, 4, 29, 10, 2, 0, TimeSpan.Zero),
                iteration: 1,
                phase: "session",
                eventType: "decision",
                status: "completed"),
            CreateAuditEntry(
                sessionId: "session-b",
                sessionSequence: 4,
                timestampUtc: new DateTimeOffset(2026, 4, 29, 11, 0, 0, TimeSpan.Zero),
                phase: "session",
                eventType: "lifecycle",
                status: "started",
                hasExplicitSensitiveOperationApproval: true),
            CreateAuditEntry(
                sessionId: "session-b",
                sessionSequence: 5,
                timestampUtc: new DateTimeOffset(2026, 4, 29, 11, 1, 0, TimeSpan.Zero),
                iteration: 1,
                phase: "session",
                eventType: "decision",
                status: "completed",
                hasExplicitSensitiveOperationApproval: true),
            CreateAuditEntry(
                sessionId: "session-c",
                sessionSequence: 6,
                timestampUtc: new DateTimeOffset(2026, 4, 29, 12, 0, 0, TimeSpan.Zero),
                phase: "session",
                eventType: "lifecycle",
                status: "started"),
            CreateAuditEntry(
                sessionId: "session-c",
                sessionSequence: 7,
                timestampUtc: new DateTimeOffset(2026, 4, 29, 12, 1, 0, TimeSpan.Zero),
                iteration: 1,
                phase: "session",
                eventType: "decision",
                status: "failed")
        };

        var report = AgentSuccessBenchmark.Evaluate(entries);

        Assert.Equal(3, report.TotalSessions);
        Assert.Equal(2, report.CompletedSessions);
        Assert.Equal(1, report.AutonomousCompletedSessions);
        Assert.Equal(1, report.HumanInterventionSessions);
        Assert.Equal(1, report.FailedSessions);
        Assert.Equal(33.33m, report.AutonomousSuccessRatePercent);
        Assert.Equal(66.67m, report.CompletionRatePercent);

        var autonomousSession = Assert.Single(
            report.Sessions,
            static session => string.Equals(session.SessionId, "session-a", StringComparison.Ordinal));
        Assert.True(autonomousSession.IsAutonomousSuccess);
        Assert.Equal(1, autonomousSession.ValidationCommandCount);
        Assert.Equal(1, autonomousSession.ChangeCount);

        var interventionSession = Assert.Single(
            report.Sessions,
            static session => string.Equals(session.SessionId, "session-b", StringComparison.Ordinal));
        Assert.True(interventionSession.RequiredHumanIntervention);
        Assert.Contains(
            "aprovacao sensivel/destrutiva explicita",
            interventionSession.HumanInterventionReasons);
    }

    [Fact]
    public void Evaluate_WhenSessionWasResumed_CountsHumanIntervention()
    {
        var entries = new[]
        {
            CreateAuditEntry(
                sessionId: "session-resumed",
                sessionSequence: 1,
                timestampUtc: new DateTimeOffset(2026, 4, 29, 10, 0, 0, TimeSpan.Zero),
                phase: "session",
                eventType: "lifecycle",
                status: "resumed"),
            CreateAuditEntry(
                sessionId: "session-resumed",
                sessionSequence: 2,
                timestampUtc: new DateTimeOffset(2026, 4, 29, 10, 1, 0, TimeSpan.Zero),
                phase: "session",
                eventType: "decision",
                status: "completed")
        };

        var report = AgentSuccessBenchmark.Evaluate(entries);
        var session = Assert.Single(report.Sessions);

        Assert.True(session.IsCompleted);
        Assert.False(session.IsAutonomousSuccess);
        Assert.Contains("sessao retomada manualmente", session.HumanInterventionReasons);
        Assert.Equal(0m, report.AutonomousSuccessRatePercent);
    }

    [Fact]
    public void ToSummary_WithMinimumSuccessRate_RendersBenchmarkStatus()
    {
        var report = new AgentSuccessBenchmarkReport(
            TotalSessions: 4,
            CompletedSessions: 3,
            AutonomousCompletedSessions: 2,
            HumanInterventionSessions: 1,
            FailedSessions: 1,
            CancelledSessions: 0,
            AutonomousSuccessRatePercent: 50m,
            CompletionRatePercent: 75m,
            Sessions: []);

        var summary = report.ToSummary(minimumAutonomousSuccessRatePercent: 60m);

        Assert.Contains("taxa_sucesso_autonomo=50.00%", summary);
        Assert.Contains("taxa_conclusao=75.00%", summary);
        Assert.Contains("minimo=60.00%", summary);
        Assert.Contains("status=failed", summary);
    }

    private static AgentAuditEntry CreateAuditEntry(
        string sessionId,
        long sessionSequence,
        DateTimeOffset timestampUtc,
        string phase,
        string eventType,
        string status,
        int iteration = 0,
        bool hasExplicitSensitiveOperationApproval = false,
        bool? isSuccess = null,
        IReadOnlyList<AgentAuditChangeEntry>? changes = null)
    {
        return new AgentAuditEntry(
            TimestampUtc: timestampUtc,
            SessionId: sessionId,
            SessionSequence: sessionSequence,
            Command: "agent",
            WorkspaceRootDirectory: "C:/workspace",
            Objective: "implementar tarefa",
            Model: "qwen3.5:4b",
            AutonomyLevel: "autonomo",
            HasExplicitSensitiveOperationApproval: hasExplicitSensitiveOperationApproval,
            Iteration: iteration,
            Phase: phase,
            EventType: eventType,
            Status: status,
            Summary: "Evento auditado.",
            Detail: string.Empty,
            ToolName: string.Equals(eventType, "command", StringComparison.Ordinal) ? "shell" : null,
            CommandLine: string.Equals(eventType, "command", StringComparison.Ordinal)
                ? "dotnet test ASXRunTerminal.slnx --nologo"
                : null,
            ExitCode: isSuccess is null ? null : isSuccess.Value ? 0 : 1,
            IsSuccess: isSuccess,
            Duration: TimeSpan.FromMilliseconds(10),
            IsTimedOut: false,
            IsCancelled: false,
            Changes: changes ?? Array.Empty<AgentAuditChangeEntry>());
    }
}
