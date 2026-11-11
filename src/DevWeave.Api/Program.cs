using System.Text;
using System.Text.Json.Serialization;
using DevWeave.Api.Endpoints;
using DevWeave.Api.Security;
using DevWeave.Api.Endpoints.Archives;
using DevWeave.Api.Endpoints.Integrations;
using DevWeave.Application;
using DevWeave.Infrastructure;
using DevWeave.Mcp.Protocol;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

// Serialize contract enums as their names (e.g. "Human", "Active") rather than numeric literals.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Register application use cases.
builder.Services.AddApplication();

// Register infrastructure layer (EF Core DbContext, adapters)
builder.Services.AddInfrastructure();
builder.Services.AddScoped<McpServer>();

var oidcAuthority = builder.Configuration["Auth:Oidc:Authority"];
var oidcAudience = builder.Configuration["Auth:Oidc:Audience"];
var agentSigningKey = builder.Configuration["Auth:SigningKey"];
if (builder.Environment.IsProduction() &&
    (string.IsNullOrWhiteSpace(oidcAuthority) || string.IsNullOrWhiteSpace(oidcAudience)))
{
    throw new InvalidOperationException(
        "Auth:Oidc:Authority and Auth:Oidc:Audience must be configured in production.");
}
if (builder.Environment.IsProduction() &&
    (string.IsNullOrWhiteSpace(agentSigningKey) ||
     agentSigningKey.Length < 32 ||
     agentSigningKey.Contains("change-in-production", StringComparison.OrdinalIgnoreCase)))
{
    throw new InvalidOperationException(
        "Auth:SigningKey must be a production secret of at least 32 characters.");
}

// Human OIDC JWTs and credential-minted agent tokens share the Bearer header while using independent
// validation paths. OIDC providers must map the tenant_id, principal_id and principal_type claims.
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = ApiAuthenticationSchemes.SmartBearer;
        options.DefaultChallengeScheme = ApiAuthenticationSchemes.SmartBearer;
        options.DefaultForbidScheme = ApiAuthenticationSchemes.SmartBearer;
    })
    .AddPolicyScheme(
        ApiAuthenticationSchemes.SmartBearer,
        ApiAuthenticationSchemes.SmartBearer,
        options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                var authorization = context.Request.Headers.Authorization.ToString();
                var token = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? authorization["Bearer ".Length..].Trim()
                    : string.Empty;
                return token.Count(character => character == '.') == 2
                    ? ApiAuthenticationSchemes.HumanOidc
                    : ApiAuthenticationSchemes.AgentBearer;
            };
        })
    .AddScheme<AuthenticationSchemeOptions, BearerTokenAuthenticationHandler>(
        BearerTokenAuthenticationHandler.SchemeName,
        _ => { })
    .AddJwtBearer(ApiAuthenticationSchemes.HumanOidc, options =>
    {
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var claims = context.Principal;
                var tenantId = claims?.FindFirst(AuthClaims.TenantId)?.Value;
                var principalId = claims?.FindFirst(AuthClaims.PrincipalId)?.Value;
                var principalType = claims?.FindFirst(AuthClaims.PrincipalType)?.Value;

                if (!Guid.TryParse(tenantId, out _) ||
                    !Guid.TryParse(principalId, out _) ||
                    !string.Equals(principalType, "Human", StringComparison.Ordinal))
                {
                    context.Fail("OIDC token is missing valid DevWeave human identity claims.");
                }

                return Task.CompletedTask;
            },
        };

        // Development pilot: when OIDC authority is empty, accept symmetrically signed human JWTs
        // minted by /auth/pilot/*. Production always requires a real OIDC authority.
        if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(oidcAuthority))
        {
            var pilotSigningKey = builder.Configuration["Auth:SigningKey"]
                ?? throw new InvalidOperationException("Auth:SigningKey is required for Development pilot JWTs.");
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = PilotHumanTokenService.Issuer,
                ValidateAudience = true,
                ValidAudience = string.IsNullOrWhiteSpace(oidcAudience)
                    ? PilotHumanTokenService.Audience
                    : oidcAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(pilotSigningKey)),
                ValidateLifetime = true,
            };
        }
        else
        {
            options.Authority = oidcAuthority;
            options.Audience = oidcAudience;
            options.RequireHttpsMetadata = builder.Configuration.GetValue(
                "Auth:Oidc:RequireHttpsMetadata", defaultValue: true);
        }
    });

