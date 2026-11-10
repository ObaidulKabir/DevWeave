using DevWeave.Domain.Knowledge;

namespace DevWeave.Contracts.Knowledge.V1;

public sealed record CreateKnowledgeItemRequest(
    string Key,
    string Title,
    Classification Classification,
    RepresentationKind RepresentationKind,
    string TypeId,
    string SchemaVersion,
    Guid CreatedBy);

public sealed record AddRevisionRequest(
    int ExpectedCurrentRevisionNumber,
    string SchemaVersion,
    string Content,
    Guid Actor,
    string Reason,
    string? SourceReference,
    Guid? SupersededRevisionId);