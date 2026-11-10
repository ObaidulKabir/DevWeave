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
        Assert.Throws<InvalidOperationException>(() => relationship.Revoke());
    }
}