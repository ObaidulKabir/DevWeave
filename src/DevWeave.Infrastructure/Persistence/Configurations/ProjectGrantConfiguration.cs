using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class ProjectGrantConfiguration : IEntityTypeConfiguration<ProjectGrant>
{
    public void Configure(EntityTypeBuilder<ProjectGrant> builder)
    {
        builder.ToTable("project_grants");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id)
            .HasConversion(
                id => id.Value,
                value => new ProjectGrantId(value))
            .ValueGeneratedNever();

        builder.Property(g => g.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .IsRequired();

        builder.Property(g => g.ProjectId)
            .HasConversion(
                id => id.Value,
                value => new ProjectId(value))
            .IsRequired();

        builder.Property(g => g.PrincipalId)
            .HasConversion(
                id => id.Value,
                value => new PrincipalId(value))
            .IsRequired();

        builder.Property(g => g.Scope)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(g => g.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(g => g.GrantedAt)
            .IsRequired();

        builder.Property(g => g.RevokedAt);

        builder.HasIndex(g => new { g.TenantId, g.ProjectId, g.PrincipalId });

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(g => g.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(g => g.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Principal>()
            .WithMany()
            .HasForeignKey(g => g.PrincipalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}