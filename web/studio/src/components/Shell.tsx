import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { useWorkspace } from '../workspace/WorkspaceContext';

const NAV = [
  ['/access', 'Access / Apps'],
  ['/', 'Overview'],
  ['/plan-map', 'Plan map'],
  ['/plan-outline', 'Plan outline'],
  ['/plan-versions', 'Plan versions'],
  ['/knowledge', 'Knowledge'],
  ['/documents', 'Documents'],
  ['/specifications', 'Specifications'],
  ['/catalog', 'Catalog'],
  ['/relations', 'Relations'],
  ['/work', 'Work'],
  ['/activity', 'Activity'],
  ['/evidence', 'Evidence'],
  ['/live', 'Live'],
  ['/coordination', 'Coordination'],
  ['/reports', 'Reports'],
  ['/settings', 'Settings'],
] as const;

export function Shell() {
  const navigate = useNavigate();
  const { logout, principalId, source } = useAuth();
  const { tenantId, project, projects, setProjectId, connection, live, error } = useWorkspace();

  function onLogout() {
    logout();
    navigate('/login', { replace: true });
  }

  return (
    <div className="studio-shell flex min-h-screen">
      <aside className="hidden w-72 shrink-0 bg-[var(--ink)] px-4 py-6 text-white lg:flex lg:flex-col" aria-label="Owner Studio navigation">
        <div className="px-3">
          <p className="font-display text-2xl font-bold tracking-tight">DevWeave</p>
          <p className="mt-1 text-[10px] font-semibold uppercase tracking-[0.2em] text-teal-200/80">Owner Studio</p>
        </div>

        <nav className="mt-10 space-y-1" aria-label="Primary">
          {NAV.map(([to, label]) => (
            <NavLink key={to} to={to} end={to === '/'} className="studio-nav-link">
              <span aria-hidden="true" className="h-1.5 w-1.5 rounded-full bg-current opacity-70" />
              {label}
            </NavLink>
          ))}
        </nav>

        <div className="mt-auto space-y-3">
          <div className="rounded-xl border border-white/10 bg-white/5 p-4 text-xs text-slate-300">
            <p className="font-semibold text-teal-100">Control plane</p>
            <p className="mt-2 flex items-center gap-2">
              <span
                className={`h-2 w-2 rounded-full ${
                  connection === 'online' ? 'bg-emerald-400' : connection === 'checking' ? 'bg-amber-300' : 'bg-rose-400'
                }`}
                aria-hidden="true"
              />
              <span>{connection}</span>
            </p>
            {principalId ? (
              <p className="mt-2 font-mono text-[10px] text-slate-400">
                Owner {principalId.slice(0, 8)}…
                {source === 'env' ? ' · env' : ''}
              </p>
            ) : null}
            <p className="mt-3 leading-relaxed text-slate-400">
              Plan, document, verify and coordinate multi-agent delivery without storing source code.
            </p>
          </div>
          <button
            type="button"
            className="w-full rounded-xl border border-white/15 bg-transparent px-4 py-2.5 text-left text-sm font-semibold text-slate-200 transition hover:bg-white/5 hover:text-white"
            onClick={onLogout}
          >
            Sign out
          </button>
        </div>
      </aside>

      <div className="min-w-0 flex-1">
        {/* UI-02: persistent tenant + project context */}
        <header className="border-b border-[var(--line)] bg-white/80 px-4 py-4 backdrop-blur sm:px-8">
          <div className="mx-auto flex max-w-7xl flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
            <div>
              <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">Current context</p>
              <div className="mt-1 flex flex-wrap items-baseline gap-x-3 gap-y-1">
                <h1 className="font-display text-2xl font-bold tracking-tight">{project?.name ?? 'DevWeave'}</h1>
                <span className="font-mono text-xs text-[var(--muted)]">{project?.key}</span>
              </div>
              <p className="mt-1 text-xs text-[var(--muted)]">
                Tenant <span className="font-mono">{tenantId ?? 'preview'}</span>
                {' · '}
                Project <span className="font-mono">{project?.id.slice(0, 8) ?? '—'}</span>
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-3">
              <label className="text-xs text-[var(--muted)]" htmlFor="project-switcher">
                Project
              </label>
              <select
                id="project-switcher"
                className="field max-w-xs"
                value={project?.id}
                onChange={(event) => setProjectId(event.target.value)}
              >
                {projects.map((item) => (
                  <option key={item.id} value={item.id}>{item.name}</option>
                ))}
              </select>
              <span className={`rounded-full px-3 py-1.5 text-xs font-semibold ${live ? 'bg-emerald-50 text-emerald-800' : 'bg-amber-50 text-amber-900'}`}>
                {live ? 'Live API' : 'Preview mode'}
              </span>
            </div>
          </div>
        </header>

        <nav className="border-b border-[var(--line)] bg-white/90 px-4 py-2 lg:hidden" aria-label="Mobile primary">
          <div className="flex gap-2 overflow-x-auto">
            {NAV.map(([to, label]) => (
              <NavLink
                key={to}
                to={to}
                end={to === '/'}
                className={({ isActive }) =>
                  `shrink-0 rounded-lg px-3 py-2 text-sm font-semibold ${
                    isActive ? 'bg-teal-50 text-[var(--signal)]' : 'text-[var(--muted)]'
                  }`
                }
              >
                {label}
              </NavLink>
            ))}
          </div>
        </nav>

        <div className="mx-auto max-w-7xl px-4 py-8 sm:px-8">
          {error ? (
            <div role="alert" className="mb-5 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-800">
              {error}
            </div>
          ) : null}
          <Outlet />
        </div>
      </div>
    </div>
  );
}
