import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { EmptyState, Panel } from '../components/Panel';
import { ApiError, apiFetch } from '../lib/api';
import { formatWhen } from '../lib/format';
import type { SpecDecisionView, SpecEffective, SpecParameter, SpecResolutionView, SpecTopic } from '../lib/types';
import { useWorkspace } from '../workspace/WorkspaceContext';

const ORIGIN_LABEL: Record<string, { label: string; tone: string; hint: string }> = {
  predefined: {
    label: 'Predefined',
    tone: 'bg-slate-100 text-slate-700',
    hint: 'Set by the platform at design time. Every agent follows it.',
  },
  firstapproved: {
    label: 'First approved',
    tone: 'bg-emerald-50 text-emerald-800',
    hint: 'The earliest approved decision for this topic. Later ones do not replace it.',
  },
  override: {
    label: 'Project override',
    tone: 'bg-sky-50 text-sky-800',
    hint: 'A narrower decision approved by a person overrides the wider default.',
  },
};

function topicState(resolution: SpecResolutionView | undefined, hasIssue: boolean) {
  if (!resolution?.decided) return { label: 'Undecided', tone: 'bg-amber-50 text-amber-900' };
  if (resolution.conflicts.length > 0) return { label: 'Conflict', tone: 'bg-rose-50 text-rose-800' };
  if (hasIssue) return { label: 'Incompatible', tone: 'bg-rose-50 text-rose-800' };
  return { label: 'Decided', tone: 'bg-emerald-50 text-emerald-800' };
}

const SUPPORT_TONE: Record<string, string> = {
  recommended: 'bg-emerald-50 text-emerald-800',
  supported: 'bg-slate-100 text-slate-700',
  discouraged: 'bg-amber-50 text-amber-900',
  deprecated: 'bg-rose-50 text-rose-800',
};

function Chip({ tone, children }: { tone: string; children: React.ReactNode }) {
  return <span className={`inline-flex rounded-md px-2 py-0.5 text-[11px] font-semibold uppercase tracking-wide ${tone}`}>{children}</span>;
}

function explain(error: unknown): { message: string; issues: { path: string; message: string }[] } {
  if (error instanceof ApiError) {
    try {
      const body = JSON.parse(error.body ?? '{}') as { message?: string; issues?: { path: string; message: string }[] };
      if (error.status === 422) return { message: 'That decision does not fit this topic.', issues: body.issues ?? [] };
      if (error.status === 409) return { message: body.message ?? 'This topic is locked by the platform and cannot be changed.', issues: [] };
      if (error.status === 403 || error.status === 404) return { message: 'You do not have permission to propose decisions here.', issues: [] };
    } catch {
      /* fall through */
    }
  }
  return { message: 'Could not send the decision. Check the connection and try again.', issues: [] };
}

function DecisionFacts({ decision }: { decision: SpecDecisionView }) {
  const entries = Object.entries(decision.params);
  return (
    <dl className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-[auto_1fr]">
      <dt className="text-[var(--muted)]">Choice</dt>
      <dd className="font-mono font-semibold">{decision.choice}</dd>
      {entries.map(([name, value]) => (
        <div key={name} className="contents">
          <dt className="text-[var(--muted)]">{name}</dt>
          <dd className="font-mono">{value}</dd>
        </div>
      ))}
      {decision.constraints.map((c) => (
        <div key={c.rule} className="contents">
          <dt className="text-[var(--muted)]">Constraint · {c.rule}</dt>
          <dd className="font-mono">{c.value}</dd>
        </div>
      ))}
      <dt className="text-[var(--muted)]">Scope</dt>
      <dd>
        {decision.scope}
        {decision.locked ? <span className="ml-2 text-xs font-semibold text-rose-700">Locked: cannot be overridden</span> : null}
      </dd>
    </dl>
  );
}

