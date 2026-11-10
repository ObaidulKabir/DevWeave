namespace DevWeave.Domain.Lifecycle;

public sealed class WorkItemConcurrencyException(int expectedVersion, int actualVersion)
    : Exception($"Work item version conflict: expected {expectedVersion}, current version is {actualVersion}.")
{
    public int ExpectedVersion { get; } = expectedVersion;
    public int ActualVersion { get; } = actualVersion;
}
