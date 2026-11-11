using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class PlanWorkLinkConfiguration : IEntityTypeConfiguration<PlanWorkLink>
{
    public void Configure(EntityTypeBuilder<PlanWorkLink> builder)
    {
        builder.ToTable("plan_work_links");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasConversion(id => id.Value, value => new PlanWorkLinkId(value)).ValueGeneratedNever();
        builder.Property(l => l.TenantId).HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(l => l.ProjectId).HasConversion(id => id.Value, value => new ProjectId(value));
        builder.Property(l => l.WorkItemId).HasConversion(id => id.Value, value => new WorkItemId(value));
        builder.Property(l => l.KnowledgeItemId).HasConversion(id => id.Value, value => new KnowledgeItemId(value));
        builder.Property(l => l.RevisionId).HasConversion(id => id.Value, value => new KnowledgeRevisionId(value));
        builder.Property(l => l.CreatedBy).HasConversion(id => id.Value, value => new PrincipalId(value));
        builder.HasIndex(l => new { l.TenantId, l.ProjectId, l.WorkItemId, l.RevisionId }).IsUnique();
        builder.HasOne<WorkItem>().WithMany()
            .HasForeignKey(l => new { l.WorkItemId, l.TenantId, l.ProjectId })
            .HasPrincipalKey(w => new { w.Id, w.TenantId, w.ProjectId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KnowledgeItem>().WithMany()
            .HasForeignKey(l => new { l.KnowledgeItemId, l.TenantId, l.ProjectId })
            .HasPrincipalKey(i => new { i.Id, i.TenantId, i.ProjectId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KnowledgeRevision>().WithMany()
            .HasForeignKey(l => new { l.RevisionId, l.TenantId, l.KnowledgeItemId })
            .HasPrincipalKey(r => new { r.Id, r.TenantId, r.KnowledgeItemId }).OnDelete(DeleteBehavior.Restrict);
    }
}
