# DevWeave Implementation Baseline Assessment

Assessment date: 2026-11-11. Purpose: reconcile the supplied specifications, actual workspace implementation and development sequence after completing Slices 9-12.

## 1. Baselines and Precedence

| Document | Observed status | Planning treatment |
| --- | --- | --- |
| `DWV-SRS-001` v1.5, prepared 2026-11-09 | Proposed business-aligned baseline; approval pending | Product requirements and release tags remain controlling |
| `DevWeave_SRS_v1.5.pdf` and `DevWeave_SRS_v1.5_Business_Aligned.pdf` | Both 75 pages; extracted text is identical | No observed product-requirement change between these PDF copies |
| `DEVW-ARCH-001` v1.0, dated 2026-11-10 | 33-page proposed engineering baseline; all review roles pending | Design recommendations to reconcile through ADRs, not an approved replacement SRS |
| Supplied DOCX counterparts | Document-control text identifies the same versions; requirement/design identifier sets match their PDFs | Editable counterparts; PDF text used for detailed requirement assessment, not a claim of byte-for-byte DOCX/PDF equivalence |

## 2. Verification Performed

| Check | Actual result |
| --- | --- |
| `dotnet build DevWeave.sln -c Release --nologo` | Succeeded, 0 warnings, 0 errors |
| `dotnet test DevWeave.sln -c Release --nologo` | 273 passed: Domain 117, Application 85, Architecture 2, API integration 69 |
| `npm run build` in `web/studio` | TypeScript and Vite production build succeeded |
| Read-only query of local `devweave` database | 12 applied migrations; 26 application tables plus EF migration history |
| Local database security inspection | `devweave` is a superuser with `BYPASSRLS`, owns the tables; RLS enabled on all tenant-scoped tables with isolation policies |
| Integration fixture inspection | Unique `devweave_it_*` databases on localhost:5433, not Testcontainers; object storage replaced with an in-memory fake |
| Git source-inclusion check | `.gitignore` corrected; Artifacts source/test directories are now tracked |

Passing tests establish the current tested behaviors, not full SRS conformance, authenticated tenant isolation, production readiness or protocol support. No load, chaos, real S3 adapter, OIDC, backup/restore or protocol-conformance exercise was performed in this assessment.

## 3. What Is Already Implemented

All twelve foundation slices are implemented as prototypes.

| Slice | Existing implementation | Acceptance still required |
| --- | --- | --- |
| 1 Identity | Tenant, three principal kinds, invitation/activation/revocation, repositories and REST | Verified identity; effective membership/project policies; service authentication; audited support grants (`TEN-004/005/007/009`) |
| 2 Projects | Project states, lifecycle-profile anchor, project membership/grants, typed references | Authorized operations, transition audit, controlled deletion/export, initial goals/constraints/risk types; profile/workflow semantics (`PRJ-007/009`, `LFC-*`) |
| 3 Knowledge | Typed envelope, immutable revision objects, current pointer, expected-revision checks | Schema validation at item creation; exact revision schema pin; independent state dimensions; database-enforced immutability/concurrency |
| 4 Schemas | Register/publish/deprecate/list/discover, JSON Schema validator, unique namespace/version | Restricted administration/discovery; schema publication validation and supported-vocabulary contract tests |
| 5 Relationships | Typed directed edges, canonical 11 types, endpoint/cardinality checks, bounded BFS paths | Per-resource authorization, database ownership constraints and concurrent cardinality tests; canonical type semantics review |
| 6 Activities | Activity input/output pins, list queries, amendment reason/reference guard, provenance projection | Validated revision references, automatic mutation events/audit, database append-only protection, complete as-of reconstruction |
| 7 Artifacts/evidence | S3 adapter port, checksums, version rows, presigned URL generation, inspector, evidence review | Real adapter tests; immutable collision-safe object keys; concurrency/retry and failure recovery; shared content policy; authorized evidence-backed verification |
| 8 Agents/context | AI principal reuse, hashed credentials, approval/revocation grant fields, pinned context entries, execution Activity | Credential enforcement/rotation/disable APIs, explicit project admission, classification policy, protected retry replay, execution dedupe, schema/tool discovery |
| 9 Search | Project-scoped metadata/text search, ranked results, permission-safe cursors | Tenant-wide search, complete ranking audit, SNI-based filtering spikes |
| 10 Integrations | Integration registry, inbound event dedupe, durable outbox | Provider-specific security validation, real webhook test harness |
| 11 Audit/Export | Portable manifest, audit journal, import/export tracking | Native JSON-LD export, storage-adapter verification, full import integrity tests |
| 12 Reporting | Project metrics projection, report audit, studio endpoints | Evidence review UX, advanced metric catalogues, reproducible report generation |

`DevWeave.Mcp` remains a skeleton; the worker only logs a timer; Studio only renders host connectivity and plane descriptions. There is no A2A gateway, runner connector, campaign/scheduler/lease engine, merge queue or commercial ledger.

