import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import { EmptyState, Panel, Row } from '../components/Panel';
import { StateBadge } from '../components/StateBadge';
import { apiFetch } from '../lib/api';
import { formatWhen, shortId } from '../lib/format';
import type {
  AgentCredential,
  AgentCredentialIssued,
  AgentPrincipal,
  LifecycleProfile,
  Membership,
  Project,
  WhoAmI,
} from '../lib/types';
import { useWorkspace } from '../workspace/WorkspaceContext';

const MCP_CAPABILITIES = ['ReadKnowledge', 'WriteKnowledge', 'AssembleContext', 'ExecuteWorkflow'] as const;
const API_ORIGIN = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') || 'http://localhost:5080';

function maskSecret(id: string): string {
  const compact = id.replace(/-/g, '');
  return `••••${compact.slice(-4) || '----'}`;
}

function slugifyKey(name: string): string {
  return name
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 48);
}

async function ensureLifecycleProfile(tenantId: string): Promise<LifecycleProfile> {
  const existing = await apiFetch<LifecycleProfile[]>(`/tenants/${tenantId}/lifecycle-profiles`);
  const list = Array.isArray(existing) ? existing : [];
  if (list[0]) return list[0];
  return apiFetch<LifecycleProfile>(`/tenants/${tenantId}/lifecycle-profiles`, {
    method: 'POST',
    body: JSON.stringify({
      key: 'default',
      name: 'Default lifecycle',
      version: '1.0',
    }),
  });
}

async function ensureTenantMembership(tenantId: string, principalId: string): Promise<void> {
  const members = await apiFetch<Membership[]>(`/tenants/${tenantId}/members`);
  const list = Array.isArray(members) ? members : [];
  const existing = list.find((item) => item.principalId === principalId);
  if (existing?.status === 'Active') return;
  if (existing) {
    await apiFetch(`/tenants/${tenantId}/members/${existing.id}/activate`, { method: 'POST' });
    return;
  }
  const invited = await apiFetch<Membership>(`/tenants/${tenantId}/members`, {
    method: 'POST',
    body: JSON.stringify({ principalId, role: 'Member' }),
  });
  if (invited.status !== 'Active') {
    await apiFetch(`/tenants/${tenantId}/members/${invited.id}/activate`, { method: 'POST' });
  }
}

async function ensureProjectMembership(tenantId: string, projectId: string, principalId: string): Promise<void> {
  try {
    await apiFetch(`/tenants/${tenantId}/projects/${projectId}/members`, {
      method: 'POST',
      body: JSON.stringify({ principalId, role: 'Member' }),
    });
  } catch {
    // Already a member (or soft conflict) — continue; grants still apply.
  }
}

async function grantAndApprove(
  tenantId: string,
  projectId: string,
  agentId: string,
  actorId: string,
  capability: string,
): Promise<void> {
  const grant = await apiFetch<{ id: string }>(`/tenants/${tenantId}/agent-grants`, {
    method: 'POST',
    body: JSON.stringify({
      agentId,
      projectId,
      capability,
      expiresAt: null,
      classificationCeiling: 'Internal',
    }),
  });
  await apiFetch(`/tenants/${tenantId}/agent-grants/${grant.id}/approve`, {
    method: 'POST',
    body: JSON.stringify({ approver: actorId }),
  });
}

