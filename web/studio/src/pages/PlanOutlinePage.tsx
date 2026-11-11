import { FormEvent, useState } from 'react';
import { Link } from 'react-router-dom';
import { StateBadge } from '../components/StateBadge';
import { apiFetch } from '../lib/api';
import { useWorkspace } from '../workspace/WorkspaceContext';

export function PlanOutlinePage() {
  const { tenantId, project, live, knowledge, workItems, outline, outlineError, run, refresh, busy } = useWorkspace();
  const [itemId, setItemId] = useState('');
  const [parentId, setParentId] = useState('');
  const [workId, setWorkId] = useState('');
  const [basisId, setBasisId] = useState('');
  const [proposalParent, setProposalParent] = useState('');
  const [proposalKey, setProposalKey] = useState('');
  const [proposalTitle, setProposalTitle] = useState('');
  const [proposalType, setProposalType] = useState('');
  const [rationale, setRationale] = useState('');
  const [acceptedItems, setAcceptedItems] = useState<Record<string, string>>({});
  const [message, setMessage] = useState('');
  const base = `/tenants/${tenantId}/projects/${project?.id}/plan`;
  const byId = new Map(knowledge.map((item) => [item.id, item]));
  const children = new Map<string, typeof outline.nodes>();
  outline.nodes.forEach((node) => {
    const key = node.parentItemId ?? '';
    children.set(key, [...(children.get(key) ?? []), node]);
  });
  const ordered = (parent: string, depth: number): { id: string; depth: number }[] =>
    (children.get(parent) ?? []).flatMap((node) => [{ id: node.itemId, depth }, ...ordered(node.itemId, depth + 1)]);
  const tree = ordered('', 0);
  const candidates = knowledge.filter((item) => item.currentRevisionId && !outline.nodes.some((node) => node.itemId === item.id));
  const reviewed = knowledge.filter((item) => item.currentRevisionId && item.reviewState === 'Reviewed' &&
    outline.nodes.some((node) => node.itemId === item.id));

  async function submit(event: FormEvent, path: string, body: object, success: string) {
    event.preventDefault();
    if (!live) return;
    const result = await run(() => apiFetch(`${base}${path}`, { method: 'POST', body: JSON.stringify(body) }));
    if (result) {
      setMessage(success);
      await refresh();
    }
  }

  return (
    <div className="space-y-6">
      <section className="panel rounded-2xl p-5">
        <p className="text-xs font-semibold uppercase tracking-wider text-[var(--signal)]">Application decomposition</p>
        <h2 className="font-display mt-2 text-2xl font-bold">Plan outline</h2>
        <p className="mt-2 max-w-3xl text-sm text-[var(--muted)]">
          Start with one application idea, then attach revised Knowledge beneath it. Each item has one permanent
          outline parent. Semantic Relations remain separate; Work links pin an approved basis to an exact revision.
          Agent expansions stay proposals until a project owner or admin reviews them.
        </p>
        {!live && <p className="mt-2 text-sm text-amber-800">Connect a live project to edit its outline.</p>}
        {outlineError && <p className="mt-2 text-sm text-rose-800" role="alert">Could not load the project outline. Check the API and your project access before editing.</p>}
        {message && <p className="mt-2 text-sm text-teal-800" role="status">{message}</p>}
      </section>

      <section className="panel rounded-2xl p-5">
        <h3 className="font-display text-lg font-bold">Knowledge hierarchy</h3>
        {!tree.length && !outlineError && <p className="mt-3 text-sm text-[var(--muted)]">No root yet. Create an application idea on the Knowledge page and add its first revision, then attach it here.</p>}
        <ol className="mt-3 space-y-2">
          {tree.map(({ id, depth }) => {
            const item = byId.get(id);
            const waiting = item?.state === 'Proposed';
            return (
              <li
                key={id}
                className={`flex flex-wrap items-center justify-between gap-2 rounded-lg border bg-white px-3 py-2 text-sm ${waiting ? 'border-amber-300' : 'border-[var(--line)]'}`}
                style={{ marginLeft: `${Math.min(depth, 8) * 20}px` }}
              >
                <span className="min-w-0">
                  <span className="font-mono text-xs text-[var(--muted)]">{item?.key ?? id}</span>{' '}
                  <span className="font-semibold">{item?.title ?? 'Knowledge item'}</span>
                </span>
                <span className="flex items-center gap-2">
                  {item ? <StateBadge value={item.state} /> : null}
                  {item ? (
                    <Link to={`/knowledge?item=${item.id}`} className="text-xs font-semibold text-[var(--signal)] hover:underline">
                      {waiting ? 'Review →' : 'Open →'}
                    </Link>
                  ) : null}
                </span>
              </li>
            );
          })}
        </ol>
        {tree.length > 0 ? (
          <p className="mt-3 text-xs text-[var(--muted)]">
            {tree.filter(({ id }) => byId.get(id)?.state === 'Proposed').length} of {tree.length} outline items are waiting for review.
            All items must be approved before the plan can be locked for agents.
          </p>
        ) : null}
        <form className="mt-5 grid gap-3 sm:grid-cols-3" onSubmit={(event) => submit(event, '/outline', {
          itemId, parentItemId: parentId || null, position: outline.nodes.length,
        }, 'Knowledge attached to the outline.')}>
          <label className="text-xs font-semibold">Revised Knowledge item
            <select className="field mt-1 w-full" value={itemId} onChange={(event) => setItemId(event.target.value)} required>
              <option value="">Select item</option>
              {candidates.map((item) => <option key={item.id} value={item.id}>{item.key} · {item.title}</option>)}
            </select>
          </label>
          <label className="text-xs font-semibold">Parent
            <select className="field mt-1 w-full" value={parentId} onChange={(event) => setParentId(event.target.value)} required={outline.nodes.length > 0}>
              {!outline.nodes.length && <option value="">Application root</option>}
              {!!outline.nodes.length && <option value="">Select parent</option>}
              {tree.map(({ id, depth }) => <option key={id} value={id}>{'· '.repeat(depth)}{byId.get(id)?.key ?? id}</option>)}
            </select>
          </label>
          <button className="self-end rounded-lg bg-[var(--signal)] px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" disabled={!live || outlineError || busy || !candidates.length}>Attach item</button>
        </form>
        <Link to="/knowledge" className="mt-3 inline-block text-sm font-semibold text-[var(--signal)] hover:underline">Create or revise Knowledge</Link>
      </section>

      <section className="panel rounded-2xl p-5">
        <h3 className="font-display text-lg font-bold">Expansion proposals</h3>
        <p className="mt-1 text-xs text-[var(--muted)]">A proposed child does not become part of the governed outline automatically. Acceptance requires a matching Knowledge item with a human-reviewed revision.</p>
        <form className="mt-4 grid gap-3 sm:grid-cols-2" onSubmit={(event) => submit(event, '/proposals', {
          parentItemId: proposalParent, key: proposalKey, title: proposalTitle, typeId: proposalType, rationale,
        }, 'Expansion submitted for review.')}>
          <label className="text-xs font-semibold">Parent Knowledge
            <select className="field mt-1 w-full" value={proposalParent} onChange={(event) => setProposalParent(event.target.value)} required>
              <option value="">Select parent</option>
              {tree.map(({ id }) => <option key={id} value={id}>{byId.get(id)?.key ?? id}</option>)}
            </select>
          </label>
          <label className="text-xs font-semibold">Suggested key<input className="field mt-1 w-full" value={proposalKey} onChange={(event) => setProposalKey(event.target.value)} maxLength={100} required /></label>
          <label className="text-xs font-semibold">Title<input className="field mt-1 w-full" value={proposalTitle} onChange={(event) => setProposalTitle(event.target.value)} maxLength={200} required /></label>
          <label className="text-xs font-semibold">Registered type ID<input className="field mt-1 w-full" value={proposalType} onChange={(event) => setProposalType(event.target.value)} maxLength={100} required /></label>
          <label className="text-xs font-semibold sm:col-span-2">Rationale<textarea className="field mt-1 w-full" value={rationale} onChange={(event) => setRationale(event.target.value)} maxLength={2000} required /></label>
          <button className="rounded-lg bg-[var(--signal)] px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" disabled={!live || outlineError || busy || !tree.length}>Propose child</button>
        </form>
        <ul className="mt-5 space-y-3">
          {outline.proposals.map((proposal) => (
            <li key={proposal.id} className="rounded-lg border border-[var(--line)] bg-white p-3 text-sm">
              <p className="font-semibold">{proposal.key} · {proposal.title} <span className="text-xs text-[var(--muted)]">({proposal.status})</span></p>
              <p className="mt-1 text-xs text-[var(--muted)]">{proposal.rationale}</p>
              {proposal.status === 'Proposed' && (
                <div className="mt-3 flex flex-wrap items-end gap-2">
                  <label className="text-xs font-semibold">Matching reviewed item
                    <select className="field mt-1 block" value={acceptedItems[proposal.id] ?? ''} onChange={(event) => setAcceptedItems({ ...acceptedItems, [proposal.id]: event.target.value })}>
                      <option value="">Select for acceptance</option>
                      {candidates.filter((item) => item.reviewState === 'Reviewed' && item.key === proposal.key &&
                        item.title === proposal.title && item.typeId === proposal.typeId).map((item) =>
                        <option key={item.id} value={item.id}>{item.key}</option>)}
                    </select>
                  </label>
                  <button className="rounded-lg border border-teal-700 px-3 py-2 text-xs font-semibold text-teal-800 disabled:opacity-50" disabled={!live || outlineError || busy || !acceptedItems[proposal.id]} onClick={(event) => submit(event, `/proposals/${proposal.id}/decide`, { acceptedItemId: acceptedItems[proposal.id] }, 'Proposal accepted.')}>Accept</button>
                  <button className="rounded-lg border border-[var(--line)] px-3 py-2 text-xs font-semibold disabled:opacity-50" disabled={!live || outlineError || busy} onClick={(event) => submit(event, `/proposals/${proposal.id}/decide`, { acceptedItemId: null }, 'Proposal rejected.')}>Reject</button>
                </div>
              )}
            </li>
          ))}
        </ul>
      </section>

      <section className="panel rounded-2xl p-5">
        <h3 className="font-display text-lg font-bold">Revision-pinned delivery</h3>
        <form className="mt-3 grid gap-3 sm:grid-cols-3" onSubmit={(event) => submit(event, '/work-links', {
          workItemId: workId, knowledgeItemId: basisId, revisionId: byId.get(basisId)?.currentRevisionId,
        }, 'Work linked to a reviewed Knowledge revision.')}>
          <label className="text-xs font-semibold">Work item<select className="field mt-1 w-full" value={workId} onChange={(event) => setWorkId(event.target.value)} required>
            <option value="">Select work</option>{workItems.map((item) => <option key={item.id} value={item.id}>{item.key}</option>)}
          </select></label>
          <label className="text-xs font-semibold">Reviewed Knowledge<select className="field mt-1 w-full" value={basisId} onChange={(event) => setBasisId(event.target.value)} required>
            <option value="">Select basis</option>{reviewed.map((item) => <option key={item.id} value={item.id}>{item.key} · revision {item.currentRevisionNumber}</option>)}
          </select></label>
          <button className="self-end rounded-lg bg-[var(--signal)] px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" disabled={!live || outlineError || busy || !reviewed.length}>Pin revision</button>
        </form>
        <ul className="mt-4 space-y-1 text-sm">
          {outline.links.map((link) => <li key={link.id}>{workItems.find((item) => item.id === link.workItemId)?.key ?? link.workItemId} ← {byId.get(link.knowledgeItemId)?.key ?? link.knowledgeItemId} <span className="font-mono text-xs text-[var(--muted)]">({link.revisionId.slice(0, 8)})</span>
            {byId.get(link.knowledgeItemId)?.currentRevisionId !== link.revisionId && <span className="ml-2 font-semibold text-amber-800">Older revision · review basis</span>}
          </li>)}
        </ul>
      </section>
    </div>
  );
}
