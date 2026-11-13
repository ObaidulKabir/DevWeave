import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import { EmptyState, Panel } from '../components/Panel';
import { Chip, ParameterField, ProblemBox, SUPPORT_TONE, explainSpecError, type SpecProblem } from '../components/SpecBits';
import { apiFetch } from '../lib/api';
import type { SpecCatalog, SpecPackStatus, SpecProfile, SpecUpgrade } from '../lib/types';
import { useWorkspace } from '../workspace/WorkspaceContext';

type Level = 'project' | 'tenant';
type ExtendMode = 'add-option' | 'deprecate-option' | 'hide-option' | 'add-topic';

const MODES: { value: ExtendMode; label: string; hint: string }[] = [
  { value: 'add-option', label: 'Add an option to a topic', hint: 'For example your own framework under Backend framework.' },
  { value: 'deprecate-option', label: 'Deprecate an option', hint: 'Keeps existing decisions working but stops new ones from choosing it.' },
  { value: 'hide-option', label: 'Hide an option', hint: 'Removes it from the choices people and agents see. The platform’s own choice cannot be hidden.' },
  { value: 'add-topic', label: 'Add your own topic', hint: 'A new decision area under your own namespace, with the answers you allow.' },
];

function useSender(base: string | undefined, run: <T>(action: () => Promise<T>) => Promise<T | undefined>, after: () => Promise<void>) {
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<SpecProblem>();
  const [sent, setSent] = useState<string>();

  const send = async (path: string, body: unknown, message: string) => {
    if (!base) return false;
    setBusy(true);
    setProblem(undefined);
    setSent(undefined);
    try {
      await apiFetch(`${base}${path}`, { method: 'POST', body: JSON.stringify(body) });
      setSent(message);
      await run(after);
      return true;
    } catch (error) {
      setProblem(explainSpecError(error));
      return false;
    } finally {
      setBusy(false);
    }
  };

  return { busy, problem, sent, send, clear: () => { setProblem(undefined); setSent(undefined); } };
}

export function CatalogPage() {
  const { tenantId, project, live, run } = useWorkspace();
  const [catalog, setCatalog] = useState<SpecCatalog | null>(null);
  const [upgrades, setUpgrades] = useState<SpecUpgrade[]>([]);
  const [level, setLevel] = useState<Level>('project');

  const base = tenantId && project ? `/tenants/${tenantId}/projects/${project.id}/spec` : undefined;

  const load = useCallback(async () => {
    if (!live || !base) return;
    const [c, u] = await Promise.all([
      apiFetch<SpecCatalog>(`${base}/catalog`),
      apiFetch<SpecUpgrade[]>(`${base}/upgrades`),
    ]);
    setCatalog(c);
    setUpgrades(u);
  }, [live, base]);

  useEffect(() => {
    void run(load);
  }, [run, load]);

  return (
    <div className="space-y-6">
      <section className="panel rounded-2xl px-6 py-6">
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">Catalog</p>
        <h2 className="font-display mt-2 text-3xl font-bold tracking-tight">Start from the built-in standards, then make them yours</h2>
        <p className="mt-2 max-w-3xl text-sm leading-relaxed text-[var(--muted)]">
          DevWeave ships topics, options and ready-made profiles. Turn on optional packs, apply a whole profile with one approval,
          or extend the catalog with your own options and topics. Nothing changes until a person approves it in Knowledge.
        </p>
        {!live ? <p className="mt-4 text-sm text-amber-800">Connect to a live API to manage the catalog.</p> : null}
        <label className="mt-5 block max-w-xs text-sm font-semibold" htmlFor="catalog-level">
          Changes apply to
          <select id="catalog-level" className="field mt-1" value={level} onChange={(e) => setLevel(e.target.value as Level)}>
            <option value="project">This project only</option>
            <option value="tenant">Every project in the organisation</option>
          </select>
        </label>
      </section>

      {catalog && catalog.problems.length > 0 ? (
        <div role="alert" className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
          <p className="font-semibold">Some approved extensions are not being applied</p>
          <ul className="mt-1 list-disc pl-5">
            {catalog.problems.flatMap((p) => p.issues.map((i) => <li key={`${p.source}${i.message}`}>{p.source}: {i.message}</li>))}
          </ul>
        </div>
      ) : null}

      {!catalog ? (
        <Panel title="Catalog"><EmptyState label={live ? 'Loading the catalog…' : 'Not connected.'} /></Panel>
      ) : (
        <>
          <PacksPanel catalog={catalog} upgrades={upgrades} level={level} base={base} run={run} reload={load} />
          <ProfilesPanel catalog={catalog} level={level} base={base} run={run} reload={load} />
          <ExtendPanel catalog={catalog} level={level} base={base} run={run} reload={load} />
        </>
      )}
    </div>
  );
}

