export function formatWhen(value?: string | null): string {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(bytes < 10 * 1024 ? 1 : 0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function shortId(value?: string | null): string {
  if (!value) return '—';
  return value.slice(0, 8);
}

export function toneForState(state: string): 'neutral' | 'info' | 'ok' | 'warn' | 'danger' {
  const normalized = state.toLowerCase();
  if (['verified', 'approved', 'accepted', 'active', 'completed', 'published'].includes(normalized)) {
    return 'ok';
  }
  if (['proposed', 'planned', 'draft', 'pending', 'unreviewed', 'unverified', 'reported', 'inprogress'].includes(normalized)) {
    return 'info';
  }
  if (['superseded', 'obsolete', 'quarantined', 'onhold', 'suspended', 'contested'].includes(normalized)) {
    return 'warn';
  }
  if (['rejected', 'failed', 'revoked', 'deleted', 'disconnected'].includes(normalized)) {
    return 'danger';
  }
  return 'neutral';
}