function ParameterField({
  parameter,
  value,
  onChange,
}: {
  parameter: SpecParameter;
  value: string;
  onChange: (value: string) => void;
}) {
  const id = `param-${parameter.name}`;
  return (
    <label className="block text-sm font-semibold" htmlFor={id}>
      {parameter.name}
      {parameter.required ? null : <span className="ml-1 text-xs font-normal text-[var(--muted)]">optional</span>}
      {parameter.kind === 'boolean' ? (
        <select id={id} className="field mt-1" value={value} onChange={(e) => onChange(e.target.value)}>
          <option value="">Choose…</option>
          <option value="true">Yes</option>
          <option value="false">No</option>
        </select>
      ) : parameter.allowedValues.length > 0 ? (
        <select id={id} className="field mt-1" value={value} onChange={(e) => onChange(e.target.value)}>
          <option value="">Choose…</option>
          {parameter.allowedValues.map((option) => (
            <option key={option} value={option}>{option}</option>
          ))}
        </select>
      ) : (
        <input
          id={id}
          className="field mt-1"
          inputMode={parameter.kind === 'integer' ? 'numeric' : undefined}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={parameter.kind === 'integer' ? 'A whole number' : 'Text'}
        />
      )}
    </label>
  );
}

export function SpecificationsPage() {
  const { tenantId, project, live, knowledge, run, refresh } = useWorkspace();
  const [topics, setTopics] = useState<SpecTopic[]>([]);
  const [effective, setEffective] = useState<SpecEffective | null>(null);
  const [loading, setLoading] = useState(false);
  const [selectedId, setSelectedId] = useState<string>();
  const [filter, setFilter] = useState<'all' | 'attention'>('all');

  const base = tenantId && project ? `/tenants/${tenantId}/projects/${project.id}` : undefined;

  const load = useCallback(async () => {
    if (!live || !tenantId || !base) return;
    setLoading(true);
    try {
      const [t, e] = await Promise.all([
        apiFetch<SpecTopic[]>(`${base}/spec/topics`),
        apiFetch<SpecEffective>(`${base}/spec/effective`),
      ]);
      setTopics(t);
      setEffective(e);
      setSelectedId((current) => current ?? t[0]?.id);
    } finally {
      setLoading(false);
    }
  }, [live, tenantId, base]);

  useEffect(() => {
    void run(load);
  }, [run, load]);

  const byTopic = useMemo(() => new Map((effective?.decisions ?? []).map((d) => [d.topic, d])), [effective]);
  const issuesByTopic = useMemo(() => {
    const map = new Map<string, string[]>();
    for (const issue of effective?.incompatibilities ?? []) map.set(issue.path, [...(map.get(issue.path) ?? []), issue.message]);
    return map;
  }, [effective]);
  const waiting = useMemo(
    () => knowledge.filter((item) => item.typeId === 'spec.decision' && item.state === 'Proposed'),
    [knowledge],
  );

  const attention = (topic: SpecTopic) => {
    const r = byTopic.get(topic.id);
    return !r?.decided || r.conflicts.length > 0 || issuesByTopic.has(topic.id);
  };
  const visible = topics.filter((t) => filter === 'all' || attention(t));
  const selected = topics.find((t) => t.id === selectedId);
  const decidedCount = (effective?.decisions ?? []).filter((d) => d.decided).length;
  const conflictCount = (effective?.decisions ?? []).reduce((sum, d) => sum + d.conflicts.length, 0);

  return (
    <div className="space-y-6">
      <section className="panel rounded-2xl px-6 py-6">
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">Specifications</p>
        <h2 className="font-display mt-2 text-3xl font-bold tracking-tight">One answer per decision, for every agent</h2>
        <p className="mt-2 max-w-3xl text-sm leading-relaxed text-[var(--muted)]">
          Each topic has one binding decision. Platform decisions are fixed at design time. If a topic is open, the first
          decision a person approves becomes the one everyone follows. A narrower decision overrides a wider one unless it is locked.
        </p>
        {!live ? (
          <p className="mt-4 text-sm text-amber-800">Connect to a live API to see this project’s specification decisions.</p>
        ) : null}
        <dl className="mt-5 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          {[
            { label: 'Topics decided', value: `${decidedCount} of ${topics.length}`, tone: '' },
            { label: 'Conflicts', value: String(conflictCount), tone: conflictCount ? 'text-rose-700' : '' },
            { label: 'Incompatible pairs', value: String(effective?.incompatibilities.length ?? 0), tone: effective?.incompatibilities.length ? 'text-rose-700' : '' },
            { label: 'Awaiting approval', value: String(waiting.length), tone: waiting.length ? 'text-amber-800' : '' },
          ].map((stat) => (
            <div key={stat.label} className="rounded-xl bg-[var(--paper)] px-4 py-3">
              <dt className="text-xs text-[var(--muted)]">{stat.label}</dt>
              <dd className={`font-display mt-1 text-2xl font-bold ${stat.tone}`}>{stat.value}</dd>
            </div>
          ))}
        </dl>
        {waiting.length > 0 ? (
          <p className="mt-4 text-sm">
            {waiting.length} proposed {waiting.length === 1 ? 'decision needs' : 'decisions need'} a person’s approval before it binds.{' '}
            {waiting.slice(0, 3).map((item) => (
              <Link key={item.id} className="mr-2 font-semibold text-[var(--signal)] hover:underline" to={`/knowledge?item=${item.id}`}>
                Review “{item.title}”
              </Link>
            ))}
          </p>
        ) : null}
      </section>

      <div className="grid gap-6 xl:grid-cols-[22rem_1fr]">
        <Panel
          title="Topics"
          subtitle="Pick a topic to see what is binding and why."
          actions={
            <div role="group" aria-label="Filter topics" className="flex gap-1 text-xs font-semibold">
              {(['all', 'attention'] as const).map((value) => (
                <button
                  key={value}
                  type="button"
                  aria-pressed={filter === value}
                  onClick={() => setFilter(value)}
                  className={`rounded-md px-2.5 py-1 ${filter === value ? 'bg-teal-50 text-[var(--signal)]' : 'text-[var(--muted)]'}`}
                >
                  {value === 'all' ? 'All' : 'Needs attention'}
                </button>
              ))}
            </div>
          }
        >
          {loading && topics.length === 0 ? <EmptyState label="Loading topics…" /> : null}
          {!loading && topics.length === 0 ? <EmptyState label={live ? 'No topics are defined.' : 'Not connected.'} /> : null}
          {topics.length > 0 && visible.length === 0 ? <EmptyState label="Nothing needs attention. Every topic is decided and consistent." /> : null}
          <ul className="divide-y divide-[var(--line)]">
            {visible.map((topic) => {
              const resolution = byTopic.get(topic.id);
              const state = topicState(resolution, issuesByTopic.has(topic.id));
              return (
                <li key={topic.id}>
                  <button
                    type="button"
                    onClick={() => setSelectedId(topic.id)}
                    aria-current={topic.id === selectedId}
                    className={`flex w-full items-start justify-between gap-3 px-5 py-3 text-left transition hover:bg-white/70 ${topic.id === selectedId ? 'bg-teal-50/70' : ''}`}
                  >
                    <span className="min-w-0">
                      <span className="block truncate text-sm font-semibold">{topic.title}</span>
                      <span className="block truncate font-mono text-xs text-[var(--muted)]">
                        {resolution?.binding ? resolution.binding.choice : topic.id}
                      </span>
                      <span className="block truncate text-[11px] text-[var(--muted)]">
                        {topic.domain}
                        {topic.source !== 'core@1.0.0' && !topic.source.startsWith('core@') ? ` · ${topic.source}` : ''}
                      </span>
                    </span>
                    <Chip tone={state.tone}>{state.label}</Chip>
                  </button>
                </li>
              );
            })}
          </ul>
        </Panel>

        {selected ? (
          <TopicDetail
            key={selected.id}
            topic={selected}
            resolution={byTopic.get(selected.id)}
            issues={issuesByTopic.get(selected.id) ?? []}
            base={base}
            run={run}
            onProposed={async () => {
              await load();
              await refresh();
            }}
          />
        ) : (
          <Panel title="Topic detail">
            <EmptyState label="Select a topic to see its binding decision and the choices it allows." />
          </Panel>
        )}
      </div>
    </div>
  );
}

