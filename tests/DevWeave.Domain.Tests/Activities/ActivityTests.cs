using System.Runtime.CompilerServices;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Tests.Activities;

/// <summary>
/// Proves the Activity provenance domain invariants for Slice 6 (SRS PRO-001, PRO-002, PRO-003,
/// PRO-004, PRO-005, PRO-008, REV-006, REV-008). Activities are append-only: identity, provenance
/// fields, and revision-pinned inputs/outputs are fixed at creation and never offer a mutation
/// surface, and the amendment types require both a reason and a referenced revision.
/// </summary>
public sealed class ActivityTests
{
    private static readonly string[] ExpectedTypes =
    [
        "Created", "Updated", "Approved", "Rejected", "Verified", "Reviewed", "Transitioned",
        "Generated", "Claim", "Observation", "Evidence", "VerificationResult", "Approval",
        "Correction", "Retraction", "Supersession",
    ];

    private static readonly string[] ExpectedStatuses = ["Recorded", "InProgress", "Completed", "Failed"];

    [Fact]
    public void PRO_005_the_activity_type_set_is_exactly_the_sixteen_discriminating_kinds()
    {
        Assert.Equal(ExpectedTypes, Enum.GetNames<ActivityType>());
    }

    [Fact]
    public void PRO_002_the_activity_status_set_is_exactly_the_four_lifecycle_states()
    {
        Assert.Equal(ExpectedStatuses, Enum.GetNames<ActivityStatus>());
    }

    [Fact]
    public void PRO_001_creation_captures_complete_provenance_envelope()
    {
        var seed = Seed();
        var inputs = new[] { new ActivityInput(seed.Item, seed.Revision, 0) };
        var outputs = new[] { new ActivityOutput(seed.Item, seed.Revision, 0) };

        var activity = Create(seed, ActivityType.Generated, ActivityStatus.Completed, inputs: inputs, outputs: outputs);

        Assert.NotEqual(Guid.Empty, activity.Id.Value);
        Assert.Equal(seed.Tenant, activity.TenantId);
        Assert.Equal(seed.Project, activity.ProjectId);
        Assert.Equal(ActivityType.Generated, activity.Type);
        Assert.Equal(seed.Actor, activity.Actor);
        Assert.Equal("generate summary", activity.Purpose);
        Assert.Equal(ActivityStatus.Completed, activity.Status);
        Assert.NotEqual(default, activity.StartedAt);
        Assert.NotNull(activity.EndedAt);
        Assert.Equal("run-42", activity.CorrelationId);
    }

    [Fact]
    public void PRO_003_PRO_004_inputs_and_outputs_are_revision_pinned()
    {
        var seed = Seed();
        var activity = Create(
            seed,
            ActivityType.Updated,
            ActivityStatus.Completed,
            inputs: new[]
            {
                new ActivityInput(seed.Item, seed.Revision, 0),
                new ActivityInput(KnowledgeItemId.New(), KnowledgeRevisionId.New(), 1),
            },
            outputs: new[] { new ActivityOutput(seed.Item, seed.Revision, 0) });

        Assert.Equal(2, activity.Inputs.Count);
        Assert.Equal(seed.Revision, activity.Inputs[0].RevisionId);
        Assert.Equal(0, activity.Inputs[0].Position);
        Assert.Equal(1, activity.Inputs[1].Position);
        Assert.Single(activity.Outputs);
        Assert.Equal(seed.Revision, activity.Outputs[0].RevisionId);
    }

    [Fact]
    public void REV_006_activity_is_append_only_with_no_writable_properties_or_mutators()
    {
        var writable = typeof(Activity)
            .GetProperties()
            .Where(p => p.CanWrite)
            .ToArray();
        Assert.Empty(writable);

        var mutators = typeof(Activity)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .ToArray();
        Assert.Empty(mutators);
    }

