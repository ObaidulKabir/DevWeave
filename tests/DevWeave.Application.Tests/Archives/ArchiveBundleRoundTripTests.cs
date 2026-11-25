using System.Text;
using DevWeave.Application.Archives;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Evidence;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Application.Tests.Archives;

public sealed class ArchiveBundleRoundTripTests
{
    [Fact]
    public void Export_then_import_preserves_graph_semantics_and_records_the_identity_map()
    {
        var source = Seed();
        var payload = ArchiveGraphs.Capture(
            source.Tenant.Id.Value,
            source.Project.Id.Value,
            [source.Requirement, source.Decision],
            [source.Revision],
            [source.Edge],
            [source.Activity],
            [source.Evidence]);

        var bytes = ArchiveBundleCodec.Encode(payload);
        var document = new ArchiveReaderRegistry().Read(bytes);
        var destinationProject = Project.Create(
            source.Tenant.Id, "Clean", "clean", null, LifecycleProfileId.New(), source.Actor.Id);
        var replay = ArchiveGraphs.Replay(document.Payload, source.Tenant.Id, destinationProject.Id);

        Assert.Equal(ArchiveBundleCodec.FormatVersion, document.FormatVersion);
        Assert.NotEqual(source.Requirement.Id.Value, Assert.Single(replay.Items, item => item.Key == "FR-1").Id.Value);
        Assert.Equal(source.Requirement.Id.Value, replay.IdentityMap.Single(pair => pair.Value == replay.Items.Single(item => item.Key == "FR-1").Id.Value).Key);
        var importedRequirement = replay.Items.Single(item => item.Key == "FR-1");
        Assert.Equal("Checkout", importedRequirement.Title);
        Assert.Equal(InfoState.Verified, importedRequirement.State);
        Assert.Equal("satisfies", Assert.Single(replay.Relationships).RelationTypeCode);
        var importedEvidence = Assert.Single(replay.Evidence);
        Assert.Equal(importedRequirement.Id, importedEvidence.CriterionItemId);
        Assert.Equal(importedRequirement.CurrentRevisionId, importedEvidence.CriterionRevisionId);
        Assert.Equal(EvidenceReviewState.Accepted, importedEvidence.ReviewState);
        Assert.Equal(document.Checksum, new ArchiveReaderRegistry().Read(ArchiveBundleCodec.Encode(document.Payload)).Checksum);
    }

    [Fact]
    public void Unknown_format_and_checksum_mismatch_fail_closed()
    {
        var payload = ArchiveGraphs.Capture(Guid.NewGuid(), Guid.NewGuid(), [], [], [], [], []);
        var bytes = ArchiveBundleCodec.Encode(payload);
        var unknown = Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(bytes).Replace(ArchiveBundleCodec.FormatVersion, "devweave.archive.v9", StringComparison.Ordinal));
        var text = Encoding.UTF8.GetString(bytes);
        const string marker = "\"checksum\":\"";
        var checksumStart = text.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var chars = text.ToCharArray();
        chars[checksumStart] = chars[checksumStart] == 'a' ? 'b' : 'a';

        Assert.Contains("Unknown archive format", Assert.Throws<DevWeave.Application.Abstractions.ConflictException>(() => ArchiveBundleCodec.Decode(unknown)).Message);
        Assert.Contains(
            "checksum",
            Assert.Throws<DevWeave.Application.Abstractions.ConflictException>(() => ArchiveBundleCodec.Decode(Encoding.UTF8.GetBytes(chars))).Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Throws<DevWeave.Application.Abstractions.ConflictException>(() => ArchiveGraphs.EnsureClean(1));
    }

    private static Source Seed()
    {
        var tenant = Tenant.Create("Archive Corp");
        var actor = Principal.Create(tenant.Id, "owner@archive.test", PrincipalType.Human);
        var project = Project.Create(tenant.Id, "Source", "source", null, LifecycleProfileId.New(), actor.Id);
        var requirement = KnowledgeItem.Create(
            tenant.Id, project.Id, "FR-1", "Checkout", Classification.Internal, RepresentationKind.StructuredData,
            "requirements.functional", "1.0", actor.Id);
        var decision = KnowledgeItem.Create(
            tenant.Id, project.Id, "ADR-1", "Checkout decision", Classification.Internal, RepresentationKind.StructuredData,
            "architecture.decision", "1.0", actor.Id);
        var revision = requirement.AddRevision("1.0", "{\"statement\":\"checkout\"}", actor.Id, "initial", null, null, 0);
        requirement.Approve();
        var edge = Relationship.Create(
            tenant.Id, project.Id, decision.Id, requirement.Id, "satisfies", actor.Id, rationale: "decision covers the requirement");
        var activity = Activity.Create(
            tenant.Id, project.Id, ActivityType.Claim, actor.Id, "criterion met", ActivityStatus.Completed,
            DateTimeOffset.Parse("2026-11-01T00:00:00Z"), DateTimeOffset.Parse("2026-11-01T00:00:00Z"),
            null, null, null, [], [new ActivityOutput(requirement.Id, revision.Id, 0)]);
        var evidence = EvidenceRecord.Create(
            tenant.Id, project.Id, activity.Id, "test-run", "sha256:abc", null, requirement.Id, revision.Id, "Checkout");
        evidence.Review(EvidenceReviewState.Accepted, actor.Id);
        requirement.Verify(true);
        return new Source(tenant, actor, project, requirement, decision, revision, edge, activity, evidence);
    }

    private sealed record Source(
        Tenant Tenant,
        Principal Actor,
        Project Project,
        KnowledgeItem Requirement,
        KnowledgeItem Decision,
        KnowledgeRevision Revision,
        Relationship Edge,
        Activity Activity,
        EvidenceRecord Evidence);
}
