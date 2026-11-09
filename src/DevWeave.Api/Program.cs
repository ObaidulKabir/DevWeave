var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();

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

app.Run();

// Exposes the generated entry point to WebApplicationFactory in the integration test project.
public partial class Program;