interface PanelProps {
  catalog: SpecCatalog;
  level: Level;
  base?: string;
  run: <T>(action: () => Promise<T>) => Promise<T | undefined>;
  reload: () => Promise<void>;
}

function PacksPanel({ catalog, upgrades, level, base, run, reload }: PanelProps & { upgrades: SpecUpgrade[] }) {
  const sender = useSender(base, run, reload);
  const overlay = (extra: Record<string, unknown>) => ({ overlay: { scope: { level }, ...extra }, reason: 'Proposed from the Catalog page.' });

  return (
    <Panel title="Packs" subtitle="Versioned sets of topics, options and profiles that ship with DevWeave.">
      <ul className="divide-y divide-[var(--line)]">
        {catalog.packs.map((pack: SpecPackStatus) => {
          const upgrade = upgrades.find((u) => u.pack === pack.id);
          return (
            <li key={pack.id} className="space-y-3 px-5 py-4">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p className="text-sm font-semibold">
                    {pack.id} <span className="font-mono text-xs font-normal text-[var(--muted)]">v{pack.version}</span>
                  </p>
                  <p className="mt-1 text-xs text-[var(--muted)]">{pack.description}</p>
                  <p className="mt-2 flex flex-wrap gap-1.5">
                    <Chip tone={pack.enabled ? 'bg-emerald-50 text-emerald-800' : 'bg-slate-100 text-slate-700'}>
                      {pack.required ? 'Always on' : pack.enabled ? 'On' : 'Off'}
                    </Chip>
                    {pack.pinned ? <Chip tone="bg-sky-50 text-sky-800">Held at v{pack.version}</Chip> : null}
                    {pack.updateAvailable ? <Chip tone="bg-amber-50 text-amber-900">v{pack.latestVersion} available</Chip> : null}
                  </p>
                </div>
                {!pack.enabled ? (
                  <button
                    type="button"
                    className="btn btn-ghost"
                    disabled={sender.busy}
                    onClick={() => void sender.send('/overlays', overlay({ enablePacks: [pack.id] }), `Turning on “${pack.id}” was sent for approval.`)}
                  >
                    Turn on
                  </button>
                ) : null}
              </div>

              {upgrade ? (
                <div className="rounded-lg bg-[var(--paper)] px-4 py-3 text-sm">
                  <p className="font-semibold">What changes in v{upgrade.to}</p>
                  <ul className="mt-1 list-disc pl-5 text-xs text-[var(--muted)]">
                    {upgrade.topicsAdded.map((t) => <li key={`a${t}`}>New topic: {t}</li>)}
                    {upgrade.topicsRemoved.map((t) => <li key={`r${t}`}>Topic removed: {t}</li>)}
                    {upgrade.choiceChanges.map((c) => <li key={`${c.topic}${c.choice}${c.change}`}>{c.topic}: option “{c.choice}” {c.change}</li>)}
                    {upgrade.defaultChanges.map((d) => <li key={`d${d.topic}`}>{d.topic}: recommended becomes “{d.to ?? 'none'}”</li>)}
                    {upgrade.profilesAdded.map((p) => <li key={`pa${p}`}>New profile: {p}</li>)}
                    {upgrade.profilesRemoved.map((p) => <li key={`pr${p}`}>Profile removed: {p}</li>)}
                  </ul>
                  {upgrade.affectedDecisions.length > 0 ? (
                    <div role="alert" className="mt-3 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900">
                      <p className="font-semibold">Decisions you would need to revisit</p>
                      <ul className="mt-1 list-disc pl-4">{upgrade.affectedDecisions.map((a) => <li key={a}>{a}</li>)}</ul>
                    </div>
                  ) : (
                    <p className="mt-3 text-xs text-emerald-800">None of your current decisions are affected.</p>
                  )}
                  <button
                    type="button"
                    className="btn btn-primary mt-3"
                    disabled={sender.busy}
                    onClick={() => void sender.send('/overlays', overlay({ pinPacks: [{ pack: pack.id, version: upgrade.to }] }), `Moving “${pack.id}” to v${upgrade.to} was sent for approval.`)}
                  >
                    Propose moving to v{upgrade.to}
                  </button>
                </div>
              ) : null}
            </li>
          );
        })}
      </ul>
      <div className="space-y-3 px-5 pb-4">
        {sender.problem ? <ProblemBox problem={sender.problem} /> : null}
        {sender.sent ? <p role="status" className="rounded-lg bg-emerald-50 px-4 py-3 text-sm text-emerald-800">{sender.sent}</p> : null}
      </div>
    </Panel>
  );
}

