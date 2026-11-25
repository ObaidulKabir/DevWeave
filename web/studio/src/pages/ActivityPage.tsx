import { useEffect, useState } from 'react';
import { EmptyState, Panel } from '../components/Panel';
import { StateBadge } from '../components/StateBadge';
import { MetaLine } from '../components/MetaLine';
import { apiFetch } from '../lib/api';
import { shortId } from '../lib/format';
import type { ActivityProvenance } from '../lib/types';
import { useWorkspace } from '../workspace/WorkspaceContext';

export function ActivityPage() {
  const { tenantId, project, activities } = useWorkspace();
  const [provenance, setProvenance] = useState<ActivityProvenance[]>([]);

  useEffect(() => {
    if (!tenantId || !project) {
      setProvenance([]);
      return;
    }
    let cancelled = false;
    apiFetch<ActivityProvenance[]>(`/tenants/${tenantId}/projects/${project.id}/provenance`)
      .then((rows) => {
        if (!cancelled) setProvenance(Array.isArray(rows) ? rows : []);
      })
      .catch(() => {
        if (!cancelled) setProvenance([]);
      });
    return () => {
      cancelled = true;
    };
  }, [tenantId, project]);

  const provenanceFor = (id: string) => provenance.find((row) => row.activityId === id);

  return (
    <Panel title="Activity timeline" subtitle="Append-only provenance. Inputs and outputs are pinned to exact revisions. Agent executions include model configuration when it was recorded.">
      {activities.length === 0 ? (
        <EmptyState label="No activities recorded for this project." />
      ) : (
        <ol className="divide-y divide-[var(--line)]">
          {activities.map((activity) => {
            const detail = provenanceFor(activity.id);
            const execution = detail?.agentExecution;
            return (
              <li key={activity.id} className="px-5 py-4">
                <div className="flex flex-wrap items-center gap-2">
                  <p className="text-sm font-semibold">{activity.type}</p>
                  <StateBadge value={activity.status} />
                </div>
                <p className="mt-2 text-sm text-[var(--muted)]">{activity.purpose}</p>
                {activity.reason ? <p className="mt-1 text-xs text-[var(--muted)]">{activity.reason}</p> : null}
                {(activity.inputs?.length ?? 0) > 0 ? (
                  <p className="mt-2 text-[11px] text-[var(--muted)]">
                    Used {activity.inputs!.map((pin) => shortId(pin.revisionId)).join(', ')}
                  </p>
                ) : null}
                {(activity.outputs?.length ?? 0) > 0 ? (
                  <p className="mt-1 text-[11px] text-[var(--muted)]">
                    Generated {activity.outputs!.map((pin) => shortId(pin.revisionId)).join(', ')}
                  </p>
                ) : null}
                {execution ? (
                  <p className="mt-1 text-[11px] text-[var(--muted)]">
                    {execution.provider} / {execution.model}
                    {execution.modelVersion ? ` @ ${execution.modelVersion}` : ''}
                    {execution.configurationFingerprint ? ` · ${execution.configurationFingerprint}` : ''}
                  </p>
                ) : null}
                <MetaLine source={activity.actor} updatedAt={activity.startedAt} />
              </li>
            );
          })}
        </ol>
      )}
    </Panel>
  );
}
