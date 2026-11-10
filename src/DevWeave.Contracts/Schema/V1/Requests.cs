namespace DevWeave.Contracts.Schema.V1;

public sealed record RegisterSchemaRequest(
    string Namespace,
    string Version,
    string Owner,
    string Description,
    string Document);