if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(oidcAuthority))
{
    builder.Services.AddSingleton(new PilotHumanTokenService(
        builder.Configuration["Auth:SigningKey"]
            ?? throw new InvalidOperationException("Auth:SigningKey is required for Development pilot JWTs.")));
}
builder.Services.AddAuthorization(options =>
{
    var humanPolicy = new AuthorizationPolicyBuilder(ApiAuthenticationSchemes.SmartBearer)
        .RequireAuthenticatedUser()
        .RequireClaim(AuthClaims.PrincipalType, "Human")
        .Build();
    options.AddPolicy(ApiAuthorizationPolicies.Human, humanPolicy);
    options.AddPolicy(ApiAuthorizationPolicies.Agent,
        new AuthorizationPolicyBuilder(ApiAuthenticationSchemes.SmartBearer)
            .RequireAuthenticatedUser()
            .RequireClaim(AuthClaims.PrincipalType, "AiAgent")
            .Build());
    options.AddPolicy(ApiAuthorizationPolicies.PlatformAdministrator,
        new AuthorizationPolicyBuilder(ApiAuthenticationSchemes.SmartBearer)
            .RequireAuthenticatedUser()
            .RequireClaim(AuthClaims.PrincipalType, "Human")
            .RequireClaim("platform_admin", "true")
            .Build());
    options.FallbackPolicy = humanPolicy;
});

var app = builder.Build();

app.UseExceptionHandler();

app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseMiddleware<TenantMembershipAuthorizationMiddleware>();
app.UseAuthorization();

// Root describes the host. The versioned REST surface is added module by module as bounded
// domains from SRS section 3.1 (Identity, Knowledge, Coordination, Commercial) come online.
app.MapGet("/", () => Results.Ok(new
{
    product = "DevWeave",
    role = "AI software delivery control plane",
    specification = "DWV-SRS-001 v1.5",
})).AllowAnonymous();

// Probe surface consumed by deployment checks and synthetic availability monitoring (SRS 12.2).
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

// Tenant and principal surface (SRS TEN-001..TEN-004).
app.MapIdentityEndpoints();

// Project and lifecycle surface (SRS PRJ-001..PRJ-006, PRJ-009, LFC-001).
app.MapProjectEndpoints();
app.MapWorkPlanningEndpoints();

// Knowledge item and revision surface (SRS KNW-001..KNW-013, REV-001..REV-008, BR-004..BR-009).
app.MapKnowledgeEndpoints();
app.MapPlanDesignEndpoints();

// Integration surface (SRS INT-001..INT-008).
app.MapIntegrationEndpoints();

// Portable archive surface (SRS DATA-01..DATA-05).
app.MapArchiveEndpoints();

// Permission-scoped PostgreSQL full-text search surface (SRS SRH-001..SRH-003).
app.MapSearchEndpoints();
app.MapSpecificationEndpoints();

// Schema registry surface (SRS SCH-001..SCH-003, KNW-008).
app.MapSchemaEndpoints();

// Relationship and relation-type registry surface (SRS REL-001..REL-005, REL-007, SCH-007, BR-007).
app.MapRelationshipEndpoints();

// Provenance and activities surface (SRS PRO-001..PRO-005, PRO-008, REV-006, REV-007, REV-008).
app.MapActivityEndpoints();

// Artifact and evidence surface (SRS ART-001..ART-004, ART-006, ART-008, PRO-006).
app.MapArtifactEndpoints();
app.MapEvidenceEndpoints();

// Agent principal, capability grant and context-package surface (SRS AGT-001..AGT-005, AGT-010, AGT-013).
app.MapAgentEndpoints();

// Commercial Pilot surface (SRS COM-001..COM-026).
app.MapCommercialEndpoints();

// Coordination plane REST surface (SRS MCO / MCP foundations).
app.MapCoordinationEndpoints();

// MCP Coordination Plane surface (SRS MCP-001).
app.MapMcpEndpoints();

// Credential-to-token exchange and verified-identity surface (SRS TEN-007).
app.MapAuthenticationEndpoints();

// Development pilot human JWT bootstrap (disabled when OIDC authority is configured or in Production).
if (PilotAuthEndpoints.IsPilotEnabled(app.Environment, app.Configuration))
{
    app.MapPilotAuthEndpoints();
}

app.Run();

// Exposes the generated entry point to WebApplicationFactory in the integration test project.
public partial class Program;