export function AccessPage() {
  const {
    tenantId,
    project,
    projects,
    setProjectId,
    members,
    runtimes,
    live,
    busy,
    run,
    refresh,
    refreshProjects,
    mcp,
  } = useWorkspace();

  const [actorId, setActorId] = useState<string | null>(null);
  const [agents, setAgents] = useState<AgentPrincipal[]>([]);
  const [selectedAgentId, setSelectedAgentId] = useState<string>('');
  const [credentials, setCredentials] = useState<AgentCredential[]>([]);
  const [appForm, setAppForm] = useState({ name: '', key: '', description: '' });
  const [agentEmail, setAgentEmail] = useState('');
  const [keyLabel, setKeyLabel] = useState('mcp-runtime');
  const [issued, setIssued] = useState<AgentCredentialIssued | null>(null);
  const [copied, setCopied] = useState(false);
  const [localNote, setLocalNote] = useState<string | null>(null);

  const selectedAgent = useMemo(
    () => agents.find((agent) => agent.id === selectedAgentId) ?? agents[0],
    [agents, selectedAgentId],
  );

  const mcpPath = tenantId && project
    ? `/tenants/${tenantId}/projects/${project.id}/mcp/rpc`
    : '/tenants/{tenantId}/projects/{projectId}/mcp/rpc';
  const mcpUrl = `${API_ORIGIN}${mcpPath}`;
  const authPath = tenantId && selectedAgent
    ? `/tenants/${tenantId}/agents/${selectedAgent.id}/authenticate`
    : '/tenants/{tenantId}/agents/{agentId}/authenticate';

  const loadAgents = useCallback(async () => {
    if (!tenantId || !live) {
      setAgents([]);
      return;
    }
    const next = await apiFetch<AgentPrincipal[]>(`/tenants/${tenantId}/agents`);
    const list = Array.isArray(next) ? next : [];
    setAgents(list);
    setSelectedAgentId((current) => {
      if (current && list.some((agent) => agent.id === current)) return current;
      return list[0]?.id ?? '';
    });
  }, [tenantId, live]);

  const loadCredentials = useCallback(async (agentId: string) => {
    if (!tenantId || !live || !agentId) {
      setCredentials([]);
      return;
    }
    const next = await apiFetch<AgentCredential[]>(`/tenants/${tenantId}/agents/${agentId}/credentials`);
    setCredentials(Array.isArray(next) ? next : []);
  }, [tenantId, live]);

  useEffect(() => {
    if (!live) return;
    run(async () => {
      const me = await apiFetch<WhoAmI>('/auth/whoami');
      setActorId(me.principalId ?? null);
      await loadAgents();
    });
  }, [live, run, loadAgents]);

  useEffect(() => {
    if (!selectedAgent?.id) {
      setCredentials([]);
      return;
    }
    run(() => loadCredentials(selectedAgent.id));
  }, [selectedAgent?.id, loadCredentials, run]);

  async function createApplication(event: FormEvent) {
    event.preventDefault();
    if (!tenantId || !live) return;
    setLocalNote(null);
    setIssued(null);

    const name = appForm.name.trim();
    const key = (appForm.key.trim() || slugifyKey(name));
    if (!name || !key) {
      setLocalNote('Application name and key are required.');
      return;
    }

    const created = await run(async () => {
      const who = actorId ?? (await apiFetch<WhoAmI>('/auth/whoami')).principalId;
      if (!who) throw new Error('Could not resolve your Owner principal. Remint the pilot token.');
      setActorId(who);
      const profile = await ensureLifecycleProfile(tenantId);
      return apiFetch<Project>(`/tenants/${tenantId}/projects`, {
        method: 'POST',
        body: JSON.stringify({
          name,
          key,
          description: appForm.description.trim() || null,
          lifecycleProfileId: profile.id,
          createdBy: who,
        }),
      });
    });

    if (!created) return;
    setAppForm({ name: '', key: '', description: '' });
    await refreshProjects();
    setProjectId(created.id);
    setLocalNote(`Application “${created.name}” created. Switcher now points at it.`);
    await refresh();
  }

  async function createApiKey(event: FormEvent) {
    event.preventDefault();
    if (!tenantId || !project || !live) return;
    setLocalNote(null);
    setCopied(false);

    const result = await run(async () => {
      const who = actorId ?? (await apiFetch<WhoAmI>('/auth/whoami')).principalId;
      if (!who) throw new Error('Could not resolve your Owner principal. Remint the pilot token.');
      setActorId(who);

      const email = agentEmail.trim();
      let agent = !email ? selectedAgent : undefined;
      if (!agent) {
        agent = await apiFetch<AgentPrincipal>(`/tenants/${tenantId}/agents`, {
          method: 'POST',
          body: JSON.stringify({
            email: email || `mcp-${slugifyKey(project.key) || 'agent'}@agents.devweave.local`,
          }),
        });
      }

      await ensureTenantMembership(tenantId, agent.id);
      await ensureProjectMembership(tenantId, project.id, agent.id);

      for (const capability of MCP_CAPABILITIES) {
        try {
          await grantAndApprove(tenantId, project.id, agent.id, who, capability);
        } catch {
          // Duplicate grants are fine; agent may already be provisioned for this project.
        }
      }

      const credential = await apiFetch<AgentCredentialIssued>(
        `/tenants/${tenantId}/agents/${agent.id}/credentials`,
        {
          method: 'POST',
          body: JSON.stringify({ label: keyLabel.trim() || 'mcp-runtime', expiresAt: null }),
        },
      );

      return { agent, credential };
    });

    if (!result) return;
    setAgentEmail('');
    setKeyLabel('mcp-runtime');
    setSelectedAgentId(result.agent.id);
    setIssued(result.credential);
    await loadAgents();
    await loadCredentials(result.agent.id);
    await refresh();
    setLocalNote('API key issued. Copy the secret now — it is not shown again.');
  }

  async function revokeCredential(credentialId: string) {
    if (!tenantId || !selectedAgent || !actorId) return;
    const updated = await run(() =>
      apiFetch<AgentCredential>(
        `/tenants/${tenantId}/agents/${selectedAgent.id}/credentials/${credentialId}/revoke`,
        {
          method: 'POST',
          body: JSON.stringify({ actor: actorId }),
        },
      ),
    );
    if (updated) {
      await loadCredentials(selectedAgent.id);
      if (issued?.id === credentialId) setIssued(null);
    }
  }

  async function copySecret() {
    if (!issued?.plainTextSecret) return;
    try {
      await navigator.clipboard.writeText(issued.plainTextSecret);
      setCopied(true);
    } catch {
      setLocalNote('Clipboard blocked — select and copy the secret manually.');
    }
  }

  return (
    <div className="space-y-6">
      <section className="panel rounded-2xl px-6 py-6">
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">Tenant · Access</p>
        <h2 className="font-display mt-2 text-3xl font-bold tracking-tight">Applications and MCP credentials</h2>
        <p className="mt-2 max-w-3xl text-sm leading-relaxed text-[var(--muted)]">
          Create an application (project), issue an AI-agent API key, then point an MCP client at the project RPC
          endpoint with a bearer token from credential exchange. Full runtime rules live in{' '}
          <code className="font-mono text-xs">docs/agent-guide.md</code>.
        </p>
        {!live ? (
          <p className="mt-4 text-sm text-amber-800">
            Live mode needs a signed-in Owner session (or local env token fallback).
          </p>
        ) : null}
        {localNote ? <p className="mt-4 text-sm text-[var(--ink)]">{localNote}</p> : null}
      </section>

      <div className="grid gap-6 xl:grid-cols-2">
        <Panel title="Applications" subtitle="Tenant projects Owners can switch into from the header.">
          <form onSubmit={createApplication} className="space-y-3 border-b border-[var(--line)] px-5 py-4">
            <div>
              <label htmlFor="app-name" className="text-xs font-semibold uppercase tracking-[0.12em] text-[var(--muted)]">
                Name
              </label>
              <input
                id="app-name"
                className="field mt-1"
                required
                disabled={!live || busy}
                value={appForm.name}
                onChange={(event) => {
                  const name = event.target.value;
                  setAppForm((current) => ({
                    ...current,
                    name,
                    key: current.key || slugifyKey(name),
                  }));
                }}
                placeholder="Online Cake Shop"
              />
            </div>
            <div>
              <label htmlFor="app-key" className="text-xs font-semibold uppercase tracking-[0.12em] text-[var(--muted)]">
                Key
              </label>
              <input
                id="app-key"
                className="field mt-1 font-mono text-sm"
                required
                disabled={!live || busy}
                value={appForm.key}
                onChange={(event) => setAppForm((current) => ({ ...current, key: slugifyKey(event.target.value) }))}
                placeholder="online-cake-shop"
              />
            </div>
            <div>
              <label htmlFor="app-desc" className="text-xs font-semibold uppercase tracking-[0.12em] text-[var(--muted)]">
                Description
              </label>
              <textarea
                id="app-desc"
                className="field mt-1 min-h-16"
                disabled={!live || busy}
                value={appForm.description}
                onChange={(event) => setAppForm((current) => ({ ...current, description: event.target.value }))}
                placeholder="Optional short summary"
              />
            </div>
            <button type="submit" className="btn btn-primary" disabled={!live || busy}>
              Create application
            </button>
          </form>
          {projects.length === 0 ? (
            <EmptyState label="No applications yet." />
          ) : (
            <div className="divide-y divide-[var(--line)]">
              {projects.map((item) => (
                <Row
                  key={item.id}
                  title={item.name}
                  subtitle={`${item.key} · ${shortId(item.id)}`}
                  active={item.id === project?.id}
                  onClick={() => setProjectId(item.id)}
                  meta={
                    <div className="flex flex-col items-end gap-1">
                      {item.status ? <StateBadge value={item.status} /> : null}
                      {item.id === project?.id ? (
                        <span className="text-[10px] font-semibold uppercase tracking-[0.12em] text-[var(--signal)]">
                          Current
                        </span>
                      ) : null}
                    </div>
                  }
                />
              ))}
            </div>
          )}
        </Panel>

        <Panel
          title="MCP API keys"
          subtitle="Issue a credential for an AI-agent principal on the current application."
        >
          <form onSubmit={createApiKey} className="space-y-3 border-b border-[var(--line)] px-5 py-4">
            <p className="text-xs leading-relaxed text-[var(--muted)]">
              Current application:{' '}
              <span className="font-semibold text-[var(--ink)]">{project?.name ?? '—'}</span>
              {project ? (
                <span className="ml-2 font-mono text-[10px] text-[var(--muted)]">{project.key}</span>
              ) : null}
            </p>
            {agents.length > 0 ? (
              <div>
                <label htmlFor="agent-select" className="text-xs font-semibold uppercase tracking-[0.12em] text-[var(--muted)]">
                  Existing agent
                </label>
                <select
                  id="agent-select"
                  className="field mt-1"
                  disabled={!live || busy}
                  value={selectedAgent?.id ?? ''}
                  onChange={(event) => setSelectedAgentId(event.target.value)}
                >
                  {agents.map((agent) => (
                    <option key={agent.id} value={agent.id}>
                      {agent.email} ({shortId(agent.id)})
                    </option>
                  ))}
                </select>
              </div>
            ) : (
              <div>
                <label htmlFor="agent-email" className="text-xs font-semibold uppercase tracking-[0.12em] text-[var(--muted)]">
                  New agent email
                </label>
                <input
                  id="agent-email"
                  className="field mt-1 font-mono text-sm"
                  disabled={!live || busy}
                  value={agentEmail}
                  onChange={(event) => setAgentEmail(event.target.value)}
                  placeholder="mcp-runtime@agents.devweave.local"
                />
              </div>
            )}
            {agents.length > 0 ? (
              <div>
                <label htmlFor="agent-email-alt" className="text-xs font-semibold uppercase tracking-[0.12em] text-[var(--muted)]">
                  Or register another agent email
                </label>
                <input
                  id="agent-email-alt"
                  className="field mt-1 font-mono text-sm"
                  disabled={!live || busy}
                  value={agentEmail}
                  onChange={(event) => setAgentEmail(event.target.value)}
                  placeholder="Leave blank to reuse the selected agent"
                />
              </div>
            ) : null}
            <div>
              <label htmlFor="key-label" className="text-xs font-semibold uppercase tracking-[0.12em] text-[var(--muted)]">
                Key label
              </label>
              <input
                id="key-label"
                className="field mt-1"
                required
                disabled={!live || busy}
                value={keyLabel}
                onChange={(event) => setKeyLabel(event.target.value)}
              />
            </div>
            <button type="submit" className="btn btn-primary" disabled={!live || busy || !project}>
              {agents.length === 0 || agentEmail.trim()
                ? 'Register agent + create API key'
                : 'Create API key for selected agent'}
            </button>
            <p className="text-[11px] leading-relaxed text-[var(--muted)]">
              Also grants project membership and approves Read/Write knowledge, assemble-context, and execute-workflow
              capabilities for MCP.
            </p>
          </form>

          {issued ? (
            <div className="border-b border-[var(--line)] bg-amber-50/70 px-5 py-4">
              <p className="text-xs font-semibold uppercase tracking-[0.14em] text-amber-900">Secret shown once</p>
              <p className="mt-2 text-sm text-amber-950">
                Label <span className="font-semibold">{issued.label}</span> · credential{' '}
                <span className="font-mono text-xs">{issued.id}</span>
              </p>
              <pre className="mt-3 overflow-x-auto rounded-lg border border-amber-200 bg-white px-3 py-2 font-mono text-xs text-[var(--ink)]">
                {issued.plainTextSecret}
              </pre>
              <div className="mt-3 flex flex-wrap gap-2">
                <button type="button" className="btn btn-ghost" onClick={() => void copySecret()}>
                  {copied ? 'Copied' : 'Copy secret'}
                </button>
                <button type="button" className="btn btn-ghost" onClick={() => setIssued(null)}>
                  Dismiss
                </button>
              </div>
            </div>
          ) : null}

          {!selectedAgent ? (
            <EmptyState label="No agent principals yet. Create an API key to register one." />
          ) : credentials.length === 0 ? (
            <EmptyState label="No credentials for this agent." />
          ) : (
            <div className="divide-y divide-[var(--line)]">
              {credentials.map((credential) => (
                <Row
                  key={credential.id}
                  title={credential.label}
                  subtitle={`${maskSecret(credential.id)} · created ${formatWhen(credential.createdAt)}`}
                  meta={
                    <div className="flex flex-col items-end gap-2">
                      <StateBadge value={credential.status} />
                      {credential.status === 'Active' ? (
                        <button
                          type="button"
                          className="btn btn-ghost px-2 py-1 text-xs"
                          disabled={!live || busy || !actorId}
                          onClick={() => void revokeCredential(credential.id)}
                        >
                          Revoke
                        </button>
                      ) : null}
                    </div>
                  }
                />
              ))}
            </div>
          )}
        </Panel>
      </div>

      <Panel title="MCP connection" subtitle="Wire an MCP client to the current application.">
        <dl className="grid gap-4 px-5 py-5 sm:grid-cols-2">
          <div className="sm:col-span-2">
            <dt className="text-xs uppercase tracking-[0.14em] text-[var(--muted)]">RPC endpoint</dt>
            <dd className="mt-1 break-all font-mono text-sm">{mcpUrl}</dd>
            <dd className="mt-1 text-xs text-[var(--muted)]">POST · JSON-RPC 2.0 · protocol version 2026-07-28</dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-[0.14em] text-[var(--muted)]">Directory (Owner)</dt>
            <dd className="mt-1 font-mono text-sm">{mcp?.serviceRef ?? '—'}</dd>
            <dd className="mt-1 text-xs text-[var(--muted)]">
              {(mcp?.capabilities ?? []).join(', ') || 'Load Live API to see advertised capabilities'}
            </dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-[0.14em] text-[var(--muted)]">Tenant / project</dt>
            <dd className="mt-1 font-mono text-xs">{tenantId ?? '—'}</dd>
            <dd className="mt-1 font-mono text-xs text-[var(--muted)]">{project?.id ?? '—'}</dd>
          </div>
          <div className="sm:col-span-2">
            <dt className="text-xs uppercase tracking-[0.14em] text-[var(--muted)]">Authentication</dt>
            <dd className="mt-2 space-y-2 text-sm leading-relaxed text-[var(--muted)]">
              <p>
                1. Exchange the API key secret for a short-lived agent bearer token:
              </p>
              <pre className="overflow-x-auto rounded-lg border border-[var(--line)] bg-white px-3 py-2 font-mono text-[11px] text-[var(--ink)]">
{`POST ${API_ORIGIN}${authPath}
{ "credentialId": "<credential id>", "secret": "<plainTextSecret>" }`}
              </pre>
              <p>
                2. Call MCP with{' '}
                <code className="font-mono text-xs text-[var(--ink)]">Authorization: Bearer &lt;accessToken&gt;</code>.
              </p>
              <p>
                3. After <code className="font-mono text-xs text-[var(--ink)]">register_runtime</code>, send{' '}
                <code className="font-mono text-xs text-[var(--ink)]">X-Runtime-Id: &lt;runtime id&gt;</code> on every
                subsequent request.
              </p>
            </dd>
          </div>
        </dl>
      </Panel>

      <div className="grid gap-6 xl:grid-cols-2">
        <Panel title="Tenant memberships" subtitle="Humans and agents invited to this tenant (UI-01).">
          {members.length === 0 ? (
            <EmptyState label="No memberships loaded." />
          ) : (
            <div className="divide-y divide-[var(--line)]">
              {members.map((member) => (
                <Row
                  key={member.id}
                  title={shortId(member.principalId)}
                  subtitle={`Principal ${member.principalId}`}
                  meta={
                    <div className="flex flex-col items-end gap-1">
                      <StateBadge value={member.role} />
                      <StateBadge value={member.status} />
                    </div>
                  }
                />
              ))}
            </div>
          )}
        </Panel>

        <Panel title="Agent runtimes" subtitle="Registered runtime instances for the current project.">
          {runtimes.length === 0 ? (
            <EmptyState label="No agent runtimes registered." />
          ) : (
            <div className="divide-y divide-[var(--line)]">
              {runtimes.map((runtime) => (
                <Row
                  key={runtime.id}
                  title={runtime.runtimeClass}
                  subtitle={`Agent ${shortId(runtime.principalId)}`}
                  meta={<StateBadge value={runtime.status} />}
                />
              ))}
            </div>
          )}
        </Panel>
      </div>
    </div>
  );
}