function ProfilesPanel({ catalog, level, base, run, reload }: PanelProps) {
  const sender = useSender(base, run, reload);
  const [openId, setOpenId] = useState<string>();
  const open = catalog.profiles.find((p) => p.id === openId);

  return (
    <Panel title="Profiles" subtitle="A whole set of decisions in one step. Review each line, change what you need, and one approval covers the lot.">
      {catalog.profiles.length === 0 ? <EmptyState label="No profiles are available." /> : (
        <ul className="grid gap-3 p-5 md:grid-cols-2">
          {catalog.profiles.map((profile) => (
            <li key={profile.id}>
              <button
                type="button"
                aria-pressed={profile.id === openId}
                onClick={() => { setOpenId(profile.id === openId ? undefined : profile.id); sender.clear(); }}
                className={`w-full rounded-xl border px-4 py-3 text-left transition hover:bg-white/70 ${profile.id === openId ? 'border-[var(--signal)] bg-teal-50/60' : 'border-[var(--line)]'}`}
              >
                <span className="block text-sm font-semibold">{profile.label}</span>
                <span className="mt-1 block text-xs text-[var(--muted)]">{profile.description}</span>
                <span className="mt-2 flex gap-1.5">
                  <Chip tone="bg-slate-100 text-slate-700">{profile.projectType}</Chip>
                  <Chip tone="bg-slate-100 text-slate-700">{profile.decisions.length} decisions</Chip>
                  {profile.source !== 'tenant' && profile.source !== 'project' ? null : <Chip tone="bg-sky-50 text-sky-800">Custom</Chip>}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
      {open ? <ProfileReview key={open.id} profile={open} catalog={catalog} level={level} sender={sender} /> : null}
    </Panel>
  );
}

function ProfileReview({
  profile,
  catalog,
  level,
  sender,
}: {
  profile: SpecProfile;
  catalog: SpecCatalog;
  level: Level;
  sender: ReturnType<typeof useSender>;
}) {
  const [lines, setLines] = useState(() => profile.decisions.map((d) => ({ topic: d.topic, choice: d.choice, params: { ...d.params } })));
  const topics = useMemo(() => new Map(catalog.topics.map((t) => [t.id, t])), [catalog]);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const typed = lines.map((l) => {
      const choice = topics.get(l.topic)?.choices.find((c) => c.id === l.choice);
      const params: Record<string, string | number | boolean> = {};
      for (const p of choice?.parameters ?? []) {
        const raw = l.params[p.name];
        if (raw === undefined || raw === '') continue;
        params[p.name] = p.kind === 'integer' ? Number(raw) : p.kind === 'boolean' ? raw === 'true' : raw;
      }
      return { topic: l.topic, choice: l.choice, params };
    });
    await sender.send(
      '/bundles',
      { profileId: profile.id, level, decisions: typed, reason: `Apply the “${profile.label}” profile.` },
      `“${profile.label}” was sent as one bundle. One approval in Knowledge makes all ${typed.length} decisions binding.`,
    );
  };

  return (
    <form className="space-y-4 border-t border-[var(--line)] px-5 py-5" onSubmit={submit}>
      <h3 className="text-sm font-semibold">Review “{profile.label}”</h3>
      <ul className="space-y-3">
        {lines.map((line, index) => {
          const topic = topics.get(line.topic);
          const choice = topic?.choices.find((c) => c.id === line.choice);
          return (
            <li key={line.topic} className="rounded-lg bg-[var(--paper)] px-4 py-3">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <p className="text-sm font-semibold">{topic?.title ?? line.topic}</p>
                <select
                  aria-label={`Choice for ${topic?.title ?? line.topic}`}
                  className="field w-auto"
                  value={line.choice}
                  onChange={(e) => setLines((cur) => cur.map((l, i) => (i === index ? { ...l, choice: e.target.value, params: {} } : l)))}
                >
                  {(topic?.choices ?? []).map((c) => (
                    <option key={c.id} value={c.id} disabled={c.support === 'deprecated'}>
                      {c.label}{c.support === 'recommended' ? ' (recommended)' : ''}{c.support === 'deprecated' ? ' (deprecated)' : ''}
                    </option>
                  ))}
                </select>
              </div>
              {choice && choice.parameters.length > 0 ? (
                <div className="mt-3 grid gap-3 sm:grid-cols-2">
                  {choice.parameters.map((p) => (
                    <ParameterField
                      key={p.name}
                      idPrefix={`pf-${line.topic}`}
                      parameter={p}
                      value={line.params[p.name] ?? ''}
                      onChange={(v) => setLines((cur) => cur.map((l, i) => (i === index ? { ...l, params: { ...l.params, [p.name]: v } } : l)))}
                    />
                  ))}
                </div>
              ) : null}
              {choice ? <Chip tone={SUPPORT_TONE[choice.support]}>{choice.support}</Chip> : null}
            </li>
          );
        })}
      </ul>
      {sender.problem ? <ProblemBox problem={sender.problem} /> : null}
      {sender.sent ? <p role="status" className="rounded-lg bg-emerald-50 px-4 py-3 text-sm text-emerald-800">{sender.sent}</p> : null}
      <button className="btn btn-primary" type="submit" disabled={sender.busy}>
        {sender.busy ? 'Sending…' : `Send ${lines.length} decisions for one approval`}
      </button>
    </form>
  );
}

function ExtendPanel({ catalog, level, base, run, reload }: PanelProps) {
  const sender = useSender(base, run, reload);
  const [mode, setMode] = useState<ExtendMode>('add-option');
  const [topicId, setTopicId] = useState('');
  const [choiceId, setChoiceId] = useState('');
  const [label, setLabel] = useState('');
  const [support, setSupport] = useState('supported');
  const [namespace, setNamespace] = useState('');
  const [key, setKey] = useState('');
  const [title, setTitle] = useState('');
  const [answers, setAnswers] = useState('');

  const topic = catalog.topics.find((t) => t.id === topicId);
  const modeInfo = MODES.find((m) => m.value === mode)!;
  const slug = (value: string) => value.trim().toLowerCase().replace(/[^a-z0-9-]+/g, '-').replace(/^-+|-+$/g, '');

  const build = (): Record<string, unknown> | undefined => {
    switch (mode) {
      case 'add-option':
        return topicId && choiceId.trim()
          ? { addChoices: [{ topic: topicId, choice: { id: slug(choiceId), label: label.trim() || choiceId.trim(), support } }] }
          : undefined;
      case 'deprecate-option':
        return topicId && choiceId ? { deprecateChoices: [{ topic: topicId, choice: choiceId }] } : undefined;
      case 'hide-option':
        return topicId && choiceId ? { hideChoices: [{ topic: topicId, choice: choiceId }] } : undefined;
      case 'add-topic': {
        const ns = slug(namespace);
        const options = answers.split(',').map((a) => slug(a)).filter(Boolean);
        return ns && slug(key) && title.trim() && options.length > 0
          ? {
              namespace: ns,
              addTopics: [{ id: `${ns}.${slug(key)}`, title: title.trim(), domain: 'custom', choices: options.map((id) => ({ id })) }],
            }
          : undefined;
      }
    }
  };

  const payload = build();

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!payload) return;
    const ok = await sender.send(
      '/overlays',
      { overlay: { scope: { level }, ...payload }, reason: 'Proposed from the Catalog page.' },
      'Sent for approval. It applies once a person approves it in Knowledge.',
    );
    if (ok) {
      setChoiceId('');
      setLabel('');
      setKey('');
      setTitle('');
      setAnswers('');
    }
  };

  return (
    <Panel title="Extend the catalog" subtitle="Add what is specific to you. The platform’s locked rules always stay in force.">
      <form className="space-y-4 px-5 py-5" onSubmit={submit}>
        <label className="block text-sm font-semibold" htmlFor="extend-mode">
          What do you want to do?
          <select id="extend-mode" className="field mt-1" value={mode} onChange={(e) => { setMode(e.target.value as ExtendMode); setChoiceId(''); sender.clear(); }}>
            {MODES.map((m) => <option key={m.value} value={m.value}>{m.label}</option>)}
          </select>
        </label>
        <p className="text-xs text-[var(--muted)]">{modeInfo.hint}</p>

        {mode !== 'add-topic' ? (
          <div className="grid gap-4 sm:grid-cols-2">
            <label className="block text-sm font-semibold" htmlFor="extend-topic">
              Topic
              <select id="extend-topic" className="field mt-1" value={topicId} onChange={(e) => { setTopicId(e.target.value); setChoiceId(''); }}>
                <option value="">Choose…</option>
                {catalog.topics.map((t) => <option key={t.id} value={t.id}>{t.title}</option>)}
              </select>
            </label>
            {mode === 'add-option' ? (
              <label className="block text-sm font-semibold" htmlFor="extend-choice">
                New option name
                <input id="extend-choice" className="field mt-1" value={choiceId} onChange={(e) => setChoiceId(e.target.value)} placeholder="for example acme-kit" />
              </label>
            ) : (
              <label className="block text-sm font-semibold" htmlFor="extend-choice">
                Option
                <select id="extend-choice" className="field mt-1" value={choiceId} onChange={(e) => setChoiceId(e.target.value)} disabled={!topic}>
                  <option value="">Choose…</option>
                  {(topic?.choices ?? []).map((c) => <option key={c.id} value={c.id}>{c.label}</option>)}
                </select>
              </label>
            )}
            {mode === 'add-option' ? (
              <>
                <label className="block text-sm font-semibold" htmlFor="extend-label">
                  Display name <span className="text-xs font-normal text-[var(--muted)]">optional</span>
                  <input id="extend-label" className="field mt-1" value={label} onChange={(e) => setLabel(e.target.value)} />
                </label>
                <label className="block text-sm font-semibold" htmlFor="extend-support">
                  How strongly to recommend it
                  <select id="extend-support" className="field mt-1" value={support} onChange={(e) => setSupport(e.target.value)}>
                    <option value="recommended">Recommended</option>
                    <option value="supported">Supported</option>
                    <option value="discouraged">Discouraged</option>
                  </select>
                </label>
              </>
            ) : null}
          </div>
        ) : (
          <div className="grid gap-4 sm:grid-cols-2">
            <label className="block text-sm font-semibold" htmlFor="topic-namespace">
              Your namespace
              <input id="topic-namespace" className="field mt-1" value={namespace} onChange={(e) => setNamespace(e.target.value)} placeholder="for example acme" />
            </label>
            <label className="block text-sm font-semibold" htmlFor="topic-key">
              Topic key
              <input id="topic-key" className="field mt-1" value={key} onChange={(e) => setKey(e.target.value)} placeholder="for example release-window" />
            </label>
            <label className="block text-sm font-semibold" htmlFor="topic-title">
              Title
              <input id="topic-title" className="field mt-1" value={title} onChange={(e) => setTitle(e.target.value)} />
            </label>
            <label className="block text-sm font-semibold" htmlFor="topic-answers">
              Allowed answers
              <input id="topic-answers" className="field mt-1" value={answers} onChange={(e) => setAnswers(e.target.value)} placeholder="weekday, weekend" />
              <span className="mt-1 block text-xs font-normal text-[var(--muted)]">Separate with commas.</span>
            </label>
            {namespace && key ? (
              <p className="text-xs text-[var(--muted)] sm:col-span-2">
                The topic will be called <span className="font-mono">{slug(namespace)}.{slug(key)}</span>.
              </p>
            ) : null}
          </div>
        )}

        {sender.problem ? <ProblemBox problem={sender.problem} /> : null}
        {sender.sent ? <p role="status" className="rounded-lg bg-emerald-50 px-4 py-3 text-sm text-emerald-800">{sender.sent}</p> : null}
        <button className="btn btn-primary" type="submit" disabled={!payload || sender.busy}>
          {sender.busy ? 'Sending…' : 'Send for approval'}
        </button>
      </form>
    </Panel>
  );
}
