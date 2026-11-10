# Full-SRS Contract Scaffold

Status: **draft definitions only**. Baseline: `DWV-SRS-001` v1.5 (proposed, approval pending).
This work adds no endpoint mapping, DI registration, handler, authorization decision, or running
workflow. It does not claim acceptance of any SRS requirement.

## Where The Definitions Live

| Surface | Source | Status |
| --- | --- | --- |
| Existing Slices 1-8 | `src/DevWeave.Application/Abstractions/`, `src/DevWeave.Contracts/`, `src/DevWeave.Infrastructure/Persistence/` | Working prototype, separately tracked hardening |
| MVP lifecycle work planning (`LFC-003..006`) | Domain/application/API under `Lifecycle/`, `WorkPlanningEndpoints`, active EF migration `LifecycleWorkPlanning` | Implemented as first vertical slice from the scaffold; authenticated project scope and transition history |
| Future ports and authenticated-scope boundary | `src/DevWeave.Application/Abstractions/Planned/` | No implementations or registrations |
| Versioned request/response types | `src/DevWeave.Contracts/Planned/V1/` | Draft C# shapes, not a wire-compatibility commitment |
| Proposed physical schema | `src/DevWeave.Infrastructure/Persistence/Planned/` | Separate EF context, not registered in the live app |
| Proposed versioned HTTP routes | `docs/planned-full-srs.openapi.yaml` | Draft OpenAPI outline for unimplemented families |
| Implemented lifecycle routes | `docs/work-planning.openapi.yaml` | Live authenticated API surface |
| Proposed project MCP tools/resources | `docs/planned-project-mcp.json` | Draft tool schemas and replay resources, no MCP server |
| Contract tests | `tests/DevWeave.Architecture.Tests/PlannedContractTests.cs` | Model/port/route consistency, not behavior |

The two proposed migrations target **only** `PlannedContractDbContext`; its design-time
factory uses a distinct migration-history table. Do not apply them to the live database.
The existing `DevWeaveDbContext`, current migrations, repositories and endpoints are unchanged.

## SRS Family Mapping

| SRS families | Existing or proposed definition | Proposed schema groups |
| --- | --- | --- |
| `TEN`, `GOV`, `AC` | Existing identity ports; `IResourcePolicy`, `ISupportAccess`, `IAuditJournal`, `IRetentionAndErasure` | authorization grants, support access, audit, retention |
| `PRJ`, `KNW`, `REV`, `SCH`, `REL`, `PRO`, `ART`, `BR` | Existing project/knowledge/relationship/evidence ports | existing implementation and H0-H3 acceptance gaps are tracked in the implementation assessment |
| `LFC-003..006` | Implemented work/milestone planning, independent reported/verified/approved state and append-only transition records | active `work_items`, `milestones`, dependency and transition tables |
| `SRH`, `AGT`, `AGI` | Existing agent/context ports; `IKnowledgeSearch` | search documents |
| `INT`, `WEB`, `DATA` | `IIntegrationRegistry`, `IInboundEventProcessor`, `ITransactionalOutbox` | connections, external references, ingress receipts, webhooks, outbox |
| `EXP`, `RPT`, `UI` | `IPortableArchive`, `IProjectReporting` | import/export jobs, metric/report snapshots |
| `APP`, `COL` | `IApplicationCatalogue`, `ICollaboration`, `INotificationDelivery` | app catalogue/installations, annotations, assignments, notifications |
| `OPS`, `API`, `NFR` | `IPlatformOperations`, `IRetentionAndErasure`; draft OpenAPI | backup jobs, feature flags, retention policies |
| `MCO`, `MCO-BR` | Plan, campaign, runtime, scheduling, lease, workspace and integration ports | baselines, targets, campaigns, packages, leases, reservations, references, queue |
| `MCP` | MCP discovery, channels, durable messages/cursors and handoffs ports | service configs, channels, messages, cursors, events, handoffs |
| `COM` | Catalogue, entitlement, usage, managed-runner and reconciliation ports | plans, subscriptions, entitlements, usage, runners, billing reconciliation |

`AC-*` and `BR-*` are acceptance/business rules, not extra deployable subsystems. `NFR-*`
includes operational targets requiring measured verification, not just DTOs. Proposed A2A
support is architecture input awaiting an ADR; it is not assigned a fabricated SRS family.
The family map is a coverage index, **not** a requirement-by-requirement traceability signoff.

## Contract Boundaries

- `AuthorizedScope` is supplied by a future verified-identity adapter. Route/body IDs are
  selectors, not authority. All reads, writes, search counts, reports, export and replay must
  check resource permissions before disclosing data.
- The coordination schema stores external commit, repository, branch and workspace **references
  only**. Source code, patches, diffs, secrets and raw code-bearing messages must never enter
  the canonical database, events, indexes or audit.
- Accepted sends mean durable persistence, not recipient delivery or verification; channel
  sequence is local to a channel. Message cursors and lease fencing require atomic writes.
- Planned, reported, verified and approved work states remain separate. A2A/MCP messages,
  agent output and usage records confer no approval or lease authority.
- The OpenAPI file gives every proposed POST a typed `requestBody`, linked to a C# record,
  and common response envelopes. It remains an **unpublished draft**: request validation
  policy, complete response fields, pagination/error catalogs, security scopes, and
  protocol conformance profiles require approval before client generation.
- MCP tools/resources are separate from REST. The JSON file is a capability contract, not an
  MCP protocol conformance claim; pin an officially supported SDK/profile before implementation.

## Before Any Deployment

1. Reconcile each proposed table/column with its owning aggregate and approved SRS/architecture
   ADR. Add composite `(Id, TenantId)` and `(ProjectId, TenantId)` FKs to existing tables,
   transaction-level `app.tenant_id` scoping and failing-closed RLS. Draft migrations do not
   yet provide those controls and **must not be applied in production**.
2. Decide append-only role permissions, retention/redaction policy, write-once message/event
   disposition, lease exclusion and durable per-channel sequence allocation.
3. Finalize DTO validation and publish a complete OpenAPI document with precise response
   payloads, pagination, idempotency, RFC-style errors, revision conflicts and authorization scopes.
4. Add failing behavior tests for every requirement as each slice begins, including tenant
   isolation, concurrent write/lease conflicts, replay, code/secret exclusion, restore and
   external-runtime failure. The current tests only validate definitions.
5. Obtain approval for the proposal, record changes to released wire/schema versions through
   change control, then register/implement individual adapters and endpoints by release.
