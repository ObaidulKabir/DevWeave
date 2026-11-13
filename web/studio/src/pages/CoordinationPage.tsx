import { FormEvent, useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { EmptyState, Panel, Row } from '../components/Panel';
import { StateBadge } from '../components/StateBadge';
import { apiFetch } from '../lib/api';
import type { Campaign, PlanBaselineSummary, PlanStatus } from '../lib/types';
import { useWorkspace } from '../workspace/WorkspaceContext';

export function CoordinationPage() {
  const { tenantId, project, live, busy, run, campaigns, mcp, setCampaigns, refresh } = useWorkspace();
  const [form, setForm] = useState({
    baselineId: '',
    objective: '',
    repositoryId: 'acme/checkout',
    branch: 'main',
    commitSha: 'abc1234',
  });
  const [confirmStart, setConfirmStart] = useState(false);
  const [baselines, setBaselines] = useState<PlanBaselineSummary[]>([]);
  const [planStatus, setPlanStatus] = useState<PlanStatus | null>(null);

  const base = tenantId && project ? `/tenants/${tenantId}/projects/${project.id}/coordination` : undefined;

  const load = useCallback(async () => {
    if (!base) {
      setBaselines([]);
      setPlanStatus(null);
      return;
    }
    try {
      const [list, status] = await Promise.all([
        apiFetch<PlanBaselineSummary[]>(`${base}/baselines`),
        apiFetch<PlanStatus>(`${base}/plan-status`),
      ]);
      const safeList = Array.isArray(list) ? list : [];
      setBaselines(safeList);
      setPlanStatus(status && Array.isArray(status.campaigns) ? status : null);
      setForm((current) => (current.baselineId ? current : { ...current, baselineId: safeList[0]?.id ?? '' }));
    } catch {
      setBaselines([]);
      setPlanStatus(null);
    }
  }, [base]);

  useEffect(() => {
    void load();
  }, [load]);

  const chosenBaseline = baselines.find((b) => b.id === form.baselineId);
  const offCurrentPlan = (planStatus?.campaigns ?? []).filter((c) => !c.onCurrentPlan && c.status !== 'Completed');

  async function startCampaign() {
    if (!base || !form.baselineId) return;
    await run(async () => {
      const target = await apiFetch<{ id: string }>(`${base}/codebase-targets`, {
        method: 'POST',
        body: JSON.stringify({
          provider: 'github',
          repositoryId: form.repositoryId,
          targetBranch: form.branch,
          baseCommitSha: form.commitSha,
        }),
      });
      const campaign = await apiFetch<{ id: string }>(`${base}/campaigns`, {
        method: 'POST',
        body: JSON.stringify({
          baselineId: form.baselineId,
          codebaseTargetIds: [target.id],
          objective: form.objective,
          executionMode: 'Mediated',
          idempotencyKey: `studio-${Date.now()}`,
        }),
      });
      await apiFetch(`${base}/campaigns/${campaign.id}/transition`, {
        method: 'POST',
        body: JSON.stringify({
          targetState: 'Active',
          reason: 'Started from Owner Studio',
          expectedVersion: 1,
          idempotencyKey: `activate-${Date.now()}`,
        }),
      });
      const next = await apiFetch<Campaign[]>(`${base}/campaigns`);
      setCampaigns(next);
      setForm((current) => ({ ...current, objective: '' }));
      await refresh();
    });
    setConfirmStart(false);
    await load();
  }

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    setConfirmStart(true);
  }

  return (
    <div className="grid gap-6 xl:grid-cols-2">
      <Panel title="Execution campaigns" subtitle="Plan-locked multi-agent coordination against external codebases.">
        {campaigns.length === 0 ? (
          <EmptyState label="No campaigns yet." />
        ) : (
          <div className="divide-y divide-[var(--line)]">
            {campaigns.map((campaign) => (
              <Row
                key={campaign.id}
                title={campaign.objective}
                subtitle={campaign.executionMode}
                meta={<StateBadge value={campaign.status} />}
              />
            ))}
          </div>
        )}
        <form onSubmit={onSubmit} className="space-y-3 border-t border-[var(--line)] p-5">
          <h3 className="text-sm font-semibold">Start a campaign on a locked plan version</h3>
          <p className="text-xs text-[var(--muted)]">
            Locking a new version happens on the{' '}
            <Link className="font-semibold text-[var(--signal)] hover:underline" to="/plan-versions">Plan versions</Link> page.
            Here you pick a version that is already locked and start execution against it.
          </p>
          {baselines.length === 0 ? (
            <p className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900">
              No plan version has been locked yet.{' '}
              <Link className="font-semibold underline" to="/plan-versions">Lock version 1 first →</Link>
            </p>
          ) : (
            <label className="block text-xs font-semibold">
              Plan version to build against
              <select className="field mt-1 w-full" required value={form.baselineId}
                onChange={(e) => setForm({ ...form, baselineId: e.target.value })}>
                {baselines.map((b) => (
                  <option key={b.id} value={b.id}>
                    v{b.planVersion}{b.planVersion === planStatus?.currentPlanVersion ? ' (current)' : ''} · {b.goal}
                  </option>
                ))}
              </select>
            </label>
          )}
          {chosenBaseline && chosenBaseline.planVersion !== planStatus?.currentPlanVersion ? (
            <p className="text-xs text-amber-800" role="status">
              A newer version (v{planStatus?.currentPlanVersion}) is locked. Starting on an older one is unusual — check the
              Plan versions page first.
            </p>
          ) : null}
          <input required className="field" placeholder="Campaign objective" value={form.objective} onChange={(e) => setForm({ ...form, objective: e.target.value })} />
          <input required className="field" placeholder="github owner/repo" value={form.repositoryId} onChange={(e) => setForm({ ...form, repositoryId: e.target.value })} />
          <div className="grid grid-cols-2 gap-3">
            <input required className="field" placeholder="Branch" value={form.branch} onChange={(e) => setForm({ ...form, branch: e.target.value })} />
            <input required className="field" placeholder="Base SHA" value={form.commitSha} onChange={(e) => setForm({ ...form, commitSha: e.target.value })} />
          </div>
          <button className="btn btn-primary" disabled={!live || busy || !form.baselineId}>Start campaign</button>
        </form>
      </Panel>

      <div className="space-y-6">
        <Panel title="Plan version" subtitle="Manage locking, drift and history on the Plan versions page.">
          <div className="space-y-3 p-5 text-sm">
            <p>
              Current version{' '}
              <span className="font-mono font-semibold text-[var(--ink)]">
                {!planStatus || planStatus.currentPlanVersion === 0 ? 'none locked' : `v${planStatus.currentPlanVersion}`}
              </span>
            </p>
            {offCurrentPlan.length > 0 ? (
              <p className="text-amber-900" role="status">
                {offCurrentPlan.length} campaign{offCurrentPlan.length === 1 ? ' is' : 's are'} still on an older version.
              </p>
            ) : (
              <p className="text-[var(--muted)]">Every active campaign is on the current version.</p>
            )}
            <Link className="btn btn-ghost inline-flex text-xs" to="/plan-versions">Open Plan versions →</Link>
          </div>
        </Panel>

        <Panel title="Project MCP" subtitle="Mandatory agent-to-platform coordination interface.">
          {mcp ? (
            <div className="space-y-3 p-5">
              <p className="font-mono text-xs text-[var(--muted)]">{mcp.serviceRef}</p>
              {(mcp.capabilities ?? []).map((capability) => (
                <div key={capability} className="rounded-lg bg-teal-50 px-3 py-2 text-sm text-teal-950">
                  {capability}
                </div>
              ))}
            </div>
          ) : (
            <EmptyState label="Connect a live tenant to inspect MCP capabilities." />
          )}
        </Panel>
      </div>

      <ConfirmDialog
        open={confirmStart}
        title="Start coordination campaign?"
        body={`This starts a campaign on plan version ${chosenBaseline?.planVersion ?? '?'} (${chosenBaseline?.goal ?? ''}) and activates it right away. Source content is never stored in DevWeave (UI-06).`}
        confirmLabel="Start campaign"
        busy={busy}
        onCancel={() => setConfirmStart(false)}
        onConfirm={startCampaign}
      />
    </div>
  );
}
