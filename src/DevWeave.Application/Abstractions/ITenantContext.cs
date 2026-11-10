namespace DevWeave.Application.Abstractions;

/// <summary>
/// Tenant scope established by a trusted request or an explicitly scoped background operation.
/// An unset value must result in database row-level security matching no tenant rows.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; set; }
}
