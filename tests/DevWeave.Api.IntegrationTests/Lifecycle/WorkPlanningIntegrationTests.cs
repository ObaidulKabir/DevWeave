using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Application.Abstractions;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Api.IntegrationTests.Lifecycle;

public sealed class WorkPlanningIntegrationTests(IdentityTestFactory factory)
    : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    [Fact]
    public async Task LFC_003_005_project_members_can_plan_report_verify_and_approve_with_audited_states()
    {
        factory.PrepareDatabase();
        var tenant = Tenant.Create("Work Planning Tenant");
        var profile = LifecycleProfile.Create(tenant.Id, "default", "Default", "1.0");
        var owner = Principal.Create(tenant.Id, "owner@example.test", PrincipalType.Human);
        var reporter = Principal.Create(tenant.Id, "reporter@example.test", PrincipalType.Human);
        var verifier = Principal.Create(tenant.Id, "verifier@example.test", PrincipalType.Human);
        var viewer = Principal.Create(tenant.Id, "viewer@example.test", PrincipalType.Human);
        var project = Project.Create(tenant.Id, "Work Planning", "work-planning", null, profile.Id, owner.Id);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            db.Tenants.Add(tenant);
            db.LifecycleProfiles.Add(profile);
            db.Principals.AddRange(owner, reporter, verifier, viewer);
            db.Projects.Add(project);
            db.ProjectMemberships.AddRange(
                ProjectMembership.Grant(tenant.Id, project.Id, owner.Id, ProjectRole.Owner),
                ProjectMembership.Grant(tenant.Id, project.Id, reporter.Id, ProjectRole.Member),
                ProjectMembership.Grant(tenant.Id, project.Id, verifier.Id, ProjectRole.Admin),
                ProjectMembership.Grant(tenant.Id, project.Id, viewer.Id, ProjectRole.Viewer));
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var reporterClient = AuthenticatedClient(factory, tenant.Id, reporter.Id);
        using var verifierClient = AuthenticatedClient(factory, tenant.Id, verifier.Id);
        using var ownerClient = AuthenticatedClient(factory, tenant.Id, owner.Id);
        using var viewerClient = AuthenticatedClient(factory, tenant.Id, viewer.Id);
        var route = $"/tenants/{tenant.Id}/projects/{project.Id}";

        var mismatchedTenant = await reporterClient.GetAsync(
            $"/tenants/{Guid.NewGuid()}/projects/{project.Id}/work-items");
        Assert.Equal(HttpStatusCode.NotFound, mismatchedTenant.StatusCode);
        var viewerWrite = await viewerClient.PostAsJsonAsync($"{route}/milestones",
            new CreateMilestoneRequest("NOPE", "Viewer cannot write", null), Json);
        Assert.Equal(HttpStatusCode.NotFound, viewerWrite.StatusCode);

        var milestoneResponse = await reporterClient.PostAsJsonAsync($"{route}/milestones",
            new CreateMilestoneRequest("M1", "Foundation", DateTimeOffset.UtcNow.AddDays(10)), Json);
        Assert.Equal(HttpStatusCode.Created, milestoneResponse.StatusCode);
        var milestone = await milestoneResponse.Content.ReadFromJsonAsync<MilestoneResponse>(Json);
        Assert.NotNull(milestone);

        var milestoneTransition = await verifierClient.PatchAsJsonAsync(
            $"{route}/milestones/{milestone!.Id}/state",
            new TransitionMilestoneRequest(MilestoneState.InProgress, "work started", milestone.Version), Json);
        Assert.Equal(HttpStatusCode.OK, milestoneTransition.StatusCode);

        var createResponse = await reporterClient.PostAsJsonAsync($"{route}/work-items",
            new CreateWorkItemRequest("DW-1", "Implement work planning", "Create the MVP work planning slice",
                milestone.Id, null, null, WorkPriority.High, Array.Empty<Guid>()), Json);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var item = await createResponse.Content.ReadFromJsonAsync<WorkItemResponse>(Json);
        Assert.NotNull(item);
        Assert.Equal(PlannedWorkState.Planned, item.PlannedState);
        Assert.Equal(ReportedExecutionState.NotStarted, item.ReportedState);
        Assert.Equal(WorkVerificationState.Unverified, item.VerificationState);
        Assert.Equal(WorkApprovalState.Pending, item.ApprovalState);
        Assert.Equal(1, item.Version);

        var dependentResponse = await reporterClient.PostAsJsonAsync($"{route}/work-items",
            new CreateWorkItemRequest("DW-2", "Add contract tests", "Test the work planning API",
                milestone.Id, null, null, WorkPriority.Normal, new[] { item.Id }), Json);
        Assert.Equal(HttpStatusCode.Created, dependentResponse.StatusCode);
        var listed = await reporterClient.GetFromJsonAsync<WorkItemResponse[]>($"{route}/work-items", Json);
        Assert.NotNull(listed);
        Assert.Equal(item.Id, Assert.Single(listed, value => value.Key == "DW-2").DependsOn.Single());

        var verifyBeforeReport = await verifierClient.PostAsJsonAsync(
            $"{route}/work-items/{item.Id}/verify", new ReviewWorkRequest("verify", item.Version), Json);
        Assert.Equal(HttpStatusCode.Conflict, verifyBeforeReport.StatusCode);

        var reportResponse = await reporterClient.PostAsJsonAsync(
            $"{route}/work-items/{item.Id}/report",
            new ReportWorkRequest(ReportedExecutionState.Completed, "implementation complete", item.Version), Json);
        Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);
        var report = await reportResponse.Content.ReadFromJsonAsync<WorkItemChangeResponse>(Json);
        Assert.NotNull(report);
        Assert.Equal(ReportedExecutionState.Completed, report.Item.ReportedState);
        Assert.Equal(PlannedWorkState.Planned, report.Item.PlannedState);
        Assert.Equal(2, report.Item.Version);

        var memberApproval = await reporterClient.PostAsJsonAsync(
            $"{route}/work-items/{item.Id}/verify", new ReviewWorkRequest("verify", report.Item.Version), Json);
        Assert.Equal(HttpStatusCode.NotFound, memberApproval.StatusCode);

        var verifyResponse = await verifierClient.PostAsJsonAsync(
            $"{route}/work-items/{item.Id}/verify",
            new ReviewWorkRequest("acceptance criteria checked", report.Item.Version), Json);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var verified = await verifyResponse.Content.ReadFromJsonAsync<WorkItemChangeResponse>(Json);
        Assert.NotNull(verified);
        Assert.Equal(WorkVerificationState.Verified, verified.Item.VerificationState);
        Assert.Equal(WorkApprovalState.Pending, verified.Item.ApprovalState);
        Assert.Equal(3, verified.Item.Version);

        var staleApproval = await ownerClient.PostAsJsonAsync(
            $"{route}/work-items/{item.Id}/approve",
            new ReviewWorkRequest("stale command", 2), Json);
        Assert.Equal(HttpStatusCode.Conflict, staleApproval.StatusCode);

        var approvalResponse = await ownerClient.PostAsJsonAsync(
            $"{route}/work-items/{item.Id}/approve",
            new ReviewWorkRequest("approved after verification", verified.Item.Version), Json);
        Assert.Equal(HttpStatusCode.OK, approvalResponse.StatusCode);
        var approved = await approvalResponse.Content.ReadFromJsonAsync<WorkItemChangeResponse>(Json);
        Assert.NotNull(approved);
        Assert.Equal(WorkApprovalState.Approved, approved.Item.ApprovalState);
        Assert.Equal(WorkVerificationState.Verified, approved.Item.VerificationState);
        Assert.Equal(4, approved.Item.Version);

        var transitions = await reporterClient.GetFromJsonAsync<WorkItemTransitionResponse[]>(
            $"{route}/work-items/{item.Id}/transitions", Json);
        Assert.NotNull(transitions);
        Assert.Equal(3, transitions.Length);
        Assert.Equal(new[]
        {
            WorkItemTransitionDimension.Reported,
            WorkItemTransitionDimension.Verification,
            WorkItemTransitionDimension.Approval,
        }, transitions.Select(transition => transition.Dimension));

        var milestoneTransitions = await verifierClient.GetFromJsonAsync<MilestoneTransitionResponse[]>(
            $"{route}/milestones/{milestone.Id}/transitions", Json);
        Assert.NotNull(milestoneTransitions);
        Assert.Equal(MilestoneState.InProgress, Assert.Single(milestoneTransitions).ToState);

        using var unauthenticatedRequest = new HttpRequestMessage(
            HttpMethod.Get, $"{route}/work-items");
        unauthenticatedRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", "invalid-token");
        var unauthenticated = await client.SendAsync(unauthenticatedRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        // Two independent units of work read the same version; only one may advance the row.
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        var firstCopy = await firstDb.WorkItems.SingleAsync(value => value.Key == "DW-1");
        var secondCopy = await secondDb.WorkItems.SingleAsync(value => value.Key == "DW-1");
        var firstTransition = firstCopy.ChangePlannedState(
            PlannedWorkState.Ready, owner.Id, "ready", firstCopy.Version);
        var secondTransition = secondCopy.ChangePlannedState(
            PlannedWorkState.Ready, owner.Id, "ready", secondCopy.Version);
        firstDb.WorkItemTransitions.Add(firstTransition);
        secondDb.WorkItemTransitions.Add(secondTransition);
        await firstDb.SaveChangesAsync();
        var transitionCount = await firstDb.WorkItemTransitions.CountAsync(
            value => value.WorkItemId == firstCopy.Id);
        var secondUnitOfWork = secondScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await Assert.ThrowsAsync<ConflictException>(() => secondUnitOfWork.SaveChangesAsync());
        Assert.Equal(transitionCount, await firstDb.WorkItemTransitions.CountAsync(
            value => value.WorkItemId == firstCopy.Id));
    }

    private static HttpClient AuthenticatedClient(IdentityTestFactory factory, TenantId tenantId,
        PrincipalId principalId)
    {
        var tokens = factory.Services.GetRequiredService<IAccessTokenService>();
        var token = tokens.Issue(tenantId, principalId, PrincipalType.Human, TimeSpan.FromMinutes(5));
        var authenticated = factory.CreateClient();
        authenticated.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return authenticated;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record WorkItemChangeResponse(WorkItemResponse Item,
        IReadOnlyList<WorkItemTransitionResponse> Transitions);
}