## 4. Findings and Remediation Ownership

These are planning findings established from static source inspection and the checks above, not exploit or load-test results. References are relative to the workspace root. Remediation IDs below are local planning IDs, not SRS IDs.

### H0. Reproducible Source and CI

- **Ignored application code:** `.gitignore:6` uses unanchored `artifacts/`. `git check-ignore -v` confirmed it matches `src/DevWeave.Domain/Artifacts/Artifact.cs`, `src/DevWeave.Application/Artifacts/UploadArtifact.cs`, `src/DevWeave.Contracts/Artifacts/V1/Responses.cs` and artifact tests. Narrow the build-output ignore and ensure all required sources appear in the intended commit. Local builds include files that a clean checkout could omit.
- **CI database missing:** `tests/DevWeave.Api.IntegrationTests/IdentityTestFactory.cs` requires a database server at localhost:5433 with create/drop privileges; `.github/workflows/ci.yml` provisions no PostgreSQL service. Add an isolated CI service/configuration or Testcontainers. Run migration tests on fresh infrastructure, report cleanup failures, and keep separate application-role security tests.
- Slices 1-8 remain largely uncommitted in this workspace. No commit/push was performed here. Clean-checkout verification must follow an explicitly authorized commit.

Owner: backend/DevOps role, assignment pending. Gate: before treating CI or repository state as a reproducible baseline.

### H1. Identity, Authorization and Agent Admission

- `src/DevWeave.Api/Program.cs` registers no authentication/authorization middleware or endpoint policies. Route tenant IDs and body actor IDs are selectors, not verified authority. Existing tenant-predicate tests do not prove `TEN-007`, `GOV-001..005`, or architecture section 15.2.
- `src/DevWeave.Application/Agents/AssembleContextPackage.cs` checks a project-covering grant but never checks item classification. `AgentGrant.cs` has no classification ceiling and null `ProjectId` covers every project. Enforce explicit project admission, authenticated agent/credential status, task/capability/classification policy, and bounded context size before fetching protected revisions.
- Context retry lookup occurs before authorization and is scoped only to tenant/project/key, not agent/operation/request fingerprint. Replays must not bypass revocation or return another agent's package.
- `ApproveAgentGrant.cs` accepts any resolvable tenant principal as approver, including non-human principals; it does not establish authorized human approval. Discovery only lists grants, not permitted schemas, relation types and tools. Domain credential methods do not constitute enforced REST authentication, rotation or disable workflows.
- `RecordAgentExecution.cs` does not verify package ownership by the submitting agent, current grant/credential validity, or idempotency. Every call appends a new Activity. The context-key index is not unique. Implement sender/operation/project-scoped durable dedupe, request-fingerprint conflict handling and concurrent retry tests (`AGT-010`, `AC-07`, architecture 11.3).

Owner: identity/security/backend roles, assignment pending. Gate: before Search, external model use, agent access or protocol collaboration.

### H2. Content Policy, Revision Safety and Database Integrity

- `CreateKnowledgeItem.cs` does not resolve the schema. `AddRevision.cs` validates against the item's schema version but records the caller's separate `schemaVersion`; these can disagree. Reject unknown/inactive types at creation and validate against the exact version recorded on each revision (`KNW-002/004`, `REV-002`, SRS 7.3).
- Code exclusion currently checks explicit `RepresentationKind.SourceCode` in context snapshots and artifact filename extensions. Knowledge revision ingestion does not invoke the artifact inspector; code/secret text labelled `Text` or `StructuredData` is not excluded by that enum guard. Build one shared policy for all supported ingest/retrieval/index/export paths, including uncertain content, diffs and transcripts (`ART-008`, SRS 7.7, `AC-09`).
- `UploadArtifact.cs` persists quarantined bytes through the ordinary storage port. Quarantine needs a separate inaccessible, bounded-lifetime disposition and explicit cleanup; it must not become permanent canonical source retention.
- The database uses a superuser/table owner and no RLS. Existing separate foreign keys to tenant and parent do not enforce matching ownership tuples. Add a privileged migration role and least-privilege application role, tenant/project composite constraints and RLS with read/write policies and pooled-connection reset tests (architecture 15.2, 16.2; `TEN-002`, `DATA-03`).
- Get-only domain properties and no PUT/DELETE endpoints do not enforce immutable database rows. Add ordinary-role UPDATE/DELETE denial for revisions/events and a separately controlled audited exceptional workflow (`REV-001/006`, SRS 7.3).
- `KnowledgeItemConfiguration.cs` declares no concurrency token/conditional current-pointer update. The unique revision index helps reject duplicate revision numbers but is not complete conflict recovery. Test independent DbContexts, atomic current-pointer/version updates and safe 409 mapping; apply concurrent cardinality enforcement to relationship creation.
- `KnowledgeItem.Approve()` updates `State` but not `ReviewState`; verification is available without qualifying evidence and new content can leave prior approval/verification in place. Correct independent lifecycle/review/verification/currency state behavior and revision-specific evidence invalidation (`KNW-012`, `PRO-005`, `AC-08`).
- `RecordActivity.cs` does not resolve input/output revision references or ensure each revision matches its item/project. Ordinary mutations do not automatically write Activity/audit/outbox records. Add validated attribution and transactional event emission, rather than relying on callers to submit optional history.
- `ReconstructProjectState.cs` filters revisions by recorded time but returns an unfiltered activity timeline and includes items created after `asOf`. It does not reconstruct historical project/relationship/approval state. Extend the recorded-time snapshot or document a narrowed endpoint; do not claim full `REV-007`.

