using System.Text.Json.Serialization;
using DevWeave.Api.Endpoints;
using DevWeave.Api.Security;
using DevWeave.Application;
using DevWeave.Infrastructure;
using Microsoft.AspNetCore.Authentication;

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

// Bearer-token authentication establishes a verified tenant/actor identity from a credential-minted
// access token; authorization attaches to endpoint coordinates that require that identity (TEN-007).
builder.Services
    .AddAuthentication(BearerTokenAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, BearerTokenAuthenticationHandler>(
        BearerTokenAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();

app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();

// Root describes the host. The versioned REST surface is added module by module as bounded
// domains from SRS section 3.1 (Identity, Knowledge, Coordination, Commercial) come online.
app.MapGet("/", () => Results.Ok(new
{
    product = "DevWeave",
    role = "AI software delivery control plane",
    specification = "DWV-SRS-001 v1.5",
}));

// Probe surface consumed by deployment checks and synthetic availability monitoring (SRS 12.2).
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));

// Tenant and principal surface (SRS TEN-001..TEN-004).
app.MapIdentityEndpoints();

// Project and lifecycle surface (SRS PRJ-001..PRJ-006, PRJ-009, LFC-001).
app.MapProjectEndpoints();
app.MapWorkPlanningEndpoints();

// Knowledge item and revision surface (SRS KNW-001..KNW-013, REV-001..REV-008, BR-004..BR-009).
app.MapKnowledgeEndpoints();

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

// Credential-to-token exchange and verified-identity surface (SRS TEN-007).
app.MapAuthenticationEndpoints();

app.Run();

// Exposes the generated entry point to WebApplicationFactory in the integration test project.
public partial class Program;
