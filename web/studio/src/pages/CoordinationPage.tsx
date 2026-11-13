import { FormEvent, useCallback, useEffect, useState } from 'react';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { EmptyState, Panel, Row } from '../components/Panel';
import { StateBadge } from '../components/StateBadge';
import { apiFetch } from '../lib/api';
import type { Campaign, PlanBaselineSummary, PlanLockPreview } from '../lib/types';
import { useWorkspace } from '../workspace/WorkspaceContext';

export function CoordinationPage() {
  const { tenantId, project, live, busy, run, campaigns, mcp, setCampaigns, refresh } = useWorkspace();
  const [form, setForm] = useState({
    goal: '',
    objective: '',
    repositoryId: 'acme/checkout',
    branch: 'main',
    commitSha: 'abc1234',
  });
  const [confirmStart, setConfirmStart] = useState(false);
  const [lockPreview, setLockPreview] = useState<PlanLockPreview | null>(null);
  const [baselines, setBaselines] = useState<PlanBaselineSummary[]>([]);

  const loadPreview = useCallback(async () => {
    if (!tenantId || !project) {
      setLockPreview(null);
      setBaselines([]);
      return;
    }
    try {
      const [preview, list] = await Promise.all([
        apiFetch<PlanLockPreview>(`/tenants/${tenantId}/projects/${project.id}/coordination/baselines/preview`),
        apiFetch<PlanBaselineSummary[]>(`/tenants/${tenantId}/projects/${project.id}/coordination/baselines`),
      ]);
      setLockPreview(preview);
      setBaselines(list);
    } catch {
      setLockPreview(null);
      setBaselines([]);
    }
  }, [tenantId, project?.id]);

  useEffect(() => {
    void loadPreview();
  }, [loadPreview]);

  async function startCampaign() {
    if (!tenantId || !project || !lockPreview?.canLock) return;
    const base = `/tenants/${tenantId}/projects/${project.id}/coordination`;
    await run(async () => {
      const baseline = await apiFetch<{ id: string }>(`${base}/baselines`, {
        method: 'POST',
        body: JSON.stringify({
          goal: form.goal,
          approvedRevisions: lockPreview.items.map((item) => ({ id: item.itemId, revisionId: item.revisionId })),
          dependencies: [],
          acceptanceCriteria: ['PR green', 'No source in DevWeave'],
          expectedPlanVersion: lockPreview.nextPlanVersion,
        }),
      });
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
          baselineId: baseline.id,
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
      setForm((current) => ({ ...current, goal: '', objective: '' }));
      await refresh();
    });
    setConfirmStart(false);
    await loadPreview();
  }

  async function suspendDrifted() {
    if (!tenantId || !project) return;
    await run(async () => {
      await apiFetch<{ suspended: number }>(
        `/tenants/${tenantId}/projects/${project.id}/coordination/campaigns/suspend-drifted`,
        { method: 'POST' },
      );
      const next = await apiFetch<Campaign[]>(`/tenants/${tenantId}/projects/${project.id}/coordination/campaigns`);
      setCampaigns(next);
      await loadPreview();
    });
  }

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    setConfirmStart(true);
  }

  const drift = lockPreview?.drift;

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
          <h3 className="text-sm font-semibold">Lock baseline and start campaign</h3>
          <div className="rounded-lg border border-[var(--line)] p-3 text-xs">
            <p className="font-semibold">Approved plan that will be locked</p>
            {lockPreview === null ? (
              <p className="mt-1 text-[var(--muted)]">Plan status is unavailable. Connect a live project to lock a plan.</p>
            ) : (
              <>
                <p className="mt-1 text-[var(--muted)]">
                  Plan version <span className="font-mono">{lockPreview.currentPlanVersion}</span>
                  {' → lock as '}
                  <span className="font-mono font-semibold text-[var(--ink)]">v{lockPreview.nextPlanVersion}</span>
                </p>
                {lockPreview.items.length > 0 ? (
                  <ul className="mt-1 space-y-0.5 text-[var(--muted)]">
                    {lockPreview.items.map((item) => (
                      <li key={item.itemId}>
                        <span className="font-mono">{item.key}</span> · revision {item.revisionNumber} · {item.title}
                      </li>
                    ))}
                  </ul>
                ) : null}
                {lockPreview.blockers.length > 0 ? (
                  <div className="mt-2 text-rose-800" role="alert">
                    <p className="font-semibold">The plan cannot be locked yet. Approve these on the Knowledge page:</p>
                    <ul className="mt-1 list-disc pl-4">
                      {lockPreview.blockers.map((blocker) => (
                        <li key={`${blocker.key}-${blocker.reason}`}>
                          {blocker.key ? <span className="font-mono">{blocker.key}</span> : null} {blocker.reason}
                        </li>
                      ))}
                    </ul>
                  </div>
                ) : null}
              </>
            )}
          </div>
          <input required className="field" placeholder="Plan goal" value={form.goal} onChange={(e) => setForm({ ...form, goal: e.target.value })} />
          <input required className="field" placeholder="Campaign objective" value={form.objective} onChange={(e) => setForm({ ...form, objective: e.target.value })} />
          <input required className="field" placeholder="github owner/repo" value={form.repositoryId} onChange={(e) => setForm({ ...form, repositoryId: e.target.value })} />
          <div className="grid grid-cols-2 gap-3">
            <input required className="field" placeholder="Branch" value={form.branch} onChange={(e) => setForm({ ...form, branch: e.target.value })} />
            <input required className="field" placeholder="Base SHA" value={form.commitSha} onChange={(e) => setForm({ ...form, commitSha: e.target.value })} />
          </div>
          <button className="btn btn-primary" disabled={!live || busy || !lockPreview?.canLock}>Start campaign</button>
        </form>
      </Panel>

      <div className="space-y-6">
        <Panel title="Plan version & drift" subtitle="Living draft vs the last locked baseline (ARCH:OD-07).">
          {!live || !lockPreview ? (
            <EmptyState label="Connect a live project to inspect plan drift." />
          ) : (
            <div className="space-y-3 p-5 text-sm">
              <p className="text-[var(--muted)]">
                Current locked version{' '}
                <span className="font-mono font-semibold text-[var(--ink)]">
                  {lockPreview.currentPlanVersion === 0 ? 'none' : `v${lockPreview.currentPlanVersion}`}
                </span>
                {drift?.lastBaselineId ? (
                  <>
                    {' '}
                    · baseline <span className="font-mono text-xs">{drift.lastBaselineId.slice(0, 8)}</span>
                  </>
                ) : null}
              </p>
              {drift?.hasDrift ? (
                <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-amber-950" role="status">
                  <p className="font-semibold">Living plan differs from the last baseline</p>
                  <ul className="mt-2 list-disc space-y-1 pl-4 text-xs">
                    {drift.changedKeys.length > 0 ? (
                      <li>Changed / re-approved: {drift.changedKeys.join(', ')}</li>
                    ) : null}
                    {drift.addedKeys.length > 0 ? <li>Added since lock: {drift.addedKeys.join(', ')}</li> : null}
                    {drift.removedKeys.length > 0 ? (
                      <li>No longer in living set: {drift.removedKeys.join(', ')}</li>
                    ) : null}
                    {drift.staleWorkLinkKeys.length > 0 ? (
                      <li>Stale work links: {drift.staleWorkLinkKeys.join(', ')}</li>
                    ) : null}
                    {drift.activeCampaignIdsOnPriorBaselines.length > 0 ? (
                      <li>
                        Active campaigns still on a prior baseline:{' '}
                        {drift.activeCampaignIdsOnPriorBaselines.length}
                      </li>
                    ) : null}
                  </ul>
                  {drift.activeCampaignIdsOnPriorBaselines.length > 0 ? (
                    <button
                      type="button"
                      className="btn btn-ghost mt-3 text-xs"
                      disabled={!live || busy}
                      onClick={() => void suspendDrifted()}
                    >
                      Suspend campaigns on prior baselines
                    </button>
                  ) : null}
                </div>
              ) : (
                <p className="text-[var(--muted)]">
                  {lockPreview.currentPlanVersion === 0
                    ? 'No baseline locked yet. Locking creates plan version 1.'
                    : 'No drift detected against the latest baseline.'}
                </p>
              )}
              {baselines.length > 0 ? (
                <div className="border-t border-[var(--line)] pt-3">
                  <p className="text-xs font-semibold uppercase tracking-wide text-[var(--muted)]">Locked baselines</p>
                  <ul className="mt-2 space-y-1 text-xs">
                    {baselines.map((baseline) => (
                      <li key={baseline.id}>
                        <span className="font-mono font-semibold">v{baseline.planVersion}</span>
                        {' · '}
                        {baseline.goal}
                        {' · '}
                        {baseline.revisionCount} revisions
                      </li>
                    ))}
                  </ul>
                </div>
              ) : null}
            </div>
          )}
        </Panel>

        <Panel title="Project MCP" subtitle="Mandatory agent-to-platform coordination interface.">
          {mcp ? (
            <div className="space-y-3 p-5">
              <p className="font-mono text-xs text-[var(--muted)]">{mcp.serviceRef}</p>
              {mcp.capabilities.map((capability) => (
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
        body={`This locks plan version ${lockPreview?.nextPlanVersion ?? '?'} with the ${lockPreview?.items.length ?? 0} approved plan items listed above and activates a campaign. Campaigns still on older baselines are suspended. Source content is never stored in DevWeave (UI-06).`}
        confirmLabel="Start campaign"
        busy={busy}
        onCancel={() => setConfirmStart(false)}
        onConfirm={startCampaign}
      />
    </div>
  );
}
