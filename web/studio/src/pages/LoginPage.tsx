import { FormEvent, useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { getEnvTenantId } from '../lib/session';

const GUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

export function LoginPage() {
  const { authenticated, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from;
  const destination = from && from !== '/login' ? from : '/access';

  const [tenantId, setTenantId] = useState(() => getEnvTenantId() ?? '');
  const [principalId, setPrincipalId] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  if (authenticated) {
    return <Navigate to={destination} replace />;
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);

    const tenant = tenantId.trim();
    const principal = principalId.trim();
    if (!GUID_RE.test(tenant) || !GUID_RE.test(principal)) {
      setError('Enter valid tenant and owner principal UUIDs.');
      return;
    }

    setBusy(true);
    try {
      await login(tenant, principal);
      navigate(destination, { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Login failed.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-page flex min-h-screen items-center justify-center px-4 py-10">
      <div className="login-panel w-full max-w-md">
        <div className="px-1">
          <p className="font-display text-3xl font-bold tracking-tight text-[var(--ink)]">DevWeave</p>
          <p className="mt-1 text-[10px] font-semibold uppercase tracking-[0.2em] text-[var(--signal)]">
            Owner Studio
          </p>
          <h1 className="mt-6 font-display text-2xl font-bold tracking-tight">Sign in</h1>
          <p className="mt-2 text-sm leading-relaxed text-[var(--muted)]">
            Pilot Owner login mints a short-lived human token for your tenant. Production OIDC is not
            required for this path.
          </p>
        </div>

        <form className="mt-8 space-y-4" onSubmit={onSubmit} noValidate>
          <label className="block space-y-1.5">
            <span className="text-xs font-semibold uppercase tracking-[0.14em] text-[var(--muted)]">
              Tenant ID
            </span>
            <input
              className="field font-mono text-sm"
              name="tenantId"
              autoComplete="off"
              spellCheck={false}
              placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
              value={tenantId}
              onChange={(event) => setTenantId(event.target.value)}
              disabled={busy}
              required
            />
          </label>

          <label className="block space-y-1.5">
            <span className="text-xs font-semibold uppercase tracking-[0.14em] text-[var(--muted)]">
              Owner principal ID
            </span>
            <input
              className="field font-mono text-sm"
              name="principalId"
              autoComplete="off"
              spellCheck={false}
              placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
              value={principalId}
              onChange={(event) => setPrincipalId(event.target.value)}
              disabled={busy}
              required
            />
          </label>

          {error ? (
            <div role="alert" className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-800">
              {error}
            </div>
          ) : null}

          <button type="submit" className="btn btn-primary w-full" disabled={busy}>
            {busy ? 'Signing in…' : 'Sign in'}
          </button>
        </form>

        <p className="mt-6 text-xs leading-relaxed text-[var(--muted)]">
          Use the tenant and owner IDs from pilot bootstrap (
          <code className="font-mono text-[var(--ink)]">docs/pilot/session.json</code>
          {' '}or{' '}
          <code className="font-mono text-[var(--ink)]">POST /auth/pilot/bootstrap</code>
          ). Tokens stay in local browser storage and are never shown here.
        </p>
      </div>
    </div>
  );
}
