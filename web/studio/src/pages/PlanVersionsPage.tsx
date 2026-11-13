import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { EmptyState, Panel } from '../components/Panel';
import { StateBadge } from '../components/StateBadge';
import { apiFetch } from '../lib/api';
import { formatWhen, shortId } from '../lib/format';
import type { PlanBaselineSummary, PlanLockPreview, PlanStatus, PlanStatusCampaign, WorkPackageStatus } from '../lib/types';
import { useWorkspace } from '../workspace/WorkspaceContext';

const DEFAULT_CRITERIA = 'PR green\nNo source in DevWeave';

function DriftRow({ label, items, tone = 'text-[var(--muted)]' }: { label: string; items: string[]; tone?: string }) {
  if (items.length === 0) return null;
  return (
    <li className={tone}>
      <span className="font-semibold">{label}:</span> {items.join(', ')}
    </li>
  );
}

/** A campaign's work packages, fetched on demand when the row is expanded. */
function WorkPackagesPanel({ campaignId, base }: { campaignId: string; base: string }) {
  const [packages, setPackages] = useState<WorkPackageStatus[] | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    apiFetch<WorkPackageStatus[]>(`${base}/campaigns/${campaignId}/packages`)
      .then((data) => !cancelled && setPackages(data))
      .catch(() => !cancelled && setFailed(true));
    return () => {
      cancelled = true;
    };
  }, [base, campaignId]);

  if (failed) return <p className="px-4 py-3 text-xs text-amber-800">Work packages could not be loaded.</p>;
  if (!packages) return <p className="px-4 py-3 text-xs text-[var(--muted)]">Loading work…</p>;
  if (packages.length === 0) return <p className="px-4 py-3 text-xs text-[var(--muted)]">No work packages on this campaign yet.</p>;

  return (
    <ul className="divide-y divide-[var(--line)] border-t border-[var(--line)] bg-[var(--paper)]">
      {packages.map((pkg) => (
        <li key={pkg.id} className="px-4 py-3 text-sm">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="min-w-0">
              <span className="font-mono text-xs text-[var(--muted)]">{pkg.key}</span>{' '}
              <span className="font-semibold">{pkg.objective}</span>
            </p>
            <div className="flex shrink-0 items-center gap-2">
              {pkg.canProgress ? (
                <StateBadge label="can progress" value="active" />
              ) : (
                <StateBadge label="blocked" value="failed" />
              )}
              <StateBadge value={pkg.status} />
            </div>
          </div>
          {pkg.blockedReason ? (
            <p className="mt-1.5 text-xs text-amber-900" role="status">{pkg.blockedReason}</p>
          ) : null}
          <p className="mt-1.5 text-xs text-[var(--muted)]">
            {pkg.unscoped ? (
              <span className="font-semibold text-amber-800">No declared scope — a plan change cannot be traced to this work.</span>
            ) : (
              <>
                Implements {pkg.implementsRevisionIds.length} revision{pkg.implementsRevisionIds.length === 1 ? '' : 's'}
                {pkg.workItemIds.length > 0 ? `, ${pkg.workItemIds.length} work item${pkg.workItemIds.length === 1 ? '' : 's'}` : ''}
                {pkg.specTopics.length > 0 ? `, follows ${pkg.specTopics.join(', ')}` : ''}
              </>
            )}
          </p>
        </li>
      ))}
    </ul>
  );
}

