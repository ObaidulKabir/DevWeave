import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { AgentPushCallout } from '../components/AgentPushCallout';
import { EmptyState, Panel } from '../components/Panel';
import { StateBadge } from '../components/StateBadge';
import { apiFetch } from '../lib/api';
import { channelMessageCount } from '../lib/coordination';
import { formatWhen, shortId } from '../lib/format';
import { useWorkspace } from '../workspace/WorkspaceContext';

type PlanImportSummary = {
  id: string;
  idempotencyKey: string;
  payloadHash: string;
  requireEmptyProject: boolean;
  status: string;
  proposedBy: string;
  proposedAt: string;
  reviewedBy?: string | null;
  reviewedAt?: string | null;
  decisionNote?: string | null;
  knowledgeCount: number;
  outlineCount: number;
  relationCount: number;
  milestoneCount: number;
  workItemCount: number;
  codebaseTargetCount?: number;
};

type PlanImportDetail = PlanImportSummary & {
  payload: {
    idempotencyKey: string;
    requireEmptyProject: boolean;
    knowledge: Array<{
      ref: string;
      key: string;
      title: string;
      typeId: string;
      schemaVersion: string;
      classification: string;
      representationKind: string;
      content: string;
      reason: string;
      description?: string | null;
    }>;
    outline: Array<{ itemRef: string; parentRef?: string | null; position: number }>;
    relations: Array<{ sourceRef: string; targetRef: string; relationTypeCode: string }>;
    milestones: Array<{ ref: string; key: string; title: string }>;
    workItems: Array<{ ref: string; key: string; title: string; description: string; priority: string }>;
    workLinks?: Array<{ workItemRef: string; knowledgeRef: string }> | null;
    codebaseTargets?: Array<{
      ref: string;
      provider: string;
      repositoryId: string;
      targetBranch: string;
      baseCommitSha: string;
      reason?: string | null;
    }> | null;
  };
  resultMapping?: Record<string, string> | null;
};

