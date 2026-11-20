using DevWeave.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class SupportAccessGrantConfiguration : IEntityTypeConfiguration<SupportAccessGrant>
{
    public void Configure(EntityTypeBuilder<SupportAccessGrant> builder)
    {
        builder.ToTable("support_access_grants");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id)
            .HasConversion(id => id.Value, value => new SupportAccessGrantId(value))
            .ValueGeneratedNever();

        builder.Property(g => g.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(g => g.OperatorId)
            .HasConversion(id => id.Value, value => new PrincipalId(value))
            .IsRequired();

        builder.Property(g => g.ApproverId)
            .HasConversion(id => id.Value, value => new PrincipalId(value))
            .IsRequired();

        builder.Property(g => g.Purpose)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(g => g.CreatedAt).IsRequired();
        builder.Property(g => g.ExpiresAt).IsRequired();
        builder.Property(g => g.RevokedAt);

        builder.HasIndex(g => new { g.TenantId, g.OperatorId });
    }
}
