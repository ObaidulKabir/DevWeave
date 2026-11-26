# Production OIDC cutover (TEN-007)

Foundation MVP requires verified human identity via OIDC. Development pilot JWTs
(`/auth/pilot/*` and registration `accessToken`) must not remain the production path.

## Target configuration

Set on the API host (compose env / secret store):

```bash
ASPNETCORE_ENVIRONMENT=Production
Auth__Oidc__Authority=https://<your-idp>/
Auth__Oidc__Audience=devweave-api
Auth__Oidc__RequireHttpsMetadata=true
Auth__SigningKey=<production-secret-at-least-32-chars>
OIDC__AUTHORITY=https://<your-idp>/
OIDC__AUDIENCE=devweave-api
```

The IdP must emit DevWeave claims on access tokens:

- `tenant_id` — UUID of the DevWeave tenant
- `principal_id` — UUID of the human principal already provisioned in that tenant
- `principal_type` — exactly `Human`

## Cutover steps

1. Provision the IdP application and claim mapping.
2. Register a tenant via `POST /auth/register` (creates Owner; response `accessToken` is null when OIDC is configured).
3. Map the Owner's `principal_id` / `tenant_id` into the IdP user.
4. Deploy with the Production env vars above; confirm `/auth/pilot/bootstrap` returns **404**.
5. Confirm Studio register shows “Tenant created. Sign in…” and login uses the IdP (not pilot GUID form) once Studio OIDC is wired; until then operators may exchange IdP tokens against the API directly.
6. Update the MVP evidence register: mark `TEN-007` as `pass` with this runbook + a recorded smoke test.

## Checks

2026-11-26, host `https://dev-weave.prospectbdsoftware.com`:

- `GET /health/ready` returned 200 `{"status":"ready"}`.
- `GET /auth/login-options` returned `pilotEnabled: true` and `oidcAuthority: null`.
- `POST /auth/pilot/bootstrap` with a one-character tenant name returned 400, so the pilot route is mapped.

Cutover steps 1–5 are not complete on this host. The MVP checklist records a Product waiver through 2026-12-11 in `docs/mvp-evidence-register.md`. That waiver is not a passed smoke.

## Rollback

Restore `ASPNETCORE_ENVIRONMENT=Development` and empty `Auth__Oidc__Authority` only for emergency pilot recovery. Re-open a time-bounded waiver in `docs/mvp-evidence-register.md`.

## Related

- [docs/authentication.md](../authentication.md)
- [docs/mvp-evidence-register.md](../mvp-evidence-register.md)