export function PlanImportPage() {
  const { tenantId, project, live, busy, run, refresh, channels } = useWorkspace();
  const postedMessages = useMemo(() => channelMessageCount(channels), [channels]);
  const [rows, setRows] = useState<PlanImportSummary[]>([]);
  const [selectedId, setSelectedId] = useState<string>('');
  const [detail, setDetail] = useState<PlanImportDetail | null>(null);
  const [note, setNote] = useState('');
  const [message, setMessage] = useState('');

  const base = tenantId && project ? `/tenants/${tenantId}/projects/${project.id}/plan-import-proposals` : null;

  const load = useCallback(async () => {
    if (!base || !live) {
      setRows([]);
      setDetail(null);
      return;
    }
    const list = await apiFetch<PlanImportSummary[]>(`${base}/`);
    const next = Array.isArray(list) ? list : [];
    setRows(next);
    setSelectedId((current) => {
      if (current && next.some((row) => row.id === current)) return current;
      return next[0]?.id ?? '';
    });
  }, [base, live]);

  useEffect(() => {
    if (!live) return;
    void run(() => load());
  }, [live, run, load]);

  useEffect(() => {
    if (!base || !live || !selectedId) {
      setDetail(null);
      return;
    }
    void run(async () => {
      const next = await apiFetch<PlanImportDetail>(`${base}/${selectedId}`);
      setDetail(next);
    });
  }, [base, live, selectedId, run]);

  async function decide(action: 'approve' | 'reject') {
    if (!base || !selectedId) return;
    setMessage('');
    await run(async () => {
      const updated = await apiFetch<PlanImportDetail>(`${base}/${selectedId}/${action}`, {
        method: 'POST',
        body: JSON.stringify({ note: note.trim() || null }),
      });
      setDetail(updated);
      setNote('');
      setMessage(action === 'approve' ? 'Plan import approved and materialized.' : 'Plan import rejected.');
      await load();
      await refresh();
    });
  }

  return (
    <div className="space-y-6">
      <section className="panel rounded-2xl px-6 py-6">
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">Control plane · Review</p>
        <h2 className="font-display mt-2 text-3xl font-bold tracking-tight">Plan import proposals</h2>
        <p className="mt-2 max-w-3xl text-sm leading-relaxed text-[var(--muted)]">
          Desktop planning agents submit full plan bundles via <code className="font-mono text-xs">propose_initial_plan</code>.
          Review the immutable payload here; approve to materialize Knowledge, Relations, outline, and Work without retyping.
          See also <Link className="font-semibold text-[var(--signal)] hover:underline" to="/plan-outline">Plan outline</Link>.
        </p>
        {message ? <p className="mt-4 text-sm text-[var(--ink)]">{message}</p> : null}
      </section>
      <AgentPushCallout
        pendingPlanImports={rows.filter((row) => row.status === 'Proposed').length}
        channelMessageCount={postedMessages}
      />

      <div className="grid gap-6 xl:grid-cols-[20rem_minmax(0,1fr)]">
        <Panel title="Proposals" subtitle={project ? project.name : 'Select an application'}>
          {!live ? (
            <EmptyState label="Sign in with Live API to review plan imports." />
          ) : rows.length === 0 ? (
            <EmptyState
              label={
                postedMessages > 0
                  ? 'No propose_initial_plan proposals. Agent send_project_message payloads are on Coordination.'
                  : 'No plan import proposals yet.'
              }
            />
          ) : (
            <ul className="divide-y divide-[var(--line)]">
              {rows.map((row) => (
                <li key={row.id}>
                  <button
                    type="button"
                    className={`flex w-full flex-col gap-1 px-4 py-3 text-left transition ${
                      selectedId === row.id ? 'bg-teal-50' : 'hover:bg-[var(--mist)]'
                    }`}
                    onClick={() => setSelectedId(row.id)}
                  >
                    <div className="flex items-center justify-between gap-2">
                      <span className="font-mono text-xs">{shortId(row.id)}</span>
                      <StateBadge value={row.status} />
                    </div>
                    <span className="text-sm font-semibold text-[var(--ink)]">{row.idempotencyKey}</span>
                    <span className="text-[11px] text-[var(--muted)]">
                      {row.knowledgeCount} knowledge · {row.workItemCount} work
                      {(row.codebaseTargetCount ?? 0) > 0 ? ` · ${row.codebaseTargetCount} git` : ''} · {formatWhen(row.proposedAt)}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </Panel>

        <Panel
          title="Payload review"
          subtitle={detail ? `hash ${detail.payloadHash.slice(0, 12)}…` : 'Select a proposal'}
        >
          {!detail ? (
            <EmptyState label="Choose a proposal to inspect its immutable contents." />
          ) : (
            <div className="space-y-5 px-5 py-5">
              <dl className="grid gap-3 sm:grid-cols-2 text-sm">
                <div>
                  <dt className="text-[11px] uppercase tracking-[0.12em] text-[var(--muted)]">Status</dt>
                  <dd className="mt-1"><StateBadge value={detail.status} /></dd>
                </div>
                <div>
                  <dt className="text-[11px] uppercase tracking-[0.12em] text-[var(--muted)]">Empty-project required</dt>
                  <dd className="mt-1">{detail.requireEmptyProject ? 'Yes' : 'No'}</dd>
                </div>
                <div>
                  <dt className="text-[11px] uppercase tracking-[0.12em] text-[var(--muted)]">Proposed by</dt>
                  <dd className="mt-1 font-mono text-xs">{detail.proposedBy}</dd>
                </div>
                <div>
                  <dt className="text-[11px] uppercase tracking-[0.12em] text-[var(--muted)]">Proposed at</dt>
                  <dd className="mt-1">{formatWhen(detail.proposedAt)}</dd>
                </div>
              </dl>

              <section>
                <h3 className="font-display text-lg font-bold">Knowledge</h3>
                <ul className="mt-3 space-y-3">
                  {detail.payload.knowledge.map((item) => (
                    <li key={item.ref} className="rounded-xl border border-[var(--line)] bg-white px-4 py-3">
                      <p className="text-sm font-semibold">
                        {item.title} <span className="font-mono text-[11px] text-[var(--muted)]">({item.key})</span>
                      </p>
                      <p className="mt-1 text-[11px] text-[var(--muted)]">
                        {item.typeId}@{item.schemaVersion} · {item.classification} · {item.representationKind}
                      </p>
                      <pre className="mt-2 max-h-40 overflow-auto rounded-lg bg-[var(--mist)]/50 px-3 py-2 font-mono text-[11px] whitespace-pre-wrap">
                        {item.content}
                      </pre>
                    </li>
                  ))}
                </ul>
              </section>

              <section>
                <h3 className="font-display text-lg font-bold">Outline</h3>
                <ul className="mt-2 space-y-1 font-mono text-xs text-[var(--muted)]">
                  {detail.payload.outline.map((node) => (
                    <li key={node.itemRef}>
                      {node.parentRef ? `${node.parentRef} → ` : 'root → '}
                      {node.itemRef} (pos {node.position})
                    </li>
                  ))}
                </ul>
              </section>

              <section>
                <h3 className="font-display text-lg font-bold">Relations / Work</h3>
                <p className="mt-1 text-sm text-[var(--muted)]">
                  {detail.payload.relations.length} relations · {detail.payload.milestones.length} milestones ·{' '}
                  {detail.payload.workItems.length} work items · {(detail.payload.workLinks ?? []).length} work links
                  · {(detail.payload.codebaseTargets ?? []).length} git pointers
                </p>
                {(detail.payload.codebaseTargets ?? []).length > 0 ? (
                  <ul className="mt-2 space-y-1 font-mono text-xs text-[var(--muted)]">
                    {(detail.payload.codebaseTargets ?? []).map((target) => (
                      <li key={target.ref}>
                        {target.provider}:{target.repositoryId} @ {target.targetBranch} #{target.baseCommitSha.slice(0, 12)}
                      </li>
                    ))}
                  </ul>
                ) : null}
                <ul className="mt-2 space-y-1 text-sm">
                  {detail.payload.workItems.map((item) => (
                    <li key={item.ref}>
                      <span className="font-semibold">{item.title}</span>{' '}
                      <span className="font-mono text-[11px] text-[var(--muted)]">{item.key}</span>
                    </li>
                  ))}
                </ul>
              </section>

              {detail.status === 'Proposed' ? (
                <div className="space-y-3 border-t border-[var(--line)] pt-4">
                  <label className="block space-y-1">
                    <span className="text-[11px] font-semibold uppercase tracking-[0.12em] text-[var(--muted)]">
                      Decision note (optional)
                    </span>
                    <textarea
                      className="field min-h-[4rem]"
                      value={note}
                      disabled={busy}
                      onChange={(event) => setNote(event.target.value)}
                    />
                  </label>
                  <div className="flex flex-wrap gap-2">
                    <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void decide('approve')}>
                      Approve and materialize
                    </button>
                    <button type="button" className="btn btn-ghost" disabled={busy} onClick={() => void decide('reject')}>
                      Reject
                    </button>
                  </div>
                </div>
              ) : detail.resultMapping ? (
                <section>
                  <h3 className="font-display text-lg font-bold">Result mapping</h3>
                  <pre className="mt-2 overflow-auto rounded-xl border border-[var(--line)] bg-white px-3 py-3 font-mono text-[11px]">
                    {JSON.stringify(detail.resultMapping, null, 2)}
                  </pre>
                </section>
              ) : null}
            </div>
          )}
        </Panel>
      </div>
    </div>
  );
}
