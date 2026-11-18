import { Link } from 'react-router-dom';

export function LandingPage() {
  return (
    <div className="landing-page min-h-screen">
      <header className="landing-topbar mx-auto flex max-w-6xl items-center justify-between px-6 py-6">
        <div>
          <p className="font-display text-2xl font-bold tracking-tight text-[var(--ink)]">DevWeave</p>
          <p className="mt-0.5 text-[10px] font-semibold uppercase tracking-[0.22em] text-[var(--signal)]">
            Multi-tenant control plane
          </p>
        </div>
        <div className="flex items-center gap-3">
          <Link to="/login" className="btn btn-ghost">
            Sign in
          </Link>
          <Link to="/register" className="btn btn-primary">
            Register tenant
          </Link>
        </div>
      </header>

      <main className="landing-hero relative mx-auto flex min-h-[calc(100vh-5.5rem)] max-w-6xl flex-col justify-center px-6 pb-16 pt-4">
        <p className="font-display text-5xl font-bold tracking-tight text-[var(--ink)] sm:text-6xl md:text-7xl">
          DevWeave
        </p>
        <h1 className="mt-6 max-w-2xl font-display text-2xl font-semibold tracking-tight text-[var(--ink-soft)] sm:text-3xl">
          Register your tenant. Run applications. Manage the agents that deliver them.
        </h1>
        <p className="mt-4 max-w-xl text-base leading-relaxed text-[var(--muted)]">
          Owners create a tenant, stand up working applications, and issue MCP credentials so agents can
          execute under explicit grants — without the control plane storing source code.
        </p>
        <div className="mt-10 flex flex-wrap gap-3">
          <Link to="/register" className="btn btn-primary px-6 py-3 text-base">
            Register tenant
          </Link>
          <Link to="/login" className="btn btn-ghost px-6 py-3 text-base">
            Login
          </Link>
        </div>
        <p className="mt-8 max-w-md text-xs leading-relaxed text-[var(--muted)]">
          Registration creates the tenant and Owner immediately. Email confirmation is not part of this step.
        </p>
      </main>
    </div>
  );
}
