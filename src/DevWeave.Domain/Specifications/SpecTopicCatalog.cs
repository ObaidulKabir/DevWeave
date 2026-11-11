namespace DevWeave.Domain.Specifications;

/// <summary>
/// The predefined, repo-versioned catalog of specification topics and the platform's design-time
/// decisions (SRS 3.3, ADR-001). Changing this file is a reviewed code change, which is what makes the
/// platform-level answer authoritative for every agent.
/// </summary>
public static class SpecTopicCatalog
{
    private static readonly DateTimeOffset DesignTime = new(2026, 11, 9, 0, 0, 0, TimeSpan.Zero);

    public static IReadOnlyList<SpecTopic> Topics { get; } =
    [
        new SpecTopic(
            "backend.language", "Backend language", "Implementation language for the control plane.",
            [new SpecChoice("csharp", [new SpecParameter("version", SpecValueKind.String, AllowedValues: ["13", "14"])]),
             new SpecChoice("java", [new SpecParameter("version", SpecValueKind.String)]),
             new SpecChoice("typescript", [new SpecParameter("version", SpecValueKind.String)])],
            ["nullable-reference-types", "warnings-as-errors"]),
        new SpecTopic(
            "backend.framework", "Backend framework", "Runtime and web framework.",
            [new SpecChoice("aspnet-core", [new SpecParameter("targetFramework", SpecValueKind.String, AllowedValues: ["net9.0", "net10.0"])]),
             new SpecChoice("spring-boot", [new SpecParameter("version", SpecValueKind.String)]),
             new SpecChoice("express", [new SpecParameter("version", SpecValueKind.String)])],
            null,
            [new SpecTopicRule("aspnet-core", "backend.language", ["csharp"]),
             new SpecTopicRule("spring-boot", "backend.language", ["java"]),
             new SpecTopicRule("express", "backend.language", ["typescript"])]),
        new SpecTopic(
            "db.engine", "Database engine", "System-of-record relational database.",
            [new SpecChoice("postgresql", [new SpecParameter("majorVersion", SpecValueKind.Integer, AllowedValues: ["16", "17", "18"])]),
             new SpecChoice("mysql", [new SpecParameter("majorVersion", SpecValueKind.Integer)])]),
        new SpecTopic(
            "db.access", "Data access", "How the application reaches the database.",
            [new SpecChoice("ef-core", [new SpecParameter("migrations", SpecValueKind.Boolean)]),
             new SpecChoice("dapper"),
             new SpecChoice("jdbc")],
            null,
            [new SpecTopicRule("ef-core", "backend.language", ["csharp"]),
             new SpecTopicRule("dapper", "backend.language", ["csharp"]),
             new SpecTopicRule("jdbc", "backend.language", ["java"])]),
        new SpecTopic(
            "auth.protocol", "Authentication", "How callers authenticate.",
            [new SpecChoice("jwt-bearer"), new SpecChoice("oidc-code-pkce"), new SpecChoice("api-key")]),
        new SpecTopic(
            "storage.object", "Object storage", "Where artifact binaries are stored.",
            [new SpecChoice("s3-compatible", [new SpecParameter("pathStyle", SpecValueKind.Boolean, Required: false)]),
             new SpecChoice("local-disk")]),
        new SpecTopic(
            "frontend.framework", "Studio frontend", "Owner Studio UI framework.",
            [new SpecChoice("react-vite", [new SpecParameter("reactVersion", SpecValueKind.Integer, AllowedValues: ["18", "19"])]),
             new SpecChoice("nextjs", [new SpecParameter("version", SpecValueKind.Integer)])]),
        new SpecTopic(
            "test.framework.backend", "Backend test framework", "Unit and integration test runner.",
            [new SpecChoice("xunit"), new SpecChoice("nunit"), new SpecChoice("junit")],
            null,
            [new SpecTopicRule("xunit", "backend.language", ["csharp"]),
             new SpecTopicRule("nunit", "backend.language", ["csharp"]),
             new SpecTopicRule("junit", "backend.language", ["java"])]),
        new SpecTopic(
            "source.code-policy", "Source code in the platform", "Whether DevWeave stores source code.",
            [new SpecChoice("references-only"), new SpecChoice("store-source")]),
    ];

    /// <summary>The platform's design-time decisions, locked where the platform must stay uniform.</summary>
    public static IReadOnlyList<SpecCandidate> PredefinedDecisions { get; } =
    [
        Predefined("backend.language", "csharp", locked: false, ("version", "14")),
        Predefined("backend.framework", "aspnet-core", locked: false, ("targetFramework", "net10.0")),
        Predefined("db.engine", "postgresql", locked: false, ("majorVersion", "18")),
        Predefined("db.access", "ef-core", locked: false, ("migrations", "true")),
        Predefined("auth.protocol", "jwt-bearer", locked: false),
        Predefined("storage.object", "s3-compatible", locked: false),
        Predefined("frontend.framework", "react-vite", locked: false, ("reactVersion", "19")),
        Predefined("test.framework.backend", "xunit", locked: false),
        Predefined("source.code-policy", "references-only", locked: true),
    ];

    public static SpecTopic? Find(string topicId) => Topics.FirstOrDefault(t => t.Id == topicId);

    private static SpecCandidate Predefined(string topic, string choice, bool locked, params (string Name, string Value)[] parameters) =>
        new(
            new SpecDecision(
                topic,
                SpecScopeLevel.Platform,
                choice,
                parameters.ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal),
                Array.Empty<SpecConstraint>(),
                locked,
                "Predefined platform decision."),
            ItemId: null,
            RevisionId: null,
            RevisionNumber: null,
            ProjectId: null,
            DefinedAt: DesignTime);
}
