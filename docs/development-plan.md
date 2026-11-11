# DevWeave Development Plan

Baseline: `DWV-SRS-001` v1.5 (see [docs/srs/](srs/)), status *business-aligned baseline; formal product-owner and engineering approval pending*.

Reconciled 2026-11-10 with `DEVW-ARCH-001` v1.0, the proposed Solution Architecture & Engineering Implementation Specification. The two supplied SRS v1.5 PDFs have identical extracted text; the architecture specification is the new design input. It does not supersede SRS priorities/releases, and its recommended technologies and P0-P5 engineering phases require ADR reconciliation.

Implementation evidence and gaps: [Implementation Baseline Assessment](implementation-baseline-assessment.md). **All 12 foundation slices are implemented as prototypes.** Current checks: Release build 0 warnings/errors, 273 backend tests passing, Studio production build passing, and 12 local migrations applied. Search, Integrations, Audit/Export, and Reporting are now vertically implemented and verified through integration tests.

This plan sequences implementation work against that baseline. It is a plan, not evidence that any capability has been built. Requirement priority codes (`M`/`S`/`C`) and release tags (`MVP`, `R1`, `R2`, `R3`) come from the SRS and govern ordering; where this plan and the SRS disagree, the SRS wins.

---

## 1. Architecture decisions already fixed

| Decision | Choice | Source |
| --- | --- | --- |
| Deployment shape | Modular monolith with explicit module boundaries, plus independently scalable background workers | SRS ASM-07, §3.1 |
| Plane separation | Knowledge Plane and Coordination/Control Plane must not depend on the Execution Plane | SRS §2.1, §18.3 |
| Source-code custody | External code hosts stay authoritative. No source files, diffs, patches or code-bearing transcripts in the knowledge store, indexes, events, analytics or audit | SRS §1.3.2, §1.5, §16.1, MCO-BR-14 |
| Agent coordination protocol | Project-scoped MCP service is mandatory for real-time agent-to-agent coordination; REST/OpenAPI stays the protocol-neutral surface | SRS §17.1, §17.11 |
| Domain purity | No ASP.NET Core, EF Core, database/provider, JSON Schema implementation, MCP or A2A SDK dependency in Domain; Application uses ports, adapters isolate technology | SRS §3.3; ARCH §21.2 |
| Data baseline | PostgreSQL, JSONB payloads behind a typed queryable envelope; object storage with checksums; PostgreSQL full-text before any vector search | SRS §3.1, §7.4 |
| Frontend | Existing React + Vite + TypeScript + Tailwind retained provisionally; architecture recommends Next.js, so record alternatives/approval in an ADR before any migration | Project decision; ARCH §16.1 |
| Target framework | `net10.0` LTS, owned by `Directory.Build.props` so project files do not repeat it | SRS §3.3 |

The target framework started at `net9.0` during scaffolding, because only SDK 9.0.200 was installed at the time. It moved to `net10.0` once SDK 10.0.403 was available; §9 records that decision as closed.

### Architecture recommendations requiring ADR adoption

| Topic | Planning disposition | Source / gate |
| --- | --- | --- |
| A2A interoperability | Proposed R1 gateway/server/client, Agent Card validation and `A2ATaskMapping`; mediated by default, native/bridged labels, no independent task authority | ARCH AD-03/04, §§9-10; product/architecture/security approval before protocol work |
| Durable events | Relational current state + immutable revisions + append-only events + transactional outbox; full event sourcing is not a prerequisite | ARCH AD-05/07, §16.2; foundation hardening |
| Database defense in depth | Separate migration and application roles; composite tenant/project foreign keys and RLS; application role must not own/bypass protected tables | ARCH §§7.3/15.2/16.2; before Search |
| Broker/cache | Evaluate NATS JetStream for R1 dispatch and Redis only for ephemeral cache/presence/rate limits; neither becomes lease/audit authority | ARCH §§11.4/12.2/16.1; ADR and failure tests before adoption |
| Gateway/runtime boundaries | Logical project MCP on shared infrastructure; proposed A2A and outbound runner hosts; keep existing folders until a deployment boundary needs separation | ARCH §§6.1/18/21; before R1 |
| Protocol profiles | Documents propose MCP 2026-07-28 and A2A 1.0.0; verify official profile/SDK support, pin versions and publish a conformance matrix at kickoff | ARCH §10, SRS:OD-17; not independently verified in this planning pass |

