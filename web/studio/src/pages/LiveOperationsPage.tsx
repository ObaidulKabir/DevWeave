/**
 * Live operations — who is doing what
 *
 * Layout zones:
 * 1. Status strip — plan version context, active runtime/lease counts, poll pulse
 * 2. Relationship diagram — Campaign → WorkPackages → Leases → Agents/Runtimes
 * 3. Live activity stream — channel messages + agent workthrough claims (not acceptance)
 *
 * Refresh: short-interval polling when Live API is up. Studio has no WebSocket/SSE today;
 * GET /leases and GET /channels are read-only Owner surfaces added for this view.
 */
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { EmptyState, Panel } from '../components/Panel';
import { StateBadge } from '../components/StateBadge';
import { ApiError, apiFetch } from '../lib/api';
import { formatWhen, shortId } from '../lib/format';
import type {
  Campaign,
  CoordinationChannel,
  CoordinationMessage,
  Lease,
  PlanStatus,
  Runtime,
  WorkPackageStatus,
} from '../lib/types';
import { useWorkspace } from '../workspace/WorkspaceContext';

const POLL_MS = 4000;
const MESSAGE_TAIL = 24;
const AUTH_HELP =
  'Owner token was rejected (401/403). Sign out and sign in again from /login to mint a fresh pilot Owner token.';

type FeedKind = 'message' | 'workthrough' | 'lease' | 'runtime';

interface FeedItem {
  id: string;
  kind: FeedKind;
  at: string;
  title: string;
  detail: string;
  tone: 'neutral' | 'ok' | 'warn' | 'signal';
  claim?: boolean;
}

interface LiveSnapshot {
  planStatus: PlanStatus | null;
  runtimes: Runtime[];
  leases: Lease[];
  packages: WorkPackageStatus[];
  channels: CoordinationChannel[];
  messages: CoordinationMessage[];
}

const EMPTY: LiveSnapshot = {
  planStatus: null,
  runtimes: [],
  leases: [],
  packages: [],
  channels: [],
  messages: [],
};

function isActiveRuntime(runtime: Runtime, now: number): boolean {
  if (runtime.status.toLowerCase() === 'revoked' || runtime.status.toLowerCase() === 'disconnected') {
    return false;
  }
  if (!runtime.lastHeartbeatAt) return runtime.status.toLowerCase() === 'active';
  const hb = new Date(runtime.lastHeartbeatAt).getTime();
  return Number.isFinite(hb) && now - hb < 90_000;
}

function pageItems<T>(payload: unknown): T[] {
  if (Array.isArray(payload)) return payload as T[];
  if (payload && typeof payload === 'object') {
    const record = payload as { items?: T[]; Items?: T[] };
    return record.items ?? record.Items ?? [];
  }
  return [];
}

