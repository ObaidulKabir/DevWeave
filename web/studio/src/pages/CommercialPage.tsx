import { useEffect, useState } from 'react';
import { Panel } from '../components/Panel';
import { apiFetch } from '../lib/api';
import { formatWhen } from '../lib/format';
import { useWorkspace } from '../workspace/WorkspaceContext';

type UsageRow = {
  meter: string;
  consumed: number;
  resourceClass: string;
  windowStart: string;
  windowEnd: string;
  projectId?: string | null;
  executionMode?: string;
};

type EntitlementRule = {
  key: string;
  limit: number;
  unit: string;
  action: string;
  overageRate: number;
};

type ActivePlan = {
  key: string;
  version: string;
  currency: string;
  price: number;
  entitlements: EntitlementRule[];
};

export function CommercialPage() {
  const { tenantId, live } = useWorkspace();
  const [rows, setRows] = useState<UsageRow[]>([]);
  const [plan, setPlan] = useState<ActivePlan | null>(null);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    if (!tenantId) return;
    const to = new Date();
    const from = new Date(to.getTime() - 30 * 24 * 60 * 60 * 1000);
    let cancelled = false;
    const usageUrl = `/tenants/${tenantId}/commercial/usage?from=${encodeURIComponent(from.toISOString())}&to=${encodeURIComponent(to.toISOString())}`;
    Promise.all([
      apiFetch<UsageRow[]>(usageUrl).catch(() => [] as UsageRow[]),
      apiFetch<ActivePlan>(`/tenants/${tenantId}/commercial/plan`).catch(() => null),
    ])
      .then(([usage, active]) => {
        if (cancelled) return;
        setRows(usage);
        setPlan(active);
      })
      .finally(() => {
        if (!cancelled) setLoaded(true);
      });
    return () => {
      cancelled = true;
    };
  }, [tenantId]);

  return (
    <Panel
      title="Usage"
      subtitle="Tenant usage for the last 30 days (COM-001). Entitlement decisions stay on the API. This screen does not bill."
    >
      <div className="space-y-3 p-5">
        <p className="text-xs leading-relaxed text-[var(--muted)]">
          A platform administrator publishes the plan and assigns it. Agents record usage. Reported work completion is not usage and is not an invoice.
        </p>
        {plan ? (
          <div className="rounded-xl border border-[var(--line)] bg-white/70 p-4">
            <p className="text-sm font-semibold">
              {plan.key} {plan.version}
            </p>
            <ul className="mt-2 space-y-1">
              {plan.entitlements.map((rule) => (
                <li key={rule.key} className="text-xs text-[var(--muted)]">
                  {rule.key}: {rule.action} at {rule.limit} {rule.unit}
                </li>
              ))}
            </ul>
          </div>
        ) : (
          <p className="text-sm text-[var(--muted)]">No active plan.</p>
        )}
        {!live || !loaded ? (
          <p className="text-sm text-[var(--muted)]">Usage loads with the signed-in tenant.</p>
        ) : rows.length === 0 ? (
          <p className="text-sm text-[var(--muted)]">No usage in this window.</p>
        ) : (
          <ul className="space-y-2">
            {rows.map((row) => (
              <li key={`${row.meter}-${row.resourceClass}-${row.projectId ?? ''}-${row.executionMode ?? ''}`} className="rounded-xl border border-[var(--line)] bg-white/70 p-4">
                <p className="text-sm font-semibold">{row.meter}</p>
                <p className="mt-1 font-display text-2xl font-bold">
                  {row.consumed}
                  <span className="ml-2 text-sm font-normal text-[var(--muted)]">{row.resourceClass}</span>
                </p>
                <p className="mt-1 text-xs text-[var(--muted)]">
                  {row.executionMode || 'Unspecified mode'}
                  {row.projectId ? ` · ${row.projectId}` : ''}
                </p>
                <p className="mt-1 text-xs text-[var(--muted)]">
                  {formatWhen(row.windowStart)} – {formatWhen(row.windowEnd)}
                </p>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Panel>
  );
}