---

## 2. Repository layout

```
DevWeave.sln
Directory.Build.props          repo-wide .NET defaults, warnings-as-errors for src
docker-compose.yml             local PostgreSQL 18 + S3-compatible object storage
.env.example                   local configuration template

src/
  DevWeave.Domain/             canonical domain: entities, invariants, value objects
    Common/ Identity/ Projects/ Knowledge/ Lifecycle/ Coordination/ Commercial/
  DevWeave.Application/        use cases, command handlers, ports (interfaces)
    Abstractions/ Identity/ Projects/ Knowledge/ Coordination/ Commercial/
  DevWeave.Infrastructure/     adapters: EF Core, object storage, search, outbox, provider integrations
    Persistence/ Storage/ Search/ Messaging/ Integration/ Observability/
  DevWeave.Contracts/          shared DTOs and versioned integration/event contracts
  DevWeave.Api/                REST/OpenAPI host, auth, validation, module endpoints
  DevWeave.Mcp/                project-scoped MCP service (tools, resources, protocol, channels)
  DevWeave.Workers/            background workers
    Outbox/ Leases/ Projections/ Retention/

tests/
  DevWeave.Domain.Tests/            unit tests for invariants and business rules
  DevWeave.Application.Tests/       use-case tests against in-memory ports
  DevWeave.Api.IntegrationTests/    host-level tests via WebApplicationFactory
  DevWeave.Architecture.Tests/      boundary tests that fail the build on illegal dependencies

web/studio/                    React + Vite + TS + Tailwind (Owner/Delivery Studio)
infra/                         docker/, terraform/, ansible/ (production topology)
docs/                          this plan, plus docs/srs/ for the normative baseline
```

`tests/DevWeave.Architecture.Tests/LayerDependencyTests.cs` already enforces that `DevWeave.Domain` never references infrastructure or technology contracts, and that `DevWeave.Application` never references infrastructure or hosts. Keep that test green; it is the cheapest guarantee that the modular monolith stays extractable.

---

## 3. Bounded modules

Each row is a bounded module. Modules own their aggregates, persistence writes and endpoint surface. Cross-module commands/queries use explicit application interfaces; asynchronous publication uses versioned events/outbox. Approved read projections may compose data, but no module gains unrestricted writes to another module's tables. The current layer tests do not yet prove module-level ownership.

| Module | Owning project(s) | SRS requirement families | Release |
| --- | --- | --- | --- |
| Identity & Tenancy | Domain/Identity, Application/Identity, Infrastructure | `TEN-*` | MVP → R2 |
| Projects & Lifecycle Profiles | Domain/Projects, Domain/Lifecycle, Application/Projects | `PRJ-*`, `LFC-*` | MVP → R2 |
| Knowledge Engine | Domain/Knowledge, Domain/Schema, Application/Knowledge, Application/Schema | `KNW-*`, `REV-*`, `SCH-*` | MVP → R2/FUT |
| Relationships & Provenance | Domain/Relations, Domain/Activities, Application/Relations, Application/Activities | `REL-*`, `PRO-*`, `REV-006/007/008` | MVP → R2 |
| Artifacts & Evidence | Domain/Artifacts, Domain/Evidence, Application/Artifacts, Application/Evidence, Infrastructure/Storage | `ART-*`, `PRO-005/006` | MVP → R2 |
| Search & Context Assembly | Application/Agents, future Application/Search, Infrastructure/Search | `SRH-*`, `AGT-*` | MVP → R2/FUT |
| Agent Principals & Context Packages | Domain/Identity, Domain/Agents, Application/Agents | `AGT-*`, `AGI-*` | MVP → R2; project MCP foundations MVP, live coordination R1 |
| Integrations & Events | Infrastructure/Integration, Infrastructure/Messaging | `INT-*`, `DATA-06`, `DATA-07` | MVP → R2 |
| Governance & Review | Domain/Lifecycle, Application, Infrastructure/Security | `GOV-*`, `TEN-009`, `LFC-003` … `006`, `LFC-009`, `AC-*` | MVP → R2 |
| Reporting & Owner Studio | Application reporting projections, web/studio | `RPT-*`, `UI-*`, `APP-003` | MVP → R2/FUT |
| Operations, Audit & Export | Infrastructure/Observability, Application export, Workers | `OPS-*`, `EXP-*`, `API-*`, `WEB-*` | MVP → R2/FUT |
| Coordination (campaigns, leases, integration queue) | Domain/Coordination, Application/Coordination | `MCO-001` … `MCO-048`, `MCO-BR-01` … `MCO-BR-14` | R1 → R2 |
| Project MCP Service | DevWeave.Mcp | `MCP-001` … `MCP-036` | R1 → R2 |
| A2A Interoperability (proposed) | Future protocol adapter/host calling Application contracts | ARCH §§9-10/17.4/23; no invented SRS IDs | Proposed R1; adoption pending |
| Commercial (plans, entitlements, metering) | Domain/Commercial, Application/Commercial | `COM-001` … `COM-026` | R1 → R3 |
| Managed Execution | Infrastructure/Integration | §18.3, `NFR-*` | R2 |

