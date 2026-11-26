namespace DevWeave.Application.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>
/// Runs several unit-of-work saves as one database transaction. A failure rolls every save back.
/// </summary>
public interface ITransactionRunner
{
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}