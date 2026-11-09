import { useEffect, useState } from 'react';
import { apiFetch } from './lib/api';

type ConnectionState = 'checking' | 'online' | 'unreachable';

interface LivenessResponse {
  status: string;
}

const PLANES = [
  {
    name: 'Knowledge Plane',
    detail: 'Canonical project knowledge: typed items, immutable revisions, relationships, provenance and evidence metadata.',
  },
  {
    name: 'Coordination / Control Plane',
    detail: 'Project-scoped MCP, campaigns, work packages, leases, workspaces and governed integration.',
  },
  {
    name: 'Execution Plane',
    detail: 'Customer-managed or DevWeave-managed runtime instances working in isolated workspaces.',
  },
];

const CONNECTION_LABEL: Record<ConnectionState, string> = {
  checking: 'checking',
  online: 'online',
  unreachable: 'unreachable',
};

const CONNECTION_CLASS: Record<ConnectionState, string> = {
  checking: 'text-amber-400',
  online: 'text-emerald-400',
  unreachable: 'text-rose-400',
};

export default function App() {
  const [connection, setConnection] = useState<ConnectionState>('checking');

  useEffect(() => {
    let cancelled = false;

    apiFetch<LivenessResponse>('/health/live')
      .then(() => {
        if (!cancelled) setConnection('online');
      })
      .catch(() => {
        if (!cancelled) setConnection('unreachable');
      });

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <main className="min-h-screen bg-slate-950 text-slate-100">
      <div className="mx-auto max-w-5xl px-6 py-12">
        <header className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <p className="text-xs uppercase tracking-[0.2em] text-sky-400">DevWeave</p>
            <h1 className="mt-1 text-2xl font-semibold">AI Software Delivery Control Plane</h1>
          </div>
          <span className="rounded-full border border-slate-700 px-3 py-1 text-xs text-slate-300">
            DWV-SRS-001 v1.5 &middot; R1 target
          </span>
        </header>

        <section className="mt-8 rounded-lg border border-slate-800 bg-slate-900/60 p-4">
          <h2 className="text-sm font-medium text-slate-200">Control plane API</h2>
          <p className="mt-1 text-sm text-slate-400">
            Status:{' '}
            <span className={CONNECTION_CLASS[connection]}>{CONNECTION_LABEL[connection]}</span>
          </p>
        </section>

        <section className="mt-6 grid gap-4 sm:grid-cols-3">
          {PLANES.map((plane) => (
            <article
              key={plane.name}
              className="rounded-lg border border-slate-800 bg-slate-900/60 p-4"
            >
              <h3 className="text-sm font-semibold text-slate-100">{plane.name}</h3>
              <p className="mt-2 text-xs leading-relaxed text-slate-400">{plane.detail}</p>
            </article>
          ))}
        </section>
      </div>
    </main>
  );
}