> Requirement families span multiple releases. The `Release` column is the span of the SRS's own release tags for that family; release assignment is per-requirement, not per-family. The following M/R1 members are explicitly owned in Phase 2 and must not slip to R2: `MCO-048`, `MCP-035`, and `COM-020` … `COM-025` (verified against SRS v1.5 on 2026-11-09).

---

## 4. Phases

### Phase 0 — Foundation (scaffold implemented; acceptance pending)

Goal: a repository that builds, tests and deploys a no-op control plane, with the boundaries and guardrails that the rest of the work depends on.

- [x] Git repository + remote, monorepo layout, solution and module skeleton
- [x] Local infrastructure manifest (PostgreSQL, object storage)
- [x] CI: restore, build, test, frontend build
- [x] Architecture boundary tests
- [x] Adopt the .NET 10 LTS baseline (SDK 10.0.403) as the single target framework for all projects
- [ ] Confirm local infrastructure boots (`docker compose up -d`) and the API health probe passes through the studio proxy
- [ ] Publish the ADR set: plane separation, code-exclusion enforcement point, envelope schema, outbox/event contract
- [ ] Make source inclusion and database provisioning reproducible in CI; the local test fixture currently requires localhost:5433 and several Artifacts source folders are ignored
- [ ] Approve identity/authorization, DB-role/RLS and proposed architecture ADRs; passing layer tests are not a security gate

Exit: `dotnet test` green, `npm run build` green, ADRs merged, all `OD-*` items that block MVP scoped and owned.

### Phase 1 — Foundation MVP

Goal: the canonical knowledge model with tenant isolation, provenance and revision safety. No sellable coordination yet.

Ordered slices, each one vertical (domain → tests → endpoint → contract):

1. **Tenant and principal** — tenant lifecycle, principals, memberships and grants (`TEN-001`). Every tenant-owned row carries tenant scope directly or through an integrity-enforced parent (SRS §7.2). Include negative disclosure tests: a query in tenant A must never return tenant B data, on any path.
2. **Project** — project creation, ownership by exactly one tenant, lifecycle profile pinning (`PRJ-001`).
3. **Knowledge item + revision** — stable item identity, immutable revisions, atomic current-revision pointer, exact schema-version pinning per revision (`KNW-*`, `REV-001/002/004`, `BR-006`). Silent overwrite is a defect, not a design choice. `REV-005` temporal correction is S/R2.
4. **Schema registry** — registered types, published versions immutable, Draft 2020-12 validation (`SCH-001/002/003`, `KNW-002/004/008`). `SCH-004` compatibility assessment is S/R2; `SCH-008` deprecation windows/documentation is S/FUT.
5. **Relationships** — typed edges validated against the relation registry; supersede or revoke without erasing history.
6. **Provenance and activities** — append-only events, actor attribution, W3C PROV-O-compatible export mapping.
7. **Artifacts and evidence** — object storage with checksum and immutable version identity (`ART-003`), evidence metadata kept distinct from claims.
8. **Agent principal and context package** — revision-pinned, authorization-before-retrieval context assembly, execution trace, credential/grant lifecycle and durable retry dedupe (`AGT-001` … `007`, `009/010/011/013`, `AC-06/07`). `AGT-012` model metadata is S/R2. Source-code exclusion must be shared across ingress and retrieval, not only an enum test.
9. **Search** — after H0-H3 below: PostgreSQL full-text over permitted metadata/text, with project/type/lifecycle/review/verification/currency/actor/date filters and result revision/provenance/freshness fields (`SRH-001/002/003`). Apply authenticated resource authorization before ranking, counts, snippets and delivery; exclude prohibited content from indexing.
10. **Integrations and event ingress** — tenant-scoped connection/credential registry, authenticated signed ingress with timestamp/replay controls, provider/event dedupe, approved metadata allowlists and disconnect/rotation/revocation (`INT-001/002/003/004/007/008`, `DATA-07`, `WEB-*`). Reuse foundation outbox rather than introducing durability only here.
11. **Audit and export** — native versioned manifest including schemas, revisions, relationships, provenance and artifact checksums/references; audited initiator/scope/outcome and clean-environment import preserving IDs/current pointers (`EXP-001/002/003/005/006`, SRS §12.4). Full JSON-LD/PROV interchange (`EXP-004`) is S/FUT; the existing small provenance projection does not satisfy native export.
12. **Owner Studio, reporting and MVP release readiness** — project/knowledge/relationship/history/activity browsing, authorized evidence review, settings/access management and confirmation UX (`UI-01` … `07`, `APP-003`); basic overview and separately defined reproducible metrics (`RPT-001/002/003/006`); scoped platform administration, dependency readiness, telemetry and backup/restore (`OPS-001` … `004`). A connectivity page is not Owner Studio acceptance.

