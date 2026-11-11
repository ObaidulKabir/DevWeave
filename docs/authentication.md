# API Authentication

DevWeave accepts two bearer-token formats:

- Human access tokens are compact OIDC JWTs validated against the configured issuer and audience.
- Agent access tokens are the existing short-lived HMAC tokens minted after agent credential verification.

Both use the `Authorization: Bearer` header. The API selects the validation scheme from the token format.

## OIDC Configuration

Configure these values per environment:

- `Auth:Oidc:Authority`: the identity provider's OIDC authority/discovery URL.
- `Auth:Oidc:Audience`: the API audience registered with the identity provider.
- `Auth:Oidc:RequireHttpsMetadata`: keep `true` outside local development.
- `Auth:SigningKey`: a secret of at least 32 characters for agent access-token signing.

Production startup fails if the OIDC authority/audience are missing or if the agent signing key is
missing, too short, or still contains the development placeholder marker. Supply secrets through the
deployment secret manager rather than editing `appsettings.json`.

The OIDC provider must map these DevWeave identity claims into its access token:

- `tenant_id`: the DevWeave tenant UUID.
- `principal_id`: the corresponding DevWeave human principal UUID.
- `principal_type`: exactly `Human`.

Issuer, signature, lifetime, and audience are validated by the OIDC bearer handler. Tokens missing
valid DevWeave identity claims are rejected.

## Development pilot auth

When `ASPNETCORE_ENVIRONMENT=Development` and `Auth:Oidc:Authority` is empty, the API accepts
symmetrically signed human JWTs minted by the pilot endpoints (same claim shape as production OIDC):

- `POST /auth/pilot/bootstrap` — create tenant + human owner membership and return a bearer token
- `POST /auth/pilot/token` — mint a bearer token for an existing active human principal

These routes return 404 outside Development or when an OIDC authority is configured. Agents still use
`POST /tenants/{tenantId}/agents/{agentId}/authenticate`.

## Current Boundary

All endpoints require an authenticated bearer token by default. Root and health probes, tenant
creation, and agent credential exchange are the explicit anonymous exceptions. For tenant-scoped
routes, the route tenant must match the verified `tenant_id` claim; mismatches return 404, and the
claim establishes the PostgreSQL tenant session scope. Human requests also require an active local
human principal and active tenant membership. Routes containing a project ID require active project
membership; viewers are read-only, and owner/admin roles are required for project membership changes
and project archive/restore/delete operations. Agent requests use their agent-specific grant and
project-membership checks instead.

Resource-ID routes for knowledge items/revisions, artifacts, relationships, activities, and evidence
resolve their owning project and apply the same membership check. Review, approval, and revocation
routes require project Owner/Admin. Tenant-wide project and knowledge collections, actor-activity
queries, tenant membership management, and tenant governance operations are restricted to tenant
Owner/Admin. Platform-global schema and relation-type mutations require a trusted OIDC
`platform_admin=true` claim.

Knowledge Search is exposed only at
`POST /tenants/{tenantId}/projects/{projectId}/search`. The application verifies an active Human
principal, tenant membership, and project membership before invoking the search adapter. Project
Owner/Admin roles can search through `Restricted`; Member/Viewer roles are limited to `Public` and
`Internal`. PostgreSQL applies tenant, project, and classification predicates before full-text
ranking and pagination. Search returns only current-revision matches and does not expose total counts
or content snippets. Its GIN expression index is maintained transactionally with revision writes;
the index is not asynchronously refreshed. Tenant-wide Search is not exposed.

Audit actor IDs for project, knowledge, revision, activity, relationship, artifact, evidence-review,
and agent credential/grant mutations are taken from the authenticated `principal_id` claim. Legacy
actor fields remain accepted in request bodies for compatibility but are ignored as authority.

## Provisioning And Remaining Gates

The identity provider must issue claims for a principal already provisioned in DevWeave. Initial
tenant-owner provisioning is an out-of-band deployment/bootstrap responsibility; the current public
tenant-creation operation does not establish an owner membership. Configure that membership before
enabling human access.

Tenant-wide collections are currently Owner/Admin-only rather than filtered to each member's project
set. Initial tenant-owner provisioning remains an out-of-band deployment/bootstrap responsibility;
the current public tenant-creation operation does not establish an owner membership. Configure that
membership before enabling human access. The
`Authorization:RequireTenantMembership` setting defaults to `true`; it can be disabled only in
isolated test hosts that use synthetic identities.
