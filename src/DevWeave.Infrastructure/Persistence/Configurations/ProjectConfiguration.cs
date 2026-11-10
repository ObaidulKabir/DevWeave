using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => new ProjectId(value))
            .ValueGeneratedNever();

        builder.Property(p => p.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .IsRequired();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Description)
            .HasMaxLength(2000);

        builder.Property(p => p.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(p => p.LifecycleProfileId)
            .HasConversion(
                id => id.Value,
                value => new LifecycleProfileId(value))
            .IsRequired();

        builder.Property(p => p.CreatedBy)
            .HasConversion(
                id => id.Value,
                value => new PrincipalId(value))
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.UpdatedAt);

        builder.Property(p => p.ArchivedAt);

        builder.Property(p => p.RestoredAt);

        builder.Property(p => p.DeletedAt);

        builder.Property(p => p.IsDeleted)
            .IsRequired();

        // Key is unique per tenant (PRJ-002) and immutable after creation.
        builder.HasIndex(p => new { p.TenantId, p.Key })
            .IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<LifecycleProfile>()
            .WithMany()
            .HasForeignKey(p => p.LifecycleProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}