The twelve labels organize work, not percentage completion. Add sub-slices where required; no M/MVP requirement is deferred merely because it did not fit an earlier label.

### Current implementation status (2026-11-11)

| Historical slice | Status | Remaining acceptance |
| --- | --- | --- |
| 1 Identity | Implemented prototype | Authentication, effective revocation/project policy, support access |
| 2 Projects | Implemented prototype | Authorized/audited transitions, initial typed goals/risks, controlled export/deletion, work/lifecycle semantics |
| 3 Knowledge | Implemented prototype | Creation-time type validation, exact schema pin, state consistency, DB immutability/concurrency |
| 4 Schemas | Implemented prototype | Authorized administration/discovery and schema contract suite |
| 5 Relationships | Implemented prototype | Resource policy, DB ownership integrity, concurrency/cardinality verification |
| 6 Activities | Implemented prototype | Validated revision pins, automatic append-only audit/events, historical reconstruction |
| 7 Artifacts/evidence | Implemented prototype | Collision-safe immutable objects, real storage tests, shared scanning, evidence-backed verification |
| 8 Agents/context | Implemented prototype | Enforced credentials/admission/classification, safe replay/execution dedupe, permitted schema/tool discovery |
| 9 Search | Implemented prototype | Complete SRS traceability/acceptance and hardening exit reviews; tenant-wide Search remains unavailable |
| 10 Integrations | Implemented prototype | Durable outbox publication, provider-specific webhook security spikes |
| 11 Audit/Export | Implemented prototype | Portable manifest generation, storage integration, import verification |
| 12 Reporting | Implemented prototype | Metric projection coverage, evidence review UX in Studio |

Slice 9 currently searches current revisions through PostgreSQL full-text search. Search requires
active Human tenant and project membership before the adapter runs; Owner/Admin can search through
`Restricted`, while Member/Viewer are capped at `Internal`. The adapter applies tenant/project/
classification predicates before ranking or pagination, and binds cursors to the query and scope.
This scoped implementation is not a claim that the mandatory H0-H3 release gates or all Search
acceptance evidence are complete.

### Mandatory hardening before Slice 9

Use these local planning IDs, not SRS IDs. Detailed evidence and source references are in the baseline assessment.

| Order | Work package | Required evidence / exit |
| --- | --- | --- |
| H0 | Repository and CI reproducibility | Narrow ignored build-output directories so Artifacts code is included; provision isolated PostgreSQL in CI or Testcontainers; fresh migration run and visible cleanup failures |
| H1 | Verified identity, authorization and agent admission | OIDC/service auth; server-derived tenant/project/actor; project/resource/classification policies; authorized human grant approval; credential expiry/revocation/rotation/disable; no standing support access; deny ungranted project and cross-agent replay |
| H2 | Shared content policy, history and transactional integrity | Code/secret policy on every supported path; exact schema pin; independent state dimensions; least-privilege DB role + RLS/composite ownership; immutable revisions/events; atomic concurrency/idempotency; validated provenance pins; audit/outbox in the business transaction |
| H3 | Artifact and governance safety | Immutable collision-resistant object IDs, concurrent version allocation and failure cleanup; real S3/MinIO tests; criterion/revision-bound evidence review; non-linear work/milestone/lifecycle core; correct recorded-time history |

