using System.Data.Common;
using DevWeave.Application.Abstractions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DevWeave.Infrastructure.Persistence;

/// <summary>
/// Installs the verified tenant on each opened PostgreSQL session. Npgsql resets session state when
/// a pooled connection is returned, and this interceptor overwrites the setting on every checkout.
/// </summary>
public sealed class TenantSessionConnectionInterceptor(ITenantContext tenantContext)
    : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.tenant_id', @tenant_id, false)";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenant_id";
        parameter.Value = tenantContext.TenantId?.ToString("D") ?? string.Empty;
        command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
