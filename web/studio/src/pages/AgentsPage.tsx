import { FormEvent, ReactNode, useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { EmptyState, Panel } from '../components/Panel';
import { StateBadge } from '../components/StateBadge';
import { ApiError, apiFetch } from '../lib/api';
import { formatWhen, shortId } from '../lib/format';
import type {
  AgentCredential,
  AgentCredentialIssued,
  AgentGrant,
  AgentPrincipal,
  Lease,
  Membership,
  WhoAmI,
} from '../lib/types';
import { useWorkspace } from '../workspace/WorkspaceContext';

const CAPABILITIES: { value: string; label: string; help: string }[] = [
  { value: 'ReadKnowledge', label: 'Read knowledge', help: 'Read the plan outline, specifications and knowledge up to the ceiling.' },
  { value: 'WriteKnowledge', label: 'Write knowledge', help: 'Propose decisions, catalog changes and plan expansions (still needs human approval).' },
  { value: 'AssembleContext', label: 'Assemble context', help: 'Build revision-pinned context packages for tasks.' },
  { value: 'TraverseRelations', label: 'Traverse relations', help: 'Follow typed relations between knowledge items.' },
  { value: 'ExecuteWorkflow', label: 'Execute workflow', help: 'Take leases, submit change sets and request integration.' },
];
const CEILINGS = ['Public', 'Internal', 'Confidential', 'Restricted'] as const;
const API_ORIGIN = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') || 'http://localhost:5080';

type AgentRecord = {
  agent: AgentPrincipal;
  credentials: AgentCredential[];
  grants: AgentGrant[];
  membership?: Membership;
};

type Confirmation = {
  title: string;
  body: ReactNode;
  confirmLabel: string;
  danger?: boolean;
  action: () => Promise<void>;
};

type Filter = 'all' | 'attention' | 'no-key' | 'offboarded';

function isPast(value?: string | null): boolean {
  return !!value && new Date(value).getTime() < Date.now();
}

/** Approved grants past their expiry are no longer effective, even though the stored status stays Approved. */
function grantState(grant: AgentGrant): string {
  return grant.status === 'Approved' && isPast(grant.expiresAt) ? 'Expired' : grant.status;
}

function credentialState(credential: AgentCredential): string {
  return credential.status === 'Active' && isPast(credential.expiresAt) ? 'Expired' : credential.status;
}

function endOfDayIso(date: string): string | null {
  return date ? new Date(`${date}T23:59:59`).toISOString() : null;
}

function capabilityLabel(value: string): string {
  return CAPABILITIES.find((item) => item.value === value)?.label ?? value;
}

function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    try {
      const parsed = JSON.parse(error.body ?? '') as { message?: string; detail?: string; title?: string };
      return parsed.message ?? parsed.detail ?? parsed.title ?? error.message;
    } catch {
      return error.body || error.message;
    }
  }
  return error instanceof Error ? error.message : 'Request failed.';
}

function ago(value?: string | null): string {
  if (!value) return 'never';
  const seconds = Math.max(0, Math.round((Date.now() - new Date(value).getTime()) / 1000));
  if (seconds < 60) return `${seconds}s ago`;
  if (seconds < 3600) return `${Math.round(seconds / 60)}m ago`;
  if (seconds < 86400) return `${Math.round(seconds / 3600)}h ago`;
  return `${Math.round(seconds / 86400)}d ago`;
}

function Stat({ label, value, hint, tone = 'neutral' }: { label: string; value: number | string; hint?: string; tone?: 'neutral' | 'warn' | 'ok' }) {
  const toneClass = tone === 'warn' ? 'text-amber-700' : tone === 'ok' ? 'text-emerald-700' : 'text-[var(--ink)]';
  return (
    <div className="panel rounded-2xl px-5 py-4">
      <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-[var(--muted)]">{label}</p>
      <p className={`font-display mt-2 text-3xl font-bold tabular-nums ${toneClass}`}>{value}</p>
      {hint ? <p className="mt-1 text-xs text-[var(--muted)]">{hint}</p> : null}
    </div>
  );
}

function SectionTitle({ children, aside }: { children: ReactNode; aside?: ReactNode }) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-2">
      <h3 className="text-xs font-semibold uppercase tracking-[0.14em] text-[var(--muted)]">{children}</h3>
      {aside}
    </div>
  );
}