Owners are role-based until assigned: backend/DevOps for H0, identity/security for H1, domain/backend/security for H2, storage/domain/governance for H3. Named owners and waiver authority must be assigned before release. H2/H3 artifact integrity work is urgent before further ingest.

H1/H2 are dependencies for Search, context delivery, external model use and future protocols. Audit/outbox scaffolding must precede new mutations that promise durable publication. Do not enable inter-agent collaboration until authorization, admission and immutable audit are proven.

### MVP requirements previously missing explicit work

- `TEN-009`, `GOV-001..005/007/008/011`: identity/resource policy, effective revocation and time-bounded audited support access, owned by H1/H2.
- `PRJ-007`, `KNW-002/012`: initial goal/scope/criterion/assumption/constraint/risk type catalogue and independent lifecycle/review/verification/currency behavior, owned by H2/H3.
- `LFC-003..006`: non-linear work execution, milestones/dependencies/owners and actor/time/reason transition history, owned by H3; advanced configuration/designer and `LFC-009` remain R2.
- `API-01..07`: versioned OpenAPI, stable documented errors/IDs, bounded deterministic pagination, retry and concurrency contracts, owned by hardening plus each new slice.
- `INT-004`, `GOV-007`, `REV-006`: durable publication/audit must be infrastructure shared by foundation mutations, not postponed to R1 communication.
- `EXP-001/002/003/005/006`: native export/import and artifact manifest acceptance, owned by Slice 11.
- `RPT-001/002/003/006`, `APP-003`, `UI-*`, `OPS-001..004`: canonical reporting/views, evidence labels, secure UI/admin, observability and measured recovery, owned by Slice 12 with operations work starting during hardening.

Exit: every `M/MVP` requirement has passing evidence or an approved waiver; source-code exclusion and tenant isolation suites pass on every ingress and retrieval path (SRS §13.3).

### Phase 2 — R1 Commercial Pilot (the wedge)

Goal: sell agent-neutral, plan-locked coordination across customer-managed runtimes.

1. **Plan lock and `PlanBaseline`** — immutable snapshot of approved revisions, dependencies, constraints and success criteria; changes require an approved new baseline (`MCO-BR-01`, `MCO-BR-10`, AC-15).
2. **`CodebaseTarget` + repository adapter** — one initial code-host provider behind a provider-neutral adapter contract.
3. **`ExecutionCampaign`, `WorkPackage`, dependency graph** — plan-derived work units with scope, constraints and acceptance criteria.
4. **`AgentRuntimeInstance` registration and presence** — independent authentication per session (`MCO-BR-03`, `MCO-BR-11`).
5. **`ParticipationOffer` and capability-based assignment** — an offer is not a grant (`MCO-BR-04`).
6. **`TaskLease` and `ResourceReservation`** — time-bounded, fenced, one active write lease per exclusive scope (`MCO-BR-05`, `MCO-BR-06`). Start at the SRS default profile: 30-second heartbeat, 2-minute stale detection (SRS §16.10 recommendation).
7. **Workspace isolation references** — no shared-filesystem assumption (`MCO-BR-07`); workspace content stays external.
8. **Project MCP service and proposed A2A interoperability** — logical project-scoped MCP identity, stable endpoint, opaque resource URIs, tools for coordination commands, resources for state and event streams (`MCP-001`, `MCP-017`), filtered fan-out so no event leaks unauthorized content. After ADR adoption, add an A2A server/client gateway, authenticated Agent Card discovery with SSRF controls, native/bridged capability labels, and explicit `A2ATaskMapping` to canonical assignments (ARCH §§9-10/17.4). A2A completion never establishes work acceptance; A2A does not replace mandatory MCP or the project event journal.
9. **Durable coordination events, outbox and replay** — monotonic per-channel sequence, cursor-based replay, at-least-once with client deduplication (SRS §17.10). Notifications are best-effort signals and are never the only copy. Persistence, dispatch, recipient read, acknowledgement and acceptance are distinct states, so no false delivery/read/acceptance claim can be made (`MCP-035`).
10. **Integration queue and governed merge** — submitted, reviewed, tested, merged and verified remain separate states; agent self-report never establishes completion (`MCO-BR-09`). Protected branches move only through policy.
11. **Revocation and kill switch** — revoking a principal, runtime, credential or channel membership terminates active subscriptions within a measurable target (`MCP-034`). A control-plane kill switch suspends a runtime, agent principal, campaign, repository integration or provider connection, invalidating short-lived credentials and leases (`MCO-048`).
12. **Commercial basics and entitlement governance** — versioned plans, shared entitlement enforcement, idempotent attributable usage, usage visibility, quota actions and external model-spend separation (`COM-001` … `COM-007`), agency/client project isolation (`COM-016`), configurable versioned commercial catalogue (`COM-019`). Also: tenant export/exit without cancellation lock-in (`COM-020`), execution-mode and provider attribution (`COM-021`), a runtime-adapter contract separating capability, provider identity, location class, workspace provider and billing attributes (`COM-022`), tenant concurrency allowances and fairness controls (`COM-023`), an auditable record of entitlement and quota decisions (`COM-024`), and opt-out-by-default of cross-customer training (`COM-025`). Billing automation and managed runtime provisioning remain R2.
13. **Agency tenant with multiple client projects** — separation of knowledge, repositories, permissions and reports (`BUS-AC-01`).

