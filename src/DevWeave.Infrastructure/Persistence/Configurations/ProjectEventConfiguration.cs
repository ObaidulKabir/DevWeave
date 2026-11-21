using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class ProjectEventConfiguration : IEntityTypeConfiguration<ProjectEvent>
{
    public void Configure(EntityTypeBuilder<ProjectEvent> builder)
    {
        builder.ToTable("project_events");
        builder.HasKey(projectEvent => projectEvent.Id);

        builder.Property(projectEvent => projectEvent.Id)
            .HasConversion(id => id.Value, value => new ProjectEventId(value))
            .ValueGeneratedNever();
        builder.Property(projectEvent => projectEvent.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(projectEvent => projectEvent.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));
        builder.Property(projectEvent => projectEvent.Sequence)
            .UseIdentityAlwaysColumn();
        builder.Property(projectEvent => projectEvent.EventType)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(projectEvent => projectEvent.EventSchemaVersion)
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(projectEvent => projectEvent.ActorId)
            .HasConversion(id => id.Value, value => new PrincipalId(value));
        builder.Property(projectEvent => projectEvent.Purpose)
            .HasMaxLength(2000)
            .IsRequired();
        builder.Property(projectEvent => projectEvent.ValidAt);
        builder.Property(projectEvent => projectEvent.RecordedAt);
        builder.Property(projectEvent => projectEvent.BaselineId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : new BaselineId(value.Value));
        builder.Property(projectEvent => projectEvent.MetadataJson)
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(projectEvent => projectEvent.CorrelationId)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(projectEvent => projectEvent.IdempotencyKey)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(projectEvent => projectEvent.Reason)
            .HasMaxLength(2000)
            .IsRequired();
        builder.Property(projectEvent => projectEvent.AuthorizationContext)
            .HasColumnType("text")
            .IsRequired();

        ConfigureReferences(builder.OwnsMany(projectEvent => projectEvent.Inputs), "project_event_inputs");
        ConfigureReferences(builder.OwnsMany(projectEvent => projectEvent.Outputs), "project_event_outputs");

        builder.HasIndex(projectEvent => new
            {
                projectEvent.TenantId,
                projectEvent.ProjectId,
                projectEvent.Sequence,
            })
            .IsUnique();
        builder.HasIndex(projectEvent => new
            {
                projectEvent.TenantId,
                projectEvent.ProjectId,
                projectEvent.IdempotencyKey,
            })
            .IsUnique();
        builder.HasIndex(projectEvent => projectEvent.CorrelationId);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(projectEvent => projectEvent.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(projectEvent => projectEvent.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureReferences(
        OwnedNavigationBuilder<ProjectEvent, ProjectEventResourceRef> references,
        string tableName)
    {
        references.ToTable(tableName);
        references.WithOwner().HasForeignKey("project_event_id");
        references.HasKey("project_event_id", nameof(ProjectEventResourceRef.Position));
        references.Property(reference => reference.Kind)
            .HasConversion<int>()
            .HasColumnName("resource_kind");
        references.Property(reference => reference.Identifier)
            .HasMaxLength(500)
            .HasColumnName("resource_identifier");
        references.Property(reference => reference.RevisionIdentifier)
            .HasMaxLength(500)
            .HasColumnName("revision_identifier");
        references.Property(reference => reference.Position)
            .HasColumnName("position")
            .ValueGeneratedNever();
    }
}
