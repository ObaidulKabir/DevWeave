import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiFetch } from '../lib/api';
import type { SpecEffective } from '../lib/types';
import { useWorkspace } from '../workspace/WorkspaceContext';

/** Dashboard summary of the project's machine-readable specification decisions. */
export function SpecStatusCard() {
  const { tenantId, project, live, knowledge } = useWorkspace();
  const [effective, setEffective] = useState<SpecEffective | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (!live || !tenantId || !project) return;
    let cancelled = false;
    setFailed(false);
    apiFetch<SpecEffective>(`/tenants/${tenantId}/projects/${project.id}/spec/effective`)
      .then((data) => !cancelled && setEffective(data))
      .catch(() => !cancelled && setFailed(true));
    return () => {
      cancelled = true;
    };
  }, [live, tenantId, project]);

  const waiting = knowledge.filter((item) => item.typeId === 'spec.decision' && item.state === 'Proposed').length;
  const total = effective?.decisions.length ?? 0;
  const decided = effective?.decisions.filter((d) => d.decided).length ?? 0;
  const conflicts = effective?.decisions.reduce((sum, d) => sum + d.conflicts.length, 0) ?? 0;
  const incompatible = effective?.incompatibilities.length ?? 0;
  const attention = conflicts + incompatible + (effective?.undecidedTopics.length ?? 0) + waiting;

  return (
    <section className="panel rounded-2xl p-5" aria-labelledby="spec-status-title">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">Specifications</p>
          <h2 id="spec-status-title" className="font-display mt-1 text-xl font-bold">Decisions every agent follows</h2>
        </div>
        <Link className="text-sm font-semibold text-[var(--signal)] hover:underline" to="/specifications">
          Open specifications →
        </Link>
      </div>
      {!live ? (
        <p className="mt-3 text-sm text-[var(--muted)]">Connect to a live API to see decision status.</p>
      ) : failed ? (
        <p className="mt-3 text-sm text-amber-800">Specification status could not be loaded.</p>
      ) : !effective ? (
        <p className="mt-3 text-sm text-[var(--muted)]">Loading…</p>
      ) : (
        <>
          <p className="mt-3 text-sm">
            <span className="font-display text-2xl font-bold">{decided}</span>
            <span className="text-[var(--muted)]"> of {total} topics decided</span>
          </p>
          <div className="mt-3 h-2 overflow-hidden rounded-full bg-[var(--line)]" role="img" aria-label={`${decided} of ${total} topics decided`}>
            <div className="h-full bg-[var(--signal)]" style={{ width: total ? `${(decided / total) * 100}%` : '0%' }} />
          </div>
          <p className={`mt-3 text-sm ${attention ? 'text-amber-900' : 'text-emerald-800'}`}>
            {attention === 0
              ? 'Everything is decided and consistent.'
              : [
                  conflicts ? `${conflicts} conflict${conflicts === 1 ? '' : 's'}` : '',
                  incompatible ? `${incompatible} incompatible pair${incompatible === 1 ? '' : 's'}` : '',
                  effective.undecidedTopics.length ? `${effective.undecidedTopics.length} undecided` : '',
                  waiting ? `${waiting} awaiting approval` : '',
                ].filter(Boolean).join(' · ')}
          </p>
        </>
      )}
    </section>
  );
}
