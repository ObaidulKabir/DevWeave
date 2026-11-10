namespace DevWeave.Domain.Knowledge;

/// <summary>
/// Data classification tier for a knowledge item (SRS §7.6). The four ordered tiers bound who may
/// store and read the content; the exact policy is enforced by later authorization slices.
/// </summary>
public enum Classification
{
    Public = 1,
    Internal = 2,
    Confidential = 3,
    Restricted = 4,
}