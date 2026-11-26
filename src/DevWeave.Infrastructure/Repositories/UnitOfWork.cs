using DevWeave.Application.Abstractions;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class UnitOfWork : IUnitOfWork, ITransactionRunner
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
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_plan_baselines_TenantId_ProjectId_ExpectedPlanVersion",
        })
        {
            throw new ConflictException("Another baseline locked this plan version at the same moment. Refresh the lock preview and try again.");
        }
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        if (_db.Database.CurrentTransaction is not null)
        {
            await action(ct);
            return;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await action(ct);
        await transaction.CommitAsync(ct);
    }
}
