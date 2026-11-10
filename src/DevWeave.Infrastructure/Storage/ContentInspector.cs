using System.Text;
using DevWeave.Application.Abstractions;

namespace DevWeave.Infrastructure.Storage;

/// <summary>
/// The single shared content governance policy for every ingress, retrieval, index and export path
/// (SRS 7.7, ART-008, AC-09). High-signal credential material is rejected outright (never persisted,
/// indexed or exported); source-code-bearing content — code files, patches/diffs, code-bearing
/// transcripts and uncertain content that matches multiple code signals — is quarantined so it can
/// never be served or assembled into agent context. Detection is deliberately conservative and keyed
/// to unmistakable markers, so it is best-effort defense in depth rather than a guarantee (SRS 7.7).
/// Because the same policy is invoked from artifact ingestion, knowledge revision ingestion and
/// context-package assembly, content cannot evade the boundary by being mislabelled as text or
/// structured data.
/// </summary>
public sealed class ContentInspector : IContentInspector
{
    private static readonly HashSet<string> SourceCodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".java", ".py", ".js", ".ts", ".tsx", ".jsx", ".go", ".rs",
        ".c", ".cpp", ".h", ".hpp", ".php", ".rb", ".sh", ".sql",
        ".kt", ".kts", ".swift", ".fs", ".vb", ".scala", ".lua", ".pl",
    };

    private static readonly string[] SecretMarkers =
    {
        "-----BEGIN RSA PRIVATE KEY-----",
        "-----BEGIN PRIVATE KEY-----",
        "-----BEGIN EC PRIVATE KEY-----",
        "-----BEGIN OPENSSH PRIVATE KEY-----",
        "AKIA",   // AWS access key id prefix
        "ASIA",   // AWS temporary access key id prefix
        "sk-",    // OpenAI-style API keys
        "ghp_",   // GitHub personal access token
        "xoxb-",  // Slack bot token
        "xoxp-",  // Slack user token
        "ya29.",  // Google OAuth access token
        "AIza",   // Google API key prefix
    };

    private static readonly string[] CredentialAssignments =
    {
        "password=", "passwd=", "secret=", "client_secret=",
        "access_key=", "access_token=", "api_key=", "private_key=",
    };

    private static readonly string[] DatabaseConnections =
    {
        "mongodb://", "mongodb+srv://", "postgres://", "postgresql://", "mysql://",
    };

    // Patch/diff markers: a single unambiguous marker marks the payload as code-bearing (SRS 7.7).
    private static readonly string[] DiffMarkers =
    {
        "diff --git", "@@ -", "*** Begin Patch", "Index: ",
    };

    // Code signals: content is treated as code-bearing once it matches at least two distinct signals.
    // Requiring two signals keeps ordinary prose, JSON metadata and short summaries from being
    // misclassified while still catching real source code labelled as text or structured data.
    private static readonly string[] CodeSignals =
    {
        "using System;", "using System.", "#include <", "#include \"",
        "public class ", "public sealed class ", "public static void ", "private static ",
        "<?php", "import java.", "#!/usr/bin/env ", "console.log(",
        "fn main(", "package main", "func main(", "import React",
        "def __init__", "if __name__",
    };

    private const int DistinctCodeSignalThreshold = 2;

    public Task<ContentInspectionResult> InspectAsync(
        string fileName,
        string mediaType,
        byte[] content,
        CancellationToken ct = default)
    {
        var text = Encoding.UTF8.GetString(content);

        // Secrets take precedence over source code: credential-bearing payloads are never persisted.
        // Each check is evaluated independently so every matched marker is reported (and every
        // out variable is definitely assigned regardless of short-circuiting).
        var reasons = new List<string>();
        if (ContainsAny(text, SecretMarkers, out var secretMarker) && secretMarker is not null)
        {
            reasons.Add($"Secret marker '{secretMarker}' detected.");
        }

        if (ContainsAny(text, CredentialAssignments, out var assignmentMarker) && assignmentMarker is not null)
        {
            reasons.Add($"Credential assignment '{assignmentMarker}' detected.");
        }

        if (ContainsAny(text, DatabaseConnections, out var connectionMarker) && connectionMarker is not null)
        {
            reasons.Add($"Database connection string '{connectionMarker}' detected.");
        }

        if (reasons.Count > 0)
        {
            return Task.FromResult(ContentInspectionResult.Reject(reasons));
        }

        var codeReasons = DetectSourceCode(fileName, text);
        if (codeReasons.Count > 0)
        {
            return Task.FromResult(ContentInspectionResult.Quarantine(codeReasons));
        }

        return Task.FromResult(ContentInspectionResult.Allow());
    }

    private static IReadOnlyList<string> DetectSourceCode(string fileName, string text)
    {
        var extension = Path.GetExtension(fileName);
        if (SourceCodeExtensions.Contains(extension))
        {
            return new[] { $"Source code with extension '{extension}' is prohibited." };
        }

        if (ContainsAny(text, DiffMarkers, out var diffMarker) && diffMarker is not null)
        {
            return new[] { $"Patch/diff content ('{diffMarker}') is prohibited." };
        }

        var signals = CodeSignals.Count(signal => text.Contains(signal, StringComparison.Ordinal));
        if (signals >= DistinctCodeSignalThreshold)
        {
            return new[]
            {
                $"Content matches {signals} source-code signals and is treated as code-bearing.",
            };
        }

        return Array.Empty<string>();
    }

    private static bool ContainsAny(string text, string[] markers, out string? matched)
    {
        foreach (var marker in markers)
        {
            if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                matched = marker;
                return true;
            }
        }

        matched = null;
        return false;
    }
}