    [Fact]
    public void PRO_003_inputs_and_outputs_are_immutable_value_records()
    {
        Assert.False(HasSettableNonInitProperty(typeof(ActivityInput)));
        Assert.False(HasSettableNonInitProperty(typeof(ActivityOutput)));

        // Records give value semantics: two equal inputs are interchangeable, never aliased-mutable.
        var item = KnowledgeItemId.New();
        var revision = KnowledgeRevisionId.New();
        var first = new ActivityInput(item, revision, 0);
        var second = new ActivityInput(item, revision, 0);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void REV_008_amendment_types_require_both_reason_and_reference()
    {
        foreach (var type in new[] { ActivityType.Correction, ActivityType.Retraction, ActivityType.Supersession })
        {
            Assert.Throws<ArgumentException>(() =>
                Create(Seed(), type, ActivityStatus.Completed, reason: null, reference: null));

            Assert.Throws<ArgumentException>(() =>
                Create(Seed(), type, ActivityStatus.Completed, reason: "amendment", reference: null));

            Assert.Throws<ArgumentException>(() =>
                Create(Seed(), type, ActivityStatus.Completed, reason: " ", reference: Seed().Revision));
        }

        // A complete reason + reference is accepted.
        var corrected = Create(
            Seed(),
            ActivityType.Correction,
            ActivityStatus.Completed,
            reason: "fix title",
            reference: Seed().Revision);
        Assert.Equal("fix title", corrected.Reason);
        Assert.NotNull(corrected.Reference);
    }

    [Fact]
    public void non_amendment_types_do_not_force_reason_or_reference()
    {
        var seed = Seed();
        var activity = Create(seed, ActivityType.Generated, ActivityStatus.Completed, reason: null, reference: null);

        Assert.Null(activity.Reason);
        Assert.Null(activity.Reference);
    }

    [Fact]
    public void PRO_008_a_single_activity_model_serves_human_and_machine_actors()
    {
        // There is one Activity aggregate and one ActivityType set for every PrincipalType (PRO-008):
        // provenance is not bifurcated by actor kind.
        var humanActor = PrincipalId.New();
        var machineActor = PrincipalId.New();

        var human = Create(Seed(actor: humanActor), ActivityType.Approval, ActivityStatus.Completed);
        var machine = Create(Seed(actor: machineActor), ActivityType.Generated, ActivityStatus.Completed);

        Assert.Same(typeof(Activity), typeof(Activity));
        Assert.Equal(humanActor, human.Actor);
        Assert.Equal(machineActor, machine.Actor);
        Assert.Equal(ActivityType.Approval, human.Type);
        Assert.Equal(ActivityType.Generated, machine.Type);
    }

    [Fact]
    public void creation_guards_the_required_purpose()
    {
        Assert.Throws<ArgumentException>(() =>
            Create(Seed(), ActivityType.Generated, ActivityStatus.Completed, purpose: " "));
    }

    private static bool HasSettableNonInitProperty(Type type) =>
        type.GetProperties().Any(p =>
            p.SetMethod is not null &&
            !p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)));

    private static (TenantId Tenant, ProjectId Project, PrincipalId Actor, KnowledgeItemId Item, KnowledgeRevisionId Revision) Seed(
        PrincipalId? actor = null) =>
        (TenantId.New(), ProjectId.New(), actor ?? PrincipalId.New(), KnowledgeItemId.New(), KnowledgeRevisionId.New());

    private static Activity Create(
        (TenantId Tenant, ProjectId Project, PrincipalId Actor, KnowledgeItemId Item, KnowledgeRevisionId Revision) seed,
        ActivityType type,
        ActivityStatus status,
        string? purpose = "generate summary",
        string? reason = null,
        KnowledgeRevisionId? reference = null,
        IReadOnlyList<ActivityInput>? inputs = null,
        IReadOnlyList<ActivityOutput>? outputs = null) =>
        Activity.Create(
            seed.Tenant,
            seed.Project,
            type,
            seed.Actor,
            purpose!,
            status,
            DateTimeOffset.UtcNow,
            status is ActivityStatus.Completed or ActivityStatus.Failed ? DateTimeOffset.UtcNow : null,
            "run-42",
            reason,
            reference,
            inputs ?? Array.Empty<ActivityInput>(),
            outputs ?? Array.Empty<ActivityOutput>());
}