import { Link } from 'react-router-dom';
import { EmptyState, Panel } from '../components/Panel';
import { StateBadge } from '../components/StateBadge';
import { MetaLine } from '../components/MetaLine';
import { shortId } from '../lib/format';
import { useWorkspace } from '../workspace/WorkspaceContext';

export function RelationsPage() {
  const { relationships, knowledge } = useWorkspace();
  const titleFor = (id: string) => knowledge.find((item) => item.id === id)?.title ?? shortId(id);

  return (
    <div className="space-y-6">
      <section className="panel rounded-2xl p-5" aria-labelledby="relations-explainer-title">
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">02 / Connect the plan</p>
        <h2 id="relations-explainer-title" className="font-display mt-2 text-xl font-bold">Relations explain how knowledge fits together</h2>
        <p className="mt-2 max-w-3xl text-sm leading-relaxed text-[var(--muted)]">
          Each arrow connects a source knowledge item to a target knowledge item in this project. The label tells you
          what the connection means. A relation type must be registered and active, with permitted source and target
          types. Work-item dependencies are separate from these knowledge relations.
        </p>
        <p className="mt-3 text-xs leading-relaxed text-[var(--muted)]">
          Example from TaskBoard Lite: K-04 "Persist tasks in PostgreSQL…" → supports → K-03 "List only the
          authenticated user tasks". Only use supports when that type is registered and endpoint types match.
          This screen currently explores existing relations only; creation and revocation require an authorized API client.
        </p>
        <Link to="/knowledge" className="mt-4 inline-flex text-sm font-semibold text-[var(--signal)] hover:underline">
          Review knowledge items first →
        </Link>
      </section>
      <Panel title="Relationship explorer" subtitle="Read each row from source to target; a revoked link is no longer active.">
        {relationships.length === 0 ? (
          <EmptyState label="No relationships loaded for this project. First create knowledge items, then connect them using a registered relation type through the API." />
        ) : (
          <div className="divide-y divide-[var(--line)]">
            {relationships.map((rel) => (
              <div key={rel.id} className="px-5 py-4">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="text-xs font-semibold uppercase tracking-wide text-[var(--signal)]">Knowledge relationship</span>
                  <StateBadge value={rel.status} />
                </div>
                <div className="mt-3 grid gap-2 sm:grid-cols-[1fr_auto_1fr] sm:items-center">
                  <div className="rounded-lg bg-[var(--paper)] px-3 py-2">
                    <p className="text-[11px] text-[var(--muted)]">Source</p>
                    <p className="text-sm font-semibold">{titleFor(rel.sourceItemId)}</p>
                  </div>
                  <div className="text-center text-xs font-semibold text-[var(--signal)]" aria-label={`relation ${rel.relationTypeCode}`}>
                    <span className="break-all font-mono">{rel.relationTypeCode}</span>
                    <span aria-hidden="true"> →</span>
                  </div>
                  <div className="rounded-lg bg-[var(--paper)] px-3 py-2">
                    <p className="text-[11px] text-[var(--muted)]">Target</p>
                    <p className="text-sm font-semibold">{titleFor(rel.targetItemId)}</p>
                  </div>
                </div>
                <MetaLine source={rel.createdBy} updatedAt={rel.createdAt} />
              </div>
            ))}
          </div>
        )}
      </Panel>
    </div>
  );
}
