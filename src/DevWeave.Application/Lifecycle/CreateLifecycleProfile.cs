using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;

namespace DevWeave.Application.Lifecycle;

public sealed class CreateLifecycleProfile
{
    private readonly ILifecycleProfileRepository _profiles;
    private readonly IUnitOfWork _unitOfWork;

    public CreateLifecycleProfile(ILifecycleProfileRepository profiles, IUnitOfWork unitOfWork)
    {
        _profiles = profiles;
        _unitOfWork = unitOfWork;
    }

    public async Task<LifecycleProfile> ExecuteAsync(
        TenantId tenantId,
        string key,
        string name,
        string version,
        CancellationToken ct = default)
    {
        var profile = LifecycleProfile.Create(tenantId, key, name, version);

        await _profiles.AddAsync(profile, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return profile;
    }
}