export function LiveOperationsPage() {
  const { tenantId, project, live, campaigns, runtimes: contextRuntimes } = useWorkspace();
  const base = tenantId && project ? `/tenants/${tenantId}/projects/${project.id}/coordination` : undefined;

  const [snapshot, setSnapshot] = useState<LiveSnapshot>(EMPTY);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastRefreshAt, setLastRefreshAt] = useState<string | null>(null);
  const [tick, setTick] = useState(0);
  const seenIds = useRef(new Set<string>());
  const [freshIds, setFreshIds] = useState<Set<string>>(new Set());

  const load = useCallback(async () => {
    if (!live || !base) {
      setSnapshot(EMPTY);
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const activeCampaigns = campaigns.filter((c) => {
        const s = c.status.toLowerCase();
        return s === 'active' || s === 'proposed';
      });

      let authFailed = false;
      const soft = async <T,>(path: string, fallback: T): Promise<T> => {
        try {
          return await apiFetch<T>(path);
        } catch (err) {
          if (err instanceof ApiError && (err.status === 401 || err.status === 403)) authFailed = true;
          return fallback;
        }
      };

      const [planStatus, runtimes, leases, channels, ...packageBatches] = await Promise.all([
        soft<PlanStatus | null>(`${base}/plan-status`, null),
        soft(`${base}/runtimes`, [] as Runtime[]),
        soft(`${base}/leases`, [] as Lease[]),
        soft(`${base}/channels`, [] as CoordinationChannel[]),
        ...activeCampaigns.map((campaign) =>
          soft(`${base}/campaigns/${campaign.id}/packages`, [] as WorkPackageStatus[]),
        ),
      ]);

      const packages = packageBatches.flatMap((batch) => pageItems<WorkPackageStatus>(batch));
      const channelList = pageItems<CoordinationChannel>(channels);

      const messagePages = await Promise.all(
        channelList
          .filter((ch) => (ch.sequenceHighWatermark ?? 0) > 0)
          .slice(0, 12)
          .map(async (ch) => {
            const after = Math.max(0, (ch.sequenceHighWatermark ?? 0) - MESSAGE_TAIL);
            const page = await soft<unknown>(
              `${base}/channels/${ch.id}/messages?afterSequence=${after}&limit=${MESSAGE_TAIL}`,
              null,
            );
            return pageItems<CoordinationMessage>(page);
          }),
      );

      const messages = messagePages
        .flat()
        .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
        .slice(0, 40);

      const next: LiveSnapshot = {
        planStatus: planStatus && Array.isArray(planStatus.campaigns) ? planStatus : null,
        runtimes: pageItems<Runtime>(runtimes),
        leases: pageItems<Lease>(leases),
        packages,
        channels: channelList,
        messages,
      };

      const incoming = new Set<string>();
      for (const msg of next.messages) incoming.add(`msg:${msg.id}`);
      for (const pkg of next.packages) {
        if (pkg.latestWorkthrough) incoming.add(`wt:${pkg.latestWorkthrough.id}`);
      }
      for (const lease of next.leases) incoming.add(`lease:${lease.id}`);

      const newly = new Set<string>();
      if (seenIds.current.size > 0) {
        for (const id of incoming) {
          if (!seenIds.current.has(id)) newly.add(id);
        }
      }
      seenIds.current = incoming;
      setFreshIds(newly);
      setSnapshot(next);
      setLastRefreshAt(new Date().toISOString());
      setTick((n) => n + 1);
      if (authFailed) setError(AUTH_HELP);
    } catch (err) {
      setError(
        err instanceof ApiError && (err.status === 401 || err.status === 403)
          ? AUTH_HELP
          : err instanceof Error
            ? err.message
            : 'Failed to refresh live operations.',
      );
    } finally {
      setLoading(false);
    }
  }, [live, base, campaigns]);

  useEffect(() => {
    void load();
    if (!live || !base) return;
    const id = window.setInterval(() => void load(), POLL_MS);
    return () => window.clearInterval(id);
  }, [load, live, base]);

  useEffect(() => {
    if (freshIds.size === 0) return;
    const id = window.setTimeout(() => setFreshIds(new Set()), 2200);
    return () => window.clearTimeout(id);
  }, [freshIds]);

  const now = Date.now();
  const runtimes = snapshot.runtimes.length ? snapshot.runtimes : contextRuntimes;
  const activeRuntimes = runtimes.filter((r) => isActiveRuntime(r, now));
  const campaignById = useMemo(() => {
    const map = new Map<string, Campaign>();
    for (const c of campaigns) map.set(c.id, c);
    return map;
  }, [campaigns]);

  const feed = useMemo(() => buildFeed(snapshot, campaignById), [snapshot, campaignById]);

  const planLabel = snapshot.planStatus?.currentPlanVersion
    ? `v${snapshot.planStatus.currentPlanVersion}`
    : '—';

  return (
    <div className="live-ops space-y-6">
      <section className="live-ops-hero panel relative overflow-hidden rounded-2xl px-6 py-7">
        <div className="live-ops-scan" aria-hidden="true" />
        <div className="relative flex flex-wrap items-start justify-between gap-4">
          <div className="max-w-2xl">
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">
              Live operations
            </p>
            <h2 className="font-display mt-2 text-3xl font-bold tracking-tight text-[var(--ink)] sm:text-4xl">
              Who is doing what
            </h2>
            <p className="mt-3 text-sm leading-relaxed text-[var(--muted)]">
              Active agents, the path scopes they lease, and recent coordination activity — polled every{' '}
              {POLL_MS / 1000}s. Agent workthroughs are claims, not acceptance.
            </p>
          </div>
          <div className="flex flex-col items-end gap-2 text-right">
            <span className={`live-ops-pulse ${live ? 'is-live' : ''}`}>
              <span className="live-ops-pulse-dot" aria-hidden="true" />
              {live ? (loading ? 'Refreshing…' : 'Streaming via poll') : 'Preview mode'}
            </span>
            <p className="font-mono text-[11px] text-[var(--muted)]">
              Plan {planLabel}
              {lastRefreshAt ? ` · ${formatWhen(lastRefreshAt)}` : ''}
              {tick > 0 ? ` · #${tick}` : ''}
            </p>
            <button type="button" className="btn btn-ghost" disabled={!live || loading} onClick={() => void load()}>
              Refresh now
            </button>
          </div>
        </div>

        <div className="relative mt-6 grid gap-3 sm:grid-cols-4">
          <Stat label="Active runtimes" value={String(activeRuntimes.length)} hint={`${runtimes.length} registered`} />
          <Stat label="Open leases" value={String(snapshot.leases.length)} hint="Exclusive path scopes" />
          <Stat
            label="Work packages"
            value={String(snapshot.packages.length)}
            hint={`${campaigns.filter((c) => c.status.toLowerCase() === 'active').length} active campaigns`}
          />
          <Stat
            label="Channels"
            value={String(snapshot.channels.length)}
            hint={`${snapshot.messages.length} recent messages`}
          />
        </div>

        {!live ? (
          <p className="relative mt-4 text-sm text-amber-900">
            Sign in with a live Owner session to stream pilot coordination. Diagram and feed stay empty until then.
          </p>
        ) : null}
        {error ? (
          <p role="alert" className="relative mt-4 text-sm text-[var(--danger)]">
            {error}
          </p>
        ) : null}
      </section>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1.35fr)_minmax(0,1fr)]">
        <Panel
          title="Relationship map"
          subtitle="Campaign → work packages → leases → agents/runtimes"
          actions={
            <Link className="text-xs font-semibold text-[var(--signal)] hover:underline" to="/coordination">
              Coordination →
            </Link>
          }
        >
          <OperationsDiagram
            campaigns={campaigns}
            packages={snapshot.packages}
            leases={snapshot.leases}
            runtimes={runtimes}
            planStatus={snapshot.planStatus}
          />
        </Panel>

        <Panel
          title="Activity stream"
          subtitle="Messages and agent workthrough claims · auto-refresh"
          actions={
            <Link className="text-xs font-semibold text-[var(--signal)] hover:underline" to="/plan-versions">
              Plan versions →
            </Link>
          }
        >
          {feed.length === 0 ? (
            <EmptyState
              label={
                live
                  ? 'No recent messages, leases, or workthroughs yet.'
                  : 'Activity appears here when the live API is connected.'
              }
            />
          ) : (
            <ol className="live-ops-feed max-h-[34rem] divide-y divide-[var(--line)] overflow-y-auto">
              {feed.map((item) => (
                <li
                  key={item.id}
                  className={`live-ops-feed-item px-5 py-3.5 ${freshIds.has(item.id) ? 'is-fresh' : ''}`}
                >
                  <div className="flex flex-wrap items-center gap-2">
                    <FeedKindBadge kind={item.kind} />
                    {item.claim ? (
                      <span className="rounded-md bg-amber-50 px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-amber-900">
                        Agent claim · not acceptance
                      </span>
                    ) : null}
                    <span className="ml-auto font-mono text-[10px] text-[var(--muted)]">{formatWhen(item.at)}</span>
                  </div>
                  <p className="mt-1.5 text-sm font-semibold text-[var(--ink)]">{item.title}</p>
                  <p className="mt-1 text-xs leading-relaxed text-[var(--muted)]">{item.detail}</p>
                </li>
              ))}
            </ol>
          )}
        </Panel>
      </div>
    </div>
  );
}