Owner: domain/backend/security roles, assignment pending. Gate: trusted knowledge history and shared projections before Search.

### H3. Artifact Integrity and Basic Governance

- `UploadArtifact.cs` derives object keys from lossy lowercasing/sanitization plus `max(version)+1`. Distinct keys such as `Design`/`design` or `a/b`/`a-b` can address the same object; concurrent uploads can overwrite bytes before the database rejects a duplicate version. Use collision-resistant immutable object IDs, write-once storage, transactional version allocation, idempotency and orphan cleanup (`ART-003/006`, `DATA-06`).
- API artifact tests replace S3 with `InMemoryObjectStorage`; they do not prove real bucket provisioning/private access, signed URL expiry or object preservation. Add MinIO/S3 adapter tests and fail-after-upload recovery tests. Revisit multipart CSRF protection once the authentication scheme is selected; deferring OIDC alone is not a security justification for disabling antiforgery.
- Evidence acceptance is separate from the unrestricted knowledge verification path. Bind verification to required criteria, authorized review, exact claim/revision and integrity checks. Keep reported, accepted and verified separate.
- Add core non-linear work/lifecycle execution, work items/milestones, owners/dependencies and auditable transitions (`LFC-003..006`, M/MVP). A lifecycle-profile anchor is not that implementation; advanced designer/configuration and `LFC-009` remain R2.

Owner: domain/storage/governance roles, assignment pending. Gate: Foundation MVP; artifact overwrite protection before further ingest.

## 5. Plan Corrections

1. Replace "8 of 12 slices complete" with "8 prototype slices implemented; acceptance/hardening pending". Track implementation, tests and release acceptance separately.
2. Execute H0-H3 remediation before Slice 9; establish audit/outbox infrastructure during hardening, not only after Search or at R1 messaging.
3. Correct requirement references: `REV-005` is S/R2, `SCH-008` is S/FUT, `AGT-012` is S/R2, and there is no `EVI-*` requirement family in this SRS. Evidence maps to `PRO-005/006`. `EXP-004` PROV/JSON-LD interchange is S/FUT even though a small projection already exists.
4. Add explicit MVP owners/work for `TEN-009`, `GOV-001..005/007/008/011`, `PRJ-007`, `LFC-003..006`, `RPT-001..003/006`, `APP-003`, `OPS-001..004`, `API-*`, `WEB-*`, native export/import and initial type-catalogue acceptance. Do not silently defer these behind the original twelve feature labels.
5. Keep SRS release tags controlling: R1 contains coordination, basic entitlements/usage and agency isolation; R2 contains managed execution, billing automation and advanced client portal. Architecture P1 plan-lock preparation can precede R1 without claiming a fully executing campaign in MVP.
6. Add proposed R1 A2A/Agent Card/task-mapping work and native/bridged conformance labels. Messages or A2A task completion never grant authority or establish DevWeave completion.
7. Treat NATS, Redis, Next.js, protocol-version/SDK profiles, runner transport and service extraction as ADRs with approval/status. Verify official protocol support at implementation kickoff; this assessment did not verify remote specification or SDK availability.
8. Namespace open decisions as `SRS:OD-*` and `ARCH:OD-*`; their numbers describe different decisions. Maintain a requirement-level evidence register per SRS 11.4/14.1; this assessment is not a complete signed traceability register.

## 6. Release Gates

Foundation MVP requires authenticated tenant/project/resource isolation, protected history, shared code/secret exclusion, initial types and lifecycle work, native export/import, real storage verification, basic reporting/Studio, operational readiness and tested recovery. Every M/MVP item requires passing evidence or an approved, owned, expiring waiver.

R1 adds plan-locked external-agent campaigns, distinct logical agent/runtime/runner identities, durable messaging/replay, safe leases/fencing, isolated workspace references, protected repository integration, basic commercial governance and two-agent end-to-end evidence. A2A adoption is pending architecture approval; mandatory project MCP is not replaced by A2A.

R2 adds optional managed execution, isolation/secrets/cleanup, hybrid campaigns, compute/billing reconciliation and advanced portal/governance. Infrastructure or gateway skeletons are not evidence these capabilities exist.

No implementation, specification PDF/DOCX, database content, or Git history was changed during this planning assessment.
