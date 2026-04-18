namespace ASXRunTerminal.Core;

internal readonly record struct WorkspacePatchAuditChangeEntry(
    WorkspacePatchChangeKind Kind,
    string Path,
    string ResolvedPath,
    bool HasChanges,
    string UnifiedDiff)
{
    public static implicit operator WorkspacePatchAuditChangeEntry(WorkspacePatchFileResult fileResult)
    {
        return new WorkspacePatchAuditChangeEntry(
            Kind: fileResult.Kind,
            Path: fileResult.Path,
            ResolvedPath: fileResult.ResolvedPath,
            HasChanges: fileResult.HasChanges,
            UnifiedDiff: fileResult.UnifiedDiff);
    }
}

internal readonly record struct WorkspacePatchAuditEntry(
    DateTimeOffset TimestampUtc,
    string SessionId,
    long SessionSequence,
    string Command,
    string WorkspaceRootDirectory,
    string PatchRequestFilePath,
    bool IsPreviewOnly,
    int PlannedChangeCount,
    int AppliedChangeCount,
    int SkippedChangeCount,
    string UnifiedDiff,
    IReadOnlyList<WorkspacePatchAuditChangeEntry> Files)
{
    public static WorkspacePatchAuditEntry FromPatchResult(
        string sessionId,
        long sessionSequence,
        string workspaceRootDirectory,
        string patchRequestFilePath,
        WorkspacePatchResult patchResult,
        DateTimeOffset? timestampUtc = null)
    {
        ArgumentNullException.ThrowIfNull(patchResult.Files);

        return new WorkspacePatchAuditEntry(
            TimestampUtc: timestampUtc ?? DateTimeOffset.UtcNow,
            SessionId: sessionId,
            SessionSequence: sessionSequence,
            Command: "patch",
            WorkspaceRootDirectory: workspaceRootDirectory,
            PatchRequestFilePath: patchRequestFilePath,
            IsPreviewOnly: patchResult.IsPreviewOnly,
            PlannedChangeCount: patchResult.PlannedChangeCount,
            AppliedChangeCount: patchResult.AppliedChangeCount,
            SkippedChangeCount: patchResult.SkippedChangeCount,
            UnifiedDiff: patchResult.UnifiedDiff,
            Files: patchResult.Files
                .Select(static fileResult => (WorkspacePatchAuditChangeEntry)fileResult)
                .ToArray());
    }
}
