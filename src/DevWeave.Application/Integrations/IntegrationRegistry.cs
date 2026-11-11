using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Integrations;

namespace DevWeave.Application.Integrations;

public sealed class IntegrationRegistry : IIntegrationRegistry
{
    private readonly IIntegrationRepository _repository;
    private readonly IUnitOfWork _uow;

    public IntegrationRegistry(IIntegrationRepository repository, IUnitOfWork uow)
    {
        _repository = repository;
        _uow = uow;
    }

    public async Task<Guid> ConnectAsync(AuthorizedScope scope, IntegrationDraft draft,
        CancellationToken ct = default)
    {
        var integration = Integration.Connect(
            scope.TenantId,
            draft.Provider,
            draft.Scope,
            draft.CredentialRef,
            draft.AllowedMetadataFields);

        await _repository.AddAsync(integration, ct);
        await _uow.SaveChangesAsync(ct);

        return integration.Id;
    }

    public async Task RotateCredentialAsync(AuthorizedScope scope, Guid connectionId, string credentialRef,
        CancellationToken ct = default)
    {
        var integration = await GetAuthorizedIntegrationAsync(scope, connectionId, ct);
        integration.RotateCredential(credentialRef);

        await _uow.SaveChangesAsync(ct);
    }

    public async Task DisconnectAsync(AuthorizedScope scope, Guid connectionId, CancellationToken ct = default)
    {
        var integration = await GetAuthorizedIntegrationAsync(scope, connectionId, ct);
        integration.Disconnect();

        await _uow.SaveChangesAsync(ct);
    }

    private async Task<Integration> GetAuthorizedIntegrationAsync(AuthorizedScope scope, Guid connectionId,
        CancellationToken ct)
    {
        var integration = await _repository.GetByIdAsync(new IntegrationId(connectionId), ct);

        if (integration == null || integration.TenantId != scope.TenantId)
        {
            throw new NotFoundException($"Integration {connectionId} not found.");
        }

        return integration;
    }
}
