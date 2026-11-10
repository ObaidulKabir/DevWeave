using DevWeave.Application.Abstractions;
using DevWeave.Infrastructure.Persistence;
using DevWeave.Infrastructure.Repositories;
using DevWeave.Infrastructure.Schema;
using DevWeave.Infrastructure.Security;
using DevWeave.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<DevWeaveDbContext>((serviceProvider, options) =>
        {
            // Resolve the connection string lazily (at DbContext creation) rather than eagerly at
            // registration. Integration tests replace this value through WebApplicationFactory's
            // ConfigureAppConfiguration, which is only visible on the final IConfiguration after
            // the host is built; an eager read bakes in the appsettings value before that happens.
            var resolvedConfiguration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = resolvedConfiguration.GetConnectionString("DevWeave");

            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(DevWeaveDbContext).Assembly.FullName);
            });
            options.AddInterceptors(serviceProvider.GetRequiredService<TenantSessionConnectionInterceptor>());
        });

        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<TenantSessionConnectionInterceptor>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IPrincipalRepository, PrincipalRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectReferenceRepository, ProjectReferenceRepository>();
        services.AddScoped<IProjectMembershipRepository, ProjectMembershipRepository>();
        services.AddScoped<ILifecycleProfileRepository, LifecycleProfileRepository>();
        services.AddScoped<IWorkPlanningRepository, WorkPlanningRepository>();
        services.AddScoped<IKnowledgeItemRepository, KnowledgeItemRepository>();
        services.AddScoped<IKnowledgeRevisionRepository, KnowledgeRevisionRepository>();
        services.AddScoped<IKnowledgeSearchRepository, KnowledgeSearchRepository>();
        services.AddScoped<ISchemaDefinitionRepository, SchemaDefinitionRepository>();
        services.AddScoped<IRelationTypeRepository, RelationTypeRepository>();
        services.AddScoped<IRelationshipRepository, RelationshipRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();
        services.AddScoped<IEvidenceRepository, EvidenceRepository>();
        services.AddScoped<IPayloadValidator, JsonSchemaPayloadValidator>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAgentGrantRepository, AgentGrantRepository>();
        services.AddScoped<IAgentCredentialRepository, AgentCredentialRepository>();
        services.AddScoped<IContextPackageRepository, ContextPackageRepository>();
        services.AddScoped<ISecretHasher, Pbkdf2SecretHasher>();

        // Access tokens are stateless and validated on every authenticated request, so the service is a
        // singleton keyed by the configured signing secret (SRS TEN-007).
        services.AddSingleton<IAccessTokenService>(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var signingKey = configuration["Auth:SigningKey"]
                ?? throw new InvalidOperationException("Configuration 'Auth:SigningKey' is required to issue access tokens.");
            return new HmacAccessTokenService(signingKey);
        });

        // Object storage and content inspection are stateless adapters registered as singletons so
        // integration tests can swap them for in-memory fakes via ConfigureTestServices.
        services.AddSingleton<IObjectStorage, S3ObjectStorage>();
        services.AddSingleton<IQuarantineStore, S3QuarantineStore>();
        services.AddSingleton<IContentInspector, ContentInspector>();

        return services;
    }
}
