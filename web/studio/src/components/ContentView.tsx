import { useState } from 'react';
import { humanizeKey, parseContent } from '../lib/content';
import type { FieldChange } from '../lib/content';

function Value({ value, depth = 0 }: { value: unknown; depth?: number }) {
  if (value === null || value === undefined || value === '') {
    return <span className="text-sm italic text-[var(--muted)]">Not provided</span>;
  }
  if (Array.isArray(value)) {
    if (value.length === 0) return <span className="text-sm italic text-[var(--muted)]">None</span>;
    return (
      <ul className="list-disc space-y-1 pl-5 text-sm leading-relaxed">
        {value.map((entry, index) => (
          <li key={index}>
            <Value value={entry} depth={depth + 1} />
          </li>
        ))}
      </ul>
    );
  }
  if (typeof value === 'object') {
    return (
      <dl className={`space-y-3 ${depth > 0 ? 'border-l-2 border-[var(--line)] pl-4' : ''}`}>
        {Object.entries(value as Record<string, unknown>).map(([key, entry]) => (
          <div key={key}>
            <dt className="text-[11px] font-semibold uppercase tracking-wide text-[var(--muted)]">{humanizeKey(key)}</dt>
            <dd className="mt-1">
              <Value value={entry} depth={depth + 1} />
            </dd>
          </div>
        ))}
      </dl>
    );
  }
  if (typeof value === 'boolean') return <span className="text-sm">{value ? 'Yes' : 'No'}</span>;
  return <p className="whitespace-pre-wrap text-sm leading-relaxed">{String(value)}</p>;
}

/** Reads revision content as a labelled document; the raw JSON stays one click away. `changed` highlights fields that differ from the previous revision. */
export function ContentView({ raw, changed }: { raw: string; changed?: FieldChange[] | null }) {
  const [showRaw, setShowRaw] = useState(false);
  const parsed = parseContent(raw);
  const changedTop = new Set((changed ?? []).map((change) => change.path.split('.')[0]));

  return (
    <div className="space-y-3">
      {parsed.ok && !showRaw ? (
        typeof parsed.value === 'object' && parsed.value !== null && !Array.isArray(parsed.value) ? (
          <dl className="space-y-4">
            {Object.entries(parsed.value as Record<string, unknown>).map(([key, entry]) => (
              <div
                key={key}
                className={`rounded-xl border px-4 py-3 ${
                  changedTop.has(key) ? 'border-amber-300 bg-amber-50/60' : 'border-[var(--line)] bg-white'
                }`}
              >
                <dt className="flex items-center gap-2 text-[11px] font-semibold uppercase tracking-wide text-[var(--muted)]">
                  {humanizeKey(key)}
                  {changedTop.has(key) ? <span className="rounded bg-amber-200 px-1.5 py-0.5 text-amber-900">changed</span> : null}
                </dt>
                <dd className="mt-1.5">
                  <Value value={entry} />
                </dd>
              </div>
            ))}
          </dl>
        ) : (
          <div className="rounded-xl border border-[var(--line)] bg-white px-4 py-3">
            <Value value={parsed.value} />
          </div>
        )
      ) : (
        <>
          {!parsed.ok ? (
            <p className="text-xs text-amber-800" role="status">
              This content is not valid JSON ({parsed.error}), so it is shown as stored.
            </p>
          ) : null}
          <pre className="max-h-96 overflow-auto rounded-xl bg-[var(--ink)] p-4 text-xs text-teal-50">{raw}</pre>
        </>
      )}
      {parsed.ok ? (
        <button type="button" className="text-xs font-semibold text-[var(--signal)] hover:underline" onClick={() => setShowRaw((v) => !v)}>
          {showRaw ? 'Show as document' : 'Show raw JSON'}
        </button>
      ) : null}
    </div>
  );
}
