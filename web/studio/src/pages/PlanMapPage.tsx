import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { StateBadge } from '../components/StateBadge';
import type { KnowledgeItem, Relationship, WorkItem } from '../lib/types';
import { useWorkspace } from '../workspace/WorkspaceContext';

type Selection = { kind: 'knowledge' | 'relation' | 'work'; id: string } | null;
type MapNode = {
  id: string;
  key: string;
  title: string;
  subtitle: string;
  x: number;
  y: number;
  kind: 'knowledge' | 'relation' | 'work';
  accent: string;
  depth?: number;
};

const NODE_W = 236;
const NODE_H = 68;
const ROW = 88;
const KNOWLEDGE_ORIGIN_X = 36;
const DEPTH_STEP = 56;
const RELATION_X = 420;
const WORK_X = 780;
const PAD_TOP = 108;
const LANE_GAP = 28;

function mentionsKey(description: string | undefined, key: string) {
  if (!description || !key) return false;
  const escaped = key.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  return new RegExp(`(^|[^a-zA-Z0-9_-])${escaped}(?=$|[^a-zA-Z0-9_-])`, 'i').test(description);
}

function truncate(value: string, max: number) {
  return value.length > max ? `${value.slice(0, max - 1)}…` : value;
}

function typeAccent(typeId: string) {
  if (typeId.includes('goal')) return '#0f766e';
  if (typeId.includes('module')) return '#0369a1';
  if (typeId.includes('requirement')) return '#7c3aed';
  if (typeId.includes('decision')) return '#b45309';
  if (typeId.includes('constraint')) return '#be123c';
  return '#0f766e';
}

function elbow(x1: number, y1: number, x2: number, y2: number, bend = 0.45) {
  const mx = x1 + (x2 - x1) * bend;
  return `M ${x1} ${y1} C ${mx} ${y1}, ${mx} ${y2}, ${x2} ${y2}`;
}

function packYs(preferred: number[], minGap: number, startY: number) {
  if (!preferred.length) return [] as number[];
  const indexed = preferred.map((y, index) => ({ y, index })).sort((a, b) => a.y - b.y);
  const placed = new Array<number>(preferred.length);
  let cursor = startY;
  for (const entry of indexed) {
    const y = Math.max(entry.y, cursor);
    placed[entry.index] = y;
    cursor = y + minGap;
  }
  return placed;
}

function MapCard({
  node,
  selected,
  dimmed,
  onSelect,
}: {
  node: MapNode;
  selected: boolean;
  dimmed: boolean;
  onSelect: () => void;
}) {
  const label = `${node.kind}: ${node.key}, ${node.title}. ${node.subtitle}`;
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
      opacity={dimmed ? 0.22 : 1}
      style={{ transition: 'opacity 160ms ease' }}
    >
      <title>{label}</title>
      <rect
        x={node.x}
        y={node.y}
        width={NODE_W}
        height={NODE_H}
        rx={14}
        fill={selected ? '#effcf9' : '#ffffff'}
        stroke={selected ? '#0b5f59' : '#c9d4e3'}
        strokeWidth={selected ? 2.5 : 1.25}
        filter={selected ? 'url(#node-glow)' : undefined}
      />
      <rect x={node.x} y={node.y} width={5} height={NODE_H} rx={2.5} fill={node.accent} />
      <text x={node.x + 16} y={node.y + 22} fill={node.accent} fontSize={11} fontWeight={700} fontFamily="IBM Plex Mono, ui-monospace, monospace">
        {truncate(node.key, 28)}
      </text>
      <text x={node.x + 16} y={node.y + 42} fill="#0b1320" fontSize={12.5} fontWeight={600}>
        {truncate(node.title, 28)}
      </text>
      <text x={node.x + 16} y={node.y + 58} fill="#5b6b7c" fontSize={10}>
        {truncate(node.subtitle, 34)}
      </text>
    </g>
  );
}

