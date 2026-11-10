using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class AgentCredentialConfiguration : IEntityTypeConfiguration<AgentCredential>
{
    public void Configure(EntityTypeBuilder<AgentCredential> builder)
    {
        builder.ToTable("agent_credentials");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new AgentCredentialId(value))
            .ValueGeneratedNever();

        builder.Property(c => c.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(c => c.AgentId)
            .HasConversion(id => id.Value, value => new PrincipalId(value))
            .IsRequired();

        builder.Property(c => c.Label)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.SecretHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(c => c.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.ExpiresAt);

        builder.Property(c => c.RevokedAt);

        builder.HasIndex(c => new { c.TenantId, c.AgentId });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Principal>()
            .WithMany()
            .HasForeignKey(c => c.AgentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}