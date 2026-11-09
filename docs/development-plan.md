# DevWeave Development Plan

Baseline: `DWV-SRS-001` v1.5 (see [docs/srs/](srs/)), status *business-aligned baseline; formal product-owner and engineering approval pending*.

This plan sequences implementation work against that baseline. It is a plan, not evidence that any capability has been built. Requirement priority codes (`M`/`S`/`C`) and release tags (`MVP`, `R1`, `R2`, `R3`) come from the SRS and govern ordering; where this plan and the SRS disagree, the SRS wins.

---

## 1. Architecture decisions already fixed

| Decision | Choice | Source |
| --- | --- | --- |
| Deployment shape | Modular monolith with explicit module boundaries, plus independently scalable background workers | SRS ASM-07, §3.1 |
| Plane separation | Knowledge Plane and Coordination/Control Plane must not depend on the Execution Plane | SRS §2.1, §18.3 |
| Source-code custody | External code hosts stay authoritative. No source files, diffs, patches or code-bearing transcripts in the knowledge store, indexes, events, analytics or audit | SRS §1.3.2, §1.5, §16.1, MCO-BR-14 |
| Agent coordination protocol | Project-scoped MCP service is mandatory for real-time agent-to-agent coordination; REST/OpenAPI stays the protocol-neutral surface | SRS §17.1, §17.11 |
| Domain purity | The domain model must not depend on .NET, PostgreSQL, JSON Schema, OpenAPI or MCP types; adapters isolate them | SRS §3.3 |
| Data baseline | PostgreSQL, JSONB payloads behind a typed queryable envelope; object storage with checksums; PostgreSQL full-text before any vector search | SRS §3.1, §7.4 |
| Frontend | React + Vite + TypeScript + Tailwind (`web/studio`) | Project decision |
| Target framework | `net10.0` LTS, owned by `Directory.Build.props` so project files do not repeat it | SRS §3.3 |

The target framework started at `net9.0` during scaffolding, because only SDK 9.0.200 was installed at the time. It moved to `net10.0` once SDK 10.0.403 was available; §9 records that decision as closed.

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

Each row is one module. Modules own their aggregates, their schema, and their endpoint surface. Cross-module communication goes through domain events and the outbox, never through direct table joins.

| Module | Owning project(s) | SRS requirement families | Release |
| --- | --- | --- | --- |
| Identity & Tenancy | Domain/Identity, Application/Identity, Infrastructure | `TEN-*` | MVP |
| Projects & Lifecycle Profiles | Domain/Projects, Domain/Lifecycle, Application/Projects | `PRJ-*`, `LFC-*` | MVP |
| Knowledge Engine | Domain/Knowledge, Application/Knowledge | `KNW-*`, `REV-*`, `SCH-*` | MVP |
| Relationships & Provenance | Domain/Knowledge, Domain/Common | `REL-*`, `PRO-*`, `BR-006` | MVP |
| Artifacts & Evidence | Domain/Knowledge, Infrastructure/Storage | `ART-*`, `EVI-*` | MVP |
| Search & Context Assembly | Application/Knowledge, Infrastructure/Search | `SRH-*`, `AGT-*` | MVP |
| Agent Principals & Context Packages | Domain/Identity, Application/Knowledge | `AGT-*`, `AGI-06`, `AGI-07` | MVP |
| Integrations & Events | Infrastructure/Integration, Infrastructure/Messaging | `INT-*`, `DATA-06`, `DATA-07` | MVP |
| Governance & Review | Domain/Lifecycle, Application | `LFC-009`, `AC-*` | MVP → R1 |
| Coordination (campaigns, leases, integration queue) | Domain/Coordination, Application/Coordination | `MCO-001` … `MCO-046`, `MCO-BR-01` … `MCO-BR-14` | R1 |
| Project MCP Service | DevWeave.Mcp | `MCP-001` … `MCP-034` | R1 |
| Commercial (plans, entitlements, metering) | Domain/Commercial, Application/Commercial | `COM-001` … `COM-019` | R1 → R2 |
| Managed Execution | Infrastructure/Integration | §18.3, `NFR-*` | R2 |

---

## 4. Phases

### Phase 0 — Foundation (current)

Goal: a repository that builds, tests and deploys a no-op control plane, with the boundaries and guardrails that the rest of the work depends on.

- [x] Git repository + remote, monorepo layout, solution and module skeleton
- [x] Local infrastructure manifest (PostgreSQL, object storage)
- [x] CI: restore, build, test, frontend build
- [x] Architecture boundary tests
- [x] Adopt the .NET 10 LTS baseline (SDK 10.0.403) as the single target framework for all projects
- [ ] Confirm local infrastructure boots (`docker compose up -d`) and the API health probe passes through the studio proxy
- [ ] Publish the ADR set: plane separation, code-exclusion enforcement point, envelope schema, outbox/event contract

Exit: `dotnet test` green, `npm run build` green, ADRs merged, all `OD-*` items that block MVP scoped and owned.

### Phase 1 — Foundation MVP

Goal: the canonical knowledge model with tenant isolation, provenance and revision safety. No sellable coordination yet.

Ordered slices, each one vertical (domain → tests → endpoint → contract):

