using DevWeave.Domain.Relations;

namespace DevWeave.Domain.Tests.Relations;

/// <summary>
/// Proves the RelationType domain invariants for Slice 5 (REL-002, REL-003, SCH-007, BR-007): a type
/// carries a code/name, optional endpoint-type constraints, direction, cardinality and an Active
/// default status; endpoint matching is a purely local decision; and the canonical seed codes are
/// exactly the eleven SRS-mandated relation types.
/// </summary>
public sealed class RelationTypeTests
{
    [Fact]
    public void REL_002_creation_assigns_identity_code_name_and_active_status()
    {
        var type = RelationType.Create("contains", "Contains");

        Assert.NotEqual(Guid.Empty, type.Id.Value);
        Assert.Equal("contains", type.Code);
        Assert.Equal("Contains", type.Name);
        Assert.Equal(RelationDirection.Forward, type.Direction);
        Assert.Equal(RelationCardinality.ManyToMany, type.Cardinality);
        Assert.Equal(RelationTypeStatus.Active, type.Status);
        Assert.True(type.IsActive);
        Assert.Empty(type.AllowedSourceTypeIds);
        Assert.Empty(type.AllowedTargetTypeIds);
    }

    [Fact]
    public void creation_guards_required_values()
    {
        Assert.Throws<ArgumentException>(() => RelationType.Create(" ", "Name"));
        Assert.Throws<ArgumentException>(() => RelationType.Create("code", " "));
    }

    [Fact]
    public void REL_002_empty_endpoint_lists_accept_any_endpoint_type()
    {
        var type = RelationType.Create("references", "References");

        Assert.True(type.Accepts("anything.source", "anything.target"));
    }

    [Fact]
    public void REL_002_source_type_constraint_is_enforced()
    {
        var type = RelationType.Create("contains", "Contains", ["requirement"]);

        Assert.True(type.Accepts("requirement", "design"));
        Assert.False(type.Accepts("design", "design"));
    }

    [Fact]
    public void REL_002_target_type_constraint_is_enforced()
    {
        var type = RelationType.Create("contains", "Contains", null, ["design"]);

        Assert.True(type.Accepts("requirement", "design"));
        Assert.False(type.Accepts("requirement", "test"));
    }

    [Fact]
    public void REL_002_both_endpoint_constraints_are_enforced_together()
    {
        var type = RelationType.Create("implements", "Implements", ["design"], ["implementation"]);

        Assert.True(type.Accepts("design", "implementation"));
        Assert.False(type.Accepts("test", "implementation"));
        Assert.False(type.Accepts("design", "design"));
    }

    [Fact]
    public void SCH_003_deprecate_marks_the_type_inactive_and_is_not_repeatable()
    {
        var type = RelationType.Create("references", "References");

        type.Deprecate();

        Assert.Equal(RelationTypeStatus.Deprecated, type.Status);
        Assert.False(type.IsActive);
        Assert.Throws<InvalidOperationException>(() => type.Deprecate());
    }

    [Fact]
    public void REL_003_canonical_codes_are_exactly_the_eleven_mandated_values()
    {
        Assert.Equal(
        [
            "contains",
            "depends_on",
            "satisfies",
            "implements",
            "validates",
            "derived_from",
            "generated_by",
            "approved_by",
            "supersedes",
            "references",
            "conflicts_with",
        ], RelationTypeCodes.Canonical);
    }

    [Fact]
    public void REL_003_canonical_types_can_each_be_created_active_and_unrestricted()
    {
        var types = RelationTypeCodes.Canonical
            .Select(code => RelationType.Create(code, Humanize(code)))
            .ToList();

        Assert.Equal(11, types.Count);
        Assert.All(types, type =>
        {
            Assert.True(type.IsActive);
            Assert.Equal(RelationDirection.Forward, type.Direction);
            Assert.Equal(RelationCardinality.ManyToMany, type.Cardinality);
            Assert.Empty(type.AllowedSourceTypeIds);
            Assert.Empty(type.AllowedTargetTypeIds);
        });
        Assert.Equal(11, types.Select(t => t.Code).Distinct(StringComparer.Ordinal).Count());
    }

    private static string Humanize(string code) => code.Replace('_', ' ');
}