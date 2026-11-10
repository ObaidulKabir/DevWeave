using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class LifecycleProfileConfiguration : IEntityTypeConfiguration<LifecycleProfile>
{
    public void Configure(EntityTypeBuilder<LifecycleProfile> builder)
    {
        builder.ToTable("lifecycle_profiles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => new LifecycleProfileId(value))
            .ValueGeneratedNever();

        builder.Property(p => p.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .IsRequired();

        builder.Property(p => p.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Version)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        // Key is unique per tenant (LFC-001).
        builder.HasIndex(p => new { p.TenantId, p.Key })
            .IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}