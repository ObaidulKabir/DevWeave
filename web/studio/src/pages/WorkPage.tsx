import { FormEvent, useState } from 'react';
import { Link } from 'react-router-dom';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { EmptyState, Panel, Row } from '../components/Panel';
import { StateBadge } from '../components/StateBadge';
import { MetaLine } from '../components/MetaLine';
import { apiFetch } from '../lib/api';
import type { Milestone, WorkItem } from '../lib/types';
import { useWorkspace } from '../workspace/WorkspaceContext';

export function WorkPage() {
  const {
    tenantId, project, live, busy, run, milestones, workItems, setMilestones, setWorkItems,
  } = useWorkspace();
  const [milestoneForm, setMilestoneForm] = useState({ key: '', title: '' });
  const [workForm, setWorkForm] = useState({ key: '', title: '', description: '' });
  const [milestoneId, setMilestoneId] = useState('');
  const [dependencyId, setDependencyId] = useState('');
  const [reportTarget, setReportTarget] = useState<WorkItem | null>(null);

  async function createMilestone(event: FormEvent) {
    event.preventDefault();
    if (!tenantId || !project) return;
    const created = await run(() =>
      apiFetch<Milestone>(`/tenants/${tenantId}/projects/${project.id}/milestones`, {
        method: 'POST',
        body: JSON.stringify({ key: milestoneForm.key, title: milestoneForm.title, dueAt: null }),
      }),
    );
    if (created) {
      setMilestones((current) => [created, ...current]);
      setMilestoneForm({ key: '', title: '' });
    }
  }

  async function createWork(event: FormEvent) {
    event.preventDefault();
    if (!tenantId || !project) return;
    const created = await run(() =>
      apiFetch<WorkItem>(`/tenants/${tenantId}/projects/${project.id}/work-items`, {
        method: 'POST',
        body: JSON.stringify({
          key: workForm.key,
          title: workForm.title,
          description: workForm.description,
          milestoneId: milestones.some((item) => item.id === milestoneId) ? milestoneId : null,
          ownerId: null,
          dueAt: null,
          priority: 'Normal',
          dependsOn: workItems.some((item) => item.id === dependencyId) ? [dependencyId] : [],
        }),
      }),
    );
    if (created) {
      setWorkItems((current) => [created, ...current]);
      setWorkForm({ key: '', title: '', description: '' });
      setDependencyId('');
    }
  }

  async function reportComplete() {
    if (!tenantId || !project || !reportTarget) return;
    const updated = await run(() =>
      apiFetch<WorkItem>(`/tenants/${tenantId}/projects/${project.id}/work-items/${reportTarget.id}/report`, {
        method: 'POST',
        body: JSON.stringify({
          state: 'Completed',
          reason: 'Reported complete from Owner Studio',
          expectedVersion: reportTarget.version ?? 1,
        }),
      }),
    );
    setReportTarget(null);
    if (updated) {
      setWorkItems((current) => current.map((item) => (item.id === updated.id ? updated : item)));
    }
  }

  return (
    <div className="space-y-6">
      <section className="panel rounded-2xl p-5" aria-labelledby="work-explainer-title">
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">03 / Deliver the plan</p>
        <h2 id="work-explainer-title" className="font-display mt-2 text-xl font-bold">Work turns reviewed intent into deliverables</h2>
        <p className="mt-2 max-w-3xl text-sm leading-relaxed text-[var(--muted)]">
          A milestone groups a delivery outcome; a work item describes one executable result. Record its supporting
          knowledge keys and revisions in the description so reviewers can trace why it exists. A dependency here
          connects work items, not knowledge items.
        </p>
        <p className="mt-3 text-xs leading-relaxed text-[var(--muted)]">
          Example: W-01 "Implement create-task API" is derived from K-02 at a specific revision; W-02 depends on W-01
          and cites K-03 plus K-04. Reporting complete is only a claim; verification and approval remain separate.
          Coordination WorkPackages used by agent runtimes are a different record.
        </p>
        <Link to="/knowledge" className="mt-4 inline-flex text-sm font-semibold text-[var(--signal)] hover:underline">
          Check supporting knowledge →
        </Link>
      </section>
      <div className="grid gap-6 xl:grid-cols-2">
      <Panel title="Milestones" subtitle="Group work around a meaningful delivery outcome, such as an MVP release.">
        {milestones.length === 0 ? <EmptyState label="No milestones yet." /> : (
          <div className="divide-y divide-[var(--line)]">
            {milestones.map((milestone) => (
              <Row
                key={milestone.id}
                title={milestone.title}
                subtitle={milestone.key}
                meta={<StateBadge value={milestone.state} />}
              />
            ))}
          </div>
        )}
        <form onSubmit={createMilestone} className="space-y-3 border-t border-[var(--line)] p-5">
          <h3 className="text-sm font-semibold">Create a milestone</h3>
          <label className="block space-y-1 text-xs font-semibold" htmlFor="milestone-key">
            <span>Milestone key</span>
            <input id="milestone-key" required className="field" placeholder="M1-MVP" value={milestoneForm.key} onChange={(e) => setMilestoneForm({ ...milestoneForm, key: e.target.value })} />
          </label>
          <label className="block space-y-1 text-xs font-semibold" htmlFor="milestone-title">
            <span>Delivery outcome</span>
            <input id="milestone-title" required className="field" placeholder="First usable task board" value={milestoneForm.title} onChange={(e) => setMilestoneForm({ ...milestoneForm, title: e.target.value })} />
          </label>
          <button className="btn btn-primary" disabled={!live || busy}>Create milestone</button>
        </form>
      </Panel>

      <Panel title="Work board" subtitle="Each badge describes a different question: planned, reported, verified and approved.">
        {workItems.length === 0 ? <EmptyState label="No work items yet." /> : (
          <div className="divide-y divide-[var(--line)]">
            {workItems.map((item) => (
              <div key={item.id} className="px-5 py-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <p className="text-sm font-semibold">{item.title}</p>
                    <p className="mt-1 text-xs text-[var(--muted)]">{item.key}</p>
                    {item.description ? <p className="mt-2 text-xs leading-relaxed text-[var(--muted)]">{item.description}</p> : null}
                  </div>
                  <button className="btn btn-ghost" disabled={!live || busy} onClick={() => setReportTarget(item)}>
                    Report complete
                  </button>
                </div>
                <div className="mt-3 flex flex-wrap gap-2">
                  <StateBadge label="plan" value={item.plannedState} />
                  <StateBadge label="report" value={item.reportedState} />
                  <StateBadge label="verify" value={item.verificationState} />
                  <StateBadge label="approve" value={item.approvalState} />
                </div>
                <MetaLine revision={item.version} updatedAt={item.updatedAt} verification={item.verificationState} />
              </div>
            ))}
          </div>
        )}
        <form onSubmit={createWork} className="space-y-3 border-t border-[var(--line)] p-5">
          <h3 className="text-sm font-semibold">Create a work item</h3>
          <label className="block space-y-1 text-xs font-semibold" htmlFor="work-key">
            <span>Work key</span>
            <input id="work-key" required className="field" placeholder="TB-01" value={workForm.key} onChange={(e) => setWorkForm({ ...workForm, key: e.target.value })} />
          </label>
          <label className="block space-y-1 text-xs font-semibold" htmlFor="work-title">
            <span>Deliverable</span>
            <input id="work-title" required className="field" placeholder="Implement create-task API" value={workForm.title} onChange={(e) => setWorkForm({ ...workForm, title: e.target.value })} />
          </label>
          <label className="block space-y-1 text-xs font-semibold" htmlFor="work-description">
            <span>Description and acceptance checks</span>
            <textarea id="work-description" required className="field min-h-24" placeholder="What is delivered? Which knowledge keys/revisions justify it? How will it be tested?" value={workForm.description} onChange={(e) => setWorkForm({ ...workForm, description: e.target.value })} />
          </label>
          <label className="block space-y-1 text-xs font-semibold" htmlFor="work-milestone">
            <span>Milestone <span className="font-normal text-[var(--muted)]">(optional)</span></span>
            <select id="work-milestone" className="field" value={milestones.some((item) => item.id === milestoneId) ? milestoneId : ''} onChange={(e) => setMilestoneId(e.target.value)}>
              <option value="">No milestone</option>
              {milestones.map((item) => <option key={item.id} value={item.id}>{item.key} - {item.title}</option>)}
            </select>
          </label>
          <label className="block space-y-1 text-xs font-semibold" htmlFor="work-dependency">
            <span>Depends on another work item <span className="font-normal text-[var(--muted)]">(optional)</span></span>
            <select id="work-dependency" className="field" value={workItems.some((item) => item.id === dependencyId) ? dependencyId : ''} onChange={(e) => setDependencyId(e.target.value)}>
              <option value="">No dependency</option>
              {workItems.map((item) => <option key={item.id} value={item.id}>{item.key} - {item.title}</option>)}
            </select>
          </label>
          <p className="text-xs leading-relaxed text-[var(--muted)]">
            The description records supporting knowledge for review; this form does not create an enforced link to knowledge.
            Additional dependency and state actions are available through the work-planning API.
          </p>
          <button className="btn btn-primary" disabled={!live || busy}>Create work item</button>
        </form>
      </Panel>
      </div>

      <ConfirmDialog
        open={Boolean(reportTarget)}
        title="Report work complete?"
        body="Reporting execution is distinct from verification and approval. This writes an audited state transition (UI-06)."
        confirmLabel="Report complete"
        busy={busy}
        onCancel={() => setReportTarget(null)}
        onConfirm={reportComplete}
      />
    </div>
  );
}
