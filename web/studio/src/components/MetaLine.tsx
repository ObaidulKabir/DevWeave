import { formatWhen, shortId } from '../lib/format';
import { StateBadge } from './StateBadge';

/** UI-04: source, revision, freshness and verification for important records. */
export function MetaLine({
  source,
  revision,
  updatedAt,
  verification,
}: {
  source?: string | null;
  revision?: string | number | null;
  updatedAt?: string | null;
  verification?: string | null;
}) {
  return (
    <div className="mt-2 flex flex-wrap items-center gap-2 text-[11px] text-[var(--muted)]">
      {source ? <span className="font-mono">src {shortId(source)}</span> : null}
      {revision !== undefined && revision !== null ? <span className="font-mono">rev {revision}</span> : null}
      <span>updated {formatWhen(updatedAt)}</span>
      {verification ? <StateBadge label="verify" value={verification} /> : null}
    </div>
  );
}
