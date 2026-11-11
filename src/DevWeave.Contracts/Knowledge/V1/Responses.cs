using DevWeave.Domain.Knowledge;

namespace DevWeave.Contracts.Knowledge.V1;

public sealed record KnowledgeItemResponse(
    Guid Id,
    Guid TenantId,
    Guid ProjectId,
    string Key,
    string Title,
    Classification Classification,
    RepresentationKind RepresentationKind,
    InfoState State,
    ReviewState ReviewState,
    VerificationState VerificationState,
    CurrencyState CurrencyState,
    string TypeId,
    string SchemaVersion,
    Guid? CurrentRevisionId,
    int CurrentRevisionNumber,
    Guid CreatedBy,
    DateTimeOffset CreatedAt);

public sealed record KnowledgeRevisionResponse(
    Guid Id,
    Guid TenantId,
    Guid KnowledgeItemId,
    int RevisionNumber,
    string SchemaVersion,
    string Content,
    Guid Actor,
    DateTimeOffset RecordedAt,
    string Reason,
    string? SourceReference,
    Guid? SupersededRevisionId);

/// <summary>
/// A conflict-friendly error body, returned with HTTP 409 for optimistic-concurrency and duplicate
/// resource conflicts (SRS REV-004).</summary>
public sealed record KnowledgeConflict(string Code, string Message);

public sealed record SearchKnowledgeHit(
    Guid ItemId,
    Guid RevisionId,
    string Title,
    string TypeId,
    InfoState Lifecycle,
    ReviewState Review,
    VerificationState Verification,
    CurrencyState Currency,
    DateTimeOffset RecordedAt,
    Guid ActorId,
    string ProvenanceRef);

public sealed record SearchKnowledgeResponse(
    IReadOnlyList<SearchKnowledgeHit> Items,
    string? NextCursor);

public sealed record ReviewDecisionResponse(
    Guid ReviewerId,
    string Decision,
    Guid RevisionId,
    int RevisionNumber,
    string? Comment,
    DateTimeOffset DecidedAt);
