using DevWeave.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("memberships");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasConversion(
                id => id.Value,
                value => new MembershipId(value))
            .ValueGeneratedNever();

        builder.Property(m => m.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .IsRequired();

        builder.Property(m => m.PrincipalId)
            .HasConversion(
                id => id.Value,
                value => new PrincipalId(value))
            .IsRequired();

        builder.Property(m => m.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(m => m.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        // One membership per principal per tenant (SRS TEN-004).
        builder.HasIndex(m => new { m.TenantId, m.PrincipalId })
            .IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(m => m.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Principal>()
            .WithMany()
            .HasForeignKey(m => m.PrincipalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}