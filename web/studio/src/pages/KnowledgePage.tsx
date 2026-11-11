import { FormEvent, useEffect, useState } from 'react';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { MetaLine } from '../components/MetaLine';
import { EmptyState, Panel, Row } from '../components/Panel';
import { StateBadge } from '../components/StateBadge';
import { apiFetch } from '../lib/api';
import type { KnowledgeItem, KnowledgeRevision, ReviewDecision, SearchHit } from '../lib/types';
import { useWorkspace } from '../workspace/WorkspaceContext';

export function KnowledgePage() {
  const { tenantId, project, live, busy, run, knowledge, setKnowledge } = useWorkspace();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [revision, setRevision] = useState<KnowledgeRevision | null>(null);
  const [search, setSearch] = useState('');
  const [hits, setHits] = useState<SearchHit[]>([]);
  const [form, setForm] = useState({ key: '', title: '', typeId: 'plan.requirement', schemaVersion: '1.0' });
  const [revisionForm, setRevisionForm] = useState({
    content: '{"statement":"","acceptance":"","rationale":"","source":""}',
    reason: 'Capture planning requirement',
  });
  const [confirmApprove, setConfirmApprove] = useState(false);
  const [confirmReject, setConfirmReject] = useState(false);
  const [reviewComment, setReviewComment] = useState('');
  const [revisions, setRevisions] = useState<KnowledgeRevision[]>([]);
  const [reviews, setReviews] = useState<ReviewDecision[]>([]);

  const selected = knowledge.find((item) => item.id === selectedId) ?? knowledge[0] ?? null;

  useEffect(() => {
    if (knowledge[0] && !selectedId) setSelectedId(knowledge[0].id);
  }, [knowledge, selectedId]);

  useEffect(() => {
    if (!tenantId || !selected?.currentRevisionId) {
      setRevision(null);
      return;
    }
    let cancelled = false;
    apiFetch<KnowledgeRevision>(`/tenants/${tenantId}/revisions/${selected.currentRevisionId}`)
      .then((next) => {
        if (!cancelled) setRevision(next);
      })
      .catch(() => {
        if (!cancelled) setRevision(null);
      });
    return () => {
      cancelled = true;
    };
  }, [tenantId, selected?.currentRevisionId]);

  useEffect(() => {
    if (!tenantId || !selected) {
      setRevisions([]);
      setReviews([]);
      return;
    }
    let cancelled = false;
    Promise.all([
      apiFetch<KnowledgeRevision[]>(`/tenants/${tenantId}/knowledge/${selected.id}/revisions`),
      apiFetch<ReviewDecision[]>(`/tenants/${tenantId}/knowledge/${selected.id}/reviews`),
    ])
      .then(([nextRevisions, nextReviews]) => {
        if (cancelled) return;
        setRevisions(nextRevisions);
        setReviews(nextReviews);
      })
      .catch(() => {
        if (cancelled) return;
        setRevisions([]);
        setReviews([]);
      });
    return () => {
      cancelled = true;
    };
  }, [tenantId, selected?.id, selected?.currentRevisionId, selected?.state]);

  async function createItem(event: FormEvent) {
    event.preventDefault();
    if (!tenantId || !project) return;
    const created = await run(() =>
      apiFetch<KnowledgeItem>(`/tenants/${tenantId}/projects/${project.id}/knowledge`, {
        method: 'POST',
        body: JSON.stringify({
          key: form.key,
          title: form.title,
          classification: 'Internal',
          representationKind: 'StructuredData',
          typeId: form.typeId,
          schemaVersion: form.schemaVersion,
          createdBy: '00000000-0000-0000-0000-000000000000',
        }),
      }),
    );
    if (created) {
      setKnowledge((current) => [created, ...current]);
      setSelectedId(created.id);
      setForm((current) => ({ ...current, key: '', title: '' }));
    }
  }

  async function addRevision(event: FormEvent) {
    event.preventDefault();
    if (!tenantId || !selected) return;
    const created = await run(() =>
      apiFetch<KnowledgeRevision>(`/tenants/${tenantId}/knowledge/${selected.id}/revisions`, {
        method: 'POST',
        body: JSON.stringify({
          expectedCurrentRevisionNumber: selected.currentRevisionNumber,
          schemaVersion: selected.schemaVersion,
          content: revisionForm.content,
          actor: '00000000-0000-0000-0000-000000000000',
          reason: revisionForm.reason,
          sourceReference: null,
          supersededRevisionId: null,
        }),
      }),
    );
    if (created) {
      setRevision(created);
      setKnowledge((current) =>
        current.map((item) =>
          item.id === selected.id
            ? { ...item, currentRevisionId: created.id, currentRevisionNumber: created.revisionNumber }
            : item,
        ),
      );
    }
  }

  async function runSearch(event: FormEvent) {
    event.preventDefault();
    if (!tenantId || !project || !search.trim()) return;
    const result = await run(() =>
      apiFetch<{ items?: SearchHit[]; Items?: SearchHit[] }>(`/tenants/${tenantId}/projects/${project.id}/search`, {
        method: 'POST',
        body: JSON.stringify({ text: search, limit: 20 }),
      }),
    );
    if (result) setHits(result.items ?? result.Items ?? []);
  }

  async function decide(decision: 'approve' | 'reject') {
    if (!tenantId || !selected?.currentRevisionId) return;
    const updated = await run(() =>
      apiFetch<KnowledgeItem>(`/tenants/${tenantId}/knowledge/${selected.id}/${decision}`, {
        method: 'POST',
        // The decision names the exact revision that was read; the server refuses it if the item has moved on.
        body: JSON.stringify({ revisionId: selected.currentRevisionId, comment: reviewComment.trim() || null }),
      }),
    );
    setConfirmApprove(false);
    setConfirmReject(false);
    if (updated) {
      setKnowledge((current) => current.map((item) => (item.id === updated.id ? updated : item)));
      setReviewComment('');
    }
  }

  const viewingOlderRevision = Boolean(revision && selected && revision.id !== selected.currentRevisionId);

  return (
    <div className="space-y-6">
      <section className="panel rounded-2xl p-5" aria-labelledby="knowledge-explainer-title">
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">01 / Define the plan</p>
        <h2 id="knowledge-explainer-title" className="font-display mt-2 text-xl font-bold">Knowledge records what the project knows</h2>
        <p className="mt-2 max-w-3xl text-sm leading-relaxed text-[var(--muted)]">
          Make a separate typed item for each requirement, constraint, decision or success criterion. Its key stays stable;
          content changes are added as immutable revisions. Review and verification are different states: approval does
          not prove the work is done.
        </p>
        <p className="mt-3 text-xs leading-relaxed text-[var(--muted)]">
          Example: "A task must have a non-empty title" is a requirement, not a work item. Use a published schema type
          and version for its content. This space holds planning information, not source code or credentials.
        </p>
      </section>

      <form onSubmit={runSearch} className="panel flex flex-col gap-3 rounded-2xl p-4 sm:flex-row sm:items-center">
        <label className="sr-only" htmlFor="knowledge-search">Search knowledge</label>
        <input
          id="knowledge-search"
          className="field"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder="Search project knowledge…"
        />
        <button className="btn btn-primary" disabled={!live || busy}>Search</button>
      </form>

      {hits.length > 0 ? (
        <Panel title="Search results" subtitle="Authorization-filtered full-text hits.">
          <div className="divide-y divide-[var(--line)]">
            {hits.map((hit) => (
              <Row
                key={`${hit.itemId}-${hit.revisionId}`}
                title={hit.title}
                subtitle={`${hit.typeId} · ${hit.lifecycle}/${hit.review}/${hit.verification}`}
                onClick={() => setSelectedId(hit.itemId)}
              />
            ))}
          </div>
        </Panel>
      ) : null}

      <div className="grid gap-6 xl:grid-cols-[1.05fr_0.95fr]">
        <Panel title="Knowledge browser" subtitle="Choose an item to see its type, latest revision and separate review/verification states.">
          {knowledge.length === 0 ? (
            <EmptyState label="No knowledge items loaded." />
          ) : (
            <div className="divide-y divide-[var(--line)]">
              {knowledge.map((item) => (
                <button
                  key={item.id}
                  type="button"
                  onClick={() => setSelectedId(item.id)}
                  className={`block w-full px-5 py-4 text-left hover:bg-white/70 ${selected?.id === item.id ? 'bg-teal-50/60' : ''}`}
                >
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="text-sm font-semibold">{item.title}</p>
                    <StateBadge value={item.state} />
                    <StateBadge label="review" value={item.reviewState} />
                    <StateBadge label="verify" value={item.verificationState} />
                  </div>
                  <MetaLine
                    source={item.createdBy}
                    revision={item.currentRevisionNumber}
                    updatedAt={item.createdAt}
                    verification={item.verificationState}
                  />
                </button>
              ))}
            </div>
          )}
        </Panel>

        <div className="space-y-6">
          <Panel
            title="Revision history"
            subtitle="The current content version; earlier revisions remain in the project history."
          >
            {selected && revision ? (
              <div className="space-y-3 p-5">
                <p className="text-sm font-semibold">{selected.title}</p>
                <MetaLine
                  source={revision.actor ?? revision.sourceReference}
                  revision={revision.revisionNumber}
                  updatedAt={revision.recordedAt}
                  verification={selected.verificationState}
                />
                <pre className="overflow-auto rounded-xl bg-[var(--ink)] p-4 text-xs text-teal-50">{revision.content}</pre>
                <p className="text-xs text-[var(--muted)]">{revision.reason}</p>
              </div>
            ) : (
              <EmptyState label="Select an item with a revision to inspect history." />
            )}
          </Panel>

          <Panel title="All revisions" subtitle="Every saved version, oldest first. Select one to read it; only the current revision can be reviewed.">
            {revisions.length === 0 ? (
              <EmptyState label="No revisions yet." />
            ) : (
              <div className="divide-y divide-[var(--line)]">
                {revisions.map((entry) => (
                  <Row
                    key={entry.id}
                    title={`Revision ${entry.revisionNumber}${entry.id === selected?.currentRevisionId ? ' (current)' : ''}`}
                    subtitle={entry.reason}
                    onClick={() => setRevision(entry)}
                  />
                ))}
              </div>
            )}
          </Panel>

          <Panel
            title="Review this revision"
            subtitle="Your decision applies to the current revision and is recorded with your identity, the time and your comment."
          >
            {selected?.currentRevisionId ? (
              <div className="space-y-3 p-5">
                <p className="text-xs leading-relaxed text-[var(--muted)]">
                  Deciding on <strong>revision {selected.currentRevisionNumber}</strong> of “{selected.title}”. If someone saves a
                  newer revision first, your decision is refused and you must read the new one.
                </p>
                {viewingOlderRevision ? (
                  <p className="text-xs text-amber-800" role="alert">
                    You are reading an older revision. Your decision will apply to the current revision
                    {` ${selected.currentRevisionNumber}`}, not the one shown.
                  </p>
                ) : null}
                <label className="block space-y-1 text-xs font-semibold" htmlFor="review-comment">
                  <span>Comment <span className="font-normal text-[var(--muted)]">(optional to approve, required to reject)</span></span>
                  <textarea id="review-comment" className="field min-h-20" value={reviewComment} onChange={(e) => setReviewComment(e.target.value)} />
                </label>
                <div className="flex flex-wrap gap-2">
                  <button className="btn btn-primary" disabled={!live || busy || selected.state !== 'Proposed'} onClick={() => setConfirmApprove(true)}>
                    Approve revision {selected.currentRevisionNumber}
                  </button>
                  <button className="btn btn-ghost" disabled={!live || busy || selected.state !== 'Proposed' || !reviewComment.trim()} onClick={() => setConfirmReject(true)}>
                    Reject
                  </button>
                </div>
                {selected.state !== 'Proposed' ? (
                  <p className="text-xs text-[var(--muted)]">This revision is already {selected.state.toLowerCase()}. Save a new revision to review again.</p>
                ) : null}
              </div>
            ) : (
              <EmptyState label="Add a content revision before reviewing." />
            )}
            {reviews.length > 0 ? (
              <div className="divide-y divide-[var(--line)] border-t border-[var(--line)]">
                {[...reviews].reverse().map((entry) => (
                  <Row
                    key={`${entry.revisionId}-${entry.decidedAt}`}
                    title={`${entry.decision} revision ${entry.revisionNumber}`}
                    subtitle={`${entry.comment ?? 'No comment'} · by ${entry.reviewerId} · ${new Date(entry.decidedAt).toLocaleString()}`}
                  />
                ))}
              </div>
            ) : null}
          </Panel>

          <form onSubmit={createItem} className="panel space-y-3 rounded-2xl p-5">
            <h3 className="font-display text-lg font-bold">Create a knowledge item</h3>
            <p className="text-xs leading-relaxed text-[var(--muted)]">
              Give one planning idea a stable identity. Creating an item does not add its content; add a revision next.
            </p>
            <label className="block space-y-1 text-xs font-semibold" htmlFor="knowledge-key">
              <span>Key <span className="font-normal text-[var(--muted)]">(unique within this project)</span></span>
              <input id="knowledge-key" required className="field" placeholder="K-02" value={form.key} onChange={(e) => setForm({ ...form, key: e.target.value })} />
            </label>
            <label className="block space-y-1 text-xs font-semibold" htmlFor="knowledge-title">
              <span>Title <span className="font-normal text-[var(--muted)]">(human-readable claim)</span></span>
              <input id="knowledge-title" required className="field" placeholder="Create a task with a non-empty title" value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} />
            </label>
            <div className="grid gap-3 sm:grid-cols-[2fr_1fr]">
              <label className="block space-y-1 text-xs font-semibold" htmlFor="knowledge-type">
                <span>Published type ID</span>
                <input id="knowledge-type" required className="field" placeholder="plan.requirement | plan.goal | design.decision" value={form.typeId} onChange={(e) => setForm({ ...form, typeId: e.target.value })} />
              </label>
              <label className="block space-y-1 text-xs font-semibold" htmlFor="knowledge-schema">
                <span>Schema version</span>
                <input id="knowledge-schema" required className="field" placeholder="1.0" value={form.schemaVersion} onChange={(e) => setForm({ ...form, schemaVersion: e.target.value })} />
              </label>
            </div>
            <p className="text-xs text-[var(--muted)]">Type and version must already be published in the schema registry.</p>
            <button className="btn btn-primary" disabled={!live || busy}>Create item</button>
          </form>

          <form onSubmit={addRevision} className="panel space-y-3 rounded-2xl p-5">
            <h3 className="font-display text-lg font-bold">Add a content revision</h3>
            <p className="text-xs leading-relaxed text-[var(--muted)]">
              A new revision preserves the old one and resets prior review and verification. Use the selected item's schema.
            </p>
            <label className="block space-y-1 text-xs font-semibold" htmlFor="revision-content">
              <span>Content <span className="font-normal text-[var(--muted)]">(schema-valid planning data)</span></span>
              <textarea id="revision-content" required className="field min-h-28" value={revisionForm.content} onChange={(e) => setRevisionForm({ ...revisionForm, content: e.target.value })} />
            </label>
            <label className="block space-y-1 text-xs font-semibold" htmlFor="revision-reason">
              <span>Reason for this revision</span>
              <input id="revision-reason" required className="field" value={revisionForm.reason} onChange={(e) => setRevisionForm({ ...revisionForm, reason: e.target.value })} />
            </label>
            <button className="btn btn-primary" disabled={!live || busy || !selected}>Save revision</button>
          </form>
        </div>
      </div>

      <ConfirmDialog
        open={confirmApprove}
        title="Approve this revision?"
        body={`You are approving revision ${selected?.currentRevisionNumber ?? ''} of “${selected?.title ?? ''}”. This is recorded with your identity and comment. It does not verify evidence and does not carry over to later revisions.`}
        confirmLabel="Approve"
        busy={busy}
        onCancel={() => setConfirmApprove(false)}
        onConfirm={() => decide('approve')}
      />
      <ConfirmDialog
        open={confirmReject}
        title="Reject this revision?"
        body="The rejection and your reason are recorded. The author must save a new revision to continue."
        confirmLabel="Reject"
        danger
        busy={busy}
        onCancel={() => setConfirmReject(false)}
        onConfirm={() => decide('reject')}
      />
    </div>
  );
}
