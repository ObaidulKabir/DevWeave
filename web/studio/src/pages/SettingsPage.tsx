import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { Panel } from '../components/Panel';
import { useWorkspace } from '../workspace/WorkspaceContext';

export function SettingsPage() {
  const navigate = useNavigate();
  const { logout, principalId, source } = useAuth();
  const { tenantId, project, live, connection } = useWorkspace();

  function onLogout() {
    logout();
    navigate('/login', { replace: true });
  }

  return (
    <div className="space-y-6">
      <Panel title="Project settings" subtitle="Context identity for this Owner Studio session (UI-02).">
        <dl className="grid gap-4 px-5 py-5 sm:grid-cols-2">
          <div>
            <dt className="text-xs uppercase tracking-[0.14em] text-[var(--muted)]">Tenant</dt>
            <dd className="mt-1 font-mono text-sm">{tenantId ?? 'preview-tenant'}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-[0.14em] text-[var(--muted)]">Owner principal</dt>
            <dd className="mt-1 font-mono text-sm">{principalId ?? '—'}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-[0.14em] text-[var(--muted)]">Project</dt>
            <dd className="mt-1 text-sm font-semibold">{project?.name}</dd>
            <dd className="mt-1 font-mono text-xs text-[var(--muted)]">{project?.id}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-[0.14em] text-[var(--muted)]">Project key</dt>
            <dd className="mt-1 font-mono text-sm">{project?.key}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-[0.14em] text-[var(--muted)]">Status</dt>
            <dd className="mt-1 text-sm">{project?.status ?? '—'}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-[0.14em] text-[var(--muted)]">API mode</dt>
            <dd className="mt-1 text-sm">{live ? 'Live authenticated' : 'Preview / demo'}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-[0.14em] text-[var(--muted)]">Auth source</dt>
            <dd className="mt-1 text-sm">
              {source === 'session' ? 'Signed-in session' : source === 'env' ? 'Local env fallback' : '—'}
            </dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-[0.14em] text-[var(--muted)]">Control plane</dt>
            <dd className="mt-1 text-sm">{connection}</dd>
          </div>
        </dl>
        <div className="border-t border-[var(--line)] px-5 py-4">
          <button type="button" className="btn btn-ghost" onClick={onLogout}>
            Sign out
          </button>
        </div>
      </Panel>

      <Panel title="Authorization note" subtitle="UI-07">
        <p className="px-5 py-5 text-sm leading-relaxed text-[var(--muted)]">
          Hidden or disabled controls are presentation only. The API re-checks tenant membership, project grants and
          principal type on every request. Sign in at <Link className="font-semibold text-[var(--signal)] hover:underline" to="/login">/login</Link> to mint a
          pilot Owner token (session preferred; <code className="font-mono">VITE_API_TOKEN</code> remains a local fallback).
          Create applications and MCP agent API keys on the{' '}
          <Link className="font-semibold text-[var(--signal)] hover:underline" to="/access">Access</Link> page.
        </p>
      </Panel>
    </div>
  );
}
