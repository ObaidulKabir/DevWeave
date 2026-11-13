namespace DevWeave.Domain.Specifications;

/// <summary>
/// Convenience view of the default built-in catalog (packs enabled by default, no overlays). Tenant and
/// project catalogs are built by <see cref="SpecCatalogBuilder"/>; this is the baseline every one starts from.
/// </summary>
public static class SpecTopicCatalog
{
    public static IReadOnlyList<SpecTopic> Topics => SpecCatalog.Default.Topics;

    public static IReadOnlyList<SpecCandidate> PredefinedDecisions => SpecCatalog.Default.PredefinedDecisions;

    public static SpecTopic? Find(string topicId) => SpecCatalog.Default.Find(topicId);
}
