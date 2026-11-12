import { Link } from 'react-router-dom';
import { EmptyState, Panel, Row } from '../components/Panel';
import { SpecStatusCard } from '../components/SpecStatusCard';
import { StateBadge } from '../components/StateBadge';
import { formatWhen } from '../lib/format';
import { useWorkspace } from '../workspace/WorkspaceContext';

export function OverviewPage() {
  const { metrics, knowledge, workItems, evidence, activities, campaigns, live } = useWorkspace();
  const planningSteps = [
    {
      number: '01',
      title: 'Knowledge',
      question: 'What are we building, and why?',
      description: 'Capture requirements, decisions, constraints and success criteria as typed, versioned project information.',
      example: 'Example: K-01 goal → K-API module → K-02 create-task requirement.',
      to: '/knowledge',
      action: 'Explore knowledge',
    },
    {
      number: '02',
      title: 'Relations',
      question: 'How do those facts connect?',
      description: 'Trace directed, registered links between knowledge items to reveal dependencies and the impact of a change.',
      example: 'Example: K-04 supports K-03 (semantic); outline parent/child is separate.',
      to: '/relations',
      action: 'Explore relations',
    },
    {
      number: '03',
      title: 'Work',
      question: 'What must be delivered?',
      description: 'Organize milestones and work items with owners, dependencies and separate reporting, verification and approval.',
      example: 'Example: W-01 pinned to K-02 via a Plan Work link on Plan outline.',
      to: '/work',
      action: 'Plan work',
    },
  ] as const;

  const cards = metrics.length
    ? metrics.slice(0, 4)
    : [
        { key: 'Knowledge items', value: knowledge.length, unit: 'items', includedPopulation: 'Current project' },
        { key: 'Work items', value: workItems.length, unit: 'items', includedPopulation: 'Current project' },
        { key: 'Evidence', value: evidence.length, unit: 'records', includedPopulation: 'Current project' },
        { key: 'Campaigns', value: campaigns.length, unit: 'active set', includedPopulation: 'Coordination plane' },
      ];

  return (
    <div className="space-y-6">
      <section className="panel rounded-2xl px-6 py-8">
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">UI-01 · Project overview</p>
        <h2 className="font-display mt-2 max-w-3xl text-4xl font-bold tracking-tight text-[var(--ink)]">
          Delivery control for plan, knowledge, evidence and agents.
        </h2>
        <p className="mt-3 max-w-2xl text-sm leading-relaxed text-[var(--muted)]">
          Owner Studio reads the canonical project model. Source code stays in external repositories; DevWeave holds
          revisions, provenance, leases and verification state.
        </p>
        {!live ? (
          <p className="mt-4 text-sm text-amber-800">
            Configure <code className="font-mono">VITE_TENANT_ID</code> and <code className="font-mono">VITE_API_TOKEN</code> to load live data.
          </p>
        ) : null}
        <div className="mt-6 flex flex-wrap gap-2">
          <Link className="btn btn-primary" to="/knowledge">Browse knowledge</Link>
          <Link className="btn btn-ghost" to="/work">Open work board</Link>
          <Link className="btn btn-ghost" to="/coordination">Coordination</Link>
        </div>
      </section>

      <section aria-labelledby="planning-flow-title" className="space-y-4">
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">Application planning</p>
          <h2 id="planning-flow-title" className="font-display mt-1 text-2xl font-bold">From an idea to a reviewable plan</h2>
          <p className="mt-2 max-w-3xl text-sm leading-relaxed text-[var(--muted)]">
            These are different records with different jobs. Start with the facts, connect them, then define delivery work.
            None of these steps automatically approves a plan or proves that implementation is complete.
          </p>
        </div>
        <div className="grid gap-4 md:grid-cols-3">
          {planningSteps.map((step) => (
            <article key={step.number} className="panel flex flex-col rounded-2xl p-5">
              <span className="font-mono text-xs font-semibold text-[var(--signal)]">{step.number} / 03</span>
              <h3 className="font-display mt-3 text-xl font-bold">{step.title}</h3>
              <p className="mt-1 text-sm font-semibold">{step.question}</p>
              <p className="mt-3 text-sm leading-relaxed text-[var(--muted)]">{step.description}</p>
              <p className="mt-4 rounded-lg bg-[var(--paper)] p-3 text-xs leading-relaxed text-[var(--muted)]">{step.example}</p>
              <Link className="mt-5 text-sm font-semibold text-[var(--signal)] hover:underline" to={step.to}>
                {step.action} →
              </Link>
            </article>
          ))}
        </div>
        <Link className="btn btn-primary" to="/plan-map">View whole plan diagram</Link>
        <p className="text-xs leading-relaxed text-[var(--muted)]">
          Planning work items are different from coordination WorkPackages. Relations connect knowledge items, not work items;
          reference supporting knowledge revisions explicitly when describing the work.
        </p>
      </section>

      <SpecStatusCard />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {cards.map((metric) => (
          <article key={metric.key} className="panel rounded-2xl p-5">
            <p className="text-sm text-[var(--muted)]">{metric.key}</p>
            <p className="mt-3 font-display text-3xl font-bold">{metric.value}</p>
            <p className="mt-1 text-xs text-[var(--muted)]">{metric.includedPopulation}</p>
          </article>
        ))}
      </div>

      <div className="grid gap-6 xl:grid-cols-2">
        <Panel title="Activity timeline" subtitle="Recent provenance across the project (AC-11).">
          {activities.length === 0 ? (
            <EmptyState label="No activities yet." />
          ) : (
            <div className="divide-y divide-[var(--line)]">
              {activities.slice(0, 8).map((activity) => (
                <Row
                  key={activity.id}
                  title={`${activity.type} · ${activity.status}`}
                  subtitle={activity.purpose}
                  meta={<span className="text-xs text-[var(--muted)]">{formatWhen(activity.startedAt)}</span>}
                />
              ))}
            </div>
          )}
        </Panel>

        <Panel title="Knowledge snapshot" subtitle="Lifecycle and verification states stay independent (UI-03).">
          {knowledge.length === 0 ? (
            <EmptyState label="No knowledge items yet." />
          ) : (
            <div className="divide-y divide-[var(--line)]">
              {knowledge.slice(0, 6).map((item) => (
                <Row
                  key={item.id}
                  title={item.title}
                  subtitle={`${item.key} · ${item.typeId}`}
                  meta={
                    <div className="flex flex-col items-end gap-1">
                      <StateBadge value={item.state} />
                      <StateBadge label="verify" value={item.verificationState} />
                    </div>
                  }
                />
              ))}
            </div>
          )}
        </Panel>
      </div>
    </div>
  );
}
