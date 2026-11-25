namespace DevWeave.Domain.Commercial;

/// <summary>
/// Whether work ran on the customer's runtime or on DevWeave-managed compute (SRS COM-021).
/// </summary>
public enum ExecutionLocation
{
    CustomerManaged = 1,
    DevWeaveManaged = 2,
}

public static class ExecutionFacts
{
    public const string CustomerProvider = "customer";

    public static ExecutionLocation ParseLocation(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ExecutionLocation.CustomerManaged;

        foreach (var name in Enum.GetNames<ExecutionLocation>())
        {
            if (name.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase))
                return Enum.Parse<ExecutionLocation>(name);
        }

        throw new ArgumentException(
            "Execution location must be CustomerManaged or DevWeaveManaged.", nameof(value));
    }

    public static string RequireProvider(string? provider, string parameter = "provider")
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider is required.", parameter);
        return provider.Trim();
    }
}
