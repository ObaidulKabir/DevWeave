namespace DevWeave.Domain.Identity;

/// <summary>
/// Classifies the kind of actor carrying a <see cref="Principal"/> (SRS TEN-003).
/// </summary>
public enum PrincipalType
{
    Human = 1,
    ServiceAccount = 2,
    AiAgent = 3,
}