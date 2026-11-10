using DevWeave.Domain.Activities;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Evidence;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
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
    public DbSet<KnowledgeItem> KnowledgeItems => Set<KnowledgeItem>();
    public DbSet<KnowledgeRevision> KnowledgeRevisions => Set<KnowledgeRevision>();
    public DbSet<SchemaDefinition> SchemaDefinitions => Set<SchemaDefinition>();
    public DbSet<RelationType> RelationTypes => Set<RelationType>();
    public DbSet<Relationship> Relationships => Set<Relationship>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Artifact> Artifacts => Set<Artifact>();
    public DbSet<Evidence> Evidence => Set<Evidence>();
    public DbSet<AgentGrant> AgentGrants => Set<AgentGrant>();
    public DbSet<AgentCredential> AgentCredentials => Set<AgentCredential>();
    public DbSet<ContextPackage> ContextPackages => Set<ContextPackage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Applies all IEntityTypeConfiguration instances found in the Infrastructure assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DevWeaveDbContext).Assembly);
    }
}
