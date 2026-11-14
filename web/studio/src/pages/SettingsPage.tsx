import { Link } from 'react-router-dom';
import { Panel } from '../components/Panel';
import { useWorkspace } from '../workspace/WorkspaceContext';

export function SettingsPage() {
  const { tenantId, project, live, connection } = useWorkspace();

  return (
    <div className="space-y-6">
      <Panel title="Project settings" subtitle="Context identity for this Owner Studio session (UI-02).">
        <dl className="grid gap-4 px-5 py-5 sm:grid-cols-2">
          <div>
            <dt className="text-xs uppercase tracking-[0.14em] text-[var(--muted)]">Tenant</dt>
            <dd className="mt-1 font-mono text-sm">{tenantId ?? 'preview-tenant'}</dd>
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
            <dt className="text-xs uppercase tracking-[0.14em] text-[var(--muted)]">Control plane</dt>
            <dd className="mt-1 text-sm">{connection}</dd>
          </div>
        </dl>
      </Panel>

      <Panel title="Authorization note" subtitle="UI-07">
        <p className="px-5 py-5 text-sm leading-relaxed text-[var(--muted)]">
          Hidden or disabled controls are presentation only. The API re-checks tenant membership, project grants and
          principal type on every request. Configure a human bearer token in <code className="font-mono">VITE_API_TOKEN</code>.
          Create applications and MCP agent API keys on the{' '}
          <Link className="font-semibold text-[var(--signal)] hover:underline" to="/access">Access</Link> page.
        </p>
      </Panel>
    </div>
  );
}
