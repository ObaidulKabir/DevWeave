namespace DevWeave.Domain.Specifications;

/// <summary>Where a decision applies. A narrower level overrides a wider one unless the wider decision is locked.</summary>
public enum SpecScopeLevel
{
    Platform = 1,
    Tenant = 2,
    Project = 3,
}

/// <summary>The machine-checkable type of one decision parameter.</summary>
public enum SpecValueKind
{
    String = 1,
    Integer = 2,
    Boolean = 3,
}

/// <summary>A typed parameter of a choice. <see cref="AllowedValues"/> (when non-empty) closes the value set.</summary>
public sealed record SpecParameter(
    string Name,
    SpecValueKind Kind,
    bool Required = true,
    IReadOnlyList<string>? AllowedValues = null);

/// <summary>One legal answer for a topic, with the parameters it accepts.</summary>
public sealed record SpecChoice(string Id, IReadOnlyList<SpecParameter>? Parameters = null)
{
    public IReadOnlyList<SpecParameter> ParameterList => Parameters ?? Array.Empty<SpecParameter>();
}

/// <summary>
/// A cross-topic compatibility rule: when this topic's choice is <see cref="WhenChoice"/>, the topic
/// <see cref="OtherTopic"/> must resolve to one of <see cref="AllowedOtherChoices"/>.
/// </summary>
public sealed record SpecTopicRule(string WhenChoice, string OtherTopic, IReadOnlyList<string> AllowedOtherChoices);

/// <summary>
/// A named decision area. Agents may only decide a topic with one of its declared choices; anything else
/// is rejected as data, so two agents can never describe the same decision in different words.
/// </summary>
public sealed record SpecTopic(
    string Id,
    string Title,
    string Description,
    IReadOnlyList<SpecChoice> Choices,
    IReadOnlyList<string>? ConstraintRules = null,
    IReadOnlyList<SpecTopicRule>? Rules = null)
{
    public IReadOnlyList<string> ConstraintRuleList => ConstraintRules ?? Array.Empty<string>();
    public IReadOnlyList<SpecTopicRule> RuleList => Rules ?? Array.Empty<SpecTopicRule>();
}

public sealed record SpecConstraint(string Rule, string Value);

/// <summary>The parsed, typed content of a decision. Parameter values are normalised to invariant strings.</summary>
public sealed record SpecDecision(
    string Topic,
    SpecScopeLevel Level,
    string Choice,
    IReadOnlyDictionary<string, string> Params,
    IReadOnlyList<SpecConstraint> Constraints,
    bool Locked,
    string? Rationale);

/// <summary>A decision together with where it came from, as the resolver sees it.</summary>
public sealed record SpecCandidate(
    SpecDecision Decision,
    Guid? ItemId,
    Guid? RevisionId,
    int? RevisionNumber,
    Guid? ProjectId,
    DateTimeOffset DefinedAt);

public enum SpecOrigin
{
    /// <summary>Shipped with the platform at design time.</summary>
    Predefined = 1,
    /// <summary>The earliest approved decision for the topic at its scope level.</summary>
    FirstApproved = 2,
    /// <summary>A narrower-scope approved decision overriding a wider, unlocked one.</summary>
    Override = 3,
}

public sealed record SpecIssue(string Path, string Message);

public sealed record SpecConflict(SpecCandidate Binding, SpecCandidate Other, string Reason);

public sealed record SpecResolution(
    string Topic,
    SpecCandidate? Binding,
    SpecOrigin? Origin,
    IReadOnlyList<SpecConflict> Conflicts);
