# Online Cake Shop pilot runbook

Second real-case example for exercising DevWeave planning, versioning, and coordination against an external sandbox.

## Stack

| Component | URL / path |
| --- | --- |
| Postgres / MinIO | `docker compose up -d` (shared with TaskBoard Lite) |
| API | `http://localhost:5080` |
| Owner Studio | `http://localhost:5174` |
| Sandbox repo | `C:\Prospectbd Software\business projects\online-cake-shop` (provider id `devweave-pilot/online-cake-shop`) |

## Phase 1 — Seed the plan

```powershell
pwsh -File scripts/pilot/bootstrap-online-cake-shop.ps1 -WriteStudioEnv
```

Reuses the existing pilot tenant when `docs/pilot/session.json` is present (TaskBoard Lite); otherwise creates a new tenant. Seeds **Online Cake Shop** as a second project with:

- Outline v1 → evolve (move `K-05`, detach `K-DRAFT`, add `K-AUTH`/`K-06`/`K-07`, revise `K-03`) → lock **plan v2**
- Scoped work packages (`implementsRevisionIds` + `workItemIds`)
- Session artifact: `docs/pilot/session-online-cake-shop.json`

In Studio, select project **Online Cake Shop**. Open Plan map, Plan outline, Plan versions, and Coordination.

## Phase 2 — Sandbox smoke

```powershell
docker exec -i devweave-postgres psql -U devweave -d postgres -c "CREATE DATABASE online_cake_shop;"
$env:CAKESHOP_CONNECTION = "Host=localhost;Port=5433;Database=online_cake_shop;Username=devweave;Password=change-me-locally"
dotnet run --project "C:\Prospectbd Software\business projects\online-cake-shop" --urls http://localhost:5091
```

`POST/GET /orders` with `X-Customer-Id`. Expected outline after bootstrap: **9** nodes (`K-DRAFT` unplaced), **2** `supports` relations, **6** Plan Work links, baselines **v1** + **v2**.
