using DevWeave.Domain.Activities;
using DevWeave.Domain.Audit;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Coordination;
using DevWeave.Domain.Commercial;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Common;
using DevWeave.Domain.Evidence;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Integrations;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;
using DevWeave.Domain.Schema;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Persistence;

public sealed class DevWeaveDbContext : DbContext
{
    public DevWeaveDbContext(DbContextOptions<DevWeaveDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Principal> Principals => Set<Principal>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectReference> ProjectReferences => Set<ProjectReference>();
    public DbSet<ProjectMembership> ProjectMemberships => Set<ProjectMembership>();
    public DbSet<ProjectGrant> ProjectGrants => Set<ProjectGrant>();
    public DbSet<LifecycleProfile> LifecycleProfiles => Set<LifecycleProfile>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<MilestoneTransition> MilestoneTransitions => Set<MilestoneTransition>();
    public DbSet<WorkItemDependency> WorkItemDependencies => Set<WorkItemDependency>();
    public DbSet<WorkItemTransition> WorkItemTransitions => Set<WorkItemTransition>();
    public DbSet<PlanOutlineNode> PlanOutlineNodes => Set<PlanOutlineNode>();
    public DbSet<PlanWorkLink> PlanWorkLinks => Set<PlanWorkLink>();
    public DbSet<PlanExpansionProposal> PlanExpansionProposals => Set<PlanExpansionProposal>();
    public DbSet<KnowledgeItem> KnowledgeItems => Set<KnowledgeItem>();
    public DbSet<KnowledgeRevision> KnowledgeRevisions => Set<KnowledgeRevision>();
    public DbSet<SchemaDefinition> SchemaDefinitions => Set<SchemaDefinition>();
    public DbSet<RelationType> RelationTypes => Set<RelationType>();
    public DbSet<Relationship> Relationships => Set<Relationship>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AuditRecord> AuditLogs => Set<AuditRecord>();
    public DbSet<Artifact> Artifacts => Set<Artifact>();

    // Coordination
    public DbSet<CoordinationChannel> CoordinationChannels => Set<CoordinationChannel>();
    public DbSet<CoordinationMessage> CoordinationMessages => Set<CoordinationMessage>();
    public DbSet<CoordinationCursor> CoordinationCursors => Set<CoordinationCursor>();
    public DbSet<CoordinationEvent> CoordinationEvents => Set<CoordinationEvent>();
    public DbSet<ExecutionCampaign> ExecutionCampaigns => Set<ExecutionCampaign>();
    public DbSet<PlanBaseline> PlanBaselines => Set<PlanBaseline>();
    public DbSet<CodebaseTarget> CodebaseTargets => Set<CodebaseTarget>();
    public DbSet<ChangeSet> ChangeSets => Set<ChangeSet>();
    public DbSet<ParticipationOffer> ParticipationOffers => Set<ParticipationOffer>();
    public DbSet<IntegrationQueueItem> IntegrationQueueItems => Set<IntegrationQueueItem>();
    public DbSet<CoordinationKillSwitch> CoordinationKillSwitches => Set<CoordinationKillSwitch>();
    public DbSet<CommercialPlan> CommercialPlans => Set<CommercialPlan>();
    public DbSet<EntitlementRule> EntitlementRules => Set<EntitlementRule>();
    public DbSet<UsageObservation> UsageObservations => Set<UsageObservation>();
    public DbSet<TenantPlanAssignment> TenantPlanAssignments => Set<TenantPlanAssignment>();
    public DbSet<WorkPackage> WorkPackages => Set<WorkPackage>();
    public DbSet<WorkPackageWorkthrough> WorkPackageWorkthroughs => Set<WorkPackageWorkthrough>();
    public DbSet<TaskLease> TaskLeases => Set<TaskLease>();
    public DbSet<AgentRuntimeInstance> AgentRuntimeInstances => Set<AgentRuntimeInstance>();
    public DbSet<Evidence> Evidence => Set<Evidence>();
    public DbSet<AgentGrant> AgentGrants => Set<AgentGrant>();
    public DbSet<AgentCredential> AgentCredentials => Set<AgentCredential>();
    public DbSet<ContextPackage> ContextPackages => Set<ContextPackage>();
    public DbSet<Integration> Integrations => Set<Integration>();
    public DbSet<InboundEvent> InboundEvents => Set<InboundEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Applies all IEntityTypeConfiguration instances found in the Infrastructure assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DevWeaveDbContext).Assembly);
    }
}
