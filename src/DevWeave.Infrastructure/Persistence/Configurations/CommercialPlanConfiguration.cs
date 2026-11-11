using DevWeave.Domain.Commercial;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class CommercialPlanConfiguration : IEntityTypeConfiguration<CommercialPlan>
{
    public void Configure(EntityTypeBuilder<CommercialPlan> builder)
    {
        builder.ToTable("commercial_plans");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new CommercialPlanId(value));

        builder.Property(p => p.Key).HasMaxLength(60).IsRequired();
        builder.Property(p => p.Version).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Price).HasPrecision(18, 4);
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.EffectiveFrom);
        builder.Property(p => p.EffectiveTo);
        builder.Property(p => p.IsActive);

        builder.HasIndex(p => new { p.Key, p.Version }).IsUnique();
    }
}