Exit: SRS §16.10 end-to-end campaign scenario passes with multiple runtimes on separate devices; `NFR-041` … `NFR-047` measured and reported; basic commercial gates pass; no unresolved critical security finding. If A2A is adopted, also pass ARCH §23's two-distinct-agent collaboration, durable recovery and protected-integration scenarios. Confirm runner/agent identities are distinct, connections are outbound-authenticated, and execution provider operations use a versioned adapter contract.

### Architecture roadmap crosswalk

| Architecture engineering phase | This plan / controlling SRS release |
| --- | --- |
| P0 Architecture foundation | Phase 0 plus H0/H1/H2; approval and boundaries remain pending |
| P1 Knowledge/project/plan preparation | Phase 1 plus early R1 plan validation/lock work; does not require executing campaigns in MVP |
| P2 External coordination pilot | Phase 2 / R1 including proposed A2A, runners, durable messaging and integration |
| P3 Commercial agency release | R1 basic entitlements/usage and multi-client isolation; R2 billing/advanced portal/multiple providers |
| P4 Managed execution | Phase 3 / R2 |
| P5 Enterprise/intelligence | Phase 4 / R3/FUT |

Engineering preparation can move earlier, but cannot move mandatory SRS requirements later without an approved change or waiver.

### Phase 3 — R2 Hybrid Growth

Optional DevWeave-managed ephemeral execution, mixed-mode campaigns under one plan baseline, managed compute provisioning and metering, billing-provider integration, multiple code-host and agent-provider adapters, agency client portal, advanced scheduling, retention and admin controls. Also assess R2-tagged compatibility/temporal/traversal/cycle features (`SCH-004/005/006`, `REV-003/005`, `REL-006/008/009`, `SRH-004/007/008`) and configurable lifecycle/governance (`LFC-002/007/008/009`). Requirements keep their individual release tags even where later business summaries use broader wording.

Gate: managed execution must stay visibly optional, separately metered and isolated from the canonical knowledge store (`BUS-AC-05`), and the control plane must stay useful when a managed provider is degraded (SRS §18.7).

### Phase 4 — R3 / Future

Private-cloud and customer-hosted deployment profiles, enterprise identity federation, data residency and policy packs, advanced semantic retrieval, portfolio intelligence and specialized compliance capabilities. Broader impact intelligence can build on the R2 `REL-006` feature; the application registry's `APP-001/002/005` work is R2, while future extensibility follows `APP-004/006`. Subject to separate investment approval.

---

## 5. Working method

- **Test-first on domain rules.** Invariants in `MCO-BR-*`, revision immutability, lease fencing and tenant isolation are written as failing tests before implementation. These are the requirements most expensive to retrofit.
- **Vertical slices, not horizontal layers.** Each slice ships domain + tests + endpoint + contract + UI affordance, so progress is demonstrable rather than assumed.
- **Contracts before consumers.** Versioned OpenAPI/event schemas land in `DevWeave.Contracts` before the MCP tools and the studio bind to them.
- **Boundaries are tested, not documented.** `DevWeave.Architecture.Tests` grows with each module.
- **One requirement ID per test.** Traceability (SRS §14.1) only works if tests name the requirement and version they verify.

