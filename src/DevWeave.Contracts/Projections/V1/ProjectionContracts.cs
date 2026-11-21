namespace DevWeave.Contracts.Projections.V1;

public sealed record StakeholderProjectionResponse(
    DateTimeOffset AsOf,
    string Profile,
    string Plan,
    string PolicyVersion,
    IReadOnlyList<ProjectionNodeView> Nodes,
    IReadOnlyList<ProjectionEdgeView> Edges,
    IReadOnlyList<ProjectionPathView> Paths,
    IReadOnlyList<ProjectionCountView> Counts,
    IReadOnlyList<ProjectionResourceRefView> SummaryInputs);

public sealed record ProjectionNodeView(
    ProjectionResourceRefView Resource,
    string Label,
    string Classification,
    DateTimeOffset RecordedAt);

public sealed record ProjectionEdgeView(
    ProjectionResourceRefView From,
    ProjectionResourceRefView To,
    string RelationType,
    ProjectionResourceRefView? Origin);

public sealed record ProjectionPathView(
    ProjectionResourceRefView Start,
    ProjectionResourceRefView End,
    IReadOnlyList<ProjectionEdgeView> Edges);

public sealed record ProjectionResourceRefView(
    string Kind,
    string Identifier,
    string? RevisionIdentifier);

public sealed record ProjectionCountView(
    string Key,
    decimal Value,
    string Unit,
    string IncludedPopulation,
    string ExcludedPopulation);
