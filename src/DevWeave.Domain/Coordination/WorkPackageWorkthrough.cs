using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Coordination;

public enum WorkthroughKind
{
    Succeeded,
    Blocked
}

/// <summary>
/// Agent-authored record of progress on a work package: what succeeded, or why work stopped.
/// This is a claim about progress, not verification or approval.
/// </summary>
public sealed class WorkPackageWorkthrough
{
    public WorkthroughId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public WorkPackageId WorkPackageId { get; }
    public PrincipalId RecordedBy { get; }
    public WorkthroughKind Kind { get; }
    public string Outcome { get; }
    public string WhatSucceeded { get; }
    public string? BlockedReason { get; }
    public IReadOnlyList<string> Steps { get; }
    public Guid? ChangeSetId { get; }
    public DateTimeOffset RecordedAt { get; }

    private WorkPackageWorkthrough(
        WorkthroughId id,
        TenantId tenantId,
        ProjectId projectId,
        WorkPackageId workPackageId,
        PrincipalId recordedBy,
        WorkthroughKind kind,
        string outcome,
        string whatSucceeded,
        string? blockedReason,
        IReadOnlyList<string> steps,
        Guid? changeSetId,
        DateTimeOffset recordedAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        WorkPackageId = workPackageId;
        RecordedBy = recordedBy;
        Kind = kind;
        Outcome = outcome;
        WhatSucceeded = whatSucceeded;
        BlockedReason = blockedReason;
        Steps = steps;
        ChangeSetId = changeSetId;
        RecordedAt = recordedAt;
    }

    public static WorkPackageWorkthrough Record(
        TenantId tenantId,
        ProjectId projectId,
        WorkPackageId workPackageId,
        PrincipalId recordedBy,
        WorkthroughKind kind,
        string outcome,
        string? whatSucceeded = null,
        string? blockedReason = null,
        IReadOnlyList<string>? steps = null,
        Guid? changeSetId = null)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        var trimmedOutcome = RequireText(outcome, nameof(outcome), 300);
        var cleanedSteps = CleanSteps(steps);

        string trimmedSucceeded;
        string? trimmedBlocked;
        if (kind == WorkthroughKind.Succeeded)
        {
            trimmedSucceeded = RequireText(whatSucceeded, nameof(whatSucceeded), 4000);
            trimmedBlocked = null;
        }
        else
        {
            trimmedBlocked = RequireText(blockedReason, nameof(blockedReason), 2000);
            trimmedSucceeded = string.IsNullOrWhiteSpace(whatSucceeded)
                ? string.Empty
                : RequireText(whatSucceeded, nameof(whatSucceeded), 4000);
        }

        return new WorkPackageWorkthrough(
            WorkthroughId.New(),
            tenantId,
            projectId,
            workPackageId,
            recordedBy,
            kind,
            trimmedOutcome,
            trimmedSucceeded,
            trimmedBlocked,
            cleanedSteps,
            changeSetId,
            DateTimeOffset.UtcNow);
    }

    public const int MaxSteps = 20;
    public const int MaxStepLength = 300;

    /// <summary>Blank steps are dropped; too many or too long steps are refused, never silently cut.</summary>
    private static IReadOnlyList<string> CleanSteps(IReadOnlyList<string>? steps)
    {
        var cleaned = (steps ?? Array.Empty<string>())
            .Select(s => s?.Trim() ?? string.Empty)
            .Where(s => s.Length > 0)
            .ToArray();
        if (cleaned.Length > MaxSteps)
            throw new ArgumentException($"A workthrough can list at most {MaxSteps} steps.", nameof(steps));
        var tooLong = Array.FindIndex(cleaned, s => s.Length > MaxStepLength);
        if (tooLong >= 0)
            throw new ArgumentException($"Step {tooLong + 1} exceeds {MaxStepLength} characters.", nameof(steps));
        return cleaned;
    }

    private static string RequireText(string? value, string name, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{name} is required.", name);
        var trimmed = value.Trim();
        if (trimmed.Length > max)
            throw new ArgumentException($"{name} cannot exceed {max} characters.", name);
        return trimmed;
    }
}
