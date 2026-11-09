using System.Reflection;

namespace DevWeave.Architecture.Tests;

/// <summary>
/// Guards the dependency direction the SRS depends on: the canonical domain model must not depend on
/// infrastructure or on a specific technology (SRS DWV-SRS-001 sections 3.1 and 3.3). These tests
/// fail the build the moment a forbidden reference is introduced.
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly string[] InfrastructureAndHosts =
    [
        "DevWeave.Infrastructure",
        "DevWeave.Api",
        "DevWeave.Mcp",
        "DevWeave.Workers",
    ];

    private static readonly string[] TechnologyContracts =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Microsoft.AspNetCore",
        "StackExchange.Redis",
    ];

    [Fact]
    public void Domain_does_not_depend_on_infrastructure_or_technology_contracts()
    {
        var violations = ReferencedAssemblyNames("DevWeave.Domain")
            .Where(referenced => IsForbidden(referenced, InfrastructureAndHosts)
                || IsForbidden(referenced, TechnologyContracts))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_hosts()
    {
        var violations = ReferencedAssemblyNames("DevWeave.Application")
            .Where(referenced => IsForbidden(referenced, InfrastructureAndHosts))
            .ToArray();

        Assert.Empty(violations);
    }

    private static bool IsForbidden(string referenced, string[] forbidden) =>
        forbidden.Any(prefix => referenced.StartsWith(prefix, StringComparison.Ordinal));

    private static string[] ReferencedAssemblyNames(string assemblyName) =>
        Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();
}
