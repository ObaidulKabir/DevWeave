import { Link } from 'react-router-dom';

export function AgentPushCallout({
  pendingPlanImports,
  channelMessageCount,
}: {
  pendingPlanImports: number;
  channelMessageCount: number;
}) {
  if (pendingPlanImports > 0) {
    return (
      <section className="rounded-2xl border border-amber-200 bg-amber-50 px-5 py-4 text-sm text-amber-950">
        <p className="font-semibold">
          {pendingPlanImports} desktop-agent plan import {pendingPlanImports === 1 ? 'needs' : 'need'} review.
        </p>
        <p className="mt-1 leading-relaxed">
          Imported Knowledge, Relations, and Work stay unpublished until an Owner approves the immutable bundle in{' '}
          <Link className="font-semibold underline" to="/plan-import">
            Plan import
          </Link>
          . Coordination channels do not display planning records.
        </p>
      </section>
    );
  }

  if (channelMessageCount > 0) {
    return (
      <section className="rounded-2xl border border-sky-200 bg-sky-50 px-5 py-4 text-sm text-sky-950">
        <p className="font-semibold">
          {channelMessageCount} agent message{channelMessageCount === 1 ? '' : 's'} arrived on Coordination — not as
          published Knowledge or Work.
        </p>
        <p className="mt-1 leading-relaxed">
          <code className="font-mono text-xs">send_project_message</code> (including types named Knowledge / Relations /
          Work) is a durable log. Open{' '}
          <Link className="font-semibold underline" to="/coordination">
            Coordination
          </Link>{' '}
          or{' '}
          <Link className="font-semibold underline" to="/live">
            Live operations
          </Link>{' '}
          to read the payloads. To materialize a plan, the planning agent must call{' '}
          <code className="font-mono text-xs">propose_initial_plan</code>, then an Owner approves it under{' '}
          <Link className="font-semibold underline" to="/plan-import">
            Plan import
          </Link>
          .
        </p>
      </section>
    );
  }

  return null;
}
