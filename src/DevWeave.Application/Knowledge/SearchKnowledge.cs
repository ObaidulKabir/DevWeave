using System.Text;
using DevWeave.Application.Abstractions;
using DevWeave.Contracts.Knowledge.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Knowledge;

/// <summary>
/// Authorizes the project before invoking any search query, then applies the agreed role-based
/// classification ceiling to every indexed result.
/// </summary>
public sealed class SearchKnowledge
{
    private readonly IPrincipalRepository _principals;
    private readonly IMembershipRepository _tenantMemberships;
    private readonly IProjectMembershipRepository _memberships;
    private readonly IKnowledgeSearchRepository _search;
    private readonly IContentInspector _inspector;

    public SearchKnowledge(
        IPrincipalRepository principals,
        IMembershipRepository tenantMemberships,
        IProjectMembershipRepository memberships,
        IKnowledgeSearchRepository search,
        IContentInspector inspector)
    {
        _principals = principals;
        _tenantMemberships = tenantMemberships;
        _memberships = memberships;
        _search = search;
        _inspector = inspector;
    }

    public async Task<SearchKnowledgeResponse> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId actorId,
        SearchKnowledgeRequest request,
        CancellationToken ct = default)
    {
        var normalizedRequest = Normalize(request);
        var principal = await _principals.GetByIdAsync(tenantId, actorId, ct);
        if (principal is null || !principal.IsActive || principal.Type != PrincipalType.Human)
            throw new UnauthorizedTenantException("An active human principal is required to search knowledge.");

        var tenantMemberships = await _tenantMemberships.ListByPrincipalAsync(tenantId, actorId, ct);
        if (!tenantMemberships.Any(candidate => candidate.Status == MembershipStatus.Active))
            throw new UnauthorizedTenantException("Active tenant membership is required to search knowledge.");

        var memberships = await _memberships.ListByPrincipalAsync(tenantId, actorId, ct);
        var membership = memberships.FirstOrDefault(candidate =>
                candidate.ProjectId == projectId &&
                candidate.Status == ProjectMembershipStatus.Active)
            ?? throw new UnauthorizedTenantException("Active project membership is required to search knowledge.");

        var queryInspection = await _inspector.InspectAsync(
            string.Empty,
            "text/plain",
            Encoding.UTF8.GetBytes(normalizedRequest.Text),
            ct);
        if (queryInspection.Verdict != ContentVerdict.Allow)
        {
            throw new ProhibitedContentException(
                "The search query was rejected by the content policy.",
                queryInspection.Reasons);
        }

        var classificationCeiling = membership.Role is ProjectRole.Owner or ProjectRole.Admin
            ? Classification.Restricted
            : Classification.Internal;

        return await _search.SearchAsync(
            tenantId,
            projectId,
            classificationCeiling,
            normalizedRequest,
            ct);
    }

    private static SearchKnowledgeRequest Normalize(SearchKnowledgeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new ArgumentException("Search text is required.", nameof(request));
        if (request.Text.Length > 300)
            throw new ArgumentException("Search text must not exceed 300 characters.", nameof(request));
        if (request.Limit is < 1 or > 100)
            throw new ArgumentException("Search limit must be between 1 and 100.", nameof(request));
        if (request.TypeId?.Length > 100)
            throw new ArgumentException("TypeId must not exceed 100 characters.", nameof(request));

        return request with
        {
            Text = request.Text.Trim(),
            TypeId = string.IsNullOrWhiteSpace(request.TypeId) ? null : request.TypeId.Trim(),
            Lifecycle = NormalizeEnumFilter<InfoState>(request.Lifecycle, nameof(request.Lifecycle)),
            Review = NormalizeEnumFilter<ReviewState>(request.Review, nameof(request.Review)),
            Verification = NormalizeEnumFilter<VerificationState>(request.Verification, nameof(request.Verification)),
            Currency = NormalizeEnumFilter<CurrencyState>(request.Currency, nameof(request.Currency)),
        };
    }

    private static string? NormalizeEnumFilter<TEnum>(string? value, string parameterName)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ||
            !Enum.IsDefined(parsed))
        {
            throw new ArgumentException($"'{value}' is not a valid {typeof(TEnum).Name} value.", parameterName);
        }

        return parsed.ToString();
    }
}
