using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Security;

/// <summary>
/// Role → classification ceiling mapping for GOV-003/GOV-004. Members and viewers may not
/// disclose Confidential or Restricted objects; Owner/Admin may reach Restricted.
/// </summary>
public static class ClassificationAccess
{
    public static Classification CeilingFor(ProjectRole role) =>
        role is ProjectRole.Owner or ProjectRole.Admin
            ? Classification.Restricted
            : Classification.Internal;

    public static bool Allows(ProjectRole role, Classification classification) =>
        classification <= CeilingFor(role);
}
