using DevWeave.Domain.Activities;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class AgentExecutionMetadataConfiguration : IEntityTypeConfiguration<AgentExecutionMetadata>
{
    public void Configure(EntityTypeBuilder<AgentExecutionMetadata> builder)
    {
        builder.ToTable("agent_execution_metadata");

        builder.HasKey(m => m.ActivityId);

        builder.Property(m => m.ActivityId)
            .HasConversion(
                id => id.Value,
                value => new ActivityId(value))
            .ValueGeneratedNever();

        builder.Property(m => m.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .IsRequired();

        builder.Property(m => m.Provider)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.Model)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.ModelVersion)
            .HasMaxLength(100);

        builder.Property(m => m.ConfigurationFingerprint)
            .HasMaxLength(200);

        builder.Property(m => m.ToolFingerprint)
            .HasMaxLength(200);

        builder.Property(m => m.ExternalRunReference)
            .HasMaxLength(500);

        builder.Property(m => m.AuthorizationContext)
            .HasMaxLength(2000);

        builder.HasIndex(m => m.TenantId);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(m => m.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Activity>()
            .WithOne()
            .HasForeignKey<AgentExecutionMetadata>(m => m.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
