import { Link } from 'react-router-dom';
import { BrandMark } from '../components/BrandMark';

export function LandingPage() {
  return (
    <div className="landing-page min-h-screen">
      <header className="landing-topbar mx-auto flex max-w-6xl items-center justify-between px-6 py-6">
        <BrandMark
          variant="icon"
          to="/"
          imgClassName="h-12 w-12 rounded-lg object-contain bg-white shadow-sm ring-1 ring-[var(--line)]"
        />
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
        <BrandMark
          variant="full"
          className="max-w-xl"
          imgClassName="h-auto w-full max-w-xl rounded-2xl object-contain shadow-[0_24px_60px_-28px_rgba(11,19,32,0.45)] ring-1 ring-[var(--line)]/60"
        />
        <h1 className="sr-only">DevWeave — control plane for coordinated multi-tenant agents</h1>
        <p className="mt-8 max-w-xl text-base leading-relaxed text-[var(--muted)]">
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
