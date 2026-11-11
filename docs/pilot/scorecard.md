# TaskBoard Lite pilot scorecard

**Date:** 2026-11-11  
**Decision rule:** Criteria 1–4 pass ⇒ control plane ready for a second customer-like pilot.

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | **Plan** — Milestone + work items; design knowledge with revision, approval, evidence-backed verification in Studio | **Pass** | `M1-MVP`, `TB-1`/`TB-2` (TB-2 depends on TB-1); knowledge `tb-design` revision approved + verified; claims/evidence accepted (`docs/pilot/session.json`, `two-agents-result.json`) |
| 2 | **Coordinate** — Campaign locked to `owner/name@branch#sha`; two runtimes; exclusive leases | **Pass** | Target `github:devweave-pilot/taskboard-lite@main#4b25ba9…`; runtimes A/B registered; B lease on TB-1 scope → **409 Conflict** |
| 3 | **Implement** — Each agent lands commit/PR refs; DevWeave stores refs only | **Pass** | A: `github:pr/1` @ `d92e1f0…` paths `src/api/CreateTask.cs`; B: `github:pr/2` @ `17392d0…` paths `src/api/ListTasks.cs`; no source blobs in change-sets |
| 4 | **Monitor** — Activity, evidence, metrics/report, campaign/MCP visible without fixtures | **Pass** | Activities + evidence recorded; project report id `dfd7081f-…`; Studio live via `VITE_TENANT_ID`/`VITE_API_TOKEN`; coordination campaigns/runtimes/MCP directory readable |
| 5 | **Recover** — Kill one session; other replays MCP cursor and continues | **Pass** | After A status message, B `read_messages_after_cursor` recovered “TB-1 create-task branch ready” then leased TB-2 |

## Adversarial checks

| Check | Expected | Result |
| --- | --- | --- |
| Source-like knowledge revision (`diff --git` / secret marker) | Rejected | **Pass** — HTTP 422 `prohibited-content` |
| Agent without project membership | 403/404, no leak | **Pass** — HTTP 403 on work-items |
| Mutation without token | Fail | **Pass** — HTTP 401 |
| Duplicate lease same scope | Conflict | **Pass** — confirmed in two-agent run (409) |
| Control-plane payloads | No `diff --git` / source files | **Pass** |

## Regression gates

- `PlanDocumentMonitorAcceptanceTests` — **Passed**
- `TwoRuntimeCoordinationAcceptanceTests` — **Passed**

## Friction log

- Development human JWT mint required (`/auth/pilot/*`) because CreatePrincipal/lifecycle need auth while OIDC authority is empty
- `CreateProject` must grant creator Owner membership or RequireTenantMembership blocks first grant (fixed in this pilot)
- `gh` not logged in — sandbox is local git with GitHub-shaped refs; push/PR URLs deferred until `gh auth login`
- Studio requires restart after writing `.env.local`
- Agent credential secrets land in `docs/pilot/session.json` (keep out of git)

## Decision

**Criteria 1–4 Pass.** Control plane is good enough for a second real customer-like pilot. Next: wire a real GitHub remote/PRs and close remaining Studio UX friction; coordination/Git-ref path is solid.