### Definition of done for any slice

1. Requirement ID(s) identified and linked.
2. Unit tests for the invariants; integration test for the host path.
3. Tenant isolation and source-code exclusion considered explicitly and proven or explicitly ruled out.
4. Migrations are forward-only and safe to roll out (expand-migrate-contract for destructive changes, SRS §12.1).
5. Observability: at minimum, a metric or log line that answers "is this working in production" (SRS §12.2).
6. Audit and provenance records emitted where the requirement demands attribution.

---

## 6. Verification strategy

| Suite | Purpose | Runs |
| --- | --- | --- |
| `DevWeave.Domain.Tests` | Invariants, business rules, state machines | Every commit |
| `DevWeave.Application.Tests` | Use cases against in-memory ports | Every commit |
| `DevWeave.Api.IntegrationTests` | Host boot, auth, endpoint behaviour, error contract | Every commit |
| `DevWeave.Architecture.Tests` | Dependency direction and module boundaries | Every commit |
| Tenant isolation suite | Negative disclosure across every read path | Every commit, release gate |
| Source-code exclusion suite | No code, diff, patch or code-bearing transcript on any ingress/retrieval path | Every commit, release gate |
| Coordination race/fencing suite | Stale lease rejection, duplicate suppression, concurrent writers | Every commit from Phase 2 |
| Load and fault injection | `NFR-041` … `NFR-047` under an agreed workload profile (OD-19) | Before any SLA claim |

Current API tests use unique databases on a pre-existing PostgreSQL server at localhost:5433; they are not Testcontainers-based. Object storage is currently replaced with an in-memory fake. H0 must make the database fixture/CI reproducible, and H2/H3 must add separate least-privilege-role, concurrency, migration and real S3/MinIO adapter tests. Do not claim object storage, RLS, OIDC, protocol conformance, load or recovery acceptance from the present 216-test suite.

Maintain a versioned requirement-level evidence register with SRS ID/version, implementation/API/schema, test/version/run, release and waiver fields (SRS §11.4/14.1). A green test count or this module table is not that register. Extend architecture tests to cover module ownership and SDK boundaries, not only the two current assembly dependency checks.

---

## 7. Immediate next actions

1. Complete H0 source-inclusion and CI database corrections; verify a clean-checkout build once a commit is authorized.
2. Complete H1 identity/project/resource policy, agent admission and protected replay tests; select OIDC provider through SRS:OD-01.
3. Complete H2 shared content exclusion, exact schema/state/history safety, least-privilege DB/RLS, durable audit/outbox and concurrent dedupe.
4. Complete H3 collision-safe artifact integrity, real storage tests and basic evidence/workflow semantics.
5. Assign ADR and requirement owners, establish the evidence register, then begin Slice 9 using the proven authorization/content-policy contracts.

No application changes, destructive database cleanup, commit/push, frontend migration or new protocol host is authorized by this planning reconciliation itself.

---

## 8. Risks this plan actively manages

| Risk | Mitigation in this plan |
| --- | --- |
| Knowledge model drifts into a generic document store (SRS R-01) | Reserved requirement families, typed relationships, schema registry before types multiply |
| Tenant customization fragments schemas and reporting (SRS R-02) | Type namespacing, schema review gate, common envelope, export tests |
| Code leaks into logs, embeddings or transcripts (SRS R-03) | H2 shared policy and exclusion suite on every ingress/retrieval/projection path; enum labels alone are insufficient |
| Search or reporting creates indirect cross-tenant disclosure (SRS R-04) | Authorize before retrieval; isolation suite as a release gate, not a spot check |
| Agent claims are read as verified facts (SRS R-05) | Explicit separation of submitted / reviewed / tested / merged / verified; evidence required for completion |
| Premature service extraction (SRS R-07) | Modular monolith with tested boundaries; extract only when scale or organisation demands it |

---

## 9. Open decisions that block work

These are SRS `OD-*` items that must either close or be consciously deferred with a named owner before the phase they block.

