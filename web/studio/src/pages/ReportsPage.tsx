import { useState } from 'react';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { Panel } from '../components/Panel';
import { apiFetch } from '../lib/api';
import { formatWhen } from '../lib/format';
import { useWorkspace } from '../workspace/WorkspaceContext';

export function ReportsPage() {
  const { tenantId, project, metrics, live, busy, run } = useWorkspace();
  const [lastReportId, setLastReportId] = useState<string | null>(null);
  const [confirm, setConfirm] = useState(false);

  async function generate() {
    if (!tenantId || !project) return;
    const result = await run(() =>
      apiFetch<{ id: string }>(`/tenants/${tenantId}/projects/${project.id}/reports`, {
        method: 'POST',
        body: JSON.stringify({ declaredPurpose: 'Owner Studio progress report' }),
      }),
    );
    setConfirm(false);
    if (result) setLastReportId(result.id);
  }

  return (
    <>
      <Panel
        title="Reporting"
        subtitle="Owner metrics and audited report generation (RPT / UI-01)."
        actions={
          <button className="btn btn-primary" disabled={!live || busy} onClick={() => setConfirm(true)}>
            Generate report
          </button>
        }
      >
        <div className="space-y-4 p-5">
          <div className="grid gap-3 sm:grid-cols-2">
            {metrics.length === 0 ? (
              <p className="text-sm text-[var(--muted)]">No metrics available yet.</p>
            ) : (
              metrics.map((metric) => (
                <article key={metric.key} className="rounded-xl border border-[var(--line)] bg-white/70 p-4">
                  <p className="text-sm text-[var(--muted)]">{metric.key}</p>
                  <p className="mt-2 font-display text-3xl font-bold">
                    {metric.value}
                    <span className="ml-2 text-sm font-normal text-[var(--muted)]">{metric.unit}</span>
                  </p>
                  <p className="mt-2 text-xs text-[var(--muted)]">
                    {metric.includedPopulation}
                    {metric.calculatedAt ? ` · ${formatWhen(metric.calculatedAt)}` : ''}
                  </p>
                </article>
              ))
            )}
          </div>
          {lastReportId ? (
            <p className="font-mono text-xs text-[var(--muted)]">Last report id: {lastReportId}</p>
          ) : null}
        </div>
      </Panel>

      <ConfirmDialog
        open={confirm}
        title="Generate project report?"
        body="Report generation is audited and uses the current authorized project projection (UI-06)."
        confirmLabel="Generate"
        busy={busy}
        onCancel={() => setConfirm(false)}
        onConfirm={generate}
      />
    </>
  );
}
