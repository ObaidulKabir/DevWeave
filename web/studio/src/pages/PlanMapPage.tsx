import { useState } from 'react';
import { Link } from 'react-router-dom';
import { StateBadge } from '../components/StateBadge';
import type { KnowledgeItem, Relationship, WorkItem } from '../lib/types';
import { useWorkspace } from '../workspace/WorkspaceContext';

type Selection = { kind: 'knowledge' | 'relation' | 'work'; id: string } | null;
type Node = { id: string; key: string; title: string; x: number; y: number; kind: 'knowledge' | 'relation' | 'work'; state: string };

const KNOWLEDGE_X = 28;
const RELATION_X = 356;
const WORK_X = 800;
const NODE_WIDTH = 250;
const NODE_HEIGHT = 72;
const MAP_WIDTH = 1200;

function mentionsKey(description: string | undefined, key: string) {
  if (!description || !key) return false;
  const escaped = key.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  return new RegExp(`(^|[^a-zA-Z0-9_-])${escaped}(?=$|[^a-zA-Z0-9_-])`, 'i').test(description);
}

function MapNode({ node, selected, onSelect }: { node: Node; selected: boolean; onSelect: () => void }) {
  const color = node.kind === 'knowledge' ? '#0f766e' : node.kind === 'relation' ? '#525ba3' : '#b45309';
  const label = `${node.kind}: ${node.key}, ${node.title}. ${node.state}`;

  return (
    <g
      role="button"
      tabIndex={0}
      aria-label={label}
      aria-pressed={selected}
      onClick={onSelect}
      onKeyDown={(event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          onSelect();
        }
      }}
      className="cursor-pointer outline-none"
    >
      <title>{label}</title>
      <rect
        x={node.x} y={node.y} width={NODE_WIDTH} height={NODE_HEIGHT} rx={12}
        fill={selected ? '#effcf9' : '#fff'} stroke={selected ? '#0b5f59' : '#c9d4e3'}
        strokeWidth={selected ? 3 : 1.5}
      />
      <rect x={node.x} y={node.y} width={5} height={NODE_HEIGHT} rx={2} fill={color} />
      <text x={node.x + 16} y={node.y + 21} fill={color} fontSize={11} fontWeight={700}>
        {node.key.length > 33 ? `${node.key.slice(0, 32)}…` : node.key}
      </text>
      <text x={node.x + 16} y={node.y + 43} fill="#0b1320" fontSize={13} fontWeight={600}>
        {node.title.length > 29 ? `${node.title.slice(0, 28)}…` : node.title}
      </text>
      <text x={node.x + 16} y={node.y + 61} fill="#5b6b7c" fontSize={10}>
        {node.state.length > 38 ? `${node.state.slice(0, 37)}…` : node.state}
      </text>
    </g>
  );
}

