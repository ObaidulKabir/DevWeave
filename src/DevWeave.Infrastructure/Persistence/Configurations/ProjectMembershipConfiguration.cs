using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class ProjectMembershipConfiguration : IEntityTypeConfiguration<ProjectMembership>
{
    public void Configure(EntityTypeBuilder<ProjectMembership> builder)
    {
        builder.ToTable("project_memberships");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasConversion(
                id => id.Value,
                value => new ProjectMembershipId(value))
            .ValueGeneratedNever();

        builder.Property(m => m.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .IsRequired();

        builder.Property(m => m.ProjectId)
            .HasConversion(
                id => id.Value,
                value => new ProjectId(value))
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

        builder.Property(m => m.RevokedAt);

        // One membership per principal per project (PRJ-003).
        builder.HasIndex(m => new { m.TenantId, m.ProjectId, m.PrincipalId })
            .IsUnique();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

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