using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Infrastructure.Persistence;
using DevWeave.Infrastructure.Persistence.Planned;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using YamlDotNet.RepresentationModel;

namespace DevWeave.Architecture.Tests;

public sealed class PlannedContractTests
{
    private static readonly string[] DisallowedMcpFields = ["sourceCode", "patch", "diff", "secret"];
    private static readonly string[] ExpectedFamilies =
    [
        "TEN", "GOV", "PRJ", "LFC", "KNW", "REV", "SCH", "REL", "PRO", "ART",
        "SRH", "AGT", "AGI", "INT", "EXP", "RPT", "APP", "OPS", "API", "WEB",
        "UI", "COL", "MCP", "MCO", "COM", "DATA", "NFR",
    ];

    [Fact]
    public void Planned_schema_has_explicit_tenant_project_and_uniqueness_scopes()
    {
        using var context = new PlannedContractDesignTimeFactory().CreateDbContext([]);
        var entities = context.Model.GetEntityTypes().ToDictionary(e => e.GetTableName()!);

        Assert.Equal(PlannedSchemaCatalog.Tables.Count, entities.Count);
        Assert.True(entities.Count >= 40);
        foreach (var table in PlannedSchemaCatalog.Tables)
        {
            var entity = entities[table.Name];
            Assert.NotNull(entity.FindPrimaryKey());
            Assert.NotNull(entity.FindProperty("CreatedAt"));
            Assert.Equal(table.TenantScoped, entity.FindProperty("TenantId") is not null);
            if (table.ProjectScoped)
                Assert.False(entity.FindProperty("ProjectId")?.IsNullable);
            else if (table.Columns.All(column => column.Name != "ProjectId"))
                Assert.Null(entity.FindProperty("ProjectId"));
            if (table.TenantScoped)
                Assert.Contains(entity.GetIndexes(), index => index.IsUnique &&
                    index.Properties.Select(p => p.Name).SequenceEqual(["Id", "TenantId"]));
            if (table.UniqueScope is { Length: > 0 })
                Assert.Contains(entity.GetIndexes(), index => index.IsUnique &&
                    index.Properties.Select(p => p.Name).SequenceEqual(table.UniqueScope));
        }
    }

    [Fact]
    public void Planned_migration_is_isolated_from_active_model()
    {
        using var planned = new PlannedContractDesignTimeFactory().CreateDbContext([]);
        Assert.Equal(2, planned.Database.GetMigrations().Count());
        Assert.DoesNotContain(
            typeof(DevWeaveDbContext).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.PropertyType == typeof(DbSet<PlannedContractDbContext>));
        Assert.NotEqual(typeof(DevWeaveDbContext), planned.GetType());
    }

    [Fact]
    public void Planned_ports_remain_interfaces_with_no_runtime_implementations()
    {
        var ports = typeof(AuthorizedScope).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(AuthorizedScope).Namespace
                && type.IsInterface)
            .ToArray();

