using DevWeave.Domain.Activities;
using DevWeave.Domain.Commercial;
using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Tests.Commercial;

public sealed class CommercialControlTests
{
    [Fact]
    public void COM_021_runtime_package_and_activity_record_location_and_provider()
    {
        var tenant = TenantId.New();
        var project = ProjectId.New();
        var actor = PrincipalId.New();

        var runtime = AgentRuntimeInstance.Register(
            tenant, project, actor, "session-1", "cursor", "1.0",
            "DevWeaveManaged", "devweave", "github", "coordination.sessions");
        var package = WorkPackage.Create(
            tenant, project, CampaignId.New(), Guid.NewGuid(), "pkg", "ship the endpoint",
            executionLocation: "CustomerManaged", provider: "customer");
        var activity = Activity.Create(
            tenant, project, ActivityType.Generated, actor, "generate", ActivityStatus.Completed,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "run", null, null,
            Array.Empty<ActivityInput>(), Array.Empty<ActivityOutput>(),
            "DevWeaveManaged", "devweave");

        Assert.Equal(ExecutionLocation.DevWeaveManaged, runtime.ExecutionLocation);
        Assert.Equal("devweave", runtime.Provider);
        Assert.Equal("github", runtime.WorkspaceProvider);
        Assert.Equal("coordination.sessions", runtime.BillingMeter);
        Assert.Equal(ExecutionLocation.CustomerManaged, package.ExecutionLocation);
        Assert.Equal("customer", package.Provider);
        Assert.Equal(ExecutionLocation.DevWeaveManaged, activity.ExecutionLocation);
        Assert.Equal("devweave", activity.Provider);
        Assert.Throws<ArgumentException>(() => AgentRuntimeInstance.Register(
            tenant, project, actor, "session-2", "cursor", "1.0", "hosted"));

        var external = AgentRuntimeInstance.Register(
            tenant, project, actor, "session-external", "cursor", "1.0");
        Assert.Equal(ExecutionLocation.CustomerManaged, external.ExecutionLocation);
        Assert.Equal(ExecutionFacts.CustomerProvider, external.Provider);
        Assert.Null(external.BillingMeter);
        Assert.Null(external.WorkspaceProvider);
    }

    [Fact]
    public void COM_025_training_use_stays_excluded_until_an_explicit_opt_in()
    {
        var tenant = TenantId.New();
        var policy = TenantTrainingPolicy.Excluded(tenant);

        Assert.Equal("Excluded", policy.Use);
        Assert.Null(policy.ActorId);
        Assert.Throws<ArgumentException>(() => policy.OptIn(PrincipalId.New(), " ", DateTimeOffset.UtcNow));

        var actor = PrincipalId.New();
        var at = DateTimeOffset.UtcNow;
        policy.OptIn(actor, "customer contract 14", at);

        Assert.Equal("OptedIn", policy.Use);
        Assert.Equal(actor, policy.ActorId);
        Assert.Equal(at, policy.ChangedAt);
        Assert.Equal("customer contract 14", policy.Reason);
    }
}
