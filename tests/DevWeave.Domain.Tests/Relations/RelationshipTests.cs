using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;

namespace DevWeave.Domain.Tests.Relations;

/// <summary>
/// Proves the Relationship domain invariants for Slice 5 (REL-001): a relationship carries a
/// tenant/project envelope, directed source/target items, a relation-type code and an Active default
/// status whose Revoke transition is terminal.
/// </summary>
public sealed class RelationshipTests
{
    [Fact]
    public void REL_001_creation_assigns_the_directed_envelope_and_active_status()
    {
        var relationship = Relationship.Create(
            TenantId.New(),
            ProjectId.New(),
            KnowledgeItemId.New(),
            KnowledgeItemId.New(),
            "contains",
            PrincipalId.New());

        Assert.NotEqual(Guid.Empty, relationship.Id.Value);
        Assert.NotEqual(Guid.Empty, relationship.TenantId.Value);
        Assert.NotEqual(Guid.Empty, relationship.ProjectId.Value);
        Assert.NotEqual(relationship.SourceItemId, relationship.TargetItemId);
        Assert.Equal("contains", relationship.RelationTypeCode);
        Assert.Equal(RelationshipStatus.Active, relationship.Status);
        Assert.Null(relationship.Rationale);
        Assert.Null(relationship.SourceReference);
        Assert.Null(relationship.ValidFrom);
        Assert.Null(relationship.ValidTo);
        Assert.Null(relationship.RevokedAt);
    }

    [Fact]
    public void creation_accepts_optional_rationale_source_and_validity_window()
    {
        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow.AddDays(30);
        var relationship = Relationship.Create(
            TenantId.New(),
            ProjectId.New(),
            KnowledgeItemId.New(),
            KnowledgeItemId.New(),
            "satisfies",
            PrincipalId.New(),
            rationale: "ADR-12",
            sourceReference: "docs/adr-12.md",
            validFrom: from,
            validTo: to);

        Assert.Equal("ADR-12", relationship.Rationale);
        Assert.Equal("docs/adr-12.md", relationship.SourceReference);
        Assert.Equal(from, relationship.ValidFrom);
        Assert.Equal(to, relationship.ValidTo);
    }

    [Fact]
    public void creation_guards_the_relation_type_code()
    {
        Assert.Throws<ArgumentException>(() => Relationship.Create(
            TenantId.New(),
            ProjectId.New(),
            KnowledgeItemId.New(),
            KnowledgeItemId.New(),
            " ",
            PrincipalId.New()));
    }

    [Fact]
    public void revoke_transitions_to_terminal_revoked_state()
    {
        var relationship = Relationship.Create(
            TenantId.New(),
            ProjectId.New(),
            KnowledgeItemId.New(),
            KnowledgeItemId.New(),
            "references",
            PrincipalId.New());

        relationship.Revoke();

        Assert.Equal(RelationshipStatus.Revoked, relationship.Status);
        Assert.NotNull(relationship.RevokedAt);
        Assert.Throws<InvalidOperationException>(() => relationship.Revoke());
    }
}