1. **Tenant and principal** — tenant lifecycle, principals, memberships and grants (`TEN-001`). Every tenant-owned row carries tenant scope directly or through an integrity-enforced parent (SRS §7.2). Include negative disclosure tests: a query in tenant A must never return tenant B data, on any path.
2. **Project** — project creation, ownership by exactly one tenant, lifecycle profile pinning (`PRJ-001`).
3. **Knowledge item + revision** — stable item identity, immutable revisions, current-revision pointer, schema-version pinning per revision (`KNW-*`, `REV-005`, `BR-006`). Silent overwrite is a defect, not a design choice.
4. **Schema registry** — registered types, published versions immutable, compatibility metadata (`SCH-008`).
5. **Relationships** — typed edges validated against the relation registry; supersede or revoke without erasing history.
6. **Provenance and activities** — append-only events, actor attribution, W3C PROV-O-compatible export mapping.
7. **Artifacts and evidence** — object storage with checksum and immutable version identity (`ART-003`), evidence metadata kept distinct from claims.
8. **Agent principal and context package** — revision-pinned, authorization-before-retrieval context assembly (`AGT-012`, SRS §3.2). Build the source-code exclusion test here, not later.
9. **Search** — PostgreSQL full-text over authorized metadata and payload projections only (`SRH-001`).
10. **Integrations and event ingress** — connector registry, webhook signature verification, deduplication by provider plus external event identity (`INT-001`, `DATA-07`).
11. **Audit and export** — machine-readable export including schemas, revisions, relationships and provenance (SRS §12.4).
12. **Owner Studio (read/publish only)** — project and knowledge browsing in `web/studio`.

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
8. **Project MCP service** — logical project-scoped identity, stable endpoint, opaque resource URIs, tools for coordination commands, resources for state and event streams (`MCP-001`, `MCP-017`), filtered fan-out so no event leaks unauthorized content.
9. **Durable coordination events, outbox and replay** — monotonic per-channel sequence, cursor-based replay, at-least-once with client deduplication (SRS §17.10). Notifications are best-effort signals and are never the only copy.
10. **Integration queue and governed merge** — submitted, reviewed, tested, merged and verified remain separate states; agent self-report never establishes completion (`MCO-BR-09`). Protected branches move only through policy.
11. **Revocation** — revoking a principal, runtime, credential or channel membership terminates active subscriptions within a measurable target (`MCP-034`).
12. **Commercial basics** — versioned plans, entitlement evaluation in a shared service (not UI-only), usage records attributable to tenant/project/campaign/mode and idempotent (`COM-001` … `COM-004`).
13. **Agency tenant with multiple client projects** — separation of knowledge, repositories, permissions and reports (`BUS-AC-01`).

Exit: SRS §16.10 end-to-end campaign scenario passes with multiple runtimes on separate devices; `NFR-041` … `NFR-047` measured and reported; no unresolved critical security finding.

### Phase 3 — R2 Hybrid Growth

Optional DevWeave-managed ephemeral execution, mixed-mode campaigns under one plan baseline, managed compute provisioning and metering, billing-provider integration, multiple code-host and agent-provider adapters, agency client portal, advanced scheduling, retention and admin controls.

Gate: managed execution must stay visibly optional, separately metered and isolated from the canonical knowledge store (`BUS-AC-05`), and the control plane must stay useful when a managed provider is degraded (SRS §18.7).

### Phase 4 — R3 / Future

Private-cloud and customer-hosted deployment profiles, enterprise identity federation, data residency and policy packs, advanced semantic retrieval, impact analysis, portfolio intelligence, application catalogue and specialized compliance capabilities. Subject to separate investment approval.

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

Database tests that need real PostgreSQL concurrency use Testcontainers rather than an in-memory provider, so isolation and locking behaviour is exercised for real.

---

## 7. Immediate next actions

1. Boot local infrastructure and confirm the studio can read the API health probe end to end.
2. Publish the ADR set (§4, Phase 0).
3. Close, or explicitly defer with an owner, the MVP-blocking open decisions in §9.
4. Start Phase 1 slice 1 (tenant and principal) with the negative disclosure test written first.

---

## 8. Risks this plan actively manages

| Risk | Mitigation in this plan |
| --- | --- |
| Knowledge model drifts into a generic document store (SRS R-01) | Reserved requirement families, typed relationships, schema registry before types multiply |
| Tenant customization fragments schemas and reporting (SRS R-02) | Type namespacing, schema review gate, common envelope, export tests |
| Code leaks into logs, embeddings or transcripts (SRS R-03) | Dedicated exclusion suite from Phase 1 slice 8 onward; ingress allowlist plus retrieval inspection |
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
| 9 | Minimum MCP protocol version support (`OD-17`) | Phase 2 MCP service | Pin the 2026-07-28 profile, add adapters only when a real client requires it |
| 10 | Billable usage dimensions (`OD-21`) | Phase 2 metering | Record concurrent sessions, managed runtime units, storage and retention; bill later |
| 11 | BYOK and provider credential policy (`OD-25`) | Phase 2 secrets | Protected credential store, never in messages, payloads or logs |

Prices, tier names and revenue targets are explicitly out of scope for this plan; they live in a separate commercial catalogue (SRS §18.4, §18.8).