        Assert.True(ports.Length >= 25);
        Assert.All(ports, port =>
        {
            Assert.StartsWith("I", port.Name);
            Assert.All(port.GetMethods(), method =>
                Assert.True(typeof(Task).IsAssignableFrom(method.ReturnType),
                    $"{port.Name}.{method.Name} must be asynchronous."));
        });
    }

    [Fact]
    public void Openapi_draft_identifies_all_srs_families_and_real_contract_types()
    {
        var root = RepoRoot();
        var specification = File.ReadAllText(Path.Combine(root, "docs", "planned-full-srs.openapi.yaml"));

        Assert.Contains("openapi: 3.1.0", specification, StringComparison.Ordinal);
        Assert.Contains("x-contract-only: true", specification, StringComparison.Ordinal);
        Assert.Contains("security:", specification, StringComparison.Ordinal);
        foreach (var family in ExpectedFamilies)
            Assert.Matches($@"\b{family}\b", specification);

        var ports = typeof(AuthorizedScope).Assembly.GetTypes()
            .Where(type => type.IsInterface && type.Namespace == typeof(AuthorizedScope).Namespace)
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);
        var dtoNames = typeof(PageRequest).Assembly.GetExportedTypes()
            .Where(type => type.Namespace == typeof(PageRequest).Namespace)
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        var declaredPorts = Regex.Matches(specification, @"x-port: (I\w+)")
            .Select(match => match.Groups[1].Value).ToArray();
        var declaredDtos = Regex.Matches(specification, @"x-request-type: (\w+)")
            .Select(match => match.Groups[1].Value).ToArray();
        var operationIds = Regex.Matches(specification, @"operationId: (\w+)")
            .Select(match => match.Groups[1].Value).ToArray();

        Assert.True(operationIds.Length >= 30);
        Assert.Equal(operationIds.Length, operationIds.Distinct(StringComparer.Ordinal).Count());
        Assert.All(declaredPorts, port => Assert.Contains(port, ports));
        Assert.All(declaredDtos, dto => Assert.Contains(dto, dtoNames));
        Assert.Equal(operationIds.Length, declaredPorts.Length);
    }

    [Fact]
    public void Openapi_typed_operations_have_matching_request_schemas_and_resolvable_refs()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "planned-full-srs.openapi.yaml"));
        var yaml = new YamlStream();
        yaml.Load(new StringReader(text));
        var root = (YamlMappingNode)yaml.Documents.Single().RootNode;
        var paths = (YamlMappingNode)Get(root, "paths");
        var components = (YamlMappingNode)Get(root, "components");
        var schemas = (YamlMappingNode)Get(components, "schemas");
        var requestBodies = (YamlMappingNode)Get(components, "requestBodies");
        var responses = (YamlMappingNode)Get(components, "responses");
        var contracts = typeof(PageRequest).Assembly.GetExportedTypes()
            .Where(type => type.Namespace == typeof(PageRequest).Namespace)
            .ToDictionary(type => type.Name);

        foreach (var path in paths.Children.Values.Cast<YamlMappingNode>())
        foreach (var method in path.Children)
        {
            var operation = (YamlMappingNode)method.Value;
            Assert.Equal("true", ((YamlScalarNode)Get(operation, "x-contract-only")).Value);
            var operationResponses = (YamlMappingNode)Get(operation, "responses");
            foreach (var value in operationResponses.Children.Values.Cast<YamlMappingNode>())
            {
                var responseName = RefName(value);
                Assert.True(responses.Children.ContainsKey(new YamlScalarNode(responseName)));
            }

            if (((YamlScalarNode)method.Key).Value == "post")
            {
                Assert.True(operation.Children.ContainsKey(new YamlScalarNode("x-request-type")));
                Assert.True(operation.Children.ContainsKey(new YamlScalarNode("requestBody")));
            }
            if (!operation.Children.TryGetValue(new YamlScalarNode("x-request-type"), out var nameNode))
                continue;
            var name = ((YamlScalarNode)nameNode).Value!;
            var requestBody = (YamlMappingNode)Get(operation, "requestBody");
            Assert.Equal(name, RefName(requestBody));
            var body = (YamlMappingNode)Get(requestBodies, name);
            var content = (YamlMappingNode)Get(body, "content");
            var media = (YamlMappingNode)Get(content, "application/json");
            Assert.Equal(name, RefName((YamlMappingNode)Get(media, "schema")));

            var schema = (YamlMappingNode)Get(schemas, name);
            var properties = (YamlMappingNode)Get(schema, "properties");
            var expected = contracts[name].GetProperties()
                .Select(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..])
                .OrderBy(p => p).ToArray();
            var actual = properties.Children.Keys.Cast<YamlScalarNode>()
                .Select(key => key.Value!).OrderBy(p => p).ToArray();
            Assert.Equal(expected, actual);
        }
    }

    private static YamlNode Get(YamlMappingNode mapping, string key) =>
        mapping.Children[new YamlScalarNode(key)];

    private static string RefName(YamlMappingNode value) =>
        ((YamlScalarNode)Get(value, "$ref")).Value!.Split('/').Last();

    [Fact]
    public void Mcp_tool_and_resource_catalogue_has_scoped_ports_and_defined_inputs()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepoRoot(), "docs", "planned-project-mcp.json")));
        var root = document.RootElement;
        Assert.Equal("contract-only", root.GetProperty("status").GetString());
        var ports = typeof(AuthorizedScope).Assembly.GetTypes()
            .Where(type => type.IsInterface && type.Namespace == typeof(AuthorizedScope).Namespace)
            .Select(type => type.Name).ToHashSet(StringComparer.Ordinal);
        var tools = root.GetProperty("tools").EnumerateArray().ToArray();
        Assert.True(tools.Length >= 6);
        Assert.Equal(tools.Length, tools.Select(tool => tool.GetProperty("name").GetString())
            .Distinct(StringComparer.Ordinal).Count());
        foreach (var tool in tools)
        {
            Assert.Contains(tool.GetProperty("port").GetString()!, ports);
            var schema = tool.GetProperty("inputSchema");
            Assert.Equal("object", schema.GetProperty("type").GetString());
            Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
            var properties = schema.GetProperty("properties");
            foreach (var required in schema.GetProperty("required").EnumerateArray())
                Assert.True(properties.TryGetProperty(required.GetString()!, out _));
            foreach (var property in properties.EnumerateObject())
                Assert.DoesNotContain(property.Name, DisallowedMcpFields);
        }
        foreach (var resource in root.GetProperty("resources").EnumerateArray())
        {
            Assert.Contains(resource.GetProperty("port").GetString()!, ports);
            Assert.Contains("{projectId}", resource.GetProperty("uriTemplate").GetString());
        }
    }

    [Fact]
    public void Implemented_work_planning_openapi_is_valid_and_declares_each_live_endpoint()
    {
        var path = Path.Combine(RepoRoot(), "docs", "work-planning.openapi.yaml");
        var yaml = new YamlStream();
        yaml.Load(new StringReader(File.ReadAllText(path)));
        var root = (YamlMappingNode)yaml.Documents.Single().RootNode;
        var paths = (YamlMappingNode)Get(root, "paths");

        Assert.Equal("3.1.0", ((YamlScalarNode)Get(root, "openapi")).Value);
        Assert.True(paths.Children.Count >= 9);
        var methods = paths.Children.Values.Cast<YamlMappingNode>()
            .SelectMany(pathNode => pathNode.Children)
            .Select(entry => ((YamlScalarNode)entry.Key).Value)
            .ToArray();
        Assert.Contains("post", methods);
        Assert.Contains("get", methods);
        Assert.Contains("patch", methods);

        var schemas = (YamlMappingNode)Get((YamlMappingNode)Get(root, "components"), "schemas");
        foreach (var pathNode in paths.Children.Values.Cast<YamlMappingNode>())
        foreach (var method in pathNode.Children)
        {
            if (((YamlScalarNode)method.Key).Value == "parameters")
                continue;
            var operation = (YamlMappingNode)method.Value;
            if (!operation.Children.TryGetValue(new YamlScalarNode("requestBody"), out var bodyNode))
                continue;
            var requestBody = (YamlMappingNode)bodyNode;
            var content = (YamlMappingNode)Get((YamlMappingNode)Get(requestBody, "content"), "application/json");
            var refName = RefName((YamlMappingNode)Get(content, "schema"));
            Assert.True(schemas.Children.ContainsKey(new YamlScalarNode(refName)));
        }
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
