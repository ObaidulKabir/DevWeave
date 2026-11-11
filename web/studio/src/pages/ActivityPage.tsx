import { EmptyState, Panel } from '../components/Panel';
import { StateBadge } from '../components/StateBadge';
import { MetaLine } from '../components/MetaLine';
import { useWorkspace } from '../workspace/WorkspaceContext';

export function ActivityPage() {
  const { activities } = useWorkspace();

  return (
    <Panel title="Activity timeline" subtitle="Append-only provenance for humans and agents (UI-01).">
      {activities.length === 0 ? (
        <EmptyState label="No activities recorded for this project." />
      ) : (
        <ol className="divide-y divide-[var(--line)]">
          {activities.map((activity) => (
            <li key={activity.id} className="px-5 py-4">
              <div className="flex flex-wrap items-center gap-2">
                <p className="text-sm font-semibold">{activity.type}</p>
                <StateBadge value={activity.status} />
              </div>
              <p className="mt-2 text-sm text-[var(--muted)]">{activity.purpose}</p>
              {activity.reason ? <p className="mt-1 text-xs text-[var(--muted)]">{activity.reason}</p> : null}
              <MetaLine source={activity.actor} updatedAt={activity.startedAt} />
            </li>
          ))}
        </ol>
      )}
    </Panel>
  );
}
