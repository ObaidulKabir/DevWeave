using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class AgentRuntimeInstanceConfiguration : IEntityTypeConfiguration<AgentRuntimeInstance>
{
    public void Configure(EntityTypeBuilder<AgentRuntimeInstance> builder)
    {
        builder.ToTable("agent_runtime_instances");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new RuntimeId(value));

        builder.Property(r => r.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(r => r.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));

        builder.Property(r => r.PrincipalId)
            .HasConversion(id => id.Value, value => new PrincipalId(value));

        builder.Property(r => r.SessionKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.RuntimeClass)
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(r => r.ProtocolVersion)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(r => r.LastHeartbeatAt);
        builder.Property(r => r.CreatedAt);

        builder.HasIndex(r => r.SessionKey).IsUnique();
    }
}