export function PlanMapPage() {
  const { project, live, knowledge, relationships, workItems, milestones, outline } = useWorkspace();
  const [showRevoked, setShowRevoked] = useState(false);
  const [selection, setSelection] = useState<Selection>(null);

  const visibleRelations = relationships.filter((item) => showRevoked || item.status.toLowerCase() !== 'revoked');
  const outlineChildren = new Map<string, string[]>();
  outline.nodes.forEach((node) => outlineChildren.set(node.parentItemId ?? '',
    [...(outlineChildren.get(node.parentItemId ?? '') ?? []), node.itemId]));
  const walked = new Set<string>();
  const knowledgeOrder: { id: string; depth: number }[] = [];
  const visit = (id: string, depth: number) => {
    if (walked.has(id)) return;
    walked.add(id);
    knowledgeOrder.push({ id, depth });
    (outlineChildren.get(id) ?? []).forEach((child) => visit(child, depth + 1));
  };
  (outlineChildren.get('') ?? []).forEach((root) => visit(root, 0));
  knowledge.forEach((item) => visit(item.id, 0));
  const order = new Map(knowledgeOrder.map((entry, index) => [entry.id, { index, depth: entry.depth }]));
  const knowledgeNodes: Node[] = knowledge.map((item) => ({
    id: item.id, key: item.key, title: item.title,
    x: KNOWLEDGE_X + Math.min(order.get(item.id)?.depth ?? 0, 2) * 20,
    y: 112 + (order.get(item.id)?.index ?? 0) * 94,
    kind: 'knowledge', state: `${item.state} · review ${item.reviewState} · verify ${item.verificationState}`,
  }));
  const relationNodes: Node[] = visibleRelations.map((item, index) => ({
    id: item.id, key: item.relationTypeCode, title: item.status === 'Revoked' ? 'Revoked relation' : 'Directed relation',
    x: RELATION_X, y: 112 + index * 110, kind: 'relation', state: item.status,
  }));
  const workNodes: Node[] = [];
  const milestoneBands: { id: string; label: string; y: number; height: number }[] = [];
  let workY = 112;
  for (const group of [...milestones.map((item) => ({ id: item.id, label: `${item.key} · ${item.title}` })), { id: '', label: 'No milestone' }]) {
    const items = workItems.filter((item) =>
      group.id ? item.milestoneId === group.id : !milestones.some((milestone) => milestone.id === item.milestoneId),
    );
    if (!items.length && !group.id) continue;
    const start = workY;
    workY += 46;
    for (const item of items) {
      workNodes.push({
        id: item.id, key: item.key, title: item.title, x: WORK_X, y: workY,
        kind: 'work', state: `report ${item.reportedState} · verify ${item.verificationState}`,
      });
      workY += 94;
    }
    milestoneBands.push({ id: group.id, label: group.label, y: start, height: workY - start + 8 });
    workY += 18;
  }
  const height = Math.max(380, 160 + knowledge.length * 94, 160 + visibleRelations.length * 110, workY + 20);
  const knowledgeById = new Map(knowledgeNodes.map((item) => [item.id, item]));
  const workById = new Map(workNodes.map((item) => [item.id, item]));
  const links = workItems.flatMap((work) =>
    knowledge.filter((item) => mentionsKey(work.description, item.key) &&
      !outline.links.some((link) => link.workItemId === work.id && link.knowledgeItemId === item.id)).map((item) => ({
      source: knowledgeById.get(item.id), target: workById.get(work.id), id: `${item.id}:${work.id}`,
    })),
  );
  const selected = selection?.kind === 'knowledge'
    ? knowledge.find((item) => item.id === selection.id)
    : selection?.kind === 'relation'
      ? visibleRelations.find((item) => item.id === selection.id)
      : workItems.find((item) => item.id === selection?.id);
  const activeSelection = selected ? selection : null;
  const isEmphasized = (from: string, to: string) => !activeSelection || activeSelection.id === from || activeSelection.id === to;

  return (
    <div className="space-y-6">
      <section className="panel rounded-2xl p-5">
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">Project planning diagram</p>
        <h2 className="font-display mt-2 text-2xl font-bold">Plan map: {project?.name ?? 'current project'}</h2>
        <p className="mt-2 max-w-3xl text-sm leading-relaxed text-[var(--muted)]">
          Read left to right: knowledge describes intent, directed relations connect knowledge, and milestones organize delivery work.
          Select a node for its current state and context. This is a read-only view of the selected project, not an approved baseline.
        </p>
        {!live && <p className="mt-3 text-xs text-amber-800">Preview mode has no persisted plan. Connect a live tenant to display its records.</p>}
        <div className="mt-4 flex flex-wrap gap-x-5 gap-y-2 text-xs text-[var(--muted)]" aria-label="Diagram legend">
          <span><span className="font-semibold text-violet-700">Violet</span> reviewed revision pinned to Work (dashed when a newer revision exists)</span>
          <span><span className="font-semibold text-sky-700">Solid blue</span> stored outline parent → child</span>
          <span><span className="font-semibold text-teal-700">Solid teal</span> stored knowledge relation</span>
          <span><span className="font-semibold text-amber-700">Solid amber</span> stored work dependency</span>
          <span><span className="font-semibold text-slate-600">Dashed gray</span> knowledge key mentioned in work description, not a governed link</span>
        </div>
        <Link className="mt-3 inline-block text-sm font-semibold text-[var(--signal)] hover:underline" to="/plan-outline">Open the editable Plan outline</Link>
        <label className="mt-4 inline-flex items-center gap-2 text-xs font-semibold">
          <input type="checkbox" checked={showRevoked} onChange={(event) => setShowRevoked(event.target.checked)} />
          Show revoked relations
        </label>
      </section>

      <section className="panel overflow-hidden rounded-2xl" aria-label="Plan diagram">
        <div className="border-b border-[var(--line)] px-5 py-3 text-xs text-[var(--muted)]">
          {knowledge.length} knowledge items · {visibleRelations.length} visible relations · {milestones.length} milestones · {workItems.length} work items
          <span className="ml-2">Scroll sideways on a small screen.</span>
        </div>
        <div className="overflow-x-auto">
          <svg width={MAP_WIDTH} height={height} viewBox={`0 0 ${MAP_WIDTH} ${height}`} role="group" aria-label="Interactive project plan diagram with knowledge, relations, milestones and work">
            <defs>
              <marker id="plan-arrow" markerWidth="7" markerHeight="7" refX="6" refY="3.5" orient="auto">
                <path d="M0 0 L7 3.5 L0 7 Z" fill="#0f766e" />
              </marker>
              <marker id="work-arrow" markerWidth="7" markerHeight="7" refX="6" refY="3.5" orient="auto">
                <path d="M0 0 L7 3.5 L0 7 Z" fill="#b45309" />
              </marker>
            </defs>
            <rect width={MAP_WIDTH} height={height} fill="#f8fafc" />
            <text x={KNOWLEDGE_X} y={40} fill="#0f766e" fontSize={14} fontWeight={700}>01  KNOWLEDGE</text>
            <text x={RELATION_X} y={40} fill="#525ba3" fontSize={14} fontWeight={700}>02  RELATIONS</text>
            <text x={WORK_X} y={40} fill="#b45309" fontSize={14} fontWeight={700}>03  MILESTONES + WORK</text>
            <text x={KNOWLEDGE_X} y={66} fill="#5b6b7c" fontSize={11}>What and why</text>
            <text x={RELATION_X} y={66} fill="#5b6b7c" fontSize={11}>Source → type → target</text>
            <text x={WORK_X} y={66} fill="#5b6b7c" fontSize={11}>What gets delivered</text>

            {milestoneBands.map((band) => (
              <g key={band.id || 'unassigned'}>
                <rect x={WORK_X - 12} y={band.y - 8} width={NODE_WIDTH + 24} height={band.height} rx={12} fill="#fff7ed" stroke="#f5d6b1" />
                <title>{band.label}</title>
                <text x={WORK_X} y={band.y + 11} fill="#92400e" fontSize={11} fontWeight={700}>
                  {band.label.length > 36 ? `${band.label.slice(0, 35)}…` : band.label}
                </text>
              </g>
            ))}

            {links.map(({ source, target, id }) => source && target && (
              <path key={id}
                d={`M ${source.x + NODE_WIDTH} ${source.y + 36} C 525 ${source.y + 36}, 670 ${target.y + 36}, ${target.x - 12} ${target.y + 36}`}
                fill="none" stroke="#64748b" strokeWidth={1.4} strokeDasharray="5 5"
                opacity={isEmphasized(source.id, target.id) ? 0.65 : 0.1}
              />
            ))}
            {outline.nodes.filter((node) => node.parentItemId).map((node) => {
              const parent = knowledgeById.get(node.parentItemId!);
              const child = knowledgeById.get(node.itemId);
              return parent && child && <path key={`outline:${node.itemId}`}
                d={`M ${parent.x + 12} ${parent.y + NODE_HEIGHT} C ${parent.x + 12} ${child.y - 8}, ${child.x + 12} ${child.y - 8}, ${child.x + 12} ${child.y}`}
                fill="none" stroke="#0284c7" strokeWidth={2}
                opacity={isEmphasized(parent.id, child.id) ? 0.9 : 0.12}
              />;
            })}
            {outline.links.map((link) => {
              const source = knowledgeById.get(link.knowledgeItemId);
              const target = workById.get(link.workItemId);
              const older = knowledge.find((item) => item.id === link.knowledgeItemId)?.currentRevisionId !== link.revisionId;
              return source && target && <path key={link.id}
                d={`M ${source.x + NODE_WIDTH} ${source.y + 32} C 535 ${source.y + 32}, 675 ${target.y + 32}, ${target.x - 12} ${target.y + 32}`}
                fill="none" stroke="#7c3aed" strokeWidth={2.5} strokeDasharray={older ? '8 4' : undefined}
                opacity={isEmphasized(source.id, target.id) ? 0.9 : 0.12}
              />;
            })}
            {visibleRelations.map((relation, index) => {
              const source = knowledgeById.get(relation.sourceItemId);
              const target = knowledgeById.get(relation.targetItemId);
              const node = relationNodes[index];
              const opacity = isEmphasized(relation.sourceItemId, relation.targetItemId) || activeSelection?.id === relation.id ? 0.85 : 0.12;
              return (
                <g key={relation.id} opacity={opacity}>
                  {source && <path d={`M ${source.x + NODE_WIDTH} ${source.y + 36} C 310 ${source.y + 36}, 315 ${node.y + 36}, ${node.x - 6} ${node.y + 36}`} fill="none" stroke="#0f766e" strokeWidth={2} markerEnd="url(#plan-arrow)" />}
                  {target && <path d={`M ${node.x + NODE_WIDTH} ${node.y + 36} C 700 ${node.y + 36}, 700 ${target.y + 36}, ${target.x + NODE_WIDTH + 6} ${target.y + 36}`} fill="none" stroke="#0f766e" strokeWidth={2} markerEnd="url(#plan-arrow)" />}
                </g>
              );
            })}
            {workItems.flatMap((item) => (item.dependsOn ?? []).map((dependencyId) => {
              const prerequisite = workById.get(dependencyId);
              const dependent = workById.get(item.id);
              if (!prerequisite || !dependent) return null;
              return (
                <path key={`${dependencyId}:${item.id}`}
                  d={`M ${prerequisite.x + NODE_WIDTH} ${prerequisite.y + 36} C 1150 ${prerequisite.y + 36}, 1150 ${dependent.y + 36}, ${dependent.x + NODE_WIDTH + 7} ${dependent.y + 36}`}
                  fill="none" stroke="#b45309" strokeWidth={2} markerEnd="url(#work-arrow)"
                  opacity={isEmphasized(prerequisite.id, dependent.id) ? 0.9 : 0.12}
                />
              );
            }))}
            {[...knowledgeNodes, ...relationNodes, ...workNodes].map((node) => (
              <MapNode key={`${node.kind}:${node.id}`} node={node}
                selected={activeSelection?.kind === node.kind && activeSelection.id === node.id}
                onSelect={() => setSelection({ kind: node.kind, id: node.id })}
              />
            ))}
            {!knowledge.length && <text x={KNOWLEDGE_X} y={150} fill="#5b6b7c" fontSize={13}>No knowledge items yet</text>}
            {!visibleRelations.length && <text x={RELATION_X} y={150} fill="#5b6b7c" fontSize={13}>No stored relations to show</text>}
            {!workItems.length && !milestones.length && <text x={WORK_X} y={150} fill="#5b6b7c" fontSize={13}>No milestones or work items yet</text>}
          </svg>
        </div>
      </section>

      <section className="panel rounded-2xl p-5" aria-live="polite">
        <h3 className="font-display text-lg font-bold">{selected ? 'Selected record' : 'How to read this map'}</h3>
        {selected ? (
          <div className="mt-3 space-y-2 text-sm">
            <p className="font-semibold">{'key' in selected ? selected.key : selected.relationTypeCode} · {'title' in selected ? selected.title : 'Directed relation'}</p>
            {selection?.kind === 'knowledge' && (
              <p>Type: {(selected as KnowledgeItem).typeId} · Revision: {(selected as KnowledgeItem).currentRevisionNumber} · Review: {(selected as KnowledgeItem).reviewState} · Verification: {(selected as KnowledgeItem).verificationState}</p>
            )}
            {selection?.kind === 'relation' && (
              <p>Source: {knowledge.find((item) => item.id === (selected as Relationship).sourceItemId)?.key ?? 'Unknown'} → Target: {knowledge.find((item) => item.id === (selected as Relationship).targetItemId)?.key ?? 'Unknown'} · {(selected as Relationship).status}</p>
            )}
            {selection?.kind === 'work' && (
              <>
                <p>Planned: {(selected as WorkItem).plannedState} · Reported: {(selected as WorkItem).reportedState} · Verified: {(selected as WorkItem).verificationState} · Approved: {(selected as WorkItem).approvalState}</p>
                <p className="text-[var(--muted)]">{(selected as WorkItem).description || 'No description recorded.'}</p>
                <p className="text-xs text-[var(--muted)]">Violet links pin reviewed Knowledge revisions; dashed lines are text mentions only. New revisions do not update existing pins.</p>
              </>
            )}
            <StateBadge value={selection?.kind === 'relation' ? (selected as Relationship).status : selection?.kind === 'work' ? (selected as WorkItem).approvalState : (selected as KnowledgeItem).state} />
            <div><Link className="text-sm font-semibold text-[var(--signal)] hover:underline" to={selection?.kind === 'knowledge' ? '/knowledge' : selection?.kind === 'relation' ? '/relations' : '/work'}>Open {selection?.kind} page →</Link></div>
          </div>
        ) : (
          <p className="mt-2 text-sm leading-relaxed text-[var(--muted)]">
            Solid lines come from stored project relationships or work dependencies. A dashed line only means a work description mentions a knowledge key;
            it does not attest to approval or a pinned revision. Missing lines can mean the reference has not been recorded yet.
          </p>
        )}
      </section>
    </div>
  );
}
