/** Helpers that turn schema-validated revision content (a JSON string) into something a person can read and compare. */

export type ParsedContent =
  | { ok: true; value: unknown }
  | { ok: false; error: string };

export function parseContent(raw: string): ParsedContent {
  try {
    return { ok: true, value: JSON.parse(raw) };
  } catch (error) {
    return { ok: false, error: error instanceof Error ? error.message : 'Not valid JSON' };
  }
}

/** "acceptanceCriteria" / "acceptance_criteria" -> "Acceptance criteria". */
export function humanizeKey(key: string): string {
  const spaced = key
    .replace(/[_-]+/g, ' ')
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .trim()
    .toLowerCase();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

export interface FieldChange {
  path: string;
  label: string;
  kind: 'added' | 'removed' | 'changed';
  before?: string;
  after?: string;
}

function stringify(value: unknown): string {
  if (value === undefined) return '';
  if (typeof value === 'string') return value;
  return JSON.stringify(value, null, 2);
}

function walk(before: unknown, after: unknown, path: string[], out: FieldChange[]) {
  const isObject = (v: unknown): v is Record<string, unknown> =>
    typeof v === 'object' && v !== null && !Array.isArray(v);

  if (isObject(before) && isObject(after)) {
    const keys = Array.from(new Set([...Object.keys(before), ...Object.keys(after)]));
    for (const key of keys) walk(before[key], after[key], [...path, key], out);
    return;
  }

  if (JSON.stringify(before) === JSON.stringify(after)) return;

  const label = path.map(humanizeKey).join(' › ') || 'Content';
  const pathKey = path.join('.');
  if (before === undefined) out.push({ path: pathKey, label, kind: 'added', after: stringify(after) });
  else if (after === undefined) out.push({ path: pathKey, label, kind: 'removed', before: stringify(before) });
  else out.push({ path: pathKey, label, kind: 'changed', before: stringify(before), after: stringify(after) });
}

/** Field-level differences between two revisions' content. Returns null when either side is not valid JSON. */
export function diffContent(beforeRaw: string, afterRaw: string): FieldChange[] | null {
  const before = parseContent(beforeRaw);
  const after = parseContent(afterRaw);
  if (!before.ok || !after.ok) return null;
  const changes: FieldChange[] = [];
  walk(before.value, after.value, [], changes);
  return changes;
}

/** Flat top-level string fields, used by the guided editor. Null when the content is not a plain object of scalars. */
export function toFlatFields(raw: string): Record<string, string> | null {
  const parsed = parseContent(raw);
  if (!parsed.ok || typeof parsed.value !== 'object' || parsed.value === null || Array.isArray(parsed.value)) return null;
  const fields: Record<string, string> = {};
  for (const [key, value] of Object.entries(parsed.value as Record<string, unknown>)) {
    if (value !== null && typeof value === 'object') return null;
    fields[key] = value === null || value === undefined ? '' : String(value);
  }
  return fields;
}
