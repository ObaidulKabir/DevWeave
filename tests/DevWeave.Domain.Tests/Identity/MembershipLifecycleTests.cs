using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Tests.Identity;

/// <summary>
/// Proves TEN-004: tenant membership lifecycle (Invited -> Active -> Revoked) and the invariant
/// that revocation is terminal.
/// </summary>
public sealed class MembershipLifecycleTests
{
    [Fact]
    public void TEN_004_invite_creates_a_membership_in_the_invited_state()
    {
        var member = CreateInvited();

        Assert.Equal(MembershipStatus.Invited, member.Status);
        Assert.Equal(MembershipRole.Member, member.Role);
        Assert.False(member.Id.Value == Guid.Empty);
        Assert.Null(member.ActivatedAt);
        Assert.Null(member.RevokedAt);
    }

    [Fact]
    public void TEN_004_activate_transitions_invited_to_active_and_stamps_the_time()
    {
        var member = CreateInvited();

        member.Activate();

        Assert.Equal(MembershipStatus.Active, member.Status);
        Assert.NotNull(member.ActivatedAt);
        Assert.Null(member.RevokedAt);
    }

    [Fact]
    public void TEN_004_activating_an_already_active_membership_fails()
    {
        var member = CreateInvited();
        member.Activate();

        Assert.Throws<InvalidOperationException>(() => member.Activate());
    }

    [Fact]
    public void TEN_004_revoke_transitions_active_to_revoked_and_stamps_the_time()
    {
        var member = CreateInvited();
        member.Activate();

        member.Revoke();

        Assert.Equal(MembershipStatus.Revoked, member.Status);
        Assert.NotNull(member.RevokedAt);
    }

    [Fact]
    public void TEN_004_revoking_an_invitation_before_activation_is_allowed()
    {
        var member = CreateInvited();

        member.Revoke();

        Assert.Equal(MembershipStatus.Revoked, member.Status);
        Assert.NotNull(member.RevokedAt);
    }

    [Fact]
    public void TEN_004_revoking_an_already_revoked_membership_fails()
    {
        var member = CreateInvited();
        member.Revoke();

        Assert.Throws<InvalidOperationException>(() => member.Revoke());
    }

    private static Membership CreateInvited()
    {
        var tenant = Tenant.Create("Alpha Corp");
        var principal = Principal.Create(tenant.Id, "user@alphacorp.com", PrincipalType.Human);
        return Membership.Invite(tenant.Id, principal.Id, MembershipRole.Member);
    }
}