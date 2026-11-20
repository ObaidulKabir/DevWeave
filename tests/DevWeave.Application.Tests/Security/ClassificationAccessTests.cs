using DevWeave.Application.Security;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Tests.Security;

public sealed class ClassificationAccessTests
{
    [Theory]
    [InlineData(ProjectRole.Owner, Classification.Restricted, true)]
    [InlineData(ProjectRole.Admin, Classification.Confidential, true)]
    [InlineData(ProjectRole.Member, Classification.Internal, true)]
    [InlineData(ProjectRole.Member, Classification.Confidential, false)]
    [InlineData(ProjectRole.Member, Classification.Restricted, false)]
    [InlineData(ProjectRole.Viewer, Classification.Restricted, false)]
    public void GOV_003_004_role_ceiling_controls_disclosure(
        ProjectRole role,
        Classification classification,
        bool allowed)
    {
        Assert.Equal(allowed, ClassificationAccess.Allows(role, classification));
    }
}
