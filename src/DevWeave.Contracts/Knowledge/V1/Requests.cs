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

/// <summary>
/// A review decision on one specific revision. <see cref="RevisionId"/> is mandatory so a decision
/// can never apply to content the reviewer did not read. <see cref="Comment"/> is optional for an
/// approval and is the required reason for a rejection.
/// </summary>
public sealed record ReviewRevisionRequest(Guid RevisionId, string? Comment);

public sealed record SearchKnowledgeRequest(
    string Text,
    string? TypeId = null,
    string? Lifecycle = null,
    string? Review = null,
    string? Verification = null,
    string? Currency = null,
    Guid? ActorId = null,
    DateTimeOffset? Since = null,
    int Limit = 20,
    string? Cursor = null);