export function PlanMapPage() {
  const { project, live, knowledge, relationships, workItems, milestones, outline } = useWorkspace();
  const [showRevoked, setShowRevoked] = useState(false);
  const [showMentions, setShowMentions] = useState(true);
  const [selection, setSelection] = useState<Selection>(null);
  const [zoom, setZoom] = useState(1);

  const layout = useMemo(() => {
    const visibleRelations = relationships.filter((item) => showRevoked || item.status.toLowerCase() !== 'revoked');
    const knowledgeById = new Map(knowledge.map((item) => [item.id, item]));
    const children = new Map<string, string[]>();
    outline.nodes.forEach((node) => {
      const parent = node.parentItemId ?? '';
      children.set(parent, [...(children.get(parent) ?? []), node.itemId]);
    });

    // Tidy-ish vertical packing: leaves claim rows; parents center on children.
    const yById = new Map<string, number>();
    const depthById = new Map<string, number>();
    let leafCursor = 0;
    const place = (id: string, depth: number): number => {
      depthById.set(id, depth);
      const kids = (children.get(id) ?? []).filter((child) => knowledgeById.has(child));
      if (!kids.length) {
        const y = PAD_TOP + leafCursor * ROW;
        leafCursor += 1;
        yById.set(id, y);
        return y;
      }
      const childYs = kids.map((child) => place(child, depth + 1));
      const y = (Math.min(...childYs) + Math.max(...childYs)) / 2;
      yById.set(id, y);
      return y;
    };
    const roots = (children.get('') ?? []).filter((id) => knowledgeById.has(id));
    roots.forEach((root) => place(root, 0));
    knowledge.forEach((item) => {
      if (yById.has(item.id)) return;
      depthById.set(item.id, 0);
      yById.set(item.id, PAD_TOP + leafCursor * ROW);
      leafCursor += 1;
    });

    const maxDepth = Math.max(0, ...[...depthById.values()]);
    const knowledgeLaneWidth = NODE_W + Math.min(maxDepth, 4) * DEPTH_STEP + 24;
    const relationX = KNOWLEDGE_ORIGIN_X + knowledgeLaneWidth + LANE_GAP;
    const workX = relationX + NODE_W + LANE_GAP + 24;

    const knowledgeNodes: MapNode[] = knowledge.map((item) => ({
      id: item.id,
      key: item.key,
      title: item.title,
      subtitle: `${item.typeId} · ${item.state}`,
      x: KNOWLEDGE_ORIGIN_X + Math.min(depthById.get(item.id) ?? 0, 4) * DEPTH_STEP,
      y: yById.get(item.id) ?? PAD_TOP,
      kind: 'knowledge',
      accent: typeAccent(item.typeId),
      depth: depthById.get(item.id) ?? 0,
    }));
    const knowledgeNodeById = new Map(knowledgeNodes.map((node) => [node.id, node]));

    const preferredRelationYs = visibleRelations.map((relation) => {
      const source = knowledgeNodeById.get(relation.sourceItemId);
      const target = knowledgeNodeById.get(relation.targetItemId);
      if (source && target) return (source.y + target.y) / 2;
      return source?.y ?? target?.y ?? PAD_TOP;
    });
    const relationYs = packYs(preferredRelationYs, ROW, PAD_TOP);
    const relationNodes: MapNode[] = visibleRelations.map((relation, index) => ({
      id: relation.id,
      key: relation.relationTypeCode,
      title: relation.status === 'Revoked' ? 'Revoked relation' : 'Directed relation',
      subtitle: relation.status,
      x: relationX,
      y: relationYs[index] ?? PAD_TOP + index * ROW,
      kind: 'relation',
      accent: '#525ba3',
    }));
    const relationNodeById = new Map(relationNodes.map((node) => [node.id, node]));

    const linkedKnowledgeY = (workId: string) => {
      const pinned = outline.links
        .filter((link) => link.workItemId === workId)
        .map((link) => knowledgeNodeById.get(link.knowledgeItemId)?.y)
        .filter((y): y is number => typeof y === 'number');
      if (pinned.length) return pinned.reduce((sum, y) => sum + y, 0) / pinned.length;
      const mentioned = knowledge
        .filter((item) => mentionsKey(workItems.find((work) => work.id === workId)?.description, item.key))
        .map((item) => knowledgeNodeById.get(item.id)?.y)
        .filter((y): y is number => typeof y === 'number');
      if (mentioned.length) return mentioned.reduce((sum, y) => sum + y, 0) / mentioned.length;
      return null;
    };

    const workNodes: MapNode[] = [];
    const milestoneBands: { id: string; label: string; y: number; height: number }[] = [];
    let workCursor = PAD_TOP;
    for (const group of [
      ...milestones.map((item) => ({ id: item.id, label: `${item.key} · ${item.title}` })),
      { id: '', label: 'No milestone' },
    ]) {
      const items = workItems
        .filter((item) =>
          group.id ? item.milestoneId === group.id : !milestones.some((milestone) => milestone.id === item.milestoneId),
        )
        .sort((a, b) => (linkedKnowledgeY(a.id) ?? 1e9) - (linkedKnowledgeY(b.id) ?? 1e9));
      if (!items.length && !group.id) continue;
      const start = workCursor;
      workCursor += 40;
      for (const item of items) {
        const preferred = linkedKnowledgeY(item.id);
        const y = preferred == null ? workCursor : Math.max(workCursor, preferred);
        workNodes.push({
          id: item.id,
          key: item.key,
          title: item.title,
          subtitle: `report ${item.reportedState} · verify ${item.verificationState}`,
          x: workX,
          y,
          kind: 'work',
          accent: '#b45309',
        });
        workCursor = y + ROW;
      }
      milestoneBands.push({ id: group.id, label: group.label, y: start, height: Math.max(ROW, workCursor - start + 8) });
      workCursor += 16;
    }
    const workNodeById = new Map(workNodes.map((node) => [node.id, node]));

    const mapWidth = Math.max(1100, workX + NODE_W + 48);
    const mapHeight = Math.max(
      420,
      ...knowledgeNodes.map((node) => node.y + NODE_H + 48),
      ...relationNodes.map((node) => node.y + NODE_H + 48),
      ...workNodes.map((node) => node.y + NODE_H + 48),
      ...milestoneBands.map((band) => band.y + band.height + 24),
    );

    return {
      visibleRelations,
      knowledgeNodes,
      knowledgeNodeById,
      relationNodes,
      relationNodeById,
      workNodes,
      workNodeById,
      milestoneBands,
      mapWidth,
      mapHeight,
      relationX,
      workX,
      knowledgeLaneWidth,
      maxDepth,
    };
  }, [knowledge, relationships, workItems, milestones, outline, showRevoked]);

  const neighborhood = useMemo(() => {
    const ids = new Set<string>();
    if (!selection) return ids;
    ids.add(selection.id);
    if (selection.kind === 'knowledge') {
      outline.nodes.forEach((node) => {
        if (node.itemId === selection.id && node.parentItemId) ids.add(node.parentItemId);
        if (node.parentItemId === selection.id) ids.add(node.itemId);
      });
      layout.visibleRelations.forEach((relation) => {
        if (relation.sourceItemId === selection.id || relation.targetItemId === selection.id) {
          ids.add(relation.id);
          ids.add(relation.sourceItemId);
          ids.add(relation.targetItemId);
        }
      });
      outline.links.forEach((link) => {
        if (link.knowledgeItemId === selection.id) ids.add(link.workItemId);
      });
      workItems.forEach((work) => {
        const item = knowledge.find((entry) => entry.id === selection.id);
        if (item && mentionsKey(work.description, item.key)) ids.add(work.id);
      });
    }
    if (selection.kind === 'relation') {
      const relation = layout.visibleRelations.find((item) => item.id === selection.id);
      if (relation) {
        ids.add(relation.sourceItemId);
        ids.add(relation.targetItemId);
      }
    }
    if (selection.kind === 'work') {
      outline.links.forEach((link) => {
        if (link.workItemId === selection.id) ids.add(link.knowledgeItemId);
      });
      const work = workItems.find((item) => item.id === selection.id);
      knowledge.forEach((item) => {
        if (mentionsKey(work?.description, item.key)) ids.add(item.id);
      });
      (work?.dependsOn ?? []).forEach((id) => ids.add(id));
      workItems.forEach((item) => {
        if ((item.dependsOn ?? []).includes(selection.id)) ids.add(item.id);
      });
    }
    return ids;
  }, [selection, outline, layout.visibleRelations, workItems, knowledge]);

  const selectedRecord = selection?.kind === 'knowledge'
    ? knowledge.find((item) => item.id === selection.id)
    : selection?.kind === 'relation'
      ? layout.visibleRelations.find((item) => item.id === selection.id)
      : workItems.find((item) => item.id === selection?.id);
  const activeSelection = selectedRecord ? selection : null;
  const focusActive = Boolean(activeSelection);
  const edgeVisible = (from: string, to: string, extra?: string) =>
    !focusActive || neighborhood.has(from) || neighborhood.has(to) || (extra ? neighborhood.has(extra) : false);

  const mentionLinks = showMentions
    ? workItems.flatMap((work) =>
        knowledge
          .filter(
            (item) =>
              mentionsKey(work.description, item.key) &&
              !outline.links.some((link) => link.workItemId === work.id && link.knowledgeItemId === item.id),
          )
          .map((item) => ({
            source: layout.knowledgeNodeById.get(item.id),
            target: layout.workNodeById.get(work.id),
            id: `mention:${item.id}:${work.id}`,
          })),
      )
    : [];

  return (
    <div className="space-y-6">
      <section className="panel rounded-2xl p-5">
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">Project planning diagram</p>
        <h2 className="font-display mt-2 text-2xl font-bold">Plan map: {project?.name ?? 'current project'}</h2>
        <p className="mt-2 max-w-3xl text-sm leading-relaxed text-[var(--muted)]">
          Knowledge is laid out as an outline tree (depth left → right). Relations sit between linked items. Work aligns toward the knowledge it references.
          Select a node to focus its neighborhood.
        </p>
        {!live && <p className="mt-3 text-xs text-amber-800">Preview mode has no persisted plan. Connect a live tenant to display its records.</p>}
        <div className="mt-4 flex flex-wrap gap-x-5 gap-y-2 text-xs text-[var(--muted)]" aria-label="Diagram legend">
          <span><span className="font-semibold text-violet-700">Violet</span> pinned Work link</span>
          <span><span className="font-semibold text-sky-700">Blue</span> outline parent → child</span>
          <span><span className="font-semibold text-teal-700">Teal</span> knowledge relation</span>
          <span><span className="font-semibold text-amber-700">Amber</span> work dependency</span>
          <span><span className="font-semibold text-slate-600">Dashed gray</span> key mention only</span>
        </div>
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <Link className="text-sm font-semibold text-[var(--signal)] hover:underline" to="/plan-outline">Open editable Plan outline</Link>
          <label className="inline-flex items-center gap-2 text-xs font-semibold">
            <input type="checkbox" checked={showRevoked} onChange={(event) => setShowRevoked(event.target.checked)} />
            Show revoked relations
          </label>
          <label className="inline-flex items-center gap-2 text-xs font-semibold">
            <input type="checkbox" checked={showMentions} onChange={(event) => setShowMentions(event.target.checked)} />
            Show text mentions
          </label>
          <div className="ml-auto flex items-center gap-1" role="group" aria-label="Zoom">
            <button type="button" className="btn btn-ghost px-2 py-1 text-xs" onClick={() => setZoom((value) => Math.max(0.65, Number((value - 0.1).toFixed(2))))}>−</button>
            <span className="min-w-12 text-center text-xs font-semibold text-[var(--muted)]">{Math.round(zoom * 100)}%</span>
            <button type="button" className="btn btn-ghost px-2 py-1 text-xs" onClick={() => setZoom((value) => Math.min(1.5, Number((value + 0.1).toFixed(2))))}>+</button>
            <button type="button" className="btn btn-ghost px-2 py-1 text-xs" onClick={() => setZoom(1)}>Reset</button>
          </div>
        </div>
      </section>

      <section className="panel overflow-hidden rounded-2xl" aria-label="Plan diagram">
        <div className="border-b border-[var(--line)] px-5 py-3 text-xs text-[var(--muted)]">
          {knowledge.length} knowledge · {layout.visibleRelations.length} relations · {milestones.length} milestones · {workItems.length} work
          <span className="ml-2">Scroll to pan · zoom controls above.</span>
        </div>
        <div className="overflow-auto">
          <svg
            width={layout.mapWidth * zoom}
            height={layout.mapHeight * zoom}
            viewBox={`0 0 ${layout.mapWidth} ${layout.mapHeight}`}
            role="group"
            aria-label="Interactive project plan diagram with knowledge, relations, milestones and work"
          >
            <defs>
              <marker id="plan-arrow" markerWidth="7" markerHeight="7" refX="6" refY="3.5" orient="auto">
                <path d="M0 0 L7 3.5 L0 7 Z" fill="#0f766e" />
              </marker>
              <marker id="work-arrow" markerWidth="7" markerHeight="7" refX="6" refY="3.5" orient="auto">
                <path d="M0 0 L7 3.5 L0 7 Z" fill="#b45309" />
              </marker>
              <filter id="node-glow" x="-20%" y="-20%" width="140%" height="140%">
                <feDropShadow dx="0" dy="1" stdDeviation="2" floodColor="#0f766e" floodOpacity="0.25" />
              </filter>
            </defs>
            <rect width={layout.mapWidth} height={layout.mapHeight} fill="#f5f8fc" />
            <rect x={12} y={72} width={layout.knowledgeLaneWidth + 12} height={layout.mapHeight - 84} rx={18} fill="#ecfdf8" opacity={0.55} />
            <rect x={layout.relationX - 16} y={72} width={NODE_W + 32} height={layout.mapHeight - 84} rx={18} fill="#eef2ff" opacity={0.45} />
            <rect x={layout.workX - 16} y={72} width={NODE_W + 32} height={layout.mapHeight - 84} rx={18} fill="#fff7ed" opacity={0.45} />

            <text x={KNOWLEDGE_ORIGIN_X} y={36} fill="#0f766e" fontSize={14} fontWeight={700}>01  KNOWLEDGE</text>
            <text x={layout.relationX} y={36} fill="#525ba3" fontSize={14} fontWeight={700}>02  RELATIONS</text>
            <text x={layout.workX} y={36} fill="#b45309" fontSize={14} fontWeight={700}>03  MILESTONES + WORK</text>
            <text x={KNOWLEDGE_ORIGIN_X} y={58} fill="#5b6b7c" fontSize={11}>Outline depth left → right</text>
            <text x={layout.relationX} y={58} fill="#5b6b7c" fontSize={11}>Aligned to linked items</text>
            <text x={layout.workX} y={58} fill="#5b6b7c" fontSize={11}>Grouped by milestone</text>

            {layout.milestoneBands.map((band) => (
              <g key={band.id || 'unassigned'}>
                <rect x={layout.workX - 12} y={band.y - 6} width={NODE_W + 24} height={band.height} rx={14} fill="#fff7ed" stroke="#f5d6b1" />
                <title>{band.label}</title>
                <text x={layout.workX} y={band.y + 14} fill="#92400e" fontSize={11} fontWeight={700}>
                  {truncate(band.label, 34)}
                </text>
              </g>
            ))}

            {mentionLinks.map(({ source, target, id }) => source && target && (
              <path
                key={id}
                d={elbow(source.x + NODE_W, source.y + NODE_H / 2, target.x - 8, target.y + NODE_H / 2, 0.55)}
                fill="none"
                stroke="#64748b"
                strokeWidth={1.3}
                strokeDasharray="5 5"
                opacity={edgeVisible(source.id, target.id) ? 0.55 : 0.06}
              />
            ))}

            {outline.nodes.filter((node) => node.parentItemId).map((node) => {
              const parent = layout.knowledgeNodeById.get(node.parentItemId!);
              const child = layout.knowledgeNodeById.get(node.itemId);
              if (!parent || !child) return null;
              const x1 = parent.x + 18;
              const y1 = parent.y + NODE_H;
              const x2 = child.x + 18;
              const y2 = child.y;
              return (
                <path
                  key={`outline:${node.itemId}`}
                  d={`M ${x1} ${y1} C ${x1} ${y1 + 18}, ${x2} ${y2 - 18}, ${x2} ${y2}`}
                  fill="none"
                  stroke="#0284c7"
                  strokeWidth={2}
                  opacity={edgeVisible(parent.id, child.id) ? 0.9 : 0.08}
                />
              );
            })}

            {outline.links.map((link) => {
              const source = layout.knowledgeNodeById.get(link.knowledgeItemId);
              const target = layout.workNodeById.get(link.workItemId);
              const older = knowledge.find((item) => item.id === link.knowledgeItemId)?.currentRevisionId !== link.revisionId;
              if (!source || !target) return null;
              return (
                <path
                  key={link.id}
                  d={elbow(source.x + NODE_W, source.y + NODE_H / 2 - 4, target.x - 8, target.y + NODE_H / 2 - 4, 0.5)}
                  fill="none"
                  stroke="#7c3aed"
                  strokeWidth={2.4}
                  strokeDasharray={older ? '8 4' : undefined}
                  opacity={edgeVisible(source.id, target.id) ? 0.92 : 0.08}
                />
              );
            })}

            {layout.visibleRelations.map((relation) => {
              const source = layout.knowledgeNodeById.get(relation.sourceItemId);
              const target = layout.knowledgeNodeById.get(relation.targetItemId);
              const node = layout.relationNodeById.get(relation.id);
              if (!node) return null;
              const visible = edgeVisible(relation.sourceItemId, relation.targetItemId, relation.id);
              return (
                <g key={relation.id} opacity={visible ? 0.9 : 0.08}>
                  {source && (
                    <path
                      d={elbow(source.x + NODE_W, source.y + NODE_H / 2, node.x - 6, node.y + NODE_H / 2, 0.4)}
                      fill="none"
                      stroke="#0f766e"
                      strokeWidth={2}
                      markerEnd="url(#plan-arrow)"
                    />
                  )}
                  {target && (
                    <path
                      d={elbow(node.x + NODE_W, node.y + NODE_H / 2, target.x + NODE_W + 6, target.y + NODE_H / 2, 0.35)}
                      fill="none"
                      stroke="#0f766e"
                      strokeWidth={2}
                      markerEnd="url(#plan-arrow)"
                    />
                  )}
                </g>
              );
            })}

            {workItems.flatMap((item) =>
              (item.dependsOn ?? []).map((dependencyId) => {
                const prerequisite = layout.workNodeById.get(dependencyId);
                const dependent = layout.workNodeById.get(item.id);
                if (!prerequisite || !dependent) return null;
                const right = layout.workX + NODE_W + 18;
                return (
                  <path
                    key={`${dependencyId}:${item.id}`}
                    d={`M ${prerequisite.x + NODE_W} ${prerequisite.y + NODE_H / 2} C ${right} ${prerequisite.y + NODE_H / 2}, ${right} ${dependent.y + NODE_H / 2}, ${dependent.x + NODE_W + 6} ${dependent.y + NODE_H / 2}`}
                    fill="none"
                    stroke="#b45309"
                    strokeWidth={2}
                    markerEnd="url(#work-arrow)"
                    opacity={edgeVisible(prerequisite.id, dependent.id) ? 0.9 : 0.08}
                  />
                );
              }),
            )}

            {[...layout.knowledgeNodes, ...layout.relationNodes, ...layout.workNodes].map((node) => (
              <MapCard
                key={`${node.kind}:${node.id}`}
                node={node}
                selected={activeSelection?.kind === node.kind && activeSelection.id === node.id}
                dimmed={focusActive && !neighborhood.has(node.id)}
                onSelect={() => setSelection({ kind: node.kind, id: node.id })}
              />
            ))}

            {!knowledge.length && <text x={KNOWLEDGE_ORIGIN_X} y={150} fill="#5b6b7c" fontSize={13}>No knowledge items yet</text>}
            {!layout.visibleRelations.length && <text x={layout.relationX} y={150} fill="#5b6b7c" fontSize={13}>No stored relations to show</text>}
            {!workItems.length && !milestones.length && <text x={layout.workX} y={150} fill="#5b6b7c" fontSize={13}>No milestones or work items yet</text>}
          </svg>
        </div>
      </section>

      <section className="panel rounded-2xl p-5" aria-live="polite">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h3 className="font-display text-lg font-bold">{selectedRecord ? 'Selected record' : 'How to read this map'}</h3>
          {activeSelection && (
            <button type="button" className="btn btn-ghost text-xs" onClick={() => setSelection(null)}>Clear focus</button>
          )}
        </div>
        {selectedRecord ? (
          <div className="mt-3 space-y-2 text-sm">
            <p className="font-semibold">
              {'key' in selectedRecord ? selectedRecord.key : selectedRecord.relationTypeCode}
              {' · '}
              {'title' in selectedRecord ? selectedRecord.title : 'Directed relation'}
            </p>
            {selection?.kind === 'knowledge' && (
              <>
                <p>
                  Type: {(selectedRecord as KnowledgeItem).typeId}
                  {' · '}Revision: {(selectedRecord as KnowledgeItem).currentRevisionNumber}
                  {' · '}Review: {(selectedRecord as KnowledgeItem).reviewState}
                  {' · '}Verification: {(selectedRecord as KnowledgeItem).verificationState}
                </p>
                {(selectedRecord as KnowledgeItem).description?.trim() ? (
                  <p className="whitespace-pre-wrap text-[var(--muted)]">{(selectedRecord as KnowledgeItem).description}</p>
                ) : (
                  <p className="italic text-[var(--muted)]">No details recorded on this item.</p>
                )}
              </>
            )}
            {selection?.kind === 'relation' && (
              <p>
                Source: {knowledge.find((item) => item.id === (selectedRecord as Relationship).sourceItemId)?.key ?? 'Unknown'}
                {' → '}
                Target: {knowledge.find((item) => item.id === (selectedRecord as Relationship).targetItemId)?.key ?? 'Unknown'}
                {' · '}
                {(selectedRecord as Relationship).status}
              </p>
            )}
            {selection?.kind === 'work' && (
              <>
                <p>
                  Planned: {(selectedRecord as WorkItem).plannedState}
                  {' · '}Reported: {(selectedRecord as WorkItem).reportedState}
                  {' · '}Verified: {(selectedRecord as WorkItem).verificationState}
                  {' · '}Approved: {(selectedRecord as WorkItem).approvalState}
                </p>
                <p className="text-[var(--muted)]">{(selectedRecord as WorkItem).description || 'No description recorded.'}</p>
              </>
            )}
            <StateBadge
              value={
                selection?.kind === 'relation'
                  ? (selectedRecord as Relationship).status
                  : selection?.kind === 'work'
                    ? (selectedRecord as WorkItem).approvalState
                    : (selectedRecord as KnowledgeItem).state
              }
            />
            <div>
              <Link
                className="text-sm font-semibold text-[var(--signal)] hover:underline"
                to={selection?.kind === 'knowledge' ? '/knowledge' : selection?.kind === 'relation' ? '/relations' : '/work'}
              >
                Open {selection?.kind} page →
              </Link>
            </div>
          </div>
        ) : (
          <p className="mt-2 text-sm leading-relaxed text-[var(--muted)]">
            Outline edges show structure. Teal paths are semantic relations. Violet pins are governed Work links.
            Selecting a node dims everything outside its neighborhood so crossings are easier to read.
          </p>
        )}
      </section>
    </div>
  );
}
