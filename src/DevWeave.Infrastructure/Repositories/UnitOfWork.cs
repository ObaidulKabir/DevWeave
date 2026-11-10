using DevWeave.Application.Abstractions;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly DevWeaveDbContext _db;

    public UnitOfWork(DevWeaveDbContext db)
    {
        _db = db;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The resource changed since it was read. Reload and retry with the current version.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_work_item_transitions_TenantId_ProjectId_WorkItemId_Version"
                or "IX_milestone_transitions_TenantId_ProjectId_MilestoneId_Version"
                or "IX_artifacts_TenantId_ProjectId_Key_Version"
        })
        {
            throw new ConflictException("The resource changed since it was read. Reload and retry with the current version.");
        }
    }
}
