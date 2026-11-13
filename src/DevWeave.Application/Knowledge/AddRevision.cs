using System.Text;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Lifecycle;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Schema;

namespace DevWeave.Application.Knowledge;

public sealed class AddRevision
{
    private readonly IKnowledgeItemRepository _items;
    private readonly IKnowledgeRevisionRepository _revisions;
    private readonly IPrincipalRepository _principals;
    private readonly ISchemaDefinitionRepository _schemas;
    private readonly IPayloadValidator _payloadValidator;
    private readonly IContentInspector _inspector;
    private readonly IProjectMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;

    public AddRevision(
        IKnowledgeItemRepository items,
        IKnowledgeRevisionRepository revisions,
        IPrincipalRepository principals,
        ISchemaDefinitionRepository schemas,
        IPayloadValidator payloadValidator,
        IContentInspector inspector,
        IProjectMembershipRepository memberships,
        IUnitOfWork unitOfWork)
    {
        _memberships = memberships;
        _items = items;
        _revisions = revisions;
        _principals = principals;
        _schemas = schemas;
        _payloadValidator = payloadValidator;
        _inspector = inspector;
        _unitOfWork = unitOfWork;
    }

    public async Task<KnowledgeRevision> ExecuteAsync(
        TenantId tenantId,
        KnowledgeItemId itemId,
        int expectedCurrentRevisionNumber,
        string schemaVersion,
        string content,
        PrincipalId actor,
        string reason,
        string? sourceReference,
        KnowledgeRevisionId? supersededRevisionId,
        CancellationToken ct = default)
    {
        var item = await _items.GetByIdAsync(tenantId, itemId, ct)
            ?? throw new NotFoundException($"Knowledge item '{itemId}' was not found within tenant '{tenantId}'.");

        // Deny-by-default: provenance actor must resolve inside the tenant (KNW-013, REV-002).
        var principal = await _principals.GetByIdAsync(tenantId, actor, ct)
            ?? throw new NotFoundException($"Principal '{actor}' was not found within tenant '{tenantId}'.");

        // Only active project members who can edit (not Viewers) may revise the project's knowledge.
        await ProjectWorkAuthorization.EnsureMemberAsync(_memberships, tenantId, item.ProjectId, actor, reviewerOnly: false, ct);

        // SRS 7.3 / REV-002: the item pins its (TypeId, SchemaVersion) immutably, so a revision's schema
        // version is derived, not caller-chosen. A caller-supplied version that disagrees is rejected so
        // the version recorded on the revision can never diverge from the schema the payload was checked
        // against.
        if (!string.Equals(schemaVersion, item.SchemaVersion, StringComparison.Ordinal))
            throw new SchemaVersionMismatchException(
                $"Revision schema version '{schemaVersion}' does not match the item's pinned version '{item.SchemaVersion}'.");

        // KNW-002: the item's pinned TypeId + SchemaVersion must reference an active (published) schema.
        // An unknown schema and an inactive (draft/deprecated) schema are indistinguishable from the
        // caller's perspective, so both surface as NotFound rather than disclosing registry internals.
        var schema = await _schemas.FindAsync(item.TypeId, item.SchemaVersion, ct);
        if (schema is null || !schema.IsActive)
            throw new NotFoundException($"No active schema '{item.TypeId}' version '{item.SchemaVersion}' is registered.");

        // Shared content policy (SRS 7.7, ART-008, AC-09): every ingress path runs the same inspection,
        // so secret material is never persisted and source-code-bearing content — including code text
        // mislabelled as Text/StructuredData, patches/diffs and code-bearing transcripts — is refused
        // before it can enter the canonical knowledge store. Revision content has no quarantine state,
        // so any non-Allow verdict is rejected outright (deny-by-default).
        var inspection = await _inspector.InspectAsync(
            string.Empty, "application/json", Encoding.UTF8.GetBytes(content), ct);
        if (inspection.Verdict != ContentVerdict.Allow)
            throw new ProhibitedContentException(
                $"Revision content for knowledge item '{itemId}' was rejected by the content policy.",
                inspection.Reasons);

        // KNW-004: the typed payload must conform to the registered JSON Schema.
        var issues = _payloadValidator.Validate(content, schema.Document);
        if (issues.Count > 0)
            throw new PayloadValidationException(issues);

        try
        {
            var revision = item.AddRevision(
                schemaVersion,
                content,
                principal.Id,
                reason,
                sourceReference,
                supersededRevisionId,
                expectedCurrentRevisionNumber);

            await _revisions.AddAsync(revision, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return revision;
        }
        catch (StaleRevisionException ex)
        {
            throw new ConflictException(ex.Message);
        }
    }
}