import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { useWorkspace } from '../workspace/WorkspaceContext';
import { BrandMark } from './BrandMark';

const NAV = [
  ['/apps', 'Applications'],
  ['/agents', 'Agents'],
  ['/access', 'Access / MCP'],
  ['/overview', 'Overview'],
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
    navigate('/', { replace: true });
  }

  return (
    <div className="studio-shell flex min-h-screen">
      <aside
        className="studio-nav-rail flex w-[13.5rem] shrink-0 flex-col border-r border-[var(--line)] bg-white/95 sm:w-56 lg:w-64"
        aria-label="Owner Studio navigation"
      >
        <div className="border-b border-[var(--line)] px-4 py-4">
          <BrandMark
            variant="icon"
            to="/apps"
            imgClassName="h-12 w-12 rounded-xl object-contain bg-white ring-1 ring-[var(--line)]"
          />
          <p className="mt-3 font-display text-lg font-extrabold tracking-tight text-[var(--ink)]">DevWeave</p>
          <p className="mt-0.5 text-[10px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">
            Owner Studio
          </p>
        </div>

        <p className="px-4 pb-1 pt-4 text-[10px] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">
          Menu
        </p>
        <nav className="flex-1 space-y-0.5 overflow-y-auto px-2 pb-3" aria-label="Primary">
          {NAV.map(([to, label]) => (
            <NavLink
              key={to}
              to={to}
              end={to === '/apps'}
              className={({ isActive }) =>
                `block rounded-lg px-3 py-2 text-sm font-semibold transition ${
                  isActive
                    ? 'bg-teal-50 text-[var(--signal)]'
                    : 'text-[var(--ink-soft)] hover:bg-[var(--mist)] hover:text-[var(--ink)]'
                }`
              }
            >
              {label}
            </NavLink>
          ))}
        </nav>

        <div className="mt-auto space-y-2 border-t border-[var(--line)] px-3 py-3">
          <div className="rounded-xl border border-[var(--line)] bg-[var(--mist)]/60 p-3 text-xs text-[var(--muted)]">
            <p className="font-semibold text-[var(--ink-soft)]">Control plane</p>
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
              <p className="mt-2 font-mono text-[10px]">
                Owner {principalId.slice(0, 8)}…
                {source === 'env' ? ' · env' : ''}
              </p>
            ) : null}
          </div>
          <button
            type="button"
            className="w-full rounded-xl border border-[var(--line)] bg-white px-3 py-2 text-left text-sm font-semibold text-[var(--ink-soft)] transition hover:bg-[var(--mist)]"
            onClick={onLogout}
          >
            Sign out
          </button>
        </div>
      </aside>

      <div className="min-w-0 flex-1">
        <header className="border-b border-[var(--line)] bg-white/80 px-4 py-4 backdrop-blur sm:px-8">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
            <div className="flex min-w-0 items-center gap-3 sm:gap-4">
              <BrandMark
                variant="icon"
                to="/apps"
                className="shrink-0"
                imgClassName="h-16 w-16 rounded-xl object-contain bg-white ring-1 ring-[var(--line)] sm:h-[72px] sm:w-[72px]"
              />
              <div className="min-w-0">
                <Link
                  to="/apps"
                  className="block font-display text-3xl font-extrabold leading-none tracking-tight text-[var(--ink)] hover:opacity-90 sm:text-[2.35rem]"
                >
                  DevWeave
                </Link>
                <h1 className="mt-2 truncate text-base font-medium leading-snug tracking-tight text-[var(--ink-soft)] sm:text-lg">
                  {project?.name ?? 'No project selected'}
                  {project?.key ? (
                    <span className="ml-2 font-mono text-xs font-normal text-[var(--muted)]">{project.key}</span>
                  ) : null}
                </h1>
                <p className="mt-0.5 text-[11px] text-[var(--muted)]">
                  Tenant <span className="font-mono">{tenantId ?? 'preview'}</span>
                  {' · '}
                  Project <span className="font-mono">{project?.id.slice(0, 8) ?? '—'}</span>
                </p>
              </div>
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
