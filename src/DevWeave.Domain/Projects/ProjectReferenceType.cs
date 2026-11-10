namespace DevWeave.Domain.Projects;

/// <summary>
/// Classifies an external system a project references (SRS PRJ-005/PRJ-006). The reference stores an
/// external identifier or URL and never owns a copy of the referenced entity.
/// </summary>
public enum ProjectReferenceType
{
    Repository = 1,
    Database = 2,
    DeploymentEnvironment = 3,
    Service = 4,
}