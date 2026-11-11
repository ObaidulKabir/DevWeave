using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class PlanExpansionProposalConfiguration : IEntityTypeConfiguration<PlanExpansionProposal>
{
    public void Configure(EntityTypeBuilder<PlanExpansionProposal> builder)
    {
        builder.ToTable("plan_expansion_proposals");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion(id => id.Value, value => new PlanExpansionProposalId(value)).ValueGeneratedNever();
        builder.Property(p => p.TenantId).HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(p => p.ProjectId).HasConversion(id => id.Value, value => new ProjectId(value));
        builder.Property(p => p.ParentItemId).HasConversion(id => id.Value, value => new KnowledgeItemId(value));
        builder.Property(p => p.ResolvedItemId).HasConversion(id => id == null ? (Guid?)null : id.Value, value => value == null ? null : new KnowledgeItemId(value.Value));
        builder.Property(p => p.ProposedBy).HasConversion(id => id.Value, value => new PrincipalId(value));
        builder.Property(p => p.ReviewedBy).HasConversion(id => id == null ? (Guid?)null : id.Value, value => value == null ? null : new PrincipalId(value.Value));
        builder.Property(p => p.Status).HasConversion<int>().IsConcurrencyToken();
        builder.Property(p => p.Key).HasMaxLength(100);
        builder.Property(p => p.Title).HasMaxLength(200);
        builder.Property(p => p.TypeId).HasMaxLength(100);
        builder.Property(p => p.Rationale).HasMaxLength(2000);
        builder.HasIndex(p => new { p.TenantId, p.ProjectId, p.Status });
        builder.HasOne<KnowledgeItem>().WithMany()
            .HasForeignKey(p => new { p.ParentItemId, p.TenantId, p.ProjectId })
            .HasPrincipalKey(i => new { i.Id, i.TenantId, i.ProjectId }).OnDelete(DeleteBehavior.Restrict);
    }
}
