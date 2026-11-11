using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class PlanOutlineNodeConfiguration : IEntityTypeConfiguration<PlanOutlineNode>
{
    public void Configure(EntityTypeBuilder<PlanOutlineNode> builder)
    {
        builder.ToTable("plan_outline_nodes");
        builder.HasKey(n => n.ItemId);
        builder.Property(n => n.ItemId).HasConversion(id => id.Value, value => new KnowledgeItemId(value)).ValueGeneratedNever();
        builder.Property(n => n.RootItemId).HasConversion(id => id.Value, value => new KnowledgeItemId(value));
        builder.Property(n => n.ParentItemId).HasConversion(id => id == null ? (Guid?)null : id.Value, value => value == null ? null : new KnowledgeItemId(value.Value));
        builder.Property(n => n.TenantId).HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(n => n.ProjectId).HasConversion(id => id.Value, value => new ProjectId(value));
        builder.Property(n => n.CreatedBy).HasConversion(id => id.Value, value => new PrincipalId(value));
        builder.HasAlternateKey(n => new { n.ItemId, n.TenantId, n.ProjectId });
        builder.HasIndex(n => new { n.TenantId, n.ProjectId, n.RootItemId, n.ParentItemId, n.Position });
        builder.HasIndex(n => new { n.TenantId, n.ProjectId })
            .IsUnique().HasFilter("\"ParentItemId\" IS NULL");
        builder.HasOne<KnowledgeItem>().WithMany()
            .HasForeignKey(n => new { n.ItemId, n.TenantId, n.ProjectId })
            .HasPrincipalKey(i => new { i.Id, i.TenantId, i.ProjectId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlanOutlineNode>().WithMany()
            .HasForeignKey(n => new { n.ParentItemId, n.TenantId, n.ProjectId })
            .HasPrincipalKey(n => new { n.ItemId, n.TenantId, n.ProjectId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KnowledgeItem>().WithMany()
            .HasForeignKey(n => new { n.RootItemId, n.TenantId, n.ProjectId })
            .HasPrincipalKey(i => new { i.Id, i.TenantId, i.ProjectId }).OnDelete(DeleteBehavior.Restrict);
    }
}