function Stat({ label, value, hint }: { label: string; value: string; hint: string }) {
  return (
    <div className="rounded-xl border border-[var(--line)] bg-white/70 px-4 py-3">
      <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-[var(--muted)]">{label}</p>
      <p className="font-display mt-1 text-2xl font-bold tabular-nums">{value}</p>
      <p className="mt-0.5 text-[11px] text-[var(--muted)]">{hint}</p>
    </div>
  );
}

function FeedKindBadge({ kind }: { kind: FeedKind }) {
  const label =
    kind === 'message'
      ? 'Message'
      : kind === 'workthrough'
        ? 'Workthrough'
        : kind === 'lease'
          ? 'Lease'
          : 'Runtime';
  return <StateBadge value={label} />;
}

function buildFeed(
  snapshot: LiveSnapshot,
  campaignById: Map<string, Campaign>,
): FeedItem[] {
  const items: FeedItem[] = [];

  for (const msg of snapshot.messages) {
    const channel = snapshot.channels.find((c) => c.id === msg.channelId);
    let metaPreview = '';
    try {
      const parsed = JSON.parse(msg.allowedMetadataJson || '{}') as Record<string, unknown>;
      const text = parsed.text ?? parsed.summary ?? parsed.body;
      if (typeof text === 'string') metaPreview = text.slice(0, 160);
    } catch {
      metaPreview = '';
    }
    items.push({
      id: `msg:${msg.id}`,
      kind: 'message',
      at: msg.createdAt,
      title: `${msg.messageType} · seq ${msg.sequence}`,
      detail: [
        channel ? `${channel.channelType} channel ${shortId(channel.id)}` : shortId(msg.channelId),
        msg.runtimeId && msg.runtimeId !== '00000000-0000-0000-0000-000000000000'
          ? `runtime ${shortId(msg.runtimeId)}`
          : null,
        metaPreview || null,
      ]
        .filter(Boolean)
        .join(' · '),
      tone: 'signal',
    });
  }

  for (const pkg of snapshot.packages) {
    const wt = pkg.latestWorkthrough;
    if (!wt) continue;
    const campaign = campaignById.get(pkg.campaignId);
    items.push({
      id: `wt:${wt.id}`,
      kind: 'workthrough',
      at: wt.recordedAt,
      title: `${pkg.key} · ${wt.kind}`,
      detail: [
        campaign?.objective ?? shortId(pkg.campaignId),
        wt.outcome,
        wt.kind === 'Blocked' && wt.blockedReason ? `Blocked: ${wt.blockedReason}` : null,
        wt.whatSucceeded ? `Succeeded: ${wt.whatSucceeded}` : null,
        wt.changeSetId ? `change-set ${shortId(wt.changeSetId)}` : null,
      ]
        .filter(Boolean)
        .join(' · '),
      tone: wt.kind === 'Blocked' ? 'warn' : 'ok',
      claim: true,
    });
  }

  for (const lease of snapshot.leases) {
    const pkg = snapshot.packages.find((p) => p.id === lease.workPackageId);
    items.push({
      id: `lease:${lease.id}`,
      kind: 'lease',
      at: lease.createdAt ?? lease.expiresAt,
      title: `Lease on ${pkg?.key ?? shortId(lease.workPackageId)}`,
      detail: `Scope ${lease.resourceScope} · runtime ${shortId(lease.runtimeId)} · expires ${formatWhen(lease.expiresAt)}`,
      tone: 'neutral',
    });
  }

  for (const runtime of snapshot.runtimes) {
    if (!runtime.lastHeartbeatAt) continue;
    items.push({
      id: `runtime:${runtime.id}:${runtime.lastHeartbeatAt}`,
      kind: 'runtime',
      at: runtime.lastHeartbeatAt,
      title: `${runtime.runtimeClass} heartbeat`,
      detail: `${runtime.status} · agent ${shortId(runtime.principalId)}`,
      tone: isActiveRuntime(runtime, Date.now()) ? 'ok' : 'warn',
    });
  }

  return items
    .sort((a, b) => new Date(b.at).getTime() - new Date(a.at).getTime())
    .slice(0, 36);
}

