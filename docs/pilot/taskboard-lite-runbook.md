# TaskBoard Lite pilot runbook

Real-case pilot: plan and implement through DevWeave Owner Studio + two agent runtimes against an external sandbox repo.

## Stack

| Component | URL / path |
| --- | --- |
| Postgres / MinIO | `docker compose up -d` |
| API | `http://localhost:5080` |
| Owner Studio | `http://localhost:5174` |
| Sandbox repo | `C:\Prospectbd Software\business projects\taskboard-lite` (local git; provider id `devweave-pilot/taskboard-lite`) |

GitHub CLI was not authenticated in this environment. The sandbox is a real git repo with commits/branches; DevWeave stores `github:` refs only. Push to GitHub when ready: `gh auth login` then create `your-org/taskboard-lite` and set `RepositoryId` in bootstrap.

## Phase 0 — Auth + smoke

1. `docker compose up -d`
2. `dotnet ef database update --project src/DevWeave.Infrastructure --startup-project src/DevWeave.Api`
3. Run API (`ASPNETCORE_ENVIRONMENT=Development`, port 5080)
4. Mint human token:

```powershell
Invoke-RestMethod http://localhost:5080/auth/pilot/bootstrap -Method Post -ContentType application/json `
  -Body '{"tenantName":"Pilot Agency","ownerEmail":"owner@pilot.devweave.local"}'
```

5. `GET /auth/whoami` with `Authorization: Bearer <token>`
6. Start Studio; write `web/studio/.env.local` with `VITE_TENANT_ID` + `VITE_API_TOKEN` (bootstrap script does this)

## Phase 1 — Baseline

```powershell
pwsh -File scripts/pilot/bootstrap.ps1
```

Creates tenant owner; publishes schemas `plan.goal`, `plan.module`, `plan.requirement`, `plan.constraint`, `design.decision@1.1`; registers `supports`; seeds TaskBoard Lite **plan v1** (outline `K-01→K-API→{K-02,K-03,K-04}` plus `K-05`, semantic `K-04 supports K-03`, milestone M-01, work W-01/W-02, revision-pinned Plan Work links, baseline + campaign); then **evolves to plan v2**: add `K-AUTH`/`K-06`/`K-07`, move `K-05` under `K-API`, detach `K-DRAFT`, revise `K-03`, add W-03 + links, lock baseline v2 (suspends the v1 campaign), and activate a new campaign with packages for W-01/W-02/W-03. Writes `docs/pilot/session.json` (includes both baseline IDs and drift captured before v2).

Canonical lock: `github:devweave-pilot/taskboard-lite@main#<baseSha>`

## Phase 2 — Two agents

```powershell
pwsh -File scripts/pilot/run-two-agents.ps1
```

- Runtime A: lease TB-1, MCP status, implement create-task on `agent-a/tb1-create-task`, change-set `github:pr/1` + commit SHA, evidence
- Runtime B: conflict on TB-1 scope (409), MCP cursor replay, lease TB-2, implement list-tasks, change-set `github:pr/2`, evidence
- Owner: approve/verify knowledge, report work items complete, integration-queue decide, project report

Result: `docs/pilot/two-agents-result.json`

## Working sample (aligned to Plan design)

The sandbox at `taskboard-lite` implements the governed design (not in-memory):

| Work | Branch / ref | Acceptance |
| --- | --- | --- |
| W-01 | `feature/w01-create-task` · `github:pr/101` · `08ce390…` | Empty title → 400; valid title → 201 with `owner_id` |
| W-02 | `feature/w02-list-tasks` · `github:pr/102` · `7bf08d8…` | `GET /tasks` returns only that owner’s rows |

```powershell
$env:TASKBOARD_CONNECTION = "Host=localhost;Port=5433;Database=taskboard_lite;Username=devweave;Password=change-me-locally"
# CREATE DATABASE taskboard_lite;  -- once
dotnet run --project "..\taskboard-lite" --urls http://localhost:5090
```

Smoke: `POST/GET /tasks` with `X-Owner-Id` header. After bootstrap, Plan outline/map show **8** outline nodes (`K-DRAFT` unplaced), **2** `supports` relations, and **6** Plan Work links (including the historical K-03 r1 link). Coordination lists baselines **v1** and **v2**; the active campaign is on v2.

## Phase 3 — Adversarial

```powershell
pwsh -File scripts/pilot/adversarial-checks.ps1
```

## Studio

Open `http://127.0.0.1:5174` after `.env.local` is written. Overview / Knowledge / Work / Coordination / Evidence / Activity should load live data (no demo fixtures).

## Key IDs (latest successful run)

See `docs/pilot/session.json` and `docs/pilot/two-agents-result.json` (session contains secrets — do not commit).
