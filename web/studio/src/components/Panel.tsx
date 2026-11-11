import { ReactNode } from 'react';

export function Panel({
  title,
  subtitle,
  actions,
  children,
}: {
  title: string;
  subtitle?: string;
  actions?: ReactNode;
  children: ReactNode;
}) {
  return (
    <section className="panel overflow-hidden rounded-2xl">
      <header className="flex flex-wrap items-start justify-between gap-3 border-b border-[var(--line)] px-5 py-4">
        <div>
          <h2 className="font-display text-lg font-bold tracking-tight">{title}</h2>
          {subtitle ? <p className="mt-1 text-xs leading-relaxed text-[var(--muted)]">{subtitle}</p> : null}
        </div>
        {actions}
      </header>
      {children}
    </section>
  );
}

export function EmptyState({ label }: { label: string }) {
  return <p className="px-5 py-10 text-sm text-[var(--muted)]">{label}</p>;
}

export function Row({
  title,
  subtitle,
  meta,
  onClick,
  active = false,
}: {
  title: string;
  subtitle?: string;
  meta?: ReactNode;
  onClick?: () => void;
  active?: boolean;
}) {
  const className = `flex w-full items-start justify-between gap-4 px-5 py-4 text-left transition ${
    onClick ? 'hover:bg-white/70' : ''
  } ${active ? 'bg-teal-50/70' : ''}`;

  if (onClick) {
    return (
      <button type="button" className={className} onClick={onClick}>
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold text-[var(--ink)]">{title}</p>
          {subtitle ? <p className="mt-1 truncate text-xs text-[var(--muted)]">{subtitle}</p> : null}
        </div>
        {meta ? <div className="shrink-0">{meta}</div> : null}
      </button>
    );
  }

  return (
    <div className={className}>
      <div className="min-w-0">
        <p className="truncate text-sm font-semibold text-[var(--ink)]">{title}</p>
        {subtitle ? <p className="mt-1 truncate text-xs text-[var(--muted)]">{subtitle}</p> : null}
      </div>
      {meta ? <div className="shrink-0">{meta}</div> : null}
    </div>
  );
}