type DiagramNode = {
  id: string;
  lane: 0 | 1 | 2 | 3;
  label: string;
  sub: string;
  dim?: boolean;
};

function OperationsDiagram({
  campaigns,
  packages,
  leases,
  runtimes,
  planStatus,
}: {
  campaigns: Campaign[];
  packages: WorkPackageStatus[];
  leases: Lease[];
  runtimes: Runtime[];
  planStatus: PlanStatus | null;
}) {
  const now = Date.now();
  const activeCampaigns = campaigns
    .filter((c) => {
      const s = c.status.toLowerCase();
      return s === 'active' || s === 'proposed' || s === 'suspended';
    })
    .slice(0, 6);

  const packageById = new Map(packages.map((p) => [p.id, p]));
  const campaignPlan = new Map(
    (planStatus?.campaigns ?? []).map((c) => [c.id, c] as const),
  );

  const campaignNodes: DiagramNode[] = activeCampaigns.map((c) => {
    const ps = campaignPlan.get(c.id);
    const objective = (c.objective ?? '').trim();
    return {
      id: `c:${c.id}`,
      lane: 0,
      label: objective ? objective.slice(0, 42) : shortId(c.id),
      sub: ps
        ? `${c.status} · plan v${ps.planVersion}${ps.onCurrentPlan ? '' : ' · behind'}`
        : c.status,
      dim: ps ? !ps.onCurrentPlan : false,
    };
  });

  const shownPackages = packages.slice(0, 10);
  const packageNodes: DiagramNode[] = shownPackages.map((p) => ({
    id: `p:${p.id}`,
    lane: 1,
    label: p.key || shortId(p.id),
    sub: `${p.status}${p.canProgress ? '' : ' · blocked'}`,
    dim: !p.onCurrentPlan || !p.canProgress,
  }));

  const leaseNodes: DiagramNode[] = leases.slice(0, 10).map((l) => ({
    id: `l:${l.id}`,
    lane: 2,
    label: (l.resourceScope ?? shortId(l.id)).slice(0, 36),
    sub: `→ ${shortId(l.runtimeId)} · fence ${l.fence}`,
  }));

  const runtimeNodes: DiagramNode[] = runtimes.slice(0, 10).map((r) => ({
    id: `r:${r.id}`,
    lane: 3,
    label: r.runtimeClass,
    sub: `${r.status}${isActiveRuntime(r, now) ? ' · live' : ''}`,
    dim: !isActiveRuntime(r, now),
  }));

  const nodes = [...campaignNodes, ...packageNodes, ...leaseNodes, ...runtimeNodes];
  const laneCounts = [0, 0, 0, 0];
  const positions = new Map<string, { x: number; y: number }>();
  const laneX = [36, 250, 464, 678];
  const rowH = 58;
  const cardW = 186;
  const cardH = 46;

  for (const node of nodes) {
    const row = laneCounts[node.lane]++;
    positions.set(node.id, { x: laneX[node.lane], y: 48 + row * rowH });
  }

  const height = Math.max(220, 56 + Math.max(...laneCounts, 1) * rowH);

  type Edge = { from: string; to: string; kind: 'plan' | 'lease' | 'hold' };
  const edges: Edge[] = [];

  for (const pkg of shownPackages) {
    if (positions.has(`c:${pkg.campaignId}`) && positions.has(`p:${pkg.id}`)) {
      edges.push({ from: `c:${pkg.campaignId}`, to: `p:${pkg.id}`, kind: 'plan' });
    }
  }
  for (const lease of leases) {
    if (positions.has(`p:${lease.workPackageId}`) && positions.has(`l:${lease.id}`)) {
      edges.push({ from: `p:${lease.workPackageId}`, to: `l:${lease.id}`, kind: 'lease' });
    }
    if (positions.has(`l:${lease.id}`) && positions.has(`r:${lease.runtimeId}`)) {
      edges.push({ from: `l:${lease.id}`, to: `r:${lease.runtimeId}`, kind: 'hold' });
    } else if (packageById.has(lease.workPackageId) && positions.has(`r:${lease.runtimeId}`)) {
      // package may be truncated from diagram; still try lease→runtime
      if (positions.has(`l:${lease.id}`)) {
        edges.push({ from: `l:${lease.id}`, to: `r:${lease.runtimeId}`, kind: 'hold' });
      }
    }
  }

  if (nodes.length === 0) {
    return (
      <div className="px-5 py-12 text-center text-sm text-[var(--muted)]">
        No campaigns, packages, leases, or runtimes to diagram yet.
      </div>
    );
  }

  const laneLabels = ['Campaign', 'Work package', 'Lease / scope', 'Runtime'];

  return (
    <div className="live-ops-diagram overflow-x-auto px-3 py-4">
      <svg
        viewBox={`0 0 900 ${height}`}
        className="min-w-[720px] w-full"
        role="img"
        aria-label="Campaign to work package to lease to runtime diagram"
      >
        {laneLabels.map((label, i) => (
          <text
            key={label}
            x={laneX[i] + cardW / 2}
            y={22}
            textAnchor="middle"
            className="fill-[var(--muted)]"
            style={{ fontSize: 11, fontFamily: 'IBM Plex Mono, monospace', fontWeight: 600 }}
          >
            {label}
          </text>
        ))}

        {edges.map((edge) => {
          const a = positions.get(edge.from);
          const b = positions.get(edge.to);
          if (!a || !b) return null;
          const x1 = a.x + cardW;
          const y1 = a.y + cardH / 2;
          const x2 = b.x;
          const y2 = b.y + cardH / 2;
          const mx = (x1 + x2) / 2;
          const stroke =
            edge.kind === 'hold' ? 'var(--signal)' : edge.kind === 'lease' ? '#0ea5a0' : '#8aa0b5';
          return (
            <path
              key={`${edge.from}-${edge.to}`}
              d={`M ${x1} ${y1} C ${mx} ${y1}, ${mx} ${y2}, ${x2} ${y2}`}
              fill="none"
              stroke={stroke}
              strokeWidth={edge.kind === 'hold' ? 2 : 1.4}
              strokeOpacity={0.75}
              className={edge.kind === 'hold' ? 'live-ops-edge-hold' : undefined}
            />
          );
        })}

        {nodes.map((node) => {
          const pos = positions.get(node.id)!;
          const fill =
            node.lane === 0
              ? '#f0f7f6'
              : node.lane === 1
                ? '#ffffff'
                : node.lane === 2
                  ? '#e8f6f4'
                  : '#f7fafc';
          const stroke =
            node.lane === 2 ? 'var(--signal)' : node.dim ? 'var(--line)' : '#b7c5d6';
          return (
            <g key={node.id} opacity={node.dim ? 0.55 : 1} className="live-ops-node">
              <rect
                x={pos.x}
                y={pos.y}
                width={cardW}
                height={cardH}
                rx={10}
                fill={fill}
                stroke={stroke}
                strokeWidth={1.5}
              />
              <text
                x={pos.x + 12}
                y={pos.y + 18}
                style={{ fontSize: 12, fontFamily: 'Syne, Sora, sans-serif', fontWeight: 700 }}
                className="fill-[var(--ink)]"
              >
                {truncate(node.label, 22)}
              </text>
              <text
                x={pos.x + 12}
                y={pos.y + 34}
                style={{ fontSize: 10, fontFamily: 'IBM Plex Mono, monospace' }}
                className="fill-[var(--muted)]"
              >
                {truncate(node.sub, 28)}
              </text>
            </g>
          );
        })}
      </svg>
    </div>
  );
}

function truncate(value: string, max: number): string {
  if (value.length <= max) return value;
  return `${value.slice(0, max - 1)}…`;
}
