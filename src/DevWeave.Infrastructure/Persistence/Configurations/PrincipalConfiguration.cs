using DevWeave.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class PrincipalConfiguration : IEntityTypeConfiguration<Principal>
{
    public void Configure(EntityTypeBuilder<Principal> builder)
    {
        builder.ToTable("principals");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => new PrincipalId(value))
            .ValueGeneratedNever();

        builder.Property(p => p.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .IsRequired();

        builder.Property(p => p.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(p => p.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(p => p.IsActive)
            .IsRequired();

        // Enforce the constraint: Email must be unique per tenant
        builder.HasIndex(p => new { p.TenantId, p.Email })
            .IsUnique();

        // Enforce foreign key relationship to Tenant
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
