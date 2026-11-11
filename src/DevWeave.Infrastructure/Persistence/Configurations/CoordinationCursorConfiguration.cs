using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class CoordinationCursorConfiguration : IEntityTypeConfiguration<CoordinationCursor>
{
    public void Configure(EntityTypeBuilder<CoordinationCursor> builder)
    {
        builder.ToTable("coordination_cursors");

        builder.HasKey(c => new { c.ChannelId, c.PrincipalId });

        builder.Property(c => c.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(c => c.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));

        builder.Property(c => c.ChannelId)
            .HasConversion(id => id.Value, value => new ChannelId(value));

        builder.Property(c => c.PrincipalId)
            .HasConversion(id => id.Value, value => new PrincipalId(value));

        builder.Property(c => c.LastReadSequence);
        builder.Property(c => c.LastAcknowledgedSequence);
        builder.Property(c => c.UpdatedAt);
    }
}
