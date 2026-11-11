namespace DevWeave.Domain.Audit;

public record AuditRecordId(Guid Value)
{
    public static AuditRecordId New() => new(Guid.NewGuid());
}
