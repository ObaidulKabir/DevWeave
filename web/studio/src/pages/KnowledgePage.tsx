import { FormEvent, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { ContentEditor } from '../components/ContentEditor';
import { ContentView } from '../components/ContentView';
import { EmptyState, Panel, Row } from '../components/Panel';
import { RevisionDiff } from '../components/RevisionDiff';
import { StateBadge } from '../components/StateBadge';
import { apiFetch } from '../lib/api';
import { diffContent } from '../lib/content';
import { formatWhen, shortId } from '../lib/format';
import type { KnowledgeItem, KnowledgeRevision, ReviewDecision, SearchHit } from '../lib/types';
import { useWorkspace } from '../workspace/WorkspaceContext';

type Filter = 'review' | 'approved' | 'rejected' | 'all';
type Tab = 'read' | 'changes' | 'history';

interface SchemaChoice {
  namespace: string;
  version: string;
}

const FILTERS: { id: Filter; label: string }[] = [
  { id: 'review', label: 'Needs review' },
  { id: 'approved', label: 'Approved' },
  { id: 'rejected', label: 'Rejected' },
  { id: 'all', label: 'All' },
];

const EMPTY_CONTENT = '{\n  "statement": "",\n  "acceptance": "",\n  "rationale": "",\n  "source": ""\n}';

function matchesFilter(item: KnowledgeItem, filter: Filter): boolean {
  if (filter === 'all') return true;
  if (filter === 'review') return item.state === 'Proposed' && Boolean(item.currentRevisionId);
  if (filter === 'approved') return item.state === 'Approved';
  return item.state === 'Rejected';
}

export function KnowledgePage() {
  const { tenantId, project, live, busy, run, knowledge, setKnowledge } = useWorkspace();
  const [params, setParams] = useSearchParams();
  const [filter, setFilter] = useState<Filter>('review');
  const [tab, setTab] = useState<Tab>('read');
  const [viewedId, setViewedId] = useState<string | null>(null);
  const [compareId, setCompareId] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [hits, setHits] = useState<SearchHit[]>([]);
  const [schemas, setSchemas] = useState<SchemaChoice[]>([]);
  const [form, setForm] = useState({ key: '', title: '', description: '', typeId: 'plan.requirement', schemaVersion: '1.0' });
  const [editorContent, setEditorContent] = useState(EMPTY_CONTENT);
  const [editorReason, setEditorReason] = useState('');
  const [editorOpen, setEditorOpen] = useState(false);
  const [confirmApprove, setConfirmApprove] = useState(false);
  const [confirmReject, setConfirmReject] = useState(false);
  const [reviewComment, setReviewComment] = useState('');
  const [touchedReject, setTouchedReject] = useState(false);
  const [revisions, setRevisions] = useState<KnowledgeRevision[]>([]);
  const [reviews, setReviews] = useState<ReviewDecision[]>([]);
  const [notice, setNotice] = useState<string | null>(null);

  const selectedId = params.get('item');
  const selected = knowledge.find((item) => item.id === selectedId) ?? null;

  const counts = useMemo(
    () => ({
      review: knowledge.filter((item) => matchesFilter(item, 'review')).length,
      approved: knowledge.filter((item) => matchesFilter(item, 'approved')).length,
      rejected: knowledge.filter((item) => matchesFilter(item, 'rejected')).length,
      all: knowledge.length,
    }),
    [knowledge],
  );
  const visible = knowledge.filter((item) => matchesFilter(item, filter));

  function select(id: string | null) {
    const next = new URLSearchParams(params);
    if (id) next.set('item', id);
    else next.delete('item');
    setParams(next, { replace: true });
    setViewedId(null);
    setCompareId(null);
    setTab('read');
    setReviewComment('');
    setTouchedReject(false);
    setNotice(null);
  }

  // Open the first item that needs review when nothing is chosen, so a reviewer lands on work rather than an empty pane.
  useEffect(() => {
    if (selectedId || knowledge.length === 0) return;
    const first = knowledge.find((item) => matchesFilter(item, 'review')) ?? knowledge[0];
    select(first.id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [knowledge, selectedId]);

  useEffect(() => {
    apiFetch<SchemaChoice[]>('/schemas/discover')
      .then((list) => setSchemas(Array.isArray(list) ? list : []))
      .catch(() => setSchemas([]));
  }, []);

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
        setRevisions([...nextRevisions].sort((a, b) => a.revisionNumber - b.revisionNumber));
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

  const current = revisions.find((entry) => entry.id === selected?.currentRevisionId) ?? null;
  const viewed = revisions.find((entry) => entry.id === viewedId) ?? current;
  const viewingOlder = Boolean(viewed && current && viewed.id !== current.id);

  // Default comparison: the last revision a human approved (what they already agreed to), else the one before.
  const lastApprovedBefore = useMemo(() => {
    if (!viewed) return null;
    const approvedNumbers = reviews
      .filter((entry) => entry.decision === 'Approved' && entry.revisionNumber < viewed.revisionNumber)
      .map((entry) => entry.revisionNumber);
    if (approvedNumbers.length === 0) return null;
    const best = Math.max(...approvedNumbers);
    return revisions.find((entry) => entry.revisionNumber === best) ?? null;
  }, [reviews, revisions, viewed]);
  const previous = viewed ? revisions.filter((entry) => entry.revisionNumber < viewed.revisionNumber).at(-1) ?? null : null;
  const baseline = revisions.find((entry) => entry.id === compareId) ?? lastApprovedBefore ?? previous;
  const changes = viewed && baseline ? diffContent(baseline.content, viewed.content) : null;
  const olderRevisions = viewed ? revisions.filter((entry) => entry.revisionNumber < viewed.revisionNumber) : [];

  const nextToReview = knowledge.find((item) => matchesFilter(item, 'review') && item.id !== selected?.id) ?? null;
  const canDecide = live && !busy && selected?.state === 'Proposed' && Boolean(selected.currentRevisionId);
  const rejectMissingReason = touchedReject && !reviewComment.trim();

  async function createItem(event: FormEvent) {
    event.preventDefault();
    if (!tenantId || !project) return;
    const created = await run(() =>
      apiFetch<KnowledgeItem>(`/tenants/${tenantId}/projects/${project.id}/knowledge`, {
        method: 'POST',
        body: JSON.stringify({
          key: form.key,
          title: form.title,
          description: form.description.trim() || null,
          classification: 'Internal',
          representationKind: 'StructuredData',
          typeId: form.typeId,
          schemaVersion: form.schemaVersion,
          createdBy: '00000000-0000-0000-0000-000000000000',
        }),
      }),
    );
    if (created) {
      setKnowledge((list) => [created, ...list]);
      setFilter('all');
      select(created.id);
      setEditorOpen(true);
      setForm((value) => ({ ...value, key: '', title: '', description: '' }));
      setNotice('Item created. Add its first revision below.');
    }
  }

  async function saveRevision(event: FormEvent) {
    event.preventDefault();
    if (!tenantId || !selected) return;
    const created = await run(() =>
      apiFetch<KnowledgeRevision>(`/tenants/${tenantId}/knowledge/${selected.id}/revisions`, {
        method: 'POST',
        body: JSON.stringify({
          expectedCurrentRevisionNumber: selected.currentRevisionNumber,
          schemaVersion: selected.schemaVersion,
          content: editorContent,
          actor: '00000000-0000-0000-0000-000000000000',
          reason: editorReason,
          sourceReference: null,
          supersededRevisionId: null,
        }),
      }),
    );
    if (created) {
      setKnowledge((list) =>
        list.map((item) =>
          item.id === selected.id
            ? { ...item, currentRevisionId: created.id, currentRevisionNumber: created.revisionNumber, state: 'Proposed', reviewState: 'Pending' }
            : item,
        ),
      );
      setViewedId(null);
      setEditorOpen(false);
      setEditorReason('');
      setTab('changes');
      setNotice(`Saved revision ${created.revisionNumber}. It now needs review.`);
    }
  }

  function startEdit() {
    if (current) setEditorContent(current.content);
    setEditorOpen(true);
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
    const decidedNumber = selected.currentRevisionNumber;
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
      setKnowledge((list) => list.map((item) => (item.id === updated.id ? updated : item)));
      setReviewComment('');
      setTouchedReject(false);
      setNotice(`Revision ${decidedNumber} ${decision === 'approve' ? 'approved' : 'rejected'}.`);
    }
  }

  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">Plan · Knowledge &amp; review</p>
          <h2 className="font-display mt-1 text-2xl font-bold">What the project knows, and what still needs a decision</h2>
        </div>
        <form onSubmit={runSearch} className="flex w-full gap-2 sm:w-auto">
          <label className="sr-only" htmlFor="knowledge-search">Search knowledge</label>
          <input id="knowledge-search" className="field sm:w-72" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search project knowledge…" />
          <button className="btn btn-ghost" disabled={!live || busy}>Search</button>
        </form>
      </header>

      <details className="panel rounded-2xl px-5 py-3 text-sm">
        <summary className="cursor-pointer font-semibold">How this works</summary>
        <p className="mt-2 max-w-3xl leading-relaxed text-[var(--muted)]">
          Each requirement, constraint, decision or success criterion is its own typed item with a stable key. Edits never overwrite:
          they add a numbered revision, and that resets review. Approving means a person accepted that exact revision as the plan; it
          does not prove the work is done. This space holds planning information, not source code or credentials.
        </p>
      </details>

      {hits.length > 0 ? (
        <Panel title="Search results" subtitle="Only results you are allowed to see.">
          <div className="divide-y divide-[var(--line)]">
            {hits.map((hit) => (
              <Row
                key={`${hit.itemId}-${hit.revisionId}`}
                title={hit.title}
                subtitle={`${hit.typeId} · ${hit.lifecycle}/${hit.review}/${hit.verification}`}
                onClick={() => {
                  setFilter('all');
                  select(hit.itemId);
                }}
              />
            ))}
          </div>
        </Panel>
      ) : null}

      <div className="grid gap-5 lg:grid-cols-[minmax(280px,0.8fr)_1.6fr]">
        {/* ---- Queue ---- */}
        <div className="space-y-4">
          <section className="panel overflow-hidden rounded-2xl" aria-labelledby="queue-title">
            <header className="border-b border-[var(--line)] px-4 py-3">
              <h3 id="queue-title" className="font-display text-base font-bold">Review queue</h3>
              <div className="mt-3 flex flex-wrap gap-1.5" role="tablist" aria-label="Filter items">
                {FILTERS.map(({ id, label }) => (
                  <button
                    key={id}
                    type="button"
                    role="tab"
                    aria-selected={filter === id}
                    onClick={() => setFilter(id)}
                    className={`rounded-full border px-3 py-1 text-xs font-semibold ${
                      filter === id ? 'border-[var(--signal)] bg-[var(--signal)] text-white' : 'border-[var(--line)] bg-white hover:bg-slate-50'
                    }`}
                  >
                    {label} <span className="opacity-70">{counts[id]}</span>
                  </button>
                ))}
              </div>
            </header>
            {visible.length === 0 ? (
              <EmptyState label={filter === 'review' ? 'Nothing is waiting for review. 🎉' : 'No items in this view.'} />
            ) : (
              <ul className="max-h-[28rem] divide-y divide-[var(--line)] overflow-auto">
                {visible.map((item) => (
                  <li key={item.id}>
                    <button
                      type="button"
                      aria-current={selected?.id === item.id ? 'true' : undefined}
                      onClick={() => select(item.id)}
                      className={`block w-full px-4 py-3 text-left hover:bg-white/80 ${selected?.id === item.id ? 'bg-teal-50/70 shadow-[inset_3px_0_0_var(--signal)]' : ''}`}
                    >
                      <div className="flex items-center justify-between gap-2">
                        <span className="font-mono text-[11px] text-[var(--muted)]">{item.key} · rev {item.currentRevisionNumber}</span>
                        <StateBadge value={item.state} />
                      </div>
                      <p className="mt-1 text-sm font-semibold leading-snug">{item.title}</p>
                      {item.description ? (
                        <p className="mt-1 line-clamp-2 text-[11px] leading-relaxed text-[var(--muted)]">{item.description}</p>
                      ) : null}
                      <p className="mt-0.5 text-[11px] text-[var(--muted)]">{item.typeId}</p>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <details className="panel rounded-2xl" open={knowledge.length === 0}>
            <summary className="cursor-pointer px-4 py-3 text-sm font-semibold">New knowledge item</summary>
            <form onSubmit={createItem} className="space-y-3 border-t border-[var(--line)] p-4">
              <label className="block space-y-1 text-xs font-semibold" htmlFor="knowledge-key">
                <span>Key <span className="font-normal text-[var(--muted)]">(unique in this project)</span></span>
                <input id="knowledge-key" required className="field" placeholder="K-02" value={form.key} onChange={(e) => setForm({ ...form, key: e.target.value })} />
              </label>
              <label className="block space-y-1 text-xs font-semibold" htmlFor="knowledge-title">
                <span>Title <span className="font-normal text-[var(--muted)]">(a claim a person can approve)</span></span>
                <input id="knowledge-title" required className="field" placeholder="A task must have a non-empty title" value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} />
              </label>
              <label className="block space-y-1 text-xs font-semibold" htmlFor="knowledge-description">
                <span>Details <span className="font-normal text-[var(--muted)]">(optional context for reviewers)</span></span>
                <textarea
                  id="knowledge-description"
                  className="field min-h-20"
                  placeholder="Why this item exists, who it serves, and what a reviewer should look for."
                  value={form.description}
                  onChange={(e) => setForm({ ...form, description: e.target.value })}
                />
              </label>
              {schemas.length > 0 ? (
                <label className="block space-y-1 text-xs font-semibold" htmlFor="knowledge-type">
                  <span>Type</span>
                  <select
                    id="knowledge-type"
                    className="field"
                    value={`${form.typeId}@${form.schemaVersion}`}
                    onChange={(e) => {
                      const [typeId, schemaVersion] = e.target.value.split('@');
                      setForm({ ...form, typeId, schemaVersion });
                    }}
                  >
                    {!schemas.some((s) => s.namespace === form.typeId && s.version === form.schemaVersion) ? (
                      <option value={`${form.typeId}@${form.schemaVersion}`}>{form.typeId} v{form.schemaVersion}</option>
                    ) : null}
                    {schemas.map((s) => (
                      <option key={`${s.namespace}@${s.version}`} value={`${s.namespace}@${s.version}`}>
                        {s.namespace} · v{s.version}
                      </option>
                    ))}
                  </select>
                </label>
              ) : (
                <div className="grid gap-3 sm:grid-cols-[2fr_1fr]">
                  <label className="block space-y-1 text-xs font-semibold" htmlFor="knowledge-type">
                    <span>Published type ID</span>
                    <input id="knowledge-type" required className="field" value={form.typeId} onChange={(e) => setForm({ ...form, typeId: e.target.value })} />
                  </label>
                  <label className="block space-y-1 text-xs font-semibold" htmlFor="knowledge-schema">
                    <span>Schema version</span>
                    <input id="knowledge-schema" required className="field" value={form.schemaVersion} onChange={(e) => setForm({ ...form, schemaVersion: e.target.value })} />
                  </label>
                </div>
              )}
              <button className="btn btn-primary" disabled={!live || busy}>Create item</button>
            </form>
          </details>
        </div>

        {/* ---- Workbench ---- */}
        <div className="space-y-4">
          {notice ? (
            <p className="rounded-xl border border-teal-200 bg-teal-50 px-4 py-2 text-sm text-teal-900" role="status">{notice}</p>
          ) : null}

          {!selected ? (
            <section className="panel rounded-2xl">
              <EmptyState label="Choose an item from the queue to read and review it." />
            </section>
          ) : (
            <>
              <section className="panel rounded-2xl p-5" aria-labelledby="item-title">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="font-mono text-xs text-[var(--muted)]">{selected.key} · {selected.typeId} v{selected.schemaVersion}</p>
                    <h3 id="item-title" className="font-display mt-1 text-xl font-bold leading-snug">{selected.title}</h3>
                  </div>
                  <button type="button" className="btn btn-ghost" onClick={startEdit} disabled={!live || busy}>Edit this item</button>
                </div>
                <div className="mt-3 flex flex-wrap items-center gap-2">
                  <StateBadge label="status" value={selected.state} />
                  <StateBadge label="review" value={selected.reviewState} />
                  <StateBadge label="verify" value={selected.verificationState} />
                </div>
                <section className="mt-4 rounded-xl border border-[var(--line)] bg-[var(--paper)] px-4 py-3" aria-labelledby="item-details-title">
                  <h4 id="item-details-title" className="text-xs font-semibold uppercase tracking-wide text-[var(--muted)]">Details</h4>
                  {selected.description?.trim() ? (
                    <p className="mt-2 whitespace-pre-wrap text-sm leading-relaxed text-[var(--ink)]">{selected.description}</p>
                  ) : (
                    <p className="mt-2 text-sm italic text-[var(--muted)]">No details yet. Add a description when creating the item so reviewers know the context without opening revision content.</p>
                  )}
                </section>

                {revisions.length > 0 ? (
                  <div className="mt-4 flex flex-wrap items-center gap-3 text-xs">
                    <label className="font-semibold" htmlFor="revision-picker">Viewing</label>
                    <select
                      id="revision-picker"
                      className="field w-auto py-1.5 text-xs"
                      value={viewed?.id ?? ''}
                      onChange={(e) => {
                        setViewedId(e.target.value);
                        setCompareId(null);
                      }}
                    >
                      {[...revisions].reverse().map((entry) => (
                        <option key={entry.id} value={entry.id}>
                          Revision {entry.revisionNumber}{entry.id === current?.id ? ' (current)' : ''} · {formatWhen(entry.recordedAt)}
                        </option>
                      ))}
                    </select>
                    {viewed ? <span className="text-[var(--muted)]">by {shortId(viewed.actor ?? viewed.sourceReference)} — “{viewed.reason}”</span> : null}
                  </div>
                ) : null}
                {viewingOlder && current ? (
                  <p className="mt-3 rounded-lg bg-amber-50 px-3 py-2 text-xs text-amber-900" role="alert">
                    You are reading an older revision. Decisions apply to the current revision ({current.revisionNumber}).{' '}
                    <button className="font-semibold underline" onClick={() => setViewedId(null)}>Switch to current</button>
                  </p>
                ) : null}
              </section>

              <section className="panel overflow-hidden rounded-2xl">
                <div className="flex gap-1 border-b border-[var(--line)] px-3 pt-2" role="tablist" aria-label="Revision views">
                  {([
                    ['read', 'Read'],
                    ['changes', baseline ? 'What changed' : 'What changed'],
                    ['history', `History (${reviews.length})`],
                  ] as [Tab, string][]).map(([id, label]) => (
                    <button
                      key={id}
                      type="button"
                      role="tab"
                      aria-selected={tab === id}
                      onClick={() => setTab(id)}
                      className={`-mb-px rounded-t-lg border-x border-t px-4 py-2 text-sm font-semibold ${
                        tab === id ? 'border-[var(--line)] bg-white' : 'border-transparent text-[var(--muted)] hover:text-[var(--ink)]'
                      }`}
                    >
                      {label}
                    </button>
                  ))}
                </div>
                <div className="p-5">
                  {!viewed ? (
                    <p className="text-sm text-[var(--muted)]">This item has no content yet. Use “Edit this item” to add the first revision.</p>
                  ) : tab === 'read' ? (
                    <ContentView raw={viewed.content} changed={changes} />
                  ) : tab === 'changes' ? (
                    baseline ? (
                      <div className="space-y-4">
                        <label className="flex flex-wrap items-center gap-2 text-xs font-semibold" htmlFor="compare-picker">
                          Compared with
                          <select id="compare-picker" className="field w-auto py-1.5 text-xs" value={baseline.id} onChange={(e) => setCompareId(e.target.value)}>
                            {[...olderRevisions].reverse().map((entry) => (
                              <option key={entry.id} value={entry.id}>
                                Revision {entry.revisionNumber}{entry.id === lastApprovedBefore?.id ? ' (last approved)' : ''}
                              </option>
                            ))}
                          </select>
                        </label>
                        <RevisionDiff changes={changes} fromLabel={`Revision ${baseline.revisionNumber}`} toLabel={`Revision ${viewed.revisionNumber}`} />
                      </div>
                    ) : (
                      <p className="text-sm text-[var(--muted)]">This is the first revision, so there is nothing earlier to compare with.</p>
                    )
                  ) : reviews.length === 0 ? (
                    <p className="text-sm text-[var(--muted)]">No one has approved or rejected a revision of this item yet.</p>
                  ) : (
                    <ol className="space-y-3">
                      {[...reviews].reverse().map((entry) => (
                        <li key={`${entry.revisionId}-${entry.decidedAt}`} className="rounded-xl border border-[var(--line)] bg-white px-4 py-3">
                          <div className="flex flex-wrap items-center gap-2">
                            <StateBadge value={entry.decision} />
                            <span className="text-sm font-semibold">Revision {entry.revisionNumber}</span>
                            <span className="text-xs text-[var(--muted)]">by {shortId(entry.reviewerId)} · {formatWhen(entry.decidedAt)}</span>
                          </div>
                          <p className="mt-2 text-sm">{entry.comment ?? <span className="italic text-[var(--muted)]">No comment</span>}</p>
                        </li>
                      ))}
                    </ol>
                  )}
                </div>
              </section>

              {current ? (
                <section className="panel rounded-2xl p-5" aria-labelledby="decision-title">
                  <h3 id="decision-title" className="font-display text-base font-bold">Your decision on revision {current.revisionNumber}</h3>
                  {selected.state !== 'Proposed' ? (
                    <p className="mt-2 text-sm text-[var(--muted)]">
                      This revision is already {selected.state.toLowerCase()}. Saving a new revision starts a new review.
                    </p>
                  ) : (
                    <p className="mt-2 text-xs leading-relaxed text-[var(--muted)]">
                      Recorded with your identity, the time and your comment. If someone saves a newer revision first, your decision is refused so you never approve text you have not read.
                    </p>
                  )}
                  <label className="mt-3 block space-y-1 text-xs font-semibold" htmlFor="review-comment">
                    <span>Comment <span className="font-normal text-[var(--muted)]">(optional to approve, required to reject)</span></span>
                    <textarea
                      id="review-comment"
                      className="field min-h-20 font-normal"
                      aria-invalid={rejectMissingReason}
                      aria-describedby={rejectMissingReason ? 'reject-hint' : undefined}
                      value={reviewComment}
                      onChange={(e) => setReviewComment(e.target.value)}
                      disabled={selected.state !== 'Proposed'}
                    />
                  </label>
                  {rejectMissingReason ? <p id="reject-hint" className="mt-1 text-xs text-rose-800" role="alert">Say what needs to change so the author can fix it.</p> : null}
                  <div className="mt-3 flex flex-wrap items-center gap-2">
                    <button className="btn btn-primary" disabled={!canDecide} onClick={() => setConfirmApprove(true)}>
                      Approve revision {current.revisionNumber}
                    </button>
                    <button
                      className="btn btn-ghost"
                      disabled={!canDecide}
                      onClick={() => {
                        setTouchedReject(true);
                        if (reviewComment.trim()) setConfirmReject(true);
                      }}
                    >
                      Reject…
                    </button>
                    {nextToReview ? (
                      <button className="ml-auto text-sm font-semibold text-[var(--signal)] hover:underline" onClick={() => select(nextToReview.id)}>
                        Next to review: {nextToReview.key} →
                      </button>
                    ) : null}
                  </div>
                </section>
              ) : null}
            </>
          )}

          {selected && editorOpen ? (
            <form onSubmit={saveRevision} className="panel space-y-4 rounded-2xl p-5" aria-labelledby="editor-title">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <h3 id="editor-title" className="font-display text-base font-bold">New revision of {selected.key}</h3>
                  <p className="mt-1 text-xs text-[var(--muted)]">Saving keeps the old revision and sends this one back for review.</p>
                </div>
                <button type="button" className="text-xs font-semibold text-[var(--muted)] hover:underline" onClick={() => setEditorOpen(false)}>Cancel</button>
              </div>
              <ContentEditor value={editorContent} onChange={setEditorContent} />
              <label className="block space-y-1 text-xs font-semibold" htmlFor="revision-reason">
                <span>What changed and why</span>
                <input id="revision-reason" required className="field font-normal" placeholder="e.g. Clarified the empty-title rule" value={editorReason} onChange={(e) => setEditorReason(e.target.value)} />
              </label>
              <button className="btn btn-primary" disabled={!live || busy || !editorReason.trim() || editorContent === current?.content}>
                Save revision
              </button>
              {current && editorContent === current.content ? <p className="text-xs text-[var(--muted)]">No changes yet.</p> : null}
            </form>
          ) : null}
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
