using DevWeave.Domain.Schema;

namespace DevWeave.Domain.Tests.Schema;

public sealed class CanonicalKnowledgeCatalogueTests
{
    [Fact]
    public void Catalogue_covers_appendix_a_types_without_duplicating_aggregate_storage()
    {
        Assert.Contains(CanonicalKnowledgeCatalogue.Types, type => type.TypeId == "requirements.functional");
        Assert.Contains(CanonicalKnowledgeCatalogue.Types, type => type.TypeId == "requirements.nonfunctional");
        Assert.Equal(
            KnowledgeStorageKind.WorkItemAggregate,
            CanonicalKnowledgeCatalogue.Find("planning.work-item")!.StorageKind);
        Assert.False(CanonicalKnowledgeCatalogue.Find("planning.work-item")!.AllowsKnowledgeEnvelope);
        Assert.False(CanonicalKnowledgeCatalogue.SchemaDocuments.ContainsKey("planning.work-item"));
        Assert.True(CanonicalKnowledgeCatalogue.SchemaDocuments.ContainsKey("requirements.nonfunctional"));
    }

    [Fact]
    public void Published_schemas_are_stable_and_published()
    {
        var schemas = CanonicalKnowledgeCatalogue.PublishedSchemas().ToArray();
        Assert.NotEmpty(schemas);
        Assert.All(schemas, schema =>
        {
            Assert.Equal(SchemaStatus.Published, schema.Status);
            Assert.Equal(CanonicalKnowledgeCatalogue.SchemaVersion, schema.Version);
            Assert.Equal(
                CanonicalKnowledgeCatalogue.StableSchemaId(schema.Namespace),
                schema.Id);
        });
        Assert.Equal(
            schemas.Length,
            schemas.Select(schema => schema.Namespace).Distinct(StringComparer.Ordinal).Count());
    }
}
