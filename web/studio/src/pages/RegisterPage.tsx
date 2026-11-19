import { FormEvent, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

export function RegisterPage() {
  const { register } = useAuth();
  const navigate = useNavigate();

  const [tenantName, setTenantName] = useState('');
  const [ownerEmail, setOwnerEmail] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [created, setCreated] = useState(false);
  const [busy, setBusy] = useState(false);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);

    const name = tenantName.trim();
    const email = ownerEmail.trim();
    if (name.length < 2) {
      setError('Enter a tenant / organization name (at least 2 characters).');
      return;
    }
    if (!email.includes('@')) {
      setError('Enter a valid Owner email.');
      return;
    }

    setBusy(true);
    try {
      const outcome = await register(name, email);
      if (outcome === 'signed-in') {
        navigate('/apps', { replace: true });
        return;
      }
      setCreated(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Registration failed.');
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
            Register tenant
          </p>
          <h1 className="mt-6 font-display text-2xl font-bold tracking-tight">Create your tenant</h1>
          <p className="mt-2 text-sm leading-relaxed text-[var(--muted)]">
            Creates the tenant and an active Owner so you can open the dashboard. No confirmation email is sent.
          </p>
        </div>

        {created ? (
          <div className="mt-8 space-y-4">
            <div role="status" className="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-900">
              Tenant created. Sign in with the owner identity to continue.
            </div>
            <Link to="/login" className="btn btn-primary w-full">
              Sign in
            </Link>
          </div>
        ) : (
          <form className="mt-8 space-y-4" onSubmit={onSubmit} noValidate>
            <label className="block space-y-1.5">
              <span className="text-xs font-semibold uppercase tracking-[0.14em] text-[var(--muted)]">
                Tenant name
              </span>
              <input
                className="field text-sm"
                name="tenantName"
                autoComplete="organization"
                placeholder="Acme Delivery"
                value={tenantName}
                onChange={(event) => setTenantName(event.target.value)}
                disabled={busy}
                required
              />
            </label>

            <label className="block space-y-1.5">
              <span className="text-xs font-semibold uppercase tracking-[0.14em] text-[var(--muted)]">
                Owner email
              </span>
              <input
                className="field text-sm"
                type="email"
                name="ownerEmail"
                autoComplete="email"
                placeholder="owner@acme.example"
                value={ownerEmail}
                onChange={(event) => setOwnerEmail(event.target.value)}
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
              {busy ? 'Creating tenant…' : 'Register and continue'}
            </button>
          </form>
        )}

        <p className="mt-6 text-center text-sm text-[var(--muted)]">
          Already have a tenant?{' '}
          <Link className="font-semibold text-[var(--signal)] hover:underline" to="/login">
            Sign in
          </Link>
        </p>
      </div>
    </div>
  );
}
