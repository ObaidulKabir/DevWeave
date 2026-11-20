# DevWeave operations runbook (OPS-001..004)

## Health probes

| Probe | Path | Expectation |
| --- | --- | --- |
| Liveness | `GET /health/live` | 200 `{ "status": "healthy" }` — process up |
| Readiness | `GET /health/ready` | 200 `{ "status": "ready" }` — database reachable |

Studio nginx proxies `/health/*` to the API. CI deploy waits for readiness via
`docker compose exec studio wget http://api:8080/health/ready`.

## Dependencies

| Dependency | Compose service | Notes |
| --- | --- | --- |
| PostgreSQL | `postgres` | Production app CS **must** use `Options=-c role=devweave_app` (not table-owner). Migrations use `ConnectionStrings__DevWeaveMigrate`. |
| Object storage | `object-storage` | Default `cgr.dev/chainguard/minio`; override with `OBJECT_STORAGE_IMAGE` |

API/workers depend on object-storage **started**, not healthy, so storage license issues do not block registration.

## Structured logging

API logs (stdout) include:

- Host start / migrate-on-startup EF messages
- Registration persistence failures (`DevWeave.Api.Registration`)
- ASP.NET request failures via the exception handler

Ask: “Did registration succeed?” → search for `Tenant registration failed` or EF `INSERT INTO tenants`.

## Backup and restore drill

Record date, operator, measured RTO/RPO in the MVP evidence register under `OPS-004`.

### PostgreSQL

```bash
# Backup
docker compose exec -T postgres pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc > "devweave-$(date +%Y%m%d).dump"

# Restore (maintenance window)
docker compose stop api workers studio
docker compose exec -T postgres pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists < "devweave-YYYYMMDD.dump"
docker compose --profile app up -d
curl -fsS "http://127.0.0.1:${API_PORT:-5080}/health/ready"
```

### Object storage

Copy the `devweave-object-storage-data` volume (or provider bucket) with the same timestamp as the DB dump. Restore both together; artifact checksums in export manifests must match stored objects.

### Acceptance

- Restore into a scratch database/volume, then `GET /health/ready` and `POST /auth/register` succeed.
- Write measured restore duration into `docs/ops/backup-restore-drill.md` and flip `OPS-004` in the evidence register.

## Migrations

`Database__MigrateOnStartup=true` (compose default) applies EF migrations using
`ConnectionStrings__DevWeaveMigrate` when set, otherwise `ConnectionStrings__DevWeave`.
Keep the migrate connection privileged enough to own schema; keep the app connection on
`devweave_app`.