export function PlanVersionsPage() {
  const { tenantId, project, live, busy, run, refresh, knowledge } = useWorkspace();
  const idByKey = useMemo(() => new Map(knowledge.map((item) => [item.key, item.id])), [knowledge]);
  const [lockPreview, setLockPreview] = useState<PlanLockPreview | null>(null);
  const [baselines, setBaselines] = useState<PlanBaselineSummary[]>([]);
  const [planStatus, setPlanStatus] = useState<PlanStatus | null>(null);
  const [goal, setGoal] = useState('');
  const [criteria, setCriteria] = useState(DEFAULT_CRITERIA);
  const [confirmLock, setConfirmLock] = useState(false);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [message, setMessage] = useState('');

  const base = tenantId && project ? `/tenants/${tenantId}/projects/${project.id}/coordination` : undefined;

  const load = useCallback(async () => {
    if (!base) {
      setLockPreview(null);
      setBaselines([]);
      setPlanStatus(null);
      return;
    }
    try {
      const [preview, list, status] = await Promise.all([
        apiFetch<PlanLockPreview>(`${base}/baselines/preview`),
        apiFetch<PlanBaselineSummary[]>(`${base}/baselines`),
        apiFetch<PlanStatus>(`${base}/plan-status`),
      ]);
      setLockPreview(preview && Array.isArray(preview.items) && Array.isArray(preview.blockers) ? preview : null);
      setBaselines(Array.isArray(list) ? list : []);
      setPlanStatus(status && Array.isArray(status.campaigns) ? status : null);
    } catch {
      setLockPreview(null);
      setBaselines([]);
      setPlanStatus(null);
    }
  }, [base]);

  useEffect(() => {
    void load();
  }, [load]);

  const campaignsByBaseline = useMemo(() => {
    const map = new Map<string, PlanStatusCampaign[]>();
    for (const campaign of planStatus?.campaigns ?? []) {
      map.set(campaign.baselineId, [...(map.get(campaign.baselineId) ?? []), campaign]);
    }
    return map;
  }, [planStatus]);

  const offCurrentPlan = (planStatus?.campaigns ?? []).filter(
    (c) => !c.onCurrentPlan && (c.status === 'Active' || c.status === 'Proposed'),
  );
  const drift = lockPreview?.drift;

  async function lockVersion() {
    if (!base || !lockPreview?.canLock) return;
    const list = criteria.split('\n').map((line) => line.trim()).filter(Boolean);
    await run(async () => {
      await apiFetch(`${base}/baselines`, {
        method: 'POST',
        body: JSON.stringify({
          goal,
          approvedRevisions: lockPreview.items.map((item) => ({ id: item.itemId, revisionId: item.revisionId })),
          dependencies: [],
          acceptanceCriteria: list.length ? list : DEFAULT_CRITERIA.split('\n'),
          expectedPlanVersion: lockPreview.nextPlanVersion,
        }),
      });
      setGoal('');
      setMessage(`Plan version ${lockPreview.nextPlanVersion} locked.`);
      await Promise.all([load(), refresh()]);
    });
    setConfirmLock(false);
  }

  async function suspendDrifted() {
    if (!base) return;
    await run(async () => {
      const { suspended } = await apiFetch<{ suspended: number }>(`${base}/campaigns/suspend-drifted`, { method: 'POST' });
      setMessage(suspended > 0 ? `Suspended ${suspended} campaign${suspended === 1 ? '' : 's'} on an older plan version.` : 'Nothing to suspend.');
      await Promise.all([load(), refresh()]);
    });
  }

  function onSubmitLock(event: FormEvent) {
    event.preventDefault();
    setConfirmLock(true);
  }

  return (
    <div className="space-y-6">
      <section className="panel rounded-2xl px-6 py-6">
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">Plan versions</p>
        <h2 className="font-display mt-2 text-3xl font-bold tracking-tight">One frozen plan at a time, with a clear history of change</h2>
        <p className="mt-2 max-w-3xl text-sm leading-relaxed text-[var(--muted)]">
          Locking takes a snapshot of every approved item in the outline as a new plan version. Agents build against
          one version at a time; locking a newer one stops work on the older ones. You can lock a new version at any
          time — nothing here needs a campaign to already exist.
        </p>
        {!live ? <p className="mt-4 text-sm text-amber-800">Connect to a live API to manage plan versions.</p> : null}
        {message ? <p className="mt-4 text-sm text-emerald-800" role="status">{message}</p> : null}

        {planStatus ? (
          <dl className="mt-5 grid gap-3 sm:grid-cols-3">
            <div className="rounded-xl bg-[var(--paper)] px-4 py-3">
              <dt className="text-xs text-[var(--muted)]">Current version</dt>
              <dd className="font-display mt-1 text-2xl font-bold">
                {planStatus.currentPlanVersion === 0 ? 'None locked' : `v${planStatus.currentPlanVersion}`}
              </dd>
              {planStatus.lockedAt ? <dd className="mt-1 text-xs text-[var(--muted)]">Locked {formatWhen(planStatus.lockedAt)}</dd> : null}
            </div>
            <div className="rounded-xl bg-[var(--paper)] px-4 py-3">
              <dt className="text-xs text-[var(--muted)]">Campaigns on this version</dt>
              <dd className="font-display mt-1 text-2xl font-bold">
                {planStatus.campaigns.filter((c) => c.onCurrentPlan).length}
              </dd>
            </div>
            <div className="rounded-xl bg-[var(--paper)] px-4 py-3">
              <dt className="text-xs text-[var(--muted)]">Still on an older version</dt>
              <dd className={`font-display mt-1 text-2xl font-bold ${offCurrentPlan.length ? 'text-amber-800' : ''}`}>
                {offCurrentPlan.length}
              </dd>
            </div>
          </dl>
        ) : null}
        {offCurrentPlan.length > 0 ? (
          <p className="mt-3 text-sm text-amber-900" role="status">
            {offCurrentPlan.length} campaign{offCurrentPlan.length === 1 ? ' is' : 's are'} still marked {offCurrentPlan.map((c) => c.status).join(', ')}{' '}
            on an older version. Work on them is refused until they are suspended.
            <button type="button" className="btn btn-ghost ml-3 text-xs" disabled={!live || busy} onClick={() => void suspendDrifted()}>
              Suspend them now
            </button>
          </p>
        ) : null}
      </section>

      <div className="grid gap-6 xl:grid-cols-2">
        <Panel title="What's changed since the last lock" subtitle="Preview before locking the next plan version.">
          {!live || !lockPreview ? (
            <EmptyState label="Connect a live project to preview a lock." />
          ) : (
            <div className="space-y-4 p-5">
              <p className="text-sm">
                Plan version <span className="font-mono">{lockPreview.currentPlanVersion || 'none'}</span>
                {' → would lock as '}
                <span className="font-mono font-semibold text-[var(--ink)]">v{lockPreview.nextPlanVersion}</span>
              </p>

              {lockPreview.blockers.length > 0 ? (
                <div className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-3 text-sm text-rose-900" role="alert">
                  <p className="font-semibold">Cannot lock yet — approve these on Knowledge first</p>
                  <ul className="mt-2 space-y-1 text-xs">
                    {lockPreview.blockers.map((blocker) => {
                      const itemId = blocker.key ? idByKey.get(blocker.key) : undefined;
                      return (
                        <li key={`${blocker.key}-${blocker.reason}`}>
                          {blocker.key ? (
                            itemId ? (
                              <Link className="font-mono font-semibold underline" to={`/knowledge?item=${itemId}`}>{blocker.key}</Link>
                            ) : (
                              <span className="font-mono font-semibold">{blocker.key}</span>
                            )
                          ) : null}{' '}
                          {blocker.reason}
                        </li>
                      );
                    })}
                  </ul>
                </div>
              ) : drift?.hasDrift ? (
                <div className="rounded-lg border border-sky-200 bg-sky-50 px-3 py-3 text-sm text-sky-950">
                  <p className="font-semibold">{lockPreview.items.length} item{lockPreview.items.length === 1 ? '' : 's'} ready to lock</p>
                  <ul className="mt-2 space-y-1 text-xs">
                    <DriftRow label="Added" items={drift.addedKeys} />
                    <DriftRow label="Changed" items={drift.changedKeys} />
                    <DriftRow label="No longer approved" items={drift.removedKeys} tone="text-rose-800" />
                    <DriftRow label="Stale work links" items={drift.staleWorkLinkKeys} tone="text-amber-800" />
                  </ul>
                </div>
              ) : (
                <p className="text-sm text-[var(--muted)]">
                  {lockPreview.currentPlanVersion === 0
                    ? `Ready to lock ${lockPreview.items.length} item${lockPreview.items.length === 1 ? '' : 's'} as plan version 1.`
                    : 'No changes since the last lock. Locking now would make an identical version.'}
                </p>
              )}

              <form onSubmit={onSubmitLock} className="space-y-3 border-t border-[var(--line)] pt-4">
                <label className="block text-sm font-semibold" htmlFor="lock-goal">
                  Goal for this version
                  <input id="lock-goal" required className="field mt-1" placeholder="What this version delivers"
                    value={goal} onChange={(e) => setGoal(e.target.value)} />
                </label>
                <label className="block text-sm font-semibold" htmlFor="lock-criteria">
                  Acceptance criteria <span className="text-xs font-normal text-[var(--muted)]">one per line</span>
                  <textarea id="lock-criteria" className="field mt-1 min-h-20 font-mono text-xs"
                    value={criteria} onChange={(e) => setCriteria(e.target.value)} />
                </label>
                <button className="btn btn-primary" disabled={!live || busy || !lockPreview.canLock}>
                  Lock version {lockPreview.nextPlanVersion}
                </button>
              </form>
            </div>
          )}
        </Panel>

        <Panel title="Version history" subtitle="Every locked plan version, newest first, with what ran on it.">
          {baselines.length === 0 ? (
            <EmptyState label="No plan version has been locked yet." />
          ) : (
            <ul className="divide-y divide-[var(--line)]">
              {baselines.map((baseline) => {
                const isCurrent = baseline.planVersion === planStatus?.currentPlanVersion;
                const campaignsHere = campaignsByBaseline.get(baseline.id) ?? [];
                return (
                  <li key={baseline.id}>
                    <div className="flex flex-wrap items-start justify-between gap-2 px-5 py-4">
                      <div className="min-w-0">
                        <p className="flex items-center gap-2 text-sm font-semibold">
                          <span className="font-mono">v{baseline.planVersion}</span>
                          {isCurrent ? <StateBadge value="active" label="current" /> : null}
                        </p>
                        <p className="mt-1 text-sm text-[var(--ink)]">{baseline.goal}</p>
                        <p className="mt-1 text-xs text-[var(--muted)]">
                          Locked {formatWhen(baseline.createdAt)} · {baseline.revisionCount} revisions · {campaignsHere.length} campaign{campaignsHere.length === 1 ? '' : 's'}
                        </p>
                      </div>
                      <p className="shrink-0 font-mono text-xs text-[var(--muted)]">{shortId(baseline.id)}</p>
                    </div>
                    {campaignsHere.length > 0 ? (
                      <ul className="divide-y divide-[var(--line)] border-t border-[var(--line)]">
                        {campaignsHere.map((campaign) => (
                          <li key={campaign.id}>
                            <button
                              type="button"
                              className="flex w-full items-center justify-between gap-3 px-5 py-3 text-left text-sm hover:bg-white/70"
                              aria-expanded={expanded === campaign.id}
                              onClick={() => setExpanded(expanded === campaign.id ? null : campaign.id)}
                            >
                              <span className="min-w-0 truncate">{campaign.objective}</span>
                              <span className="flex shrink-0 items-center gap-2">
                                <StateBadge value={campaign.status} />
                                <span className="text-xs text-[var(--muted)]">{expanded === campaign.id ? 'Hide work ▲' : 'Show work ▼'}</span>
                              </span>
                            </button>
                            {expanded === campaign.id && base ? <WorkPackagesPanel campaignId={campaign.id} base={base} /> : null}
                          </li>
                        ))}
                      </ul>
                    ) : null}
                  </li>
                );
              })}
            </ul>
          )}
        </Panel>
      </div>

      <ConfirmDialog
        open={confirmLock}
        title={`Lock plan version ${lockPreview?.nextPlanVersion ?? '?'}?`}
        body={`This freezes the ${lockPreview?.items?.length ?? 0} approved item${lockPreview?.items?.length === 1 ? '' : 's'} listed above as plan version ${lockPreview?.nextPlanVersion ?? '?'}. Any campaign still running on an older version is suspended, and agents are stopped from leasing, submitting or integrating work on it. This does not start a campaign — do that separately on the Coordination page.`}
        confirmLabel="Lock version"
        busy={busy}
        onCancel={() => setConfirmLock(false)}
        onConfirm={lockVersion}
      />
    </div>
  );
}
