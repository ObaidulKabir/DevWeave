using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Knowledge;

public sealed class CreateKnowledgeItem
{
    private readonly IKnowledgeItemRepository _items;
    private readonly IProjectRepository _projects;
    private readonly IPrincipalRepository _principals;
    private readonly ISchemaDefinitionRepository _schemas;
    private readonly IUnitOfWork _unitOfWork;

    public CreateKnowledgeItem(
        IKnowledgeItemRepository items,
        IProjectRepository projects,
        IPrincipalRepository principals,
        ISchemaDefinitionRepository schemas,
        IUnitOfWork unitOfWork)
    {
        _items = items;
        _projects = projects;
        _principals = principals;
        _schemas = schemas;
        _unitOfWork = unitOfWork;
    }

    public async Task<KnowledgeItem> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        string key,
        string title,
        Classification classification,
        RepresentationKind representationKind,
        string typeId,
        string schemaVersion,
        PrincipalId createdBy,
        CancellationToken ct = default,
        string? description = null)
    {
        // Deny-by-default: a project from another tenant is indistinguishable from a missing one.
        _ = await _projects.GetByIdAsync(tenantId, projectId, ct)
            ?? throw new NotFoundException($"Project '{projectId}' was not found within tenant '{tenantId}'.");

        // Deny-by-default: only an actor resolvable inside the tenant may create an item (KNW-013).
        _ = await _principals.GetByIdAsync(tenantId, createdBy, ct)
            ?? throw new NotFoundException($"Principal '{createdBy}' was not found within tenant '{tenantId}'.");

        // KNW-002 / SRS 7.3: the requested type + version must reference an active (published) schema so
        // an item can never be created against an unknown or inactive type. An unknown schema and a
        // non-active one are indistinguishable from the caller's perspective, so both surface as NotFound.
        var schema = await _schemas.FindAsync(typeId, schemaVersion, ct);
        if (schema is null || !schema.IsActive)
            throw new NotFoundException($"No active schema '{typeId}' version '{schemaVersion}' is registered.");

        if (await _items.KeyExistsAsync(tenantId, projectId, key, ct))
            throw new ConflictException($"A knowledge item with key '{key}' already exists in project '{projectId}' within tenant '{tenantId}'.");

        var item = KnowledgeItem.Create(
            tenantId,
            projectId,
            key,
            title,
            classification,
            representationKind,
            typeId,
            schemaVersion,
            createdBy,
            description);

        await _items.AddAsync(item, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return item;
    }
}