namespace DevWeave.Domain.Activities;

/// <summary>
/// The discriminating kind of a recorded provenance <see cref="Activity"/> (SRS PRO-005). Human and
/// machine activities share this single set (PRO-008): there is no bifurcation by actor kind, and the
/// enum distinguishes a claim, an observation, a piece of evidence, a verification result and an
/// approval so each is expressible in its own right.
/// </summary>
public enum ActivityType
{
    Created = 1,
    Updated = 2,
    Approved = 3,
    Rejected = 4,
    Verified = 5,
    Reviewed = 6,
    Transitioned = 7,
    Generated = 8,
    Claim = 9,
    Observation = 10,
    Evidence = 11,
    VerificationResult = 12,
    Approval = 13,
    Correction = 14,
    Retraction = 15,
    Supersession = 16,
}