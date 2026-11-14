using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Tests.Coordination;

public sealed class WorkPackageWorkthroughTests
{
    private static readonly TenantId Tenant = new(Guid.NewGuid());
    private static readonly ProjectId Project = new(Guid.NewGuid());
    private static readonly WorkPackageId Package = WorkPackageId.New();
    private static readonly PrincipalId Agent = new(Guid.NewGuid());

    [Fact]
    public void Succeeded_requires_whatSucceeded()
    {
        Assert.Throws<ArgumentException>(() =>
            WorkPackageWorkthrough.Record(Tenant, Project, Package, Agent, WorkthroughKind.Succeeded,
                "Done", whatSucceeded: null));

        var ok = WorkPackageWorkthrough.Record(Tenant, Project, Package, Agent, WorkthroughKind.Succeeded,
            "Place-order API ready", "Validated empty cake type returns 400; valid order returns 201.");
        Assert.Equal(WorkthroughKind.Succeeded, ok.Kind);
        Assert.Null(ok.BlockedReason);
        Assert.Contains("201", ok.WhatSucceeded);
    }

    [Fact]
    public void Blocked_requires_reason_and_allows_empty_partial_progress()
    {
        Assert.Throws<ArgumentException>(() =>
            WorkPackageWorkthrough.Record(Tenant, Project, Package, Agent, WorkthroughKind.Blocked,
                "Stopped", blockedReason: null));

        var blocked = WorkPackageWorkthrough.Record(Tenant, Project, Package, Agent, WorkthroughKind.Blocked,
            "Cannot finish list-orders",
            whatSucceeded: null,
            blockedReason: "Missing customer auth header contract; waiting for K-07 decision.",
            steps: ["Opened list endpoint", "Hit 401 without guidance"]);
        Assert.Equal(WorkthroughKind.Blocked, blocked.Kind);
        Assert.Equal(string.Empty, blocked.WhatSucceeded);
        Assert.Contains("K-07", blocked.BlockedReason);
        Assert.Equal(2, blocked.Steps.Count);
    }

    [Fact]
    public void Blank_steps_are_dropped_and_steps_are_trimmed()
    {
        var recorded = WorkPackageWorkthrough.Record(Tenant, Project, Package, Agent, WorkthroughKind.Succeeded,
            "Done", "It works.", steps: ["  first  ", "", "   ", "second"]);

        Assert.Equal(["first", "second"], recorded.Steps);
    }

    [Fact]
    public void Too_many_or_too_long_steps_are_refused_instead_of_silently_cut()
    {
        var atLimit = Enumerable.Range(1, WorkPackageWorkthrough.MaxSteps).Select(n => $"step {n}").ToArray();
        Assert.Equal(WorkPackageWorkthrough.MaxSteps, WorkPackageWorkthrough.Record(
            Tenant, Project, Package, Agent, WorkthroughKind.Succeeded, "Done", "ok", steps: atLimit).Steps.Count);

        var tooMany = Assert.Throws<ArgumentException>(() => WorkPackageWorkthrough.Record(
            Tenant, Project, Package, Agent, WorkthroughKind.Succeeded, "Done", "ok",
            steps: [.. atLimit, "one more"]));
        Assert.Contains("at most", tooMany.Message);

        var tooLong = Assert.Throws<ArgumentException>(() => WorkPackageWorkthrough.Record(
            Tenant, Project, Package, Agent, WorkthroughKind.Succeeded, "Done", "ok",
            steps: ["fine", new string('s', WorkPackageWorkthrough.MaxStepLength + 1)]));
        Assert.Contains("Step 2", tooLong.Message);

        // Exactly the limit is allowed.
        Assert.Single(WorkPackageWorkthrough.Record(
            Tenant, Project, Package, Agent, WorkthroughKind.Succeeded, "Done", "ok",
            steps: [new string('s', WorkPackageWorkthrough.MaxStepLength)]).Steps);
    }
}
