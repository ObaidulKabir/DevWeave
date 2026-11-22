import { FormEvent, useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { EmptyState, Panel, Row } from '../components/Panel';
import { StateBadge } from '../components/StateBadge';
import { apiFetch } from '../lib/api';
import { formatWhen, shortId } from '../lib/format';
import { campaignsStillOnOlderPlan } from '../lib/plan';
import type {
  Campaign,
  CodebaseTarget,
  CoordinationChannel,
  CoordinationMessage,
  PlanBaselineSummary,
  PlanStatus,
} from '../lib/types';
import {
  asChannelList,
  asMessageList,
  messagePayloadText,
  prettyCoordinationPayload,
  summarizeCoordinationPayload,
} from '../lib/coordination';
import { useWorkspace } from '../workspace/WorkspaceContext';

export function CoordinationPage() {
  const { tenantId, project, live, busy, run, campaigns, mcp, setCampaigns, refresh } = useWorkspace();
  const [form, setForm] = useState({
    baselineId: '',
    objective: '',
    repositoryId: '',
    branch: 'main',
    commitSha: '',
    existingTargetId: '',
  });
  const [confirmStart, setConfirmStart] = useState(false);
  const [baselines, setBaselines] = useState<PlanBaselineSummary[]>([]);
  const [planStatus, setPlanStatus] = useState<PlanStatus | null>(null);
  const [channels, setChannels] = useState<CoordinationChannel[]>([]);
  const [selectedChannelId, setSelectedChannelId] = useState<string>('');
  const [messages, setMessages] = useState<CoordinationMessage[]>([]);
  const [messagesError, setMessagesError] = useState<string | null>(null);
  const [codebaseTargets, setCodebaseTargets] = useState<CodebaseTarget[]>([]);

  const base = tenantId && project ? `/tenants/${tenantId}/projects/${project.id}/coordination` : undefined;

  const load = useCallback(async () => {
    if (!base) {
      setBaselines([]);
      setPlanStatus(null);
      setChannels([]);
      setSelectedChannelId('');
      setMessages([]);
      setCodebaseTargets([]);
      return;
    }
    try {
      const [list, status, channelPayload, targets] = await Promise.all([
        apiFetch<PlanBaselineSummary[]>(`${base}/baselines`),
        apiFetch<PlanStatus>(`${base}/plan-status`),
        apiFetch<unknown>(`${base}/channels`).catch(() => []),
        apiFetch<CodebaseTarget[]>(`${base}/codebase-targets`).catch(() => []),
      ]);
      const safeList = Array.isArray(list) ? list : [];
      setBaselines(safeList);
      setPlanStatus(status && Array.isArray(status.campaigns) ? status : null);
      const nextChannels = asChannelList(channelPayload);
      setChannels(nextChannels);
      setSelectedChannelId((current) => {
        if (current && nextChannels.some((channel) => channel.id === current)) return current;
        return nextChannels[0]?.id ?? '';
      });
      const safeTargets = Array.isArray(targets) ? targets : [];
      setCodebaseTargets(safeTargets);
      setForm((current) => ({
        ...current,
        baselineId: current.baselineId || safeList[0]?.id || '',
        existingTargetId: current.existingTargetId || safeTargets[0]?.id || '',
      }));
    } catch {
      setBaselines([]);
      setPlanStatus(null);
      setChannels([]);
      setSelectedChannelId('');
      setMessages([]);
      setCodebaseTargets([]);
    }
  }, [base]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (!base || !selectedChannelId) {
      setMessages([]);
      setMessagesError(null);
      return;
    }
    let cancelled = false;
    apiFetch<unknown>(`${base}/channels/${selectedChannelId}/messages?afterSequence=0&limit=200`)
      .then((payload) => {
        if (cancelled) return;
        setMessages(asMessageList(payload).sort((a, b) => a.sequence - b.sequence));
        setMessagesError(null);
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        setMessages([]);
        setMessagesError(err instanceof Error ? err.message : 'Could not load channel messages.');
      });
    return () => {
      cancelled = true;
    };
  }, [base, selectedChannelId]);

  const chosenBaseline = baselines.find((b) => b.id === form.baselineId);
  const offCurrentPlan = campaignsStillOnOlderPlan(planStatus?.campaigns);

  async function startCampaign() {
    if (!base || !form.baselineId) return;
    await run(async () => {
      let targetId = form.existingTargetId;
      if (!targetId) {
        const target = await apiFetch<{ id: string }>(`${base}/codebase-targets`, {
          method: 'POST',
          body: JSON.stringify({
            provider: 'github',
            repositoryId: form.repositoryId,
            targetBranch: form.branch,
            baseCommitSha: form.commitSha,
          }),
        });
        targetId = target.id;
      }
      const campaign = await apiFetch<{ id: string }>(`${base}/campaigns`, {
        method: 'POST',
        body: JSON.stringify({
          baselineId: form.baselineId,
          codebaseTargetIds: [targetId],
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
      <Panel title="Execution campaigns" subtitle="Plan-locked multi-agent coordination against external codebases. Git pointers are identity only — source stays on the host.">
        {codebaseTargets.length > 0 ? (
          <ul className="space-y-2 border-b border-[var(--line)] px-5 py-4 text-sm">
            {codebaseTargets.map((target) => (
              <li key={target.id} className="flex flex-wrap items-baseline justify-between gap-2">
                <span className="font-semibold">{target.provider}:{target.repositoryId}</span>
                <span className="font-mono text-[11px] text-[var(--muted)]">
                  {target.targetBranch} · {target.baseCommitSha.slice(0, 12)}
                </span>
              </li>
            ))}
          </ul>
        ) : null}
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
          {codebaseTargets.length > 0 ? (
            <label className="block text-xs font-semibold">
              Linked git repository
              <select
                className="field mt-1 w-full"
                value={form.existingTargetId}
                onChange={(e) => setForm({ ...form, existingTargetId: e.target.value })}
              >
                <option value="">Register a new pointer…</option>
                {codebaseTargets.map((target) => (
                  <option key={target.id} value={target.id}>
                    {target.provider}:{target.repositoryId} @ {target.targetBranch} ({target.baseCommitSha.slice(0, 7)})
                  </option>
                ))}
              </select>
            </label>
          ) : (
            <p className="text-xs text-[var(--muted)]">
              No git pointers yet. Enter owner/name, branch, and SHA (references only — no clone).
            </p>
          )}
          {!form.existingTargetId ? (
            <>
              <input required className="field" placeholder="github owner/repo or https://github.com/owner/name" value={form.repositoryId} onChange={(e) => setForm({ ...form, repositoryId: e.target.value })} />
              <div className="grid grid-cols-2 gap-3">
                <input required className="field" placeholder="Branch" value={form.branch} onChange={(e) => setForm({ ...form, branch: e.target.value })} />
                <input required className="field" placeholder="Base SHA" value={form.commitSha} onChange={(e) => setForm({ ...form, commitSha: e.target.value })} />
              </div>
            </>
          ) : null}
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

        <Panel
          title="Channels"
          subtitle="Durable agent messages. Knowledge/Relations/Work payloads here are logs, not published plan records."
          actions={
            <Link className="text-xs font-semibold text-[var(--signal)] hover:underline" to="/live">
              Live operations →
            </Link>
          }
        >
          {channels.length === 0 ? (
            <EmptyState label="No coordination channels yet. Execution agents create them with open_channel." />
          ) : (
            <div className="divide-y divide-[var(--line)]">
              {channels.map((channel) => (
                <Row
                  key={channel.id}
                  title={`${channel.channelType} · ${shortId(channel.id)}`}
                  subtitle={`${channel.sequenceHighWatermark} messages · ${formatWhen(channel.createdAt)}`}
                  meta={<StateBadge value={channel.visibility} />}
                  active={selectedChannelId === channel.id}
                  onClick={() => setSelectedChannelId(channel.id)}
                />
              ))}
            </div>
          )}
        </Panel>

        <Panel
          title="Channel messages"
          subtitle={
            selectedChannelId
              ? `Payloads posted to ${shortId(selectedChannelId)}. These are not Knowledge items until Plan import.`
              : 'Select a channel to read what the agent posted.'
          }
        >
          {messagesError ? (
            <p className="px-5 py-4 text-sm text-[var(--danger)]" role="alert">
              {messagesError}
            </p>
          ) : messages.length === 0 ? (
            <EmptyState label="No messages in this channel yet." />
          ) : (
            <ol className="divide-y divide-[var(--line)]">
              {messages.map((message) => {
                const raw = messagePayloadText(message);
                const pretty = prettyCoordinationPayload(raw);
                return (
                  <li key={message.id} className="px-5 py-4">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <p className="text-sm font-semibold">
                        {message.messageType} · seq {message.sequence}
                      </p>
                      <span className="text-xs text-[var(--muted)]">{formatWhen(message.createdAt)}</span>
                    </div>
                    <p className="mt-1 text-xs text-[var(--muted)]">{summarizeCoordinationPayload(raw)}</p>
                    {pretty ? (
                      <pre className="mt-3 max-h-64 overflow-auto rounded-lg bg-[var(--paper)] p-3 font-mono text-[11px] leading-relaxed text-[var(--ink)]">
                        {pretty}
                      </pre>
                    ) : null}
                  </li>
                );
              })}
            </ol>
          )}
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
