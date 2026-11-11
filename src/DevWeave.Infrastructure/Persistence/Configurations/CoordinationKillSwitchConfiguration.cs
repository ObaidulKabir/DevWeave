using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class CoordinationKillSwitchConfiguration : IEntityTypeConfiguration<CoordinationKillSwitch>
{
    public void Configure(EntityTypeBuilder<CoordinationKillSwitch> builder)
    {
        builder.ToTable("coordination_kill_switches");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(k => k.ProjectId)
            .HasConversion(id => id != null ? id.Value : (Guid?)null, value => value.HasValue ? new ProjectId(value.Value) : null);

        builder.Property(k => k.TargetType).HasConversion<string>();
        builder.Property(k => k.TargetId);
        builder.Property(k => k.Reason).HasMaxLength(500).IsRequired();
        
        builder.Property(k => k.CreatedBy)
            .HasConversion(id => id.Value, value => new PrincipalId(value));

        builder.Property(k => k.CreatedAt);
        builder.Property(k => k.ExpiresAt);
        builder.Property(k => k.IsActive);

        builder.HasIndex(k => new { k.TenantId, k.TargetType, k.TargetId, k.IsActive });
    }
}
