using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class ProjectReferenceConfiguration : IEntityTypeConfiguration<ProjectReference>
{
    public void Configure(EntityTypeBuilder<ProjectReference> builder)
    {
        builder.ToTable("project_references");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasConversion(
                id => id.Value,
                value => new ProjectReferenceId(value))
            .ValueGeneratedNever();

        builder.Property(r => r.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .IsRequired();

        builder.Property(r => r.ProjectId)
            .HasConversion(
                id => id.Value,
                value => new ProjectId(value))
            .IsRequired();

        builder.Property(r => r.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(r => r.ExternalId)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(r => r.Url)
            .HasMaxLength(2000);

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.HasIndex(r => new { r.TenantId, r.ProjectId });

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(r => r.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}