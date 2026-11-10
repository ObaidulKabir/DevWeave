using DevWeave.Application.Abstractions;

namespace DevWeave.Infrastructure.Persistence;

internal sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; set; }
}