export function AgentsPage() {
  const { principalId } = useAuth();
  const { tenantId, live, projects, project, members, runtimes, refresh } = useWorkspace();

  const [records, setRecords] = useState<AgentRecord[]>([]);
  const [leases, setLeases] = useState<Lease[]>([]);
  const [selectedId, setSelectedId] = useState('');
  const [loading, setLoading] = useState(false);
  const [working, setWorking] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [note, setNote] = useState<string | null>(null);
  const [actorId, setActorId] = useState<string | undefined>(principalId);
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const [issued, setIssued] = useState<AgentCredentialIssued | null>(null);
  const [copied, setCopied] = useState(false);

  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState<Filter>('all');
  const [newEmail, setNewEmail] = useState('');
  const [keyForm, setKeyForm] = useState({ label: 'mcp-runtime', expires: '' });
  const [grantForm, setGrantForm] = useState({
    scope: 'project',
    capability: 'ReadKnowledge',
    ceiling: 'Internal',
    expires: '',
    approveNow: true,
  });

  const base = tenantId ? `/tenants/${tenantId}` : '';

  const loadAll = useCallback(async () => {
    if (!tenantId || !live) {
      setRecords([]);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const agents = await apiFetch<AgentPrincipal[]>(`${base}/agents`);
      const list = Array.isArray(agents) ? agents : [];
      const loaded = await Promise.all(
        list.map(async (agent) => {
          const [credentials, grants] = await Promise.all([
            apiFetch<AgentCredential[]>(`${base}/agents/${agent.id}/credentials`).catch(() => [] as AgentCredential[]),
            apiFetch<AgentGrant[]>(`${base}/agents/${agent.id}/grants`).catch(() => [] as AgentGrant[]),
          ]);
          return { agent, credentials, grants } satisfies AgentRecord;
        }),
      );
      setRecords(loaded);
      setSelectedId((current) => (current && loaded.some((r) => r.agent.id === current) ? current : loaded[0]?.agent.id ?? ''));

      if (project) {
        const active = await apiFetch<Lease[]>(`${base}/projects/${project.id}/coordination/leases`).catch(() => [] as Lease[]);
        setLeases(Array.isArray(active) ? active : []);
      } else {
        setLeases([]);
      }
    } catch (err) {
      setError(describeError(err));
    } finally {
      setLoading(false);
    }
  }, [base, tenantId, live, project]);

  useEffect(() => {
    void loadAll();
  }, [loadAll]);

  useEffect(() => {
    if (principalId) {
      setActorId(principalId);
      return;
    }
    if (!live) return;
    apiFetch<WhoAmI>('/auth/whoami')
      .then((me) => setActorId(me.principalId ?? undefined))
      .catch(() => undefined);
  }, [principalId, live]);

  const enriched = useMemo(
    () =>
      records.map((record) => ({
        ...record,
        membership: members.find((member) => member.principalId === record.agent.id),
      })),
    [records, members],
  );

  const stats = useMemo(() => {
    const activeKeys = enriched.reduce((sum, r) => sum + r.credentials.filter((c) => credentialState(c) === 'Active').length, 0);
    const pending = enriched.reduce((sum, r) => sum + r.grants.filter((g) => g.status === 'Proposed').length, 0);
    const connected = runtimes.filter((runtime) => runtime.status === 'Connected').length;
    const activeAgents = enriched.filter((r) => r.agent.isActive && r.membership?.status !== 'Revoked').length;
    return { activeKeys, pending, connected, activeAgents };
  }, [enriched, runtimes]);

  const pendingGrants = useMemo(
    () =>
      enriched.flatMap((record) =>
        record.grants.filter((grant) => grant.status === 'Proposed').map((grant) => ({ grant, agent: record.agent })),
      ),
    [enriched],
  );

  const visible = useMemo(() => {
    const term = search.trim().toLowerCase();
    return enriched.filter((record) => {
      if (term && !record.agent.email.toLowerCase().includes(term) && !record.agent.id.includes(term)) return false;
      const activeKeys = record.credentials.filter((c) => credentialState(c) === 'Active').length;
      const offboarded = !record.agent.isActive || record.membership?.status === 'Revoked';
      switch (filter) {
        case 'attention':
          return record.grants.some((g) => g.status === 'Proposed') || (!offboarded && activeKeys === 0);
        case 'no-key':
          return activeKeys === 0;
        case 'offboarded':
          return offboarded;
        default:
          return true;
      }
    });
  }, [enriched, search, filter]);

  const selected = enriched.find((record) => record.agent.id === selectedId) ?? enriched[0];
  const selectedRuntimes = selected ? runtimes.filter((runtime) => runtime.principalId === selected.agent.id) : [];
  const selectedLeases = leases.filter((lease) => selectedRuntimes.some((runtime) => runtime.id === lease.runtimeId));

  async function act(action: () => Promise<void>, success?: string) {
    setWorking(true);
    setError(null);
    setNote(null);
    try {
      await action();
      if (success) setNote(success);
      await loadAll();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setWorking(false);
    }
  }

  function requireActor(): string {
    if (!actorId) throw new Error('Could not resolve your Owner principal. Sign in again.');
    return actorId;
  }

  async function ensureMemberships(agentId: string) {
    const current = members.find((member) => member.principalId === agentId);
    if (!current) {
      const invited = await apiFetch<Membership>(`${base}/members`, {
        method: 'POST',
        body: JSON.stringify({ principalId: agentId, role: 'Member' }),
      });
      if (invited.status !== 'Active') await apiFetch(`${base}/members/${invited.id}/activate`, { method: 'POST' });
    } else if (current.status !== 'Active') {
      await apiFetch(`${base}/members/${current.id}/activate`, { method: 'POST' });
    }
    if (project) {
      await apiFetch(`${base}/projects/${project.id}/members`, {
        method: 'POST',
        body: JSON.stringify({ principalId: agentId, role: 'Member' }),
      }).catch(() => undefined); // already a project member
    }
  }

  function registerAgent(event: FormEvent) {
    event.preventDefault();
    const email = newEmail.trim();
    if (!email) return;
    void act(async () => {
      const agent = await apiFetch<AgentPrincipal>(`${base}/agents`, { method: 'POST', body: JSON.stringify({ email }) });
      await ensureMemberships(agent.id);
      await refresh();
      setNewEmail('');
      setSelectedId(agent.id);
    }, `Registered ${email}${project ? ` and added it to ${project.name}` : ''}. Grant capabilities and issue a key next.`);
  }

  function addToProject(agentId: string) {
    if (!project) return;
    void act(async () => {
      await ensureMemberships(agentId);
      await refresh();
    }, `Agent is a member of the tenant and ${project.name}.`);
  }

  function issueKey(event: FormEvent) {
    event.preventDefault();
    if (!selected) return;
    void act(async () => {
      const credential = await apiFetch<AgentCredentialIssued>(`${base}/agents/${selected.agent.id}/credentials`, {
        method: 'POST',
        body: JSON.stringify({ label: keyForm.label.trim() || 'mcp-runtime', expiresAt: endOfDayIso(keyForm.expires) }),
      });
      setIssued(credential);
      setCopied(false);
      setKeyForm({ label: 'mcp-runtime', expires: '' });
    }, 'API key issued. Copy the secret now: it is shown only once.');
  }

  function credentialAction(credential: AgentCredential, verb: 'disable' | 'enable') {
    if (!selected) return;
    void act(async () => {
      await apiFetch(`${base}/agents/${selected.agent.id}/credentials/${credential.id}/${verb}`, {
        method: 'POST',
        body: JSON.stringify({ actor: requireActor() }),
      });
    }, verb === 'disable' ? `Key “${credential.label}” disabled. Enable it again at any time.` : `Key “${credential.label}” enabled.`);
  }

  function confirmRotate(credential: AgentCredential) {
    if (!selected) return;
    const agentId = selected.agent.id;
    setConfirmation({
      title: 'Rotate API key?',
      body: <>The key <strong>{credential.label}</strong> stops working immediately and a new secret is issued once. Update every runtime that uses it.</>,
      confirmLabel: 'Rotate key',
      action: async () => {
        const rotated = await apiFetch<AgentCredentialIssued>(`${base}/agents/${agentId}/credentials/${credential.id}/rotate`, {
          method: 'POST',
          body: JSON.stringify({ actor: requireActor(), expiresAt: credential.expiresAt ?? null }),
        });
        setIssued(rotated);
        setCopied(false);
      },
    });
  }

  function confirmRevokeKey(credential: AgentCredential) {
    if (!selected) return;
    const agentId = selected.agent.id;
    setConfirmation({
      title: 'Revoke API key?',
      body: <>Revoking <strong>{credential.label}</strong> is permanent. Runtimes using it lose access at their next token exchange.</>,
      confirmLabel: 'Revoke key',
      danger: true,
      action: async () => {
        await apiFetch(`${base}/agents/${agentId}/credentials/${credential.id}/revoke`, {
          method: 'POST',
          body: JSON.stringify({ actor: requireActor() }),
        });
        if (issued?.id === credential.id) setIssued(null);
      },
    });
  }

  function proposeGrant(event: FormEvent) {
    event.preventDefault();
    if (!selected) return;
    const projectId = grantForm.scope === 'tenant' ? null : grantForm.scope === 'project' ? project?.id ?? null : grantForm.scope;
    if (grantForm.scope === 'project' && !project) {
      setError('Select a project first, or choose tenant-wide scope.');
      return;
    }
    void act(async () => {
      if (projectId) await ensureMemberships(selected.agent.id);
      const grant = await apiFetch<AgentGrant>(`${base}/agent-grants`, {
        method: 'POST',
        body: JSON.stringify({
          agentId: selected.agent.id,
          projectId,
          capability: grantForm.capability,
          expiresAt: endOfDayIso(grantForm.expires),
          classificationCeiling: grantForm.ceiling,
        }),
      });
      if (grantForm.approveNow) {
        await apiFetch(`${base}/agent-grants/${grant.id}/approve`, {
          method: 'POST',
          body: JSON.stringify({ approver: requireActor() }),
        });
      }
    }, grantForm.approveNow
      ? `${capabilityLabel(grantForm.capability)} granted and approved.`
      : `${capabilityLabel(grantForm.capability)} proposed. It takes effect after approval.`);
  }

  function approveGrant(grant: AgentGrant) {
    void act(async () => {
      await apiFetch(`${base}/agent-grants/${grant.id}/approve`, {
        method: 'POST',
        body: JSON.stringify({ approver: requireActor() }),
      });
    }, `${capabilityLabel(grant.capability)} approved.`);
  }

  function confirmRevokeGrant(grant: AgentGrant, email: string) {
    setConfirmation({
      title: grant.status === 'Proposed' ? 'Reject grant?' : 'Revoke grant?',
      body: <><strong>{capabilityLabel(grant.capability)}</strong> for {email} is removed. Revocation is permanent; propose a new grant to restore access.</>,
      confirmLabel: grant.status === 'Proposed' ? 'Reject' : 'Revoke grant',
      danger: true,
      action: async () => {
        await apiFetch(`${base}/agent-grants/${grant.id}/revoke`, {
          method: 'POST',
          body: JSON.stringify({ revoker: requireActor() }),
        });
      },
    });
  }

  function confirmRevokeRuntime(runtimeId: string) {
    if (!project) return;
    setConfirmation({
      title: 'Disconnect runtime?',
      body: 'The runtime is revoked for this project. It must register again before it can take work.',
      confirmLabel: 'Disconnect',
      danger: true,
      action: async () => {
        await apiFetch(`${base}/projects/${project.id}/coordination/runtimes/${runtimeId}/revoke`, { method: 'POST' });
        await refresh();
      },
    });
  }

  function confirmOffboard(record: AgentRecord & { membership?: Membership }) {
    const keys = record.credentials.filter((c) => c.status !== 'Revoked');
    const grants = record.grants.filter((g) => g.status !== 'Revoked');
    setConfirmation({
      title: `Offboard ${record.agent.email}?`,
      body: (
        <>
          This revokes <strong>{keys.length}</strong> API key(s), <strong>{grants.length}</strong> grant(s) and the tenant
          membership. History stays in the audit trail. This cannot be undone.
        </>
      ),
      confirmLabel: 'Offboard agent',
      danger: true,
      action: async () => {
        const actor = requireActor();
        for (const credential of keys) {
          await apiFetch(`${base}/agents/${record.agent.id}/credentials/${credential.id}/revoke`, {
            method: 'POST',
            body: JSON.stringify({ actor }),
          });
        }
        for (const grant of grants) {
          await apiFetch(`${base}/agent-grants/${grant.id}/revoke`, { method: 'POST', body: JSON.stringify({ revoker: actor }) });
        }
        if (record.membership && record.membership.status !== 'Revoked') {
          await apiFetch(`${base}/members/${record.membership.id}/revoke`, { method: 'POST' });
        }
        await refresh();
      },
    });
  }

  async function runConfirmation() {
    if (!confirmation) return;
    const current = confirmation;
    await act(current.action, `${current.confirmLabel}: done.`);
    setConfirmation(null);
  }

  async function copySecret() {
    if (!issued) return;
    try {
      await navigator.clipboard.writeText(issued.plainTextSecret);
      setCopied(true);
    } catch {
      setNote('Clipboard blocked: select the secret and copy it manually.');
    }
  }

  const projectName = (id?: string | null) => (id ? projects.find((p) => p.id === id)?.name ?? shortId(id) : 'All projects');
  const disabled = !live || working;

  return (
    <div className="space-y-6">
      <section className="panel rounded-2xl px-6 py-6">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">Tenant · Agents</p>
            <h2 className="font-display mt-2 text-3xl font-bold tracking-tight">Agent management</h2>
            <p className="mt-2 max-w-3xl text-sm leading-relaxed text-[var(--muted)]">
              Every AI agent in this tenant: what it may do (grants), how it signs in (API keys), and where it is running
              right now. Agents only propose; grants take effect after a person approves them.
            </p>
          </div>
          <button type="button" className="btn btn-ghost" onClick={() => void loadAll()} disabled={!live || loading}>
            {loading ? 'Refreshing…' : 'Refresh'}
          </button>
        </div>
        {!live ? (
          <p className="mt-4 text-sm text-amber-800">Sign in as a tenant Owner to manage agents.</p>
        ) : null}
        {error ? (
          <div role="alert" className="mt-4 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-800">{error}</div>
        ) : null}
        {note ? <p className="mt-4 text-sm text-[var(--ink)]" role="status">{note}</p> : null}
      </section>

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <Stat label="Agents" value={stats.activeAgents} hint={`${enriched.length} registered`} />
        <Stat label="Active API keys" value={stats.activeKeys} />
        <Stat label="Awaiting approval" value={stats.pending} tone={stats.pending > 0 ? 'warn' : 'neutral'} hint="Proposed grants" />
        <Stat
          label="Connected runtimes"
          value={stats.connected}
          tone={stats.connected > 0 ? 'ok' : 'neutral'}
          hint={project ? `in ${project.name} · ${leases.length} active lease(s)` : 'select a project'}
        />
      </div>

      {issued ? (
        <section className="panel rounded-2xl border-amber-300 bg-amber-50/80 px-5 py-4" aria-live="polite">
          <p className="text-xs font-semibold uppercase tracking-[0.14em] text-amber-900">Secret shown once</p>
          <p className="mt-2 text-sm text-amber-950">
            Key <span className="font-semibold">{issued.label}</span> · credential id{' '}
            <span className="font-mono text-xs">{issued.id}</span>
          </p>
          <pre className="mt-3 overflow-x-auto rounded-lg border border-amber-200 bg-white px-3 py-2 font-mono text-xs text-[var(--ink)]">
            {issued.plainTextSecret}
          </pre>
          <div className="mt-3 flex flex-wrap gap-2">
            <button type="button" className="btn btn-primary" onClick={() => void copySecret()}>
              {copied ? 'Copied' : 'Copy secret'}
            </button>
            <button type="button" className="btn btn-ghost" onClick={() => setIssued(null)}>
              I have stored it
            </button>
          </div>
        </section>
      ) : null}

      {pendingGrants.length > 0 ? (
        <Panel title="Needs your approval" subtitle="Proposed grants have no effect until a tenant Owner approves them.">
          <ul className="divide-y divide-[var(--line)]">
            {pendingGrants.map(({ grant, agent }) => (
              <li key={grant.id} className="flex flex-wrap items-center justify-between gap-3 px-5 py-3">
                <div className="min-w-0">
                  <p className="text-sm font-semibold text-[var(--ink)]">
                    {capabilityLabel(grant.capability)} <span className="font-normal text-[var(--muted)]">for</span> {agent.email}
                  </p>
                  <p className="mt-0.5 text-xs text-[var(--muted)]">
                    {projectName(grant.projectId)} · up to {grant.classificationCeiling} · proposed {formatWhen(grant.grantedAt)}
                    {grant.expiresAt ? ` · expires ${formatWhen(grant.expiresAt)}` : ''}
                  </p>
                </div>
                <div className="flex gap-2">
                  <button type="button" className="btn btn-primary px-3 py-1.5 text-xs" disabled={disabled} onClick={() => approveGrant(grant)}>
                    Approve
                  </button>
                  <button type="button" className="btn btn-ghost px-3 py-1.5 text-xs" disabled={disabled} onClick={() => confirmRevokeGrant(grant, agent.email)}>
                    Reject
                  </button>
                </div>
              </li>
            ))}
          </ul>
        </Panel>
      ) : null}

      <div className="grid gap-6 xl:grid-cols-[minmax(0,22rem)_minmax(0,1fr)]">
        <Panel title="Agents" subtitle={`${visible.length} of ${enriched.length} shown`}>
          <form onSubmit={registerAgent} className="space-y-2 border-b border-[var(--line)] px-5 py-4">
            <label htmlFor="new-agent-email" className="text-xs font-semibold uppercase tracking-[0.12em] text-[var(--muted)]">
              Register agent
            </label>
            <div className="flex gap-2">
              <input
                id="new-agent-email"
                type="email"
                className="field font-mono text-sm"
                placeholder="builder@agents.example.com"
                value={newEmail}
                disabled={disabled}
                onChange={(event) => setNewEmail(event.target.value)}
                required
              />
              <button type="submit" className="btn btn-primary shrink-0" disabled={disabled || !newEmail.trim()}>
                Add
              </button>
            </div>
          </form>

          <div className="space-y-2 border-b border-[var(--line)] px-5 py-3">
            <input
              aria-label="Search agents"
              className="field text-sm"
              placeholder="Search by email or id"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
            <div className="flex flex-wrap gap-1.5" role="group" aria-label="Filter agents">
              {([
                ['all', 'All'],
                ['attention', 'Needs attention'],
                ['no-key', 'No active key'],
                ['offboarded', 'Offboarded'],
              ] as const).map(([value, label]) => (
                <button
                  key={value}
                  type="button"
                  aria-pressed={filter === value}
                  onClick={() => setFilter(value)}
                  className={`rounded-full px-3 py-1 text-xs font-semibold transition ${
                    filter === value ? 'bg-[var(--ink)] text-white' : 'bg-slate-100 text-[var(--muted)] hover:text-[var(--ink)]'
                  }`}
                >
                  {label}
                </button>
              ))}
            </div>
          </div>

          {visible.length === 0 ? (
            <EmptyState label={enriched.length === 0 ? 'No agents registered yet.' : 'No agents match this filter.'} />
          ) : (
            <ul className="max-h-[36rem] divide-y divide-[var(--line)] overflow-y-auto">
              {visible.map((record) => {
                const activeKeys = record.credentials.filter((c) => credentialState(c) === 'Active').length;
                const approved = record.grants.filter((g) => grantState(g) === 'Approved').length;
                const pending = record.grants.filter((g) => g.status === 'Proposed').length;
                const online = runtimes.some((r) => r.principalId === record.agent.id && r.status === 'Connected');
                const offboarded = !record.agent.isActive || record.membership?.status === 'Revoked';
                const active = record.agent.id === selected?.agent.id;
                return (
                  <li key={record.agent.id}>
                    <button
                      type="button"
                      onClick={() => setSelectedId(record.agent.id)}
                      aria-current={active ? 'true' : undefined}
                      className={`w-full px-5 py-3 text-left transition hover:bg-white/70 ${active ? 'bg-teal-50/70' : ''}`}
                    >
                      <div className="flex items-center gap-2">
                        <span
                          aria-hidden="true"
                          className={`h-2 w-2 shrink-0 rounded-full ${online ? 'bg-emerald-500' : offboarded ? 'bg-rose-400' : 'bg-slate-300'}`}
                        />
                        <p className="truncate text-sm font-semibold text-[var(--ink)]">{record.agent.email}</p>
                      </div>
                      <p className="mt-1 pl-4 text-xs text-[var(--muted)]">
                        {offboarded ? 'Offboarded' : `${activeKeys} key${activeKeys === 1 ? '' : 's'} · ${approved} grant${approved === 1 ? '' : 's'}`}
                        {online ? ' · online' : ''}
                      </p>
                      {pending > 0 ? (
                        <span className="mt-1.5 ml-4 inline-block rounded-md bg-amber-50 px-2 py-0.5 text-[11px] font-semibold text-amber-900">
                          {pending} awaiting approval
                        </span>
                      ) : null}
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </Panel>

        {!selected ? (
          <Panel title="Agent details">
            <EmptyState label="Register an agent to manage its access." />
          </Panel>
        ) : (
          <section className="panel overflow-hidden rounded-2xl">
            <header className="flex flex-wrap items-start justify-between gap-3 border-b border-[var(--line)] px-5 py-4">
              <div className="min-w-0">
                <h2 className="font-display truncate text-xl font-bold tracking-tight">{selected.agent.email}</h2>
                <p className="mt-1 font-mono text-[11px] text-[var(--muted)]">{selected.agent.id}</p>
                <div className="mt-2 flex flex-wrap gap-1.5">
                  <StateBadge label="principal" value={selected.agent.isActive ? 'Active' : 'Inactive'} />
                  <StateBadge label="tenant" value={selected.membership?.status ?? 'Not a member'} />
                  {selected.membership ? <StateBadge label="role" value={selected.membership.role} /> : null}
                </div>
              </div>
              <div className="flex flex-wrap gap-2">
                {project ? (
                  <button type="button" className="btn btn-ghost text-xs" disabled={disabled} onClick={() => addToProject(selected.agent.id)}>
                    Add to {project.name}
                  </button>
                ) : null}
                <button type="button" className="btn btn-danger text-xs" disabled={disabled} onClick={() => confirmOffboard(selected)}>
                  Offboard
                </button>
              </div>
            </header>

            <div className="space-y-8 px-5 py-5">
              {/* Grants */}
              <div className="space-y-3">
                <SectionTitle aside={<span className="text-xs text-[var(--muted)]">{selected.grants.length} total</span>}>
                  Access grants
                </SectionTitle>
                {selected.grants.length === 0 ? (
                  <p className="text-sm text-[var(--muted)]">No grants. The agent can authenticate but cannot read or do anything.</p>
                ) : (
                  <div className="overflow-x-auto rounded-xl border border-[var(--line)]">
                    <table className="w-full min-w-[36rem] text-left text-sm">
                      <thead className="bg-slate-50 text-[11px] uppercase tracking-[0.1em] text-[var(--muted)]">
                        <tr>
                          <th scope="col" className="px-3 py-2 font-semibold">Capability</th>
                          <th scope="col" className="px-3 py-2 font-semibold">Scope</th>
                          <th scope="col" className="px-3 py-2 font-semibold">Ceiling</th>
                          <th scope="col" className="px-3 py-2 font-semibold">Status</th>
                          <th scope="col" className="px-3 py-2 font-semibold">Expires</th>
                          <th scope="col" className="px-3 py-2"><span className="sr-only">Actions</span></th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-[var(--line)]">
                        {selected.grants.map((grant) => {
                          const state = grantState(grant);
                          return (
                            <tr key={grant.id} className={state === 'Revoked' ? 'text-[var(--muted)]' : ''}>
                              <td className="px-3 py-2 font-medium">{capabilityLabel(grant.capability)}</td>
                              <td className="px-3 py-2">{projectName(grant.projectId)}</td>
                              <td className="px-3 py-2">{grant.classificationCeiling}</td>
                              <td className="px-3 py-2"><StateBadge value={state} /></td>
                              <td className="px-3 py-2 text-xs">{grant.expiresAt ? formatWhen(grant.expiresAt) : 'Never'}</td>
                              <td className="px-3 py-2 text-right">
                                <div className="flex justify-end gap-1.5">
                                  {grant.status === 'Proposed' ? (
                                    <button type="button" className="btn btn-primary px-2 py-1 text-xs" disabled={disabled} onClick={() => approveGrant(grant)}>
                                      Approve
                                    </button>
                                  ) : null}
                                  {grant.status !== 'Revoked' ? (
                                    <button
                                      type="button"
                                      className="btn btn-ghost px-2 py-1 text-xs"
                                      disabled={disabled}
                                      onClick={() => confirmRevokeGrant(grant, selected.agent.email)}
                                    >
                                      {grant.status === 'Proposed' ? 'Reject' : 'Revoke'}
                                    </button>
                                  ) : null}
                                </div>
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                )}

                <form onSubmit={proposeGrant} className="grid gap-3 rounded-xl border border-dashed border-[var(--line)] p-4 sm:grid-cols-2 lg:grid-cols-4">
                  <label className="space-y-1">
                    <span className="text-[11px] font-semibold uppercase tracking-[0.12em] text-[var(--muted)]">Capability</span>
                    <select
                      className="field text-sm"
                      value={grantForm.capability}
                      disabled={disabled}
                      onChange={(event) => setGrantForm((f) => ({ ...f, capability: event.target.value }))}
                    >
                      {CAPABILITIES.map((item) => (
                        <option key={item.value} value={item.value}>{item.label}</option>
                      ))}
                    </select>
                  </label>
                  <label className="space-y-1">
                    <span className="text-[11px] font-semibold uppercase tracking-[0.12em] text-[var(--muted)]">Scope</span>
                    <select
                      className="field text-sm"
                      value={grantForm.scope}
                      disabled={disabled}
                      onChange={(event) => setGrantForm((f) => ({ ...f, scope: event.target.value }))}
                    >
                      <option value="project">Current project{project ? ` (${project.name})` : ''}</option>
                      {projects
                        .filter((item) => item.id !== project?.id)
                        .map((item) => (
                          <option key={item.id} value={item.id}>{item.name}</option>
                        ))}
                      <option value="tenant">All projects (tenant-wide)</option>
                    </select>
                  </label>
                  <label className="space-y-1">
                    <span className="text-[11px] font-semibold uppercase tracking-[0.12em] text-[var(--muted)]" title="Highest knowledge classification the agent can see">Ceiling</span>
                    <select
                      className="field text-sm"
                      value={grantForm.ceiling}
                      disabled={disabled}
                      onChange={(event) => setGrantForm((f) => ({ ...f, ceiling: event.target.value }))}
                    >
                      {CEILINGS.map((value) => (
                        <option key={value} value={value}>{value}</option>
                      ))}
                    </select>
                  </label>
                  <label className="space-y-1">
                    <span className="text-[11px] font-semibold uppercase tracking-[0.12em] text-[var(--muted)]">Expires (optional)</span>
                    <input
                      type="date"
                      className="field text-sm"
                      value={grantForm.expires}
                      disabled={disabled}
                      onChange={(event) => setGrantForm((f) => ({ ...f, expires: event.target.value }))}
                    />
                  </label>
                  <p className="text-xs leading-relaxed text-[var(--muted)] sm:col-span-2 lg:col-span-4">
                    {CAPABILITIES.find((item) => item.value === grantForm.capability)?.help}
                  </p>
                  <div className="flex flex-wrap items-center justify-between gap-3 sm:col-span-2 lg:col-span-4">
                    <label className="flex items-center gap-2 text-sm">
                      <input
                        type="checkbox"
                        checked={grantForm.approveNow}
                        disabled={disabled}
                        onChange={(event) => setGrantForm((f) => ({ ...f, approveNow: event.target.checked }))}
                      />
                      Approve now (I am the reviewing Owner)
                    </label>
                    <button type="submit" className="btn btn-primary" disabled={disabled}>
                      {grantForm.approveNow ? 'Grant access' : 'Propose grant'}
                    </button>
                  </div>
                </form>
              </div>

              {/* API keys */}
              <div className="space-y-3">
                <SectionTitle aside={<span className="text-xs text-[var(--muted)]">{selected.credentials.length} total</span>}>
                  API keys
                </SectionTitle>
                {selected.credentials.length === 0 ? (
                  <p className="text-sm text-[var(--muted)]">No keys. Issue one so a runtime can exchange it for a bearer token.</p>
                ) : (
                  <ul className="divide-y divide-[var(--line)] rounded-xl border border-[var(--line)]">
                    {[...selected.credentials]
                      .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
                      .map((credential) => {
                        const state = credentialState(credential);
                        return (
                          <li key={credential.id} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
                            <div className="min-w-0">
                              <p className={`text-sm font-semibold ${state === 'Revoked' ? 'text-[var(--muted)] line-through' : 'text-[var(--ink)]'}`}>
                                {credential.label}
                              </p>
                              <p className="mt-0.5 font-mono text-[11px] text-[var(--muted)]">
                                {shortId(credential.id)} · created {formatWhen(credential.createdAt)}
                                {credential.expiresAt ? ` · expires ${formatWhen(credential.expiresAt)}` : ''}
                                {credential.revokedAt ? ` · revoked ${formatWhen(credential.revokedAt)}` : ''}
                              </p>
                            </div>
                            <div className="flex flex-wrap items-center gap-1.5">
                              <StateBadge value={state} />
                              {credential.status === 'Active' ? (
                                <button type="button" className="btn btn-ghost px-2 py-1 text-xs" disabled={disabled} onClick={() => credentialAction(credential, 'disable')}>
                                  Disable
                                </button>
                              ) : null}
                              {credential.status === 'Disabled' ? (
                                <button type="button" className="btn btn-ghost px-2 py-1 text-xs" disabled={disabled} onClick={() => credentialAction(credential, 'enable')}>
                                  Enable
                                </button>
                              ) : null}
                              {credential.status !== 'Revoked' ? (
                                <>
                                  <button type="button" className="btn btn-ghost px-2 py-1 text-xs" disabled={disabled} onClick={() => confirmRotate(credential)}>
                                    Rotate
                                  </button>
                                  <button type="button" className="btn btn-ghost px-2 py-1 text-xs text-rose-700" disabled={disabled} onClick={() => confirmRevokeKey(credential)}>
                                    Revoke
                                  </button>
                                </>
                              ) : null}
                            </div>
                          </li>
                        );
                      })}
                  </ul>
                )}
                <form onSubmit={issueKey} className="flex flex-wrap items-end gap-3 rounded-xl border border-dashed border-[var(--line)] p-4">
                  <label className="min-w-[12rem] flex-1 space-y-1">
                    <span className="text-[11px] font-semibold uppercase tracking-[0.12em] text-[var(--muted)]">Key label</span>
                    <input
                      className="field text-sm"
                      value={keyForm.label}
                      disabled={disabled}
                      onChange={(event) => setKeyForm((f) => ({ ...f, label: event.target.value }))}
                      required
                    />
                  </label>
                  <label className="space-y-1">
                    <span className="text-[11px] font-semibold uppercase tracking-[0.12em] text-[var(--muted)]">Expires (optional)</span>
                    <input
                      type="date"
                      className="field text-sm"
                      value={keyForm.expires}
                      disabled={disabled}
                      onChange={(event) => setKeyForm((f) => ({ ...f, expires: event.target.value }))}
                    />
                  </label>
                  <button type="submit" className="btn btn-primary" disabled={disabled}>
                    Issue key
                  </button>
                </form>
              </div>

              {/* Runtimes */}
              <div className="space-y-3">
                <SectionTitle aside={<span className="text-xs text-[var(--muted)]">{project ? project.name : 'No project selected'}</span>}>
                  Runtimes and leases
                </SectionTitle>
                {selectedRuntimes.length === 0 ? (
                  <p className="text-sm text-[var(--muted)]">
                    No runtime registered in this project. A runtime appears after the agent calls <code className="font-mono text-xs">register_runtime</code>.
                  </p>
                ) : (
                  <ul className="divide-y divide-[var(--line)] rounded-xl border border-[var(--line)]">
                    {selectedRuntimes.map((runtime) => {
                      const held = selectedLeases.filter((lease) => lease.runtimeId === runtime.id);
                      return (
                        <li key={runtime.id} className="px-4 py-3">
                          <div className="flex flex-wrap items-center justify-between gap-3">
                            <div>
                              <p className="text-sm font-semibold">{runtime.runtimeClass}</p>
                              <p className="mt-0.5 font-mono text-[11px] text-[var(--muted)]">
                                {shortId(runtime.id)} · heartbeat {ago(runtime.lastHeartbeatAt)}
                              </p>
                            </div>
                            <div className="flex items-center gap-1.5">
                              <StateBadge value={runtime.status} />
                              {runtime.status !== 'Suspended' ? (
                                <button type="button" className="btn btn-ghost px-2 py-1 text-xs" disabled={disabled} onClick={() => confirmRevokeRuntime(runtime.id)}>
                                  Disconnect
                                </button>
                              ) : null}
                            </div>
                          </div>
                          {held.length > 0 ? (
                            <ul className="mt-2 space-y-1">
                              {held.map((lease) => (
                                <li key={lease.id} className="rounded-lg bg-slate-50 px-3 py-1.5 font-mono text-[11px] text-[var(--muted)]">
                                  lease {lease.resourceScope} · package {shortId(lease.workPackageId)} · expires {formatWhen(lease.expiresAt)}
                                </li>
                              ))}
                            </ul>
                          ) : null}
                        </li>
                      );
                    })}
                  </ul>
                )}
              </div>

              {/* Connect */}
              <div className="space-y-2">
                <SectionTitle aside={<Link to="/access" className="text-xs font-semibold text-[var(--signal)] hover:underline">Full MCP setup</Link>}>
                  Connect this agent
                </SectionTitle>
                <pre className="overflow-x-auto rounded-xl border border-[var(--line)] bg-white px-3 py-3 font-mono text-[11px] leading-relaxed text-[var(--ink)]">
{`# 1. Exchange an API key for a short-lived bearer token
POST ${API_ORIGIN}/tenants/${tenantId ?? '{tenantId}'}/agents/${selected.agent.id}/authenticate
{ "credentialId": "<key id>", "secret": "<secret>" }

# 2. Call MCP with  Authorization: Bearer <accessToken>
POST ${API_ORIGIN}/tenants/${tenantId ?? '{tenantId}'}/projects/${project?.id ?? '{projectId}'}/mcp/rpc`}
                </pre>
              </div>
            </div>
          </section>
        )}
      </div>

      <ConfirmDialog
        open={confirmation !== null}
        title={confirmation?.title ?? ''}
        body={confirmation?.body}
        confirmLabel={confirmation?.confirmLabel}
        danger={confirmation?.danger}
        busy={working}
        onConfirm={() => void runConfirmation()}
        onCancel={() => setConfirmation(null)}
      />
    </div>
  );
}
