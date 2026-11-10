using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class AgentGrantConfiguration : IEntityTypeConfiguration<AgentGrant>
{
    public void Configure(EntityTypeBuilder<AgentGrant> builder)
    {
        builder.ToTable("agent_grants");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id)
            .HasConversion(id => id.Value, value => new AgentGrantId(value))
            .ValueGeneratedNever();

        builder.Property(g => g.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        // A null project means the capability applies tenant-wide (AGT-002).
        builder.Property(g => g.ProjectId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : new ProjectId(value.Value))
            .IsRequired(false);

        builder.Property(g => g.AgentId)
            .HasConversion(id => id.Value, value => new PrincipalId(value))
            .IsRequired();

        builder.Property(g => g.Capability)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(g => g.ClassificationCeiling)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(g => g.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(g => g.GrantedAt)
            .IsRequired();

        builder.Property(g => g.ExpiresAt);

        builder.Property(g => g.ApprovedBy)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : new PrincipalId(value.Value));

        builder.Property(g => g.ApprovedAt);

        builder.Property(g => g.RevokedBy)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : new PrincipalId(value.Value));

        builder.Property(g => g.RevokedAt);

        builder.HasIndex(g => new { g.TenantId, g.AgentId });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(g => g.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(g => g.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Principal>()
            .WithMany()
            .HasForeignKey(g => g.AgentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}