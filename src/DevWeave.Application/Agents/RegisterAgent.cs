using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Agents;

/// <summary>
/// Registers a new AI agent machine principal (SRS AGT-001). The agent is stored as a standard
/// <see cref="Principal"/> with <see cref="PrincipalType.AiAgent"/>, so it shares the existing principal
/// identity and lifecycle rather than forking a second machine-identity entity.
/// </summary>
public sealed class RegisterAgent
{
    private readonly IPrincipalRepository _principals;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterAgent(IPrincipalRepository principals, IUnitOfWork unitOfWork)
    {
        _principals = principals;
        _unitOfWork = unitOfWork;
    }

    public async Task<Principal> ExecuteAsync(TenantId tenantId, string email, CancellationToken ct = default)
    {
        var agent = Principal.Create(tenantId, email, PrincipalType.AiAgent);

        await _principals.AddAsync(agent, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return agent;
    }
}