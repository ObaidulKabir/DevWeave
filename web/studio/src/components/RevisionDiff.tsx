import type { FieldChange } from '../lib/content';

const KIND: Record<FieldChange['kind'], { label: string; chip: string }> = {
  added: { label: 'Added', chip: 'bg-emerald-100 text-emerald-900' },
  removed: { label: 'Removed', chip: 'bg-rose-100 text-rose-900' },
  changed: { label: 'Changed', chip: 'bg-amber-100 text-amber-900' },
};

/** What changed between two revisions, field by field, so a reviewer does not have to compare JSON by eye. */
export function RevisionDiff({
  changes,
  fromLabel,
  toLabel,
}: {
  changes: FieldChange[] | null;
  fromLabel: string;
  toLabel: string;
}) {
  if (changes === null) {
    return <p className="text-sm text-[var(--muted)]">These revisions cannot be compared field by field because one is not valid JSON.</p>;
  }
  if (changes.length === 0) {
    return (
      <p className="text-sm text-[var(--muted)]">
        {toLabel} has the same content as {fromLabel}. Only the reason or review state differs.
      </p>
    );
  }
  return (
    <div className="space-y-3">
      <p className="text-xs text-[var(--muted)]">
        {changes.length} field{changes.length === 1 ? '' : 's'} differ between {fromLabel} and {toLabel}.
      </p>
      {changes.map((change) => (
        <div key={change.path} className="overflow-hidden rounded-xl border border-[var(--line)] bg-white">
          <div className="flex items-center gap-2 border-b border-[var(--line)] px-4 py-2">
            <span className={`rounded px-1.5 py-0.5 text-[11px] font-semibold uppercase ${KIND[change.kind].chip}`}>{KIND[change.kind].label}</span>
            <span className="text-sm font-semibold">{change.label}</span>
          </div>
          <div className="grid gap-px bg-[var(--line)] sm:grid-cols-2">
            <div className="bg-rose-50/50 px-4 py-3">
              <p className="text-[11px] font-semibold uppercase tracking-wide text-rose-800">{fromLabel}</p>
              <p className="mt-1 whitespace-pre-wrap text-sm leading-relaxed">
                {change.before ? change.before : <span className="italic text-[var(--muted)]">Not present</span>}
              </p>
            </div>
            <div className="bg-emerald-50/50 px-4 py-3">
              <p className="text-[11px] font-semibold uppercase tracking-wide text-emerald-800">{toLabel}</p>
              <p className="mt-1 whitespace-pre-wrap text-sm leading-relaxed">
                {change.after ? change.after : <span className="italic text-[var(--muted)]">Removed</span>}
              </p>
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}
