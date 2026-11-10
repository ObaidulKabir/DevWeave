using DevWeave.Domain.Lifecycle;

namespace DevWeave.Application.Lifecycle;

public sealed record WorkItemDetails(WorkItem Item, IReadOnlyList<WorkItemId> DependsOn);
