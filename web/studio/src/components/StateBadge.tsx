import { toneForState } from '../lib/format';

const TONE_CLASS: Record<ReturnType<typeof toneForState>, string> = {
  neutral: 'bg-slate-100 text-slate-700',
  info: 'bg-sky-50 text-sky-800',
  ok: 'bg-emerald-50 text-emerald-800',
  warn: 'bg-amber-50 text-amber-900',
  danger: 'bg-rose-50 text-rose-800',
};

/** UI-03: visually distinguish lifecycle / review / verification / currency states. */
export function StateBadge({ label, value }: { label?: string; value: string }) {
  return (
    <span className={`inline-flex items-center gap-1 rounded-md px-2 py-0.5 text-[11px] font-semibold uppercase tracking-wide ${TONE_CLASS[toneForState(value)]}`}>
      {label ? <span className="opacity-60">{label}</span> : null}
      <span>{value}</span>
    </span>
  );
}
