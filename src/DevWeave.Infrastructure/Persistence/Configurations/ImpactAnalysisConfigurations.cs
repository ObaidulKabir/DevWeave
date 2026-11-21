using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class RequirementChangeSetConfiguration : IEntityTypeConfiguration<RequirementChangeSet>
{
    public void Configure(EntityTypeBuilder<RequirementChangeSet> builder)
    {
        builder.ToTable("requirement_change_sets");
        builder.HasKey(set => set.Id);

        builder.Property(set => set.Id)
            .HasConversion(id => id.Value, value => new RequirementChangeSetId(value))
            .ValueGeneratedNever();
        builder.Property(set => set.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(set => set.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));
        builder.Property(set => set.RecordedBy)
            .HasConversion(id => id.Value, value => new PrincipalId(value));
        builder.Property(set => set.RecordedAt);
        builder.Property(set => set.CorrelationId).HasMaxLength(200);

        builder.OwnsMany(set => set.Changes, changes =>
        {
            changes.ToTable("requirement_change_entries");
            changes.WithOwner().HasForeignKey("change_set_id");
            changes.HasKey("change_set_id", nameof(RequirementChangeEntry.Position));
            changes.Property(entry => entry.Position).ValueGeneratedNever();
            changes.Property(entry => entry.Kind).HasConversion<int>();
            changes.Property(entry => entry.PreviousRevisionIds).HasColumnType("uuid[]");
            changes.Property(entry => entry.ResultRevisionIds).HasColumnType("uuid[]");
            changes.Property(entry => entry.ActorId);
            changes.Property(entry => entry.Reason).HasMaxLength(2000).IsRequired();
            changes.Property(entry => entry.ValidAt);
            changes.Property(entry => entry.IdempotencyKey).HasMaxLength(200).IsRequired();
        });

        builder.Navigation(set => set.Changes)
            .HasField("_changes")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(set => new { set.TenantId, set.ProjectId });
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(set => set.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(set => set.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ImpactAnalysisReportConfiguration : IEntityTypeConfiguration<ImpactAnalysisReport>
{
    public void Configure(EntityTypeBuilder<ImpactAnalysisReport> builder)
    {
        builder.ToTable("impact_analysis_reports");
        builder.HasKey(report => report.Id);

        builder.Property(report => report.Id)
            .HasConversion(id => id.Value, value => new ImpactAnalysisReportId(value))
            .ValueGeneratedNever();
        builder.Property(report => report.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(report => report.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));
        builder.Property(report => report.ChangeSetId)
            .HasConversion(id => id.Value, value => new RequirementChangeSetId(value));
        builder.Property(report => report.RulesVersion).HasMaxLength(100).IsRequired();
        builder.Property(report => report.PolicyVersion).HasMaxLength(100).IsRequired();
        builder.Property(report => report.AnalyzedAt);
        builder.Property(report => report.AnalyzedBy)
            .HasConversion(id => id.Value, value => new PrincipalId(value));

        builder.OwnsMany(report => report.Findings, findings =>
        {
            findings.ToTable("impact_analysis_findings");
            findings.WithOwner().HasForeignKey("report_id");
            findings.HasKey("report_id", nameof(ImpactFinding.Position));
            findings.Property(finding => finding.Position).ValueGeneratedNever();
            findings.Property(finding => finding.ItemId);
            findings.Property(finding => finding.RevisionId);
            findings.Property(finding => finding.ResourceKind).HasMaxLength(200).IsRequired();
            findings.Property(finding => finding.Disposition).HasConversion<int>();
            findings.Property(finding => finding.RelationPath).HasMaxLength(2000).IsRequired();
            findings.Property(finding => finding.Rule).HasMaxLength(500).IsRequired();
        });

        builder.Navigation(report => report.Findings)
            .HasField("_findings")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(report => new { report.TenantId, report.ProjectId });
        builder.HasIndex(report => new { report.TenantId, report.ChangeSetId });
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(report => report.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(report => report.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PlanRebaseDecisionConfiguration : IEntityTypeConfiguration<PlanRebaseDecision>
{
    public void Configure(EntityTypeBuilder<PlanRebaseDecision> builder)
    {
        builder.ToTable("plan_rebase_decisions");
        builder.HasKey(decision => decision.Id);

        builder.Property(decision => decision.Id)
            .HasConversion(id => id.Value, value => new PlanRebaseDecisionId(value))
            .ValueGeneratedNever();
        builder.Property(decision => decision.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(decision => decision.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));
        builder.Property(decision => decision.WorkPackageId)
            .HasConversion(id => id.Value, value => new WorkPackageId(value));
        builder.Property(decision => decision.ImpactReportId)
            .HasConversion(id => id.Value, value => new ImpactAnalysisReportId(value));
        builder.Property(decision => decision.Kind).HasConversion<int>();
        builder.Property(decision => decision.AuthorizedBy)
            .HasConversion(id => id.Value, value => new PrincipalId(value));
        builder.Property(decision => decision.AuthorizedAt);
        builder.Property(decision => decision.Reason).HasMaxLength(2000).IsRequired();

        builder.HasIndex(decision => new { decision.TenantId, decision.WorkPackageId, decision.AuthorizedAt });
        builder.HasIndex(decision => new { decision.TenantId, decision.ImpactReportId });
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(decision => decision.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(decision => decision.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