function TopicDetail({
  topic,
  resolution,
  issues,
  base,
  run,
  onProposed,
}: {
  topic: SpecTopic;
  resolution?: SpecResolutionView;
  issues: string[];
  base?: string;
  run: <T>(action: () => Promise<T>) => Promise<T | undefined>;
  onProposed: () => Promise<void>;
}) {
  const [choiceId, setChoiceId] = useState('');
  const [level, setLevel] = useState<'project' | 'tenant'>('project');
  const [values, setValues] = useState<Record<string, string>>({});
  const [rationale, setRationale] = useState('');
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<{ message: string; issues: { path: string; message: string }[] }>();
  const [sent, setSent] = useState(false);

  const choice = topic.choices.find((c) => c.id === choiceId);
  const binding = resolution?.binding;
  const origin = resolution?.origin ? ORIGIN_LABEL[resolution.origin] : undefined;
  const locked = Boolean(binding?.locked);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!choice || !base) return;
    setBusy(true);
    setProblem(undefined);
    setSent(false);
    const params: Record<string, string | number | boolean> = {};
    for (const p of choice.parameters) {
      const raw = values[p.name];
      if (raw === undefined || raw === '') continue;
      params[p.name] = p.kind === 'integer' ? Number(raw) : p.kind === 'boolean' ? raw === 'true' : raw;
    }
    try {
      await apiFetch(`${base}/spec/decisions`, {
        method: 'POST',
        body: JSON.stringify({
          topicId: topic.id,
          level,
          reason: rationale || 'Proposed from Studio.',
          decision: {
            topic: topic.id,
            scope: { level },
            decision: { choice: choice.id, params },
            ...(rationale ? { rationale } : {}),
          },
        }),
      });
      setSent(true);
      setChoiceId('');
      setValues({});
      setRationale('');
      await run(onProposed);
    } catch (error) {
      setProblem(explain(error));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6">
      <Panel title={topic.title} subtitle={`${topic.description} · ${topic.id}`}>
        <div className="space-y-5 px-5 py-5">
          {binding ? (
            <>
              <div className="flex flex-wrap items-center gap-2">
                <h3 className="text-sm font-semibold">Binding decision</h3>
                {origin ? <Chip tone={origin.tone}>{origin.label}</Chip> : null}
              </div>
              {origin ? <p className="text-xs text-[var(--muted)]">{origin.hint}</p> : null}
              <DecisionFacts decision={binding} />
              <p className="text-xs text-[var(--muted)]">
                {binding.itemId ? (
                  <>
                    Source: revision {binding.revisionNumber} ·{' '}
                    <Link className="font-semibold text-[var(--signal)] hover:underline" to={`/knowledge?item=${binding.itemId}`}>
                      open the decision
                    </Link>{' '}
                    · defined {formatWhen(binding.definedAt)}
                  </>
                ) : (
                  'Source: the platform’s built-in catalog.'
                )}
              </p>
            </>
          ) : (
            <p className="rounded-lg bg-amber-50 px-4 py-3 text-sm text-amber-900">
              No decision yet. Agents will wait for one. The first decision a person approves becomes binding.
            </p>
          )}

          {issues.length > 0 ? (
            <div role="alert" className="rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-800">
              <p className="font-semibold">Doesn’t fit with other decisions</p>
              <ul className="mt-1 list-disc pl-5">{issues.map((m) => <li key={m}>{m}</li>)}</ul>
            </div>
          ) : null}

          {resolution && resolution.conflicts.length > 0 ? (
            <div role="alert" className="rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-800">
              <p className="font-semibold">Disagreeing decisions need a person to settle them</p>
              <ul className="mt-1 space-y-1">
                {resolution.conflicts.map((c, i) => (
                  <li key={i}>
                    {c.reason} Following <span className="font-mono">{c.binding.choice}</span>; also proposed{' '}
                    <span className="font-mono">{c.other.choice}</span>.{' '}
                    {c.other.itemId ? (
                      <Link className="font-semibold underline" to={`/knowledge?item=${c.other.itemId}`}>Review it</Link>
                    ) : null}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </div>
      </Panel>

      <Panel title="Allowed choices" subtitle="Only these answers are valid. Anything else is refused before it is stored.">
        <ul className="divide-y divide-[var(--line)]">
          {topic.choices.map((c) => (
            <li key={c.id} className="px-5 py-3 text-sm">
              <span className={`font-semibold ${binding?.choice === c.id ? 'text-[var(--signal)]' : ''}`}>{c.label}</span>
              <span className="ml-2 font-mono text-xs text-[var(--muted)]">{c.id}</span>
              {binding?.choice === c.id ? <span className="ml-2 text-xs text-[var(--muted)]">current</span> : null}
              <span className="ml-2 inline-flex gap-1 align-middle">
                <Chip tone={SUPPORT_TONE[c.support]}>{c.support}</Chip>
                {c.custom ? <Chip tone="bg-sky-50 text-sky-800">Custom · {c.source}</Chip> : null}
              </span>
              {c.parameters.length > 0 ? (
                <p className="mt-1 text-xs text-[var(--muted)]">
                  {c.parameters
                    .map((p) => `${p.name}${p.required ? '' : '?'}: ${p.allowedValues.length ? p.allowedValues.join(' | ') : p.kind}`)
                    .join(' · ')}
                </p>
              ) : null}
            </li>
          ))}
        </ul>
        {topic.rules.length > 0 ? (
          <p className="border-t border-[var(--line)] px-5 py-3 text-xs text-[var(--muted)]">
            Compatibility:{' '}
            {topic.rules.map((r) => `${r.whenChoice} needs ${r.otherTopic} = ${r.allowedOtherChoices.join(' or ')}`).join('; ')}.
          </p>
        ) : null}
      </Panel>

      <Panel title="Propose a decision" subtitle="A proposal is reviewed by a person. It binds only after approval.">
        {locked ? (
          <p className="px-5 py-6 text-sm text-[var(--muted)]">This topic is locked by the platform, so it cannot be changed here.</p>
        ) : (
          <form className="space-y-4 px-5 py-5" onSubmit={submit}>
            <div className="grid gap-4 sm:grid-cols-2">
              <label className="block text-sm font-semibold" htmlFor="spec-choice">
                Choice
                <select id="spec-choice" className="field mt-1" value={choiceId} onChange={(e) => { setChoiceId(e.target.value); setValues({}); setProblem(undefined); }}>
                  <option value="">Choose…</option>
                  {topic.choices.map((c) => (
                    <option key={c.id} value={c.id} disabled={c.support === 'deprecated'}>
                      {c.label}
                      {c.support === 'recommended' ? ' (recommended)' : ''}
                      {c.support === 'discouraged' ? ' (discouraged)' : ''}
                      {c.support === 'deprecated' ? ' (deprecated)' : ''}
                      {c.custom ? ' (custom)' : ''}
                    </option>
                  ))}
                </select>
              </label>
              <label className="block text-sm font-semibold" htmlFor="spec-level">
                Applies to
                <select id="spec-level" className="field mt-1" value={level} onChange={(e) => setLevel(e.target.value as 'project' | 'tenant')}>
                  <option value="project">This project only</option>
                  <option value="tenant">Every project in the organisation</option>
                </select>
              </label>
            </div>
            {choice && choice.parameters.length > 0 ? (
              <div className="grid gap-4 sm:grid-cols-2">
                {choice.parameters.map((p) => (
                  <ParameterField key={p.name} parameter={p} value={values[p.name] ?? ''} onChange={(v) => setValues((cur) => ({ ...cur, [p.name]: v }))} />
                ))}
              </div>
            ) : null}
            <label className="block text-sm font-semibold" htmlFor="spec-rationale">
              Why <span className="text-xs font-normal text-[var(--muted)]">optional, for reviewers</span>
              <textarea id="spec-rationale" className="field mt-1 min-h-20" value={rationale} onChange={(e) => setRationale(e.target.value)} />
            </label>
            {problem ? (
              <div role="alert" className="rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-800">
                <p className="font-semibold">{problem.message}</p>
                {problem.issues.length > 0 ? (
                  <ul className="mt-1 list-disc pl-5">{problem.issues.map((i) => <li key={`${i.path}${i.message}`}>{i.path.replace('/decision/', '')}: {i.message}</li>)}</ul>
                ) : null}
              </div>
            ) : null}
            {sent ? (
              <p role="status" className="rounded-lg bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
                Proposal sent. It will bind once a person approves it in Knowledge.
              </p>
            ) : null}
            <button className="btn btn-primary" type="submit" disabled={!choice || busy || !base}>
              {busy ? 'Sending…' : 'Send for approval'}
            </button>
          </form>
        )}
      </Panel>
    </div>
  );
}
