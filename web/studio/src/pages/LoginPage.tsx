import { FormEvent, useMemo, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { getEnvTenantId, getLastIdentity } from '../lib/session';

const GUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

function resolveOidcAuthorizeUrl(): string | null {
  const authority = (import.meta.env.VITE_OIDC_AUTHORITY as string | undefined)?.trim();
  if (!authority) return null;
  const clientId = (import.meta.env.VITE_OIDC_CLIENT_ID as string | undefined)?.trim() || 'devweave-studio';
  const redirectUri =
    (import.meta.env.VITE_OIDC_REDIRECT_URI as string | undefined)?.trim() ||
    `${window.location.origin}/login`;
  const audience = (import.meta.env.VITE_OIDC_AUDIENCE as string | undefined)?.trim() || 'devweave-api';
  const base = authority.endsWith('/') ? authority : `${authority}/`;
  const url = new URL('authorize', base);
  url.searchParams.set('client_id', clientId);
  url.searchParams.set('response_type', 'code');
  url.searchParams.set('scope', 'openid profile');
  url.searchParams.set('redirect_uri', redirectUri);
  url.searchParams.set('audience', audience);
  return url.toString();
}

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from;
  const destination = from && from !== '/login' && from !== '/' ? from : '/apps';

  const oidcAuthorizeUrl = useMemo(() => resolveOidcAuthorizeUrl(), []);
  const last = getLastIdentity();
  const [tenantId, setTenantId] = useState(() => last?.tenantId ?? getEnvTenantId() ?? '');
  const [principalId, setPrincipalId] = useState(() => last?.principalId ?? '');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

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
          <Link to="/" className="font-display text-3xl font-bold tracking-tight text-[var(--ink)] hover:opacity-90">
            DevWeave
          </Link>
          <p className="mt-1 text-[10px] font-semibold uppercase tracking-[0.2em] text-[var(--signal)]">
            Tenant login
          </p>
          <h1 className="mt-6 font-display text-2xl font-bold tracking-tight">Sign in</h1>
          <p className="mt-2 text-sm leading-relaxed text-[var(--muted)]">
            {oidcAuthorizeUrl
              ? 'Production sign-in uses your identity provider. After registering a tenant, map the Owner principal in the IdP and continue here.'
              : 'Development sign-in uses your tenant and Owner principal (pilot JWT). Set VITE_OIDC_AUTHORITY to enable IdP sign-in.'}
            {last?.email || last?.tenantName ? (
              <>
                {' '}
                Last session
                {last.tenantName ? (
                  <>
                    {' '}
                    for <span className="font-semibold text-[var(--ink)]">{last.tenantName}</span>
                  </>
                ) : null}
                {last.email ? (
                  <>
                    {' '}
                    (<span className="font-mono text-xs">{last.email}</span>)
                  </>
                ) : null}
                .
              </>
            ) : null}
          </p>
        </div>

        {oidcAuthorizeUrl ? (
          <div className="mt-8 space-y-4">
            <a className="btn btn-primary w-full" href={oidcAuthorizeUrl}>
              Sign in with identity provider
            </a>
            <p className="text-center text-xs text-[var(--muted)]">
              Pilot GUID form remains available below for emergency break-glass only.
            </p>
          </div>
        ) : null}

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
            {busy ? 'Signing in…' : oidcAuthorizeUrl ? 'Break-glass pilot sign-in' : 'Sign in to dashboard'}
          </button>
        </form>

        <p className="mt-6 text-center text-sm text-[var(--muted)]">
          New here?{' '}
          <Link className="font-semibold text-[var(--signal)] hover:underline" to="/register">
            Register a tenant
          </Link>
          {' · '}
          <Link className="font-semibold text-[var(--signal)] hover:underline" to="/">
            Landing
          </Link>
        </p>
      </div>
    </div>
  );
}
