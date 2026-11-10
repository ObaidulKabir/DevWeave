using DevWeave.Domain.Projects;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Application.Evidence;

/// <summary>
/// The pure result of checking whether a claim is verified (SRS PRO-006): the claim identifier, its
/// project, the boolean verification verdict and the evidence records that drove it. A claim is verified
/// only when at least one of its evidence records is accepted.
/// </summary>
public sealed record ClaimVerificationResult(
    Guid ClaimId,
    ProjectId ProjectId,
    bool IsVerified,
    IReadOnlyList<EvidenceRecord> Evidence);