| # | Decision | Blocks | Recommended default if deferred |
| --- | --- | --- | --- |
| 1 | ~~.NET 10 LTS baseline~~ | — | **Closed 2026-11-09:** SDK 10.0.403 installed, all projects target `net10.0`, CI pinned to `10.0.x` |
| 2 | Identity provider for OIDC (`OD-01`) | Tenancy, agent principals | Stand up local OIDC in Phase 1, keep the provider behind an adapter |
| 3 | Retention, privacy and residency schedule (`OD-02`, `OD-03`) | Data model, export, deletion | Model retention as data from day one; defer specific periods |
| 4 | Evidence assurance levels per project profile (`OD-08`) | Governance, review gates | Implement two levels (self-reported, human-approved) and keep the profile versioned |
| 5 | Initial code-host provider (`OD-11`) | Phase 2 repository adapter | One provider behind a provider-neutral adapter contract |
| 6 | Workspace provisioning adapter set (`OD-13`) | Phase 2 workspace references | References only in R1; no provisioning dependency |
| 7 | Default lease and scheduling policy (`OD-14`) | Phase 2 leases | SRS §16.10 starting profile: 30s heartbeat, 2min stale detection, fencing tokens |
| 8 | Plan-lock authority roles (`OD-15`) | Phase 2 plan lock | Explicit approver role; locking is never implicit in an edit |
| 9 | Minimum MCP protocol version support (`SRS:OD-17`) | Phase 2 MCP service | Documents propose 2026-07-28; verify official semantics/SDK support, then pin and contract-test the adopted profile |
| 10 | Billable usage dimensions (`OD-21`) | Phase 2 metering | Record concurrent sessions, managed runtime units, storage and retention; bill later |
| 11 | BYOK and provider credential policy (`OD-25`) | Phase 2 secrets | Protected credential store, never in messages, payloads or logs |

Additional R2 design-freeze decisions are defined in SRS §17.17 as `MCP-OD-02` (default channel policy), `MCP-OD-03` (message retention), `MCP-OD-04` (presence policy) and `MCP-OD-05` (production workload). They do not block MVP/R1 but must be owned before R2 design freeze; this plan tracks them here so they are not lost.

The unqualified `OD-*` entries above refer to the SRS. Use `SRS:OD-*` in tickets and ADRs; the architecture document reuses the same numbers for different decisions.

### Architecture decision backlog (not approved)

| Decision | Proposed default / alternatives to assess | Owner role / gate |
| --- | --- | --- |
| Frontend ADR | Keep current React/Vite pending comparison with Next.js/BFF; no automatic rewrite | Frontend + architecture, before full Studio |
| Database/identity ADR | Verified request context, non-owner application role, RLS/composite constraints; privileged migration path | Security + backend, H1/H2 |
| ARCH:OD-01 Broker | NATS JetStream for R1; PostgreSQL outbox/journal remains durable truth | Platform architect, before R1 messaging |
| ARCH:OD-02 MCP compatibility | Verified SDK/profile matrix, isolated adapter | Interoperability lead, before R1 |
| ARCH:OD-03 A2A binding/adoption | Mediated A2A 1.0.0 HTTP+JSON/JSON-RPC recommended; verify upstream and approve product/design scope | Product + architecture + interoperability + security, before R1 |
| ARCH:OD-04 Runner transport | Outbound authenticated HTTPS streaming/WebSocket or gRPC, network/recovery spike | Runtime lead, before R1 |
| ARCH:OD-05 Isolation | Least-privilege containers vs microVMs based on workload trust | Security + platform, before R2 |
| ARCH:OD-06 Providers | Provider-neutral GitHub-first contract; GitLab based on release demand | Integration lead, R1/R2 |
| ARCH:OD-07 Plan changes | Freeze affected work; approved new baseline or versioned amendment | Product owner, before R1 |
| ARCH:OD-08 Retention | Configurable bounded communication/quarantine retention and audited legal exceptions | Security/legal, before pilot |
| ARCH:OD-09 Peer networking | Mediated gateway; direct A2A disabled unless explicit enterprise policy | Security, before GA |
| ARCH:OD-10 Semantic retrieval | PostgreSQL FTS first; vectors only after permission-safe evaluation | Knowledge lead, later release |
| ARCH:OD-11 Billing | Provider-neutral usage ledger; market-specific integration | Product/finance, R2 billing |
| ARCH:OD-12 Extraction | Separate hosts only for measured scaling/isolation/release needs | Architecture, continuous |

Prices, tier names and revenue targets are explicitly out of scope for this plan; they live in a separate commercial catalogue (SRS §18.4, §18.8).
