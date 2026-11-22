using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Projections;

/// <summary>
/// Named presentation intent for a stakeholder view. Profiles never grant authority; membership
/// and agent grants remain the source of access. A profile may only further restrict what an
/// already-authorized caller sees.
/// </summary>
public enum StakeholderProfile
{
    Owner = 1,
    Investor = 2,
    Agent = 3,
    Learner = 4,
    User = 5,
}

public static class StakeholderProfileCatalog
{
    public static Classification MaxClassification(StakeholderProfile profile) =>
        profile switch
        {
            StakeholderProfile.Owner => Classification.Restricted,
            StakeholderProfile.Agent => Classification.Restricted,
            StakeholderProfile.Investor => Classification.Internal,
            StakeholderProfile.Learner => Classification.Internal,
            StakeholderProfile.User => Classification.Public,
            _ => Classification.Public,
        };

    public static bool AllowsKind(StakeholderProfile profile, ProjectResourceKind kind) =>
        profile switch
        {
            StakeholderProfile.Owner => true,
            StakeholderProfile.Investor => kind is
                ProjectResourceKind.Project or
                ProjectResourceKind.KnowledgeItem or
                ProjectResourceKind.KnowledgeRevision or
                ProjectResourceKind.WorkItem or
                ProjectResourceKind.PlanBaseline or
                ProjectResourceKind.Evidence,
            StakeholderProfile.Agent => kind is
                ProjectResourceKind.KnowledgeItem or
                ProjectResourceKind.KnowledgeRevision or
                ProjectResourceKind.Relationship or
                ProjectResourceKind.WorkItem or
                ProjectResourceKind.WorkPackage or
                ProjectResourceKind.Evidence or
                ProjectResourceKind.Activity or
                ProjectResourceKind.PlanBaseline or
                ProjectResourceKind.ChangeSet or
                ProjectResourceKind.CodebaseTarget,
            StakeholderProfile.Learner => kind is
                ProjectResourceKind.KnowledgeItem or
                ProjectResourceKind.KnowledgeRevision or
                ProjectResourceKind.Relationship,
            StakeholderProfile.User => kind is
                ProjectResourceKind.KnowledgeItem or
                ProjectResourceKind.KnowledgeRevision or
                ProjectResourceKind.PlanBaseline,
            _ => false,
        };
}

public static class ProjectionPlanCodes
{
    public const string Overview = "overview";
    public const string Knowledge = "knowledge";
    public const string Traceability = "traceability";
    public const string Progress = "progress";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Overview, Knowledge, Traceability, Progress,
    };

    public static bool AllowsKind(string plan, ProjectResourceKind kind) =>
        plan.ToLowerInvariant() switch
        {
            Knowledge => kind is
                ProjectResourceKind.KnowledgeItem or
                ProjectResourceKind.KnowledgeRevision or
                ProjectResourceKind.Relationship,
            Traceability => kind is
                ProjectResourceKind.KnowledgeItem or
                ProjectResourceKind.KnowledgeRevision or
                ProjectResourceKind.Relationship or
                ProjectResourceKind.Evidence or
                ProjectResourceKind.WorkItem,
            Progress => kind is
                ProjectResourceKind.WorkItem or
                ProjectResourceKind.WorkPackage or
                ProjectResourceKind.PlanBaseline or
                ProjectResourceKind.Evidence,
            _ => true,
        };
}
