using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Tests.Identity;

public sealed class SupportAccessGrantTests
{
    [Fact]
    public void Issue_creates_an_active_time_bounded_grant()
    {
        var now = DateTimeOffset.Parse("2026-11-19T20:00:00Z");
        var grant = SupportAccessGrant.Issue(
            new TenantId(Guid.NewGuid()),
            new PrincipalId(Guid.NewGuid()),
            new PrincipalId(Guid.NewGuid()),
            "Incident triage",
            TimeSpan.FromHours(8),
            now);

        Assert.True(grant.IsActive(now.AddHours(1)));
        Assert.False(grant.IsActive(now.AddHours(9)));
        Assert.Equal("Incident triage", grant.Purpose);
    }

    [Fact]
    public void Revoke_ends_access_immediately()
    {
        var now = DateTimeOffset.UtcNow;
        var grant = SupportAccessGrant.Issue(
            new TenantId(Guid.NewGuid()),
            new PrincipalId(Guid.NewGuid()),
            new PrincipalId(Guid.NewGuid()),
            "Break-glass",
            TimeSpan.FromHours(4),
            now);

        grant.Revoke(now.AddMinutes(5));

        Assert.False(grant.IsActive(now.AddMinutes(6)));
        Assert.Throws<InvalidOperationException>(() => grant.Revoke());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31 * 24)]
    public void Issue_rejects_invalid_lifetime_hours(int hours)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SupportAccessGrant.Issue(
                new TenantId(Guid.NewGuid()),
                new PrincipalId(Guid.NewGuid()),
                new PrincipalId(Guid.NewGuid()),
                "x",
                TimeSpan.FromHours(hours)));
    }
}
