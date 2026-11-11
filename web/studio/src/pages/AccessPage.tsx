import { EmptyState, Panel, Row } from '../components/Panel';
import { StateBadge } from '../components/StateBadge';
import { shortId } from '../lib/format';
import { useWorkspace } from '../workspace/WorkspaceContext';

export function AccessPage() {
  const { members, runtimes } = useWorkspace();

  return (
    <div className="grid gap-6 xl:grid-cols-2">
      <Panel title="Access management" subtitle="Tenant memberships for humans and agents (UI-01).">
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

      <Panel title="Agent runtimes" subtitle="Registered runtime instances for this project.">
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
  );
}
