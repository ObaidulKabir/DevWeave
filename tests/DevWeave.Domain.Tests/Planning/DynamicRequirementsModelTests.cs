using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Tests.Planning;

public sealed class DynamicRequirementsModelTests
{
    [Fact]
    public void Added_split_merge_and_withdrawal_have_unambiguous_revision_cardinality()
    {
        var actor = PrincipalId.New();
        var a = KnowledgeRevisionId.New();
        var b = KnowledgeRevisionId.New();
        var c = KnowledgeRevisionId.New();

        _ = Change(RequirementChangeKind.Added, [], [a], actor);
        _ = Change(RequirementChangeKind.Split, [a], [b, c], actor);
        _ = Change(RequirementChangeKind.Merged, [a, b], [c], actor);
        _ = Change(RequirementChangeKind.Withdrawn, [a], [], actor);

        Assert.Throws<ArgumentException>(() =>
            Change(RequirementChangeKind.Added, [a], [b], actor));
        Assert.Throws<ArgumentException>(() =>
            Change(RequirementChangeKind.Split, [a], [b], actor));
        Assert.Throws<ArgumentException>(() =>
            Change(RequirementChangeKind.Merged, [a], [b], actor));
        Assert.Throws<ArgumentException>(() =>
            Change(RequirementChangeKind.Withdrawn, [a], [b], actor));
    }

    [Theory]
    [InlineData(RequirementChangeKind.Clarified)]
    [InlineData(RequirementChangeKind.ScopeExpanded)]
    [InlineData(RequirementChangeKind.ScopeReduced)]
    [InlineData(RequirementChangeKind.ThresholdTightened)]
    [InlineData(RequirementChangeKind.ThresholdRelaxed)]
    [InlineData(RequirementChangeKind.Replaced)]
    [InlineData(RequirementChangeKind.Reinstated)]
    public void One_to_one_requirement_changes_always_create_a_new_revision(
        RequirementChangeKind kind)
    {
        var previous = KnowledgeRevisionId.New();
        var result = KnowledgeRevisionId.New();

        var change = Change(kind, [previous], [result], PrincipalId.New());

        Assert.Equal(previous, Assert.Single(change.PreviousRevisions));
        Assert.Equal(result, Assert.Single(change.ResultRevisions));
        Assert.Throws<ArgumentException>(() =>
            Change(kind, [previous], [previous], PrincipalId.New()));
    }

    [Fact]
    public void Revision_lists_are_unique_and_nonempty()
    {
        var revision = KnowledgeRevisionId.New();

        Assert.Throws<ArgumentException>(() =>
            Change(RequirementChangeKind.Split, [revision], [revision, KnowledgeRevisionId.New()],
                PrincipalId.New()));

        var duplicate = KnowledgeRevisionId.New();
        Assert.Throws<ArgumentException>(() =>
            Change(RequirementChangeKind.Split, [revision], [duplicate, duplicate], PrincipalId.New()));
        Assert.Throws<ArgumentException>(() =>
            Change(RequirementChangeKind.Added, [], [new KnowledgeRevisionId(Guid.Empty)], PrincipalId.New()));
    }

    [Fact]
    public void Agent_can_execute_only_when_every_independent_precondition_is_true()
    {
        // Exhaust every combination so adding an OR where the model requires AND cannot pass unnoticed.
        for (var mask = 0; mask < 256; mask++)
        {
            var flags = Enumerable.Range(0, 8).Select(bit => (mask & (1 << bit)) != 0).ToArray();
            var snapshot = new ExecutionEligibilitySnapshot(
                flags[0], flags[1], flags[2], flags[3],
                flags[4], flags[5], flags[6], flags[7]);

            var decision = snapshot.Evaluate();

            Assert.Equal(flags.All(value => value), decision.Allowed);
            Assert.Equal(flags.Count(value => !value), decision.FailureCodes.Count);
        }
    }

    [Fact]
    public void Eligibility_explains_stale_context_and_baseline_independently()
    {
        var decision = new ExecutionEligibilitySnapshot(
            Authorized: true,
            BaselineCurrentOrRebased: false,
            DependenciesSatisfied: true,
            LeaseValid: true,
            FenceValid: true,
            ContextCurrent: false,
            NoUnresolvedSemanticConflict: true,
            KillSwitchAllows: true).Evaluate();

        Assert.False(decision.Allowed);
        Assert.Equal(["baseline-stale", "context-stale"], decision.FailureCodes);
    }

    [Fact]
    public void Semantic_conflict_preserves_both_revisions_until_one_resolution_is_recorded()
    {
        var left = KnowledgeRevisionId.New();
        var right = KnowledgeRevisionId.New();
        var conflict = SemanticConflict.Record(
            TenantId.New(),
            ProjectId.New(),
            left,
            right,
            "Competing architecture decisions",
            PrincipalId.New());

        Assert.Equal(left, conflict.LeftRevisionId);
        Assert.Equal(right, conflict.RightRevisionId);
        Assert.False(conflict.IsResolved);

        var resolver = PrincipalId.New();
        conflict.Resolve(SemanticConflictResolutionKind.Combine, resolver,
            "Combine the compatible parts and issue a new revision");

        Assert.True(conflict.IsResolved);
        Assert.Equal(SemanticConflictResolutionKind.Combine, conflict.Resolution);
        Assert.Equal(resolver, conflict.ResolvedBy);
        Assert.Throws<InvalidOperationException>(() =>
            conflict.Resolve(SemanticConflictResolutionKind.SelectLeft, resolver, "Already resolved"));
    }

    private static RequirementChangeDescriptor Change(
        RequirementChangeKind kind,
        IReadOnlyList<KnowledgeRevisionId> previous,
        IReadOnlyList<KnowledgeRevisionId> result,
        PrincipalId actor) =>
        new(kind, previous, result, actor, "Approved requirement change",
            DateTimeOffset.Parse("2026-11-21T00:00:00Z"), $"change-{Guid.NewGuid():N}");
}
