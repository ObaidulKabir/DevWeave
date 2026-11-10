using DevWeave.Domain.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class SchemaDefinitionConfiguration : IEntityTypeConfiguration<SchemaDefinition>
{
    public void Configure(EntityTypeBuilder<SchemaDefinition> builder)
    {
        builder.ToTable("schema_definitions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasConversion(
                id => id.Value,
                value => new SchemaDefinitionId(value))
            .ValueGeneratedNever();

        builder.Property(s => s.Namespace)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Version)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.Owner)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Description)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(s => s.Status)
            .HasConversion<int>()
            .IsRequired();

        // Retain the JSON Schema document verbatim as jsonb so the exact dialect/keywords survive
        // the round trip (SRS SCH-002).
        builder.Property(s => s.Document)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.PublishedAt);

        // The registry identity is the (Namespace, Version) pair (SRS SCH-001).
        builder.HasIndex(s => new { s.Namespace, s.Version })
            .IsUnique();
    }
}