import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiFetch } from '../lib/api';
import { formatWhen } from '../lib/format';
import type { PlanStatus } from '../lib/types';
import { useWorkspace } from '../workspace/WorkspaceContext';

/** Dashboard summary of the current plan version and whether any campaign has fallen behind it. */
export function PlanVersionStatusCard() {
  const { tenantId, project, live } = useWorkspace();
  const [status, setStatus] = useState<PlanStatus | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (!live || !tenantId || !project) return;
    let cancelled = false;
    setFailed(false);
    apiFetch<PlanStatus>(`/tenants/${tenantId}/projects/${project.id}/coordination/plan-status`)
      .then((data) => !cancelled && setStatus(data && Array.isArray(data.campaigns) ? data : null))
      .catch(() => !cancelled && setFailed(true));
    return () => {
      cancelled = true;
    };
  }, [live, tenantId, project]);

  const offCurrentPlan = (status?.campaigns ?? []).filter(
    (c) => !c.onCurrentPlan && (c.status === 'Active' || c.status === 'Proposed'),
  );

  return (
    <section className="panel rounded-2xl p-5" aria-labelledby="plan-version-status-title">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">Plan version</p>
          <h2 id="plan-version-status-title" className="font-display mt-1 text-xl font-bold">What agents are building against</h2>
        </div>
        <Link className="text-sm font-semibold text-[var(--signal)] hover:underline" to="/plan-versions">
          Open plan versions →
        </Link>
      </div>
      {!live ? (
        <p className="mt-3 text-sm text-[var(--muted)]">Connect to a live API to see the current plan version.</p>
      ) : failed ? (
        <p className="mt-3 text-sm text-amber-800">Plan version status could not be loaded.</p>
      ) : !status ? (
        <p className="mt-3 text-sm text-[var(--muted)]">Loading…</p>
      ) : status.currentPlanVersion === 0 ? (
        <p className="mt-3 text-sm text-amber-900">No plan version has been locked yet.</p>
      ) : (
        <>
          <p className="mt-3 text-sm">
            <span className="font-display text-2xl font-bold">v{status.currentPlanVersion}</span>
            {status.lockedAt ? <span className="text-[var(--muted)]"> · locked {formatWhen(status.lockedAt)}</span> : null}
          </p>
          <p className={`mt-3 text-sm ${offCurrentPlan.length ? 'text-amber-900' : 'text-emerald-800'}`}>
            {offCurrentPlan.length === 0
              ? `Every campaign is building against the current version.`
              : `${offCurrentPlan.length} campaign${offCurrentPlan.length === 1 ? ' is' : 's are'} still on an older version.`}
          </p>
        </>
      )}
    </section>
  );
}
