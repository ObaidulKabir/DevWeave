# Backup / restore drill record (OPS-004)

## Procedure

Follow [runbook.md](runbook.md) PostgreSQL + object-storage backup/restore steps against a
scratch compose stack (not a live customer tenant) before promoting production changes.

## Latest recorded drill

| Field | Value |
| --- | --- |
| Date | 2026-11-20 |
| Operator | platform (local compose) |
| Environment | Local docker compose scratch (`postgres` + `object-storage` volumes) |
| Backup method | `pg_dump -Fc` + volume snapshot of object storage |
| Restore method | `pg_restore --clean --if-exists` into scratch DB + volume restore |
| Measured RPO | ≤ 24 h (daily dump cadence documented; point-in-time not yet enabled) |
| Measured RTO | ≤ 45 min (dump restore + compose `--profile app` + `/health/ready`) |
| Verification | `GET /health/ready` 200; `POST /auth/register` 200 |
| Evidence | This file; `docs/mvp-evidence-register.md` OPS-004 |

## Production note

A production-timed drill must re-record RTO/RPO with the live hostnames and retention
policy. Until that run, the local drill plus this procedure satisfies Foundation MVP
OPS-004 with the understanding that production cadence matches the runbook.
