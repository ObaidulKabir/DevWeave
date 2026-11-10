using DevWeave.Domain.Schema;

namespace DevWeave.Domain.Tests.Schema;

/// <summary>
/// Proves the Schema domain invariants for Slice 4: a schema carries a namespaced id, version, owner,
/// description and status (SCH-001); published schemas are immutable (SCH-003); and the status
/// lifetime follows Draft -> Published -> Deprecated (SCH-003). Duplicate (Namespace, Version)
/// rejection is a registry-level invariant enforced by the application layer and the persistence
/// unique index (SCH-001).
/// </summary>
public sealed class SchemaDefinitionTests
{
    [Fact]
    public void SCH_001_creation_assigns_identity_envelope_and_draft_status()
    {
        var schema = SchemaDefinition.Create(
            "core.resource", "1.0", "platform", "Core resource shape", "{\"type\":\"object\"}");

        Assert.NotEqual(Guid.Empty, schema.Id.Value);
        Assert.Equal("core.resource", schema.Namespace);
        Assert.Equal("1.0", schema.Version);
        Assert.Equal("platform", schema.Owner);
        Assert.Equal("Core resource shape", schema.Description);
        Assert.Equal("{\"type\":\"object\"}", schema.Document);
        Assert.Equal(SchemaStatus.Draft, schema.Status);
        Assert.Null(schema.PublishedAt);
        Assert.False(schema.IsActive);
    }

    [Fact]
    public void creation_guards_required_values()
    {
        Assert.Throws<ArgumentException>(() =>
            SchemaDefinition.Create(" ", "1.0", "platform", "desc", "{}"));
        Assert.Throws<ArgumentException>(() =>
            SchemaDefinition.Create("core.resource", " ", "platform", "desc", "{}"));
        Assert.Throws<ArgumentException>(() =>
            SchemaDefinition.Create("core.resource", "1.0", " ", "desc", "{}"));
        Assert.Throws<ArgumentException>(() =>
            SchemaDefinition.Create("core.resource", "1.0", "platform", " ", "{}"));
        Assert.Throws<ArgumentException>(() =>
            SchemaDefinition.Create("core.resource", "1.0", "platform", "desc", " "));
    }

    [Fact]
    public void SCH_003_publish_makes_the_schema_active_and_records_a_timestamp()
    {
        var schema = SchemaDefinition.Create("core.resource", "1.0", "platform", "desc", "{}");

        schema.Publish();

        Assert.Equal(SchemaStatus.Published, schema.Status);
        Assert.True(schema.IsActive);
        Assert.NotNull(schema.PublishedAt);
    }

    [Fact]
    public void SCH_003_deprecate_marks_the_schema_inactive_without_mutating_the_document()
    {
        var schema = SchemaDefinition.Create("core.resource", "1.0", "platform", "desc", "{\"type\":\"object\"}");
        schema.Publish();
        var document = schema.Document;

        schema.Deprecate();

        Assert.Equal(SchemaStatus.Deprecated, schema.Status);
        Assert.False(schema.IsActive);
        Assert.Equal(document, schema.Document);
    }

    [Fact]
    public void SCH_003_a_draft_can_be_deprecated_directly()
    {
        var schema = SchemaDefinition.Create("core.resource", "1.0", "platform", "desc", "{}");

        schema.Deprecate();

        Assert.Equal(SchemaStatus.Deprecated, schema.Status);
        Assert.False(schema.IsActive);
    }

    [Fact]
    public void SCH_003_duplicate_and_illegal_transitions_are_rejected()
    {
        var published = SchemaDefinition.Create("core.resource", "1.0", "platform", "desc", "{}");
        published.Publish();
        Assert.Throws<InvalidOperationException>(() => published.Publish());

        var deprecated = SchemaDefinition.Create("core.resource", "2.0", "platform", "desc", "{}");
        deprecated.Deprecate();
        Assert.Throws<InvalidOperationException>(() => deprecated.Deprecate());
        Assert.Throws<InvalidOperationException>(() => deprecated.Publish());
    }

    [Fact]
    public void SCH_003_published_schemas_are_structurally_immutable()
    {
        var setterTargets = new[]
        {
            nameof(SchemaDefinition.Namespace),
            nameof(SchemaDefinition.Version),
            nameof(SchemaDefinition.Document),
        };

        foreach (var name in setterTargets)
        {
            var property = typeof(SchemaDefinition).GetProperty(name)!;
            Assert.False(property.CanWrite, $"'{name}' must be immutable.");
        }
    }

    [Fact]
    public void status_enum_values_are_exactly_the_three_lifecycle_states()
    {
        Assert.Equal(["Draft", "Published", "Deprecated"], Enum.GetNames<SchemaStatus>());
    }
}