using DevWeave.Domain.Agents;

namespace DevWeave.Application.Agents;

/// <summary>The persisted credential plus the plaintext secret returned exactly once at issuance (AGT-002).</summary>
public sealed record AgentCredentialIssued(AgentCredential Credential, string PlainTextSecret);