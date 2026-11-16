import { FormEvent, useState } from 'react';
import { Link } from 'react-router-dom';
import { EmptyState, Panel } from '../components/Panel';
import { StateBadge } from '../components/StateBadge';
import { createTenantApplication, slugifyKey } from '../lib/apps';
import { formatWhen, shortId } from '../lib/format';
import { useWorkspace } from '../workspace/WorkspaceContext';

/** Tenant home: working applications, with a clear path to manage Agents per app. */
export function TenantAppsPage() {
  const {
    tenantId,
    projects,
    project,
    setProjectId,
    live,
    busy,
    run,
    refreshProjects,
    refresh,
  } = useWorkspace();

  const [form, setForm] = useState({ name: '', key: '', description: '' });
  const [note, setNote] = useState<string | null>(null);

  async function onCreate(event: FormEvent) {
    event.preventDefault();
    if (!tenantId || !live) return;
    setNote(null);

    const created = await run(async () =>
      createTenantApplication(tenantId, {
        name: form.name,
        key: form.key || slugifyKey(form.name),
        description: form.description,
      }),
    );

    if (!created) return;
    setForm({ name: '', key: '', description: '' });
    await refreshProjects();
    setProjectId(created.id);
    setNote(`Application “${created.name}” is ready. Open Manage Agents to wire MCP runtimes.`);
    await refresh();
  }

  return (
    <div className="space-y-8">
      <section className="panel rounded-2xl px-6 py-6">
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">
          Tenant dashboard
        </p>
        <h2 className="font-display mt-2 text-3xl font-bold tracking-tight">Applications</h2>
        <p className="mt-2 max-w-2xl text-sm leading-relaxed text-[var(--muted)]">
          Each application is a project workspace: plan, knowledge, coordination, and the agents allowed to work in it.
          Open <span className="font-semibold text-[var(--ink)]">Manage Agents</span> on an application to register
          agents, issue API keys, and approve grants.
        </p>
        {!live ? (
          <p className="mt-4 text-sm text-amber-800">Sign in to load live applications for this tenant.</p>
        ) : null}
        {note ? (
          <p className="mt-4 text-sm text-[var(--ink)]" role="status">
            {note}
          </p>
        ) : null}
      </section>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,22rem)_minmax(0,1fr)]">
        <Panel title="New application" subtitle="Creates a project under your tenant.">
          <form className="space-y-3 px-5 py-4" onSubmit={onCreate}>
            <label className="block space-y-1">
              <span className="text-xs font-semibold uppercase tracking-[0.12em] text-[var(--muted)]">Name</span>
              <input
                className="field text-sm"
                value={form.name}
                disabled={!live || busy}
                onChange={(event) => {
                  const name = event.target.value;
                  setForm((current) => ({
                    ...current,
                    name,
                    key: current.key || slugifyKey(name),
                  }));
                }}
                placeholder="Online Cake Shop"
                required
              />
            </label>
            <label className="block space-y-1">
              <span className="text-xs font-semibold uppercase tracking-[0.12em] text-[var(--muted)]">Key</span>
              <input
                className="field font-mono text-sm"
                value={form.key}
                disabled={!live || busy}
                onChange={(event) => setForm((current) => ({ ...current, key: event.target.value }))}
                placeholder="online-cake-shop"
                required
              />
            </label>
            <label className="block space-y-1">
              <span className="text-xs font-semibold uppercase tracking-[0.12em] text-[var(--muted)]">
                Description
              </span>
              <textarea
                className="field min-h-[4.5rem] text-sm"
                value={form.description}
                disabled={!live || busy}
                onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))}
                placeholder="What this application delivers"
              />
            </label>
            <button type="submit" className="btn btn-primary w-full" disabled={!live || busy}>
              {busy ? 'Creating…' : 'Create application'}
            </button>
          </form>
        </Panel>

        <Panel
          title="Working applications"
          subtitle={
            live
              ? `${projects.length} application${projects.length === 1 ? '' : 's'} in this tenant`
              : 'Preview'
          }
        >
          {projects.length === 0 ? (
            <EmptyState label="No applications yet. Create one to start managing agents." />
          ) : (
            <ul className="divide-y divide-[var(--line)]">
              {projects.map((item) => {
                const active = item.id === project?.id;
                return (
                  <li key={item.id} className="flex flex-wrap items-center justify-between gap-4 px-5 py-4">
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <p className="font-display text-lg font-semibold tracking-tight text-[var(--ink)]">
                          {item.name}
                        </p>
                        {active ? <StateBadge value="Active" /> : null}
                        {item.status ? <StateBadge value={item.status} /> : null}
                      </div>
                      <p className="mt-1 font-mono text-xs text-[var(--muted)]">
                        {item.key} · {shortId(item.id)}
                        {item.createdAt ? ` · ${formatWhen(item.createdAt)}` : ''}
                      </p>
                      {item.description ? (
                        <p className="mt-2 max-w-xl text-sm text-[var(--muted)]">{item.description}</p>
                      ) : null}
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <button
                        type="button"
                        className="btn btn-ghost"
                        disabled={active}
                        onClick={() => setProjectId(item.id)}
                      >
                        {active ? 'Selected' : 'Select'}
                      </button>
                      <Link
                        className="btn btn-primary"
                        to={`/agents?projectId=${encodeURIComponent(item.id)}`}
                        onClick={() => setProjectId(item.id)}
                      >
                        Manage Agents
                      </Link>
                      <Link
                        className="btn btn-ghost"
                        to="/access"
                        onClick={() => setProjectId(item.id)}
                      >
                        MCP keys
                      </Link>
                    </div>
                  </li>
                );
              })}
            </ul>
          )}
        </Panel>
      </div>
    </div>
  );
}
