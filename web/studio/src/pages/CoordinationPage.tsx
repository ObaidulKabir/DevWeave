import { FormEvent, useCallback, useEffect, useState } from 'react';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { EmptyState, Panel, Row } from '../components/Panel';
import { StateBadge } from '../components/StateBadge';
import { apiFetch } from '../lib/api';
import type { Campaign, PlanLockPreview } from '../lib/types';
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

  // What would be locked is decided by the server from the approved plan outline; the form only shows it.
  const loadPreview = useCallback(async () => {
    if (!tenantId || !project) {
      setLockPreview(null);
      return;
    }
    try {
      setLockPreview(
        await apiFetch<PlanLockPreview>(`/tenants/${tenantId}/projects/${project.id}/coordination/baselines/preview`),
      );
    } catch {
      setLockPreview(null);
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
          // Exactly the plan the person reviewed on this screen; the server refuses it if the plan changed since.
          approvedRevisions: lockPreview.items.map((item) => ({ id: item.itemId, revisionId: item.revisionId })),
          dependencies: [],
          acceptanceCriteria: ['PR green', 'No source in DevWeave'],
          expectedPlanVersion: 1,
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
          <h3 className="text-sm font-semibold">Lock baseline and start campaign</h3>
          <div className="rounded-lg border border-[var(--line)] p-3 text-xs">
            <p className="font-semibold">Approved plan that will be locked</p>
            {lockPreview === null ? (
              <p className="mt-1 text-[var(--muted)]">Plan status is unavailable. Connect a live project to lock a plan.</p>
            ) : (
              <>
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

      <ConfirmDialog
        open={confirmStart}
        title="Start coordination campaign?"
        body={`This locks exactly the ${lockPreview?.items.length ?? 0} approved plan items listed above and activates a campaign against the supplied repository reference. Source content is never stored in DevWeave (UI-06).`}
        confirmLabel="Start campaign"
        busy={busy}
        onCancel={() => setConfirmStart(false)}
        onConfirm={startCampaign}
      />
    </div>
  );
}
