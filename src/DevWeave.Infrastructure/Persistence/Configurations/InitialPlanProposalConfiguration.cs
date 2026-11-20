using DevWeave.Domain.Identity;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class InitialPlanProposalConfiguration : IEntityTypeConfiguration<InitialPlanProposal>
{
    public void Configure(EntityTypeBuilder<InitialPlanProposal> builder)
    {
        builder.ToTable("initial_plan_proposals");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion(id => id.Value, value => new InitialPlanProposalId(value)).ValueGeneratedNever();
        builder.Property(p => p.TenantId).HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(p => p.ProjectId).HasConversion(id => id.Value, value => new ProjectId(value));
        builder.Property(p => p.ProposedBy).HasConversion(id => id.Value, value => new PrincipalId(value));
        builder.Property(p => p.ReviewedBy).HasConversion(
            id => id == null ? (Guid?)null : id.Value,
            value => value == null ? null : new PrincipalId(value.Value));
        builder.Property(p => p.Status).HasConversion<int>().IsConcurrencyToken();
        builder.Property(p => p.IdempotencyKey).HasMaxLength(200);
        builder.Property(p => p.PayloadHash).HasMaxLength(64);
        builder.Property(p => p.PayloadJson).HasColumnType("text");
        builder.Property(p => p.DecisionNote).HasMaxLength(2000);
        builder.Property(p => p.ResultMappingJson).HasColumnType("text");
        builder.HasIndex(p => new { p.TenantId, p.ProjectId, p.Status });
        builder.HasIndex(p => new { p.TenantId, p.ProjectId, p.ProposedBy, p.IdempotencyKey }).IsUnique();
    }
}
