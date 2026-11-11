using DevWeave.Application.Activities;
using DevWeave.Application.Audit;
using DevWeave.Application.Commercial;
using DevWeave.Application.Coordination;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.Archives;
using DevWeave.Application.Integrations;
using DevWeave.Application.Agents;
using DevWeave.Application.Artifacts;
using DevWeave.Application.Evidence;
using DevWeave.Application.Identity;
using DevWeave.Application.Knowledge;
using DevWeave.Application.Lifecycle;
using DevWeave.Application.Planning;
using DevWeave.Application.Projects;
using DevWeave.Application.Relations;
using DevWeave.Application.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateTenant>();
        services.AddScoped<GetTenant>();
        services.AddScoped<CreatePrincipal>();
        services.AddScoped<InviteMember>();
        services.AddScoped<ActivateMember>();
        services.AddScoped<RevokeMember>();
        services.AddScoped<ListMembersForTenant>();

        services.AddScoped<CreateProject>();
        services.AddScoped<GetProject>();
        services.AddScoped<ListProjectsForTenant>();
        services.AddScoped<ArchiveProject>();
        services.AddScoped<RestoreProject>();
        services.AddScoped<DeleteProject>();
        services.AddScoped<AddProjectReference>();
        services.AddScoped<GrantProjectMembership>();
        services.AddScoped<RevokeProjectMembership>();
        services.AddScoped<CreateLifecycleProfile>();
        services.AddScoped<ListLifecycleProfilesForTenant>();
        services.AddScoped<CreateMilestone>();
        services.AddScoped<ListMilestones>();
        services.AddScoped<TransitionMilestone>();
        services.AddScoped<ListMilestoneTransitions>();
        services.AddScoped<CreateWorkItem>();
        services.AddScoped<ListWorkItems>();
        services.AddScoped<TransitionWorkItem>();
        services.AddScoped<ListWorkItemTransitions>();

        services.AddScoped<CreateKnowledgeItem>();
        services.AddScoped<PlanDesignService>();
        services.AddScoped<PlanLockReview>();
        services.AddScoped<GetKnowledgeItem>();
        services.AddScoped<ListKnowledgeItemsForProject>();
        services.AddScoped<ListKnowledgeItemsForTenant>();
        services.AddScoped<SearchKnowledge>();
        services.AddScoped<AddRevision>();
        services.AddScoped<ApproveItem>();
        services.AddScoped<RejectItem>();
        services.AddScoped<ReviewKnowledge>();
        services.AddScoped<ListReviewDecisions>();
        services.AddScoped<ListRevisionsForItem>();
        services.AddScoped<VerifyItem>();
        services.AddScoped<GetRevision>();

        services.AddScoped<RegisterSchema>();
        services.AddScoped<PublishSchema>();
        services.AddScoped<DeprecateSchema>();
        services.AddScoped<GetSchema>();
        services.AddScoped<ListSchemas>();
        services.AddScoped<DiscoverSchemas>();

        services.AddScoped<CreateRelationship>();
        services.AddScoped<RevokeRelationship>();
        services.AddScoped<GetRelationship>();
        services.AddScoped<ListRelationshipsForProject>();
        services.AddScoped<ListRelationshipsForItem>();
        services.AddScoped<TraverseRelationships>();
        services.AddScoped<ListRelationTypes>();
        services.AddScoped<RegisterRelationType>();
        services.AddScoped<DeprecateRelationType>();
        services.AddScoped<GetRelationType>();

        services.AddScoped<RecordActivity>();
        services.AddScoped<GetActivity>();
        services.AddScoped<ListActivitiesForProject>();
        services.AddScoped<ListActivitiesForItem>();
        services.AddScoped<ListActivitiesByActor>();
        services.AddScoped<ReconstructProjectState>();
        services.AddScoped<ExportProvenance>();

        services.AddScoped<UploadArtifact>();
        services.AddScoped<GetArtifact>();
        services.AddScoped<ListArtifactsForProject>();
        services.AddScoped<ListArtifactVersions>();
        services.AddScoped<VerifyArtifact>();
        services.AddScoped<GetArtifactDownloadUrl>();
        services.AddScoped<PurgeExpiredQuarantine>();

        services.AddScoped<RecordEvidence>();
        services.AddScoped<ReviewEvidence>();
        services.AddScoped<ListEvidenceForClaim>();
        services.AddScoped<ListEvidenceForProject>();
        services.AddScoped<GetClaimVerification>();

        services.AddScoped<RegisterAgent>();
        services.AddScoped<IssueAgentCredential>();
        services.AddScoped<RevokeAgentCredential>();
        services.AddScoped<DisableAgentCredential>();
        services.AddScoped<EnableAgentCredential>();
        services.AddScoped<RotateAgentCredential>();
        services.AddScoped<ListAgentCredentials>();
        services.AddScoped<VerifyAgentCredential>();
        services.AddScoped<AuthenticateAgent>();
        services.AddScoped<GrantAgentCapability>();
        services.AddScoped<ApproveAgentGrant>();
        services.AddScoped<RevokeAgentGrant>();
        services.AddScoped<DiscoverAgentCapabilities>();
        services.AddScoped<AssembleContextPackage>();
        services.AddScoped<RecordAgentExecution>();

        services.AddScoped<IIntegrationRegistry, IntegrationRegistry>();
        services.AddScoped<IInboundEventProcessor, InboundEventProcessor>();
        services.AddScoped<IAuditJournal, AuditJournal>();
        services.AddScoped<IPortableArchive, PortableArchive>();
        services.AddScoped<IProjectReporting, ProjectReporting>();

        var coordinationService = typeof(CoordinationService);
        services.AddScoped<ICoordinationChannels>(sp => sp.GetRequiredService<CoordinationService>());
        services.AddScoped<IRuntimeRegistry>(sp => sp.GetRequiredService<CoordinationService>());
        services.AddScoped<ICampaignCoordinator>(sp => sp.GetRequiredService<CoordinationService>());
        services.AddScoped<ILeaseAuthority>(sp => sp.GetRequiredService<CoordinationService>());
        services.AddScoped<IPlanBaselines>(sp => sp.GetRequiredService<CoordinationService>());
        services.AddScoped<ICodebaseRegistry>(sp => sp.GetRequiredService<CoordinationService>());
        services.AddScoped<IProjectMcpDirectory>(sp => sp.GetRequiredService<CoordinationService>());
        services.AddScoped<IParticipationScheduler>(sp => sp.GetRequiredService<CoordinationService>());
        services.AddScoped<IIntegrationQueue>(sp => sp.GetRequiredService<CoordinationService>());
        services.AddScoped<IKillSwitchAuthority>(sp => sp.GetRequiredService<CoordinationService>());
        services.AddScoped<CoordinationService>();

        services.AddScoped<IPlanCatalogue>(sp => sp.GetRequiredService<CommercialService>());
        services.AddScoped<IEntitlementEvaluator>(sp => sp.GetRequiredService<CommercialService>());
        services.AddScoped<IUsageMeter>(sp => sp.GetRequiredService<CommercialService>());
        services.AddScoped<CommercialService>();

        return services;
    }
}
