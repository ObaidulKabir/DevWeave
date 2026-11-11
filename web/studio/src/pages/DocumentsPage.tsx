import { DragEvent, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { StateBadge } from '../components/StateBadge';
import { ApiError, apiFetch, apiUpload } from '../lib/api';
import { formatBytes, formatWhen, shortId } from '../lib/format';
import type { ArtifactDownload, ArtifactVerification, ArtifactVersion } from '../lib/types';
import { useWorkspace } from '../workspace/WorkspaceContext';

const MAX_BYTES = 64 * 1024 * 1024;

// Mirrors the server's content policy so people hear "no" before waiting for an upload.
const SOURCE_CODE_EXTENSIONS = new Set([
  '.cs', '.java', '.py', '.js', '.ts', '.tsx', '.jsx', '.go', '.rs', '.c', '.cpp', '.h', '.hpp', '.php', '.rb', '.sh',
  '.sql', '.kt', '.kts', '.swift', '.fs', '.vb', '.scala', '.lua', '.pl',
]);

const CLASSIFICATIONS = [
  { value: 'Public', hint: 'Safe to share outside the organisation' },
  { value: 'Internal', hint: 'Anyone on the team (default)' },
  { value: 'Confidential', hint: 'Only people who need it' },
  { value: 'Restricted', hint: 'Tightly controlled, for example legal or security' },
] as const;

const CLASSIFICATION_BY_NUMBER: Record<number, string> = { 1: 'Public', 2: 'Internal', 3: 'Confidential', 4: 'Restricted' };
const STATUS_BY_NUMBER: Record<number, string> = { 1: 'Stored', 2: 'Quarantined' };

const classificationName = (value: string | number) => (typeof value === 'number' ? CLASSIFICATION_BY_NUMBER[value] ?? String(value) : value);
const statusName = (value: string | number) => (typeof value === 'number' ? STATUS_BY_NUMBER[value] ?? String(value) : value);

type RowState = 'ready' | 'uploading' | 'done' | 'error';

interface QueueRow {
  id: string;
  file: File;
  key: string;
  state: RowState;
  progress: number;
  message?: string;
  checksum?: string;
}

function extensionOf(name: string): string {
  const dot = name.lastIndexOf('.');
  return dot < 0 ? '' : name.slice(dot).toLowerCase();
}

async function sha256Hex(file: File): Promise<string | undefined> {
  try {
    if (!globalThis.crypto?.subtle) return undefined;
    const digest = await crypto.subtle.digest('SHA-256', await file.arrayBuffer());
    return Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, '0')).join('');
  } catch {
    return undefined;
  }
}

/** Plain-language explanation of a failed upload. */
function explainFailure(error: unknown): string {
  if (error instanceof ApiError) {
    let code: string | undefined;
    let detail: string | undefined;
    try {
      const body = JSON.parse(error.body ?? '{}') as { code?: string; message?: string };
      code = body.code;
      detail = body.message;
    } catch {
      /* body was not JSON */
    }
    if (code === 'prohibited-content') return 'Rejected: this file appears to contain credentials or secrets. Remove them and upload again.';
    if (code === 'quarantined-content') return 'Held for review: this file looks like source code, which DevWeave does not keep. It was not stored for use.';
    if (error.status === 413) return 'The file is larger than the server accepts.';
    if (error.status === 403) return 'You do not have permission to add documents to this project.';
    if (error.status === 404) return 'The project could not be found.';
    if (error.status === 0) return 'Could not reach the server. Check your connection and try again.';
    return detail ?? `The server refused the upload (${error.status}).`;
  }
  return 'The upload failed unexpectedly.';
}

export function DocumentsPage() {
  const { tenantId, project, live } = useWorkspace();
  const [versions, setVersions] = useState<ArtifactVersion[]>([]);
  const [loadError, setLoadError] = useState(false);
  const [loading, setLoading] = useState(false);
  const [rows, setRows] = useState<QueueRow[]>([]);
  const [classification, setClassification] = useState<string>('Internal');
  const [dragging, setDragging] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [filter, setFilter] = useState('');
  const [open, setOpen] = useState<string | null>(null);
  const [verified, setVerified] = useState<Record<string, ArtifactVerification | 'error'>>({});
  const [actionError, setActionError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const base = tenantId && project ? `/tenants/${tenantId}` : null;

  const load = useCallback(async () => {
    if (!live || !base || !project) {
      setVersions([]);
      return;
    }
    setLoading(true);
    try {
      setVersions(await apiFetch<ArtifactVersion[]>(`${base}/projects/${project.id}/artifacts`));
      setLoadError(false);
    } catch {
      setLoadError(true);
    } finally {
      setLoading(false);
    }
  }, [live, base, project?.id]);

  useEffect(() => {
    void load();
  }, [load]);

  const documents = useMemo(() => {
    const groups = new Map<string, ArtifactVersion[]>();
    for (const entry of versions) groups.set(entry.key, [...(groups.get(entry.key) ?? []), entry]);
    return [...groups.entries()]
      .map(([key, list]) => {
        const sorted = [...list].sort((a, b) => b.version - a.version);
        return { key, versions: sorted, latest: sorted[0] };
      })
      .sort((a, b) => b.latest.createdAt.localeCompare(a.latest.createdAt));
  }, [versions]);

  const shown = documents.filter((doc) => doc.key.toLowerCase().includes(filter.trim().toLowerCase()));

  function addFiles(list: FileList | File[]) {
    const incoming = Array.from(list).map<QueueRow>((file) => ({
      id: `${file.name}-${file.size}-${file.lastModified}-${Math.random().toString(36).slice(2, 7)}`,
      file,
      key: file.name,
      state: 'ready',
      progress: 0,
    }));
    if (incoming.length === 0) return;
    setRows((current) => [...current, ...incoming]);
    incoming.forEach((row) => {
      void sha256Hex(row.file).then((checksum) => {
        if (checksum) setRows((current) => current.map((r) => (r.id === row.id ? { ...r, checksum } : r)));
      });
    });
  }

  function onDrop(event: DragEvent) {
    event.preventDefault();
    setDragging(false);
    if (live) addFiles(event.dataTransfer.files);
  }

  /** What will happen to this row if it is uploaded. Computed, never guessed. */
  function assess(row: QueueRow): { kind: 'blocked' | 'duplicate' | 'version' | 'new' | 'empty-name'; text: string } {
    const name = row.key.trim();
    if (!name) return { kind: 'empty-name', text: 'Give this document a name.' };
    if (SOURCE_CODE_EXTENSIONS.has(extensionOf(row.file.name))) {
      return { kind: 'blocked', text: 'Source code is not accepted. DevWeave keeps plans and documents, not code.' };
    }
    if (row.file.size === 0) return { kind: 'blocked', text: 'This file is empty.' };
    if (row.file.size > MAX_BYTES) return { kind: 'blocked', text: `Larger than the ${formatBytes(MAX_BYTES)} limit.` };
    const existing = documents.find((doc) => doc.key === name);
    if (existing) {
      const same = row.checksum ? existing.versions.find((v) => v.checksum.toLowerCase() === row.checksum) : undefined;
      if (same) return { kind: 'duplicate', text: `Identical to version ${same.version} already stored. Uploading would only add a copy.` };
      return { kind: 'version', text: `Adds version ${existing.latest.version + 1}. Earlier versions stay available.` };
    }
    return { kind: 'new', text: 'New document, version 1.' };
  }

  const uploadable = rows.filter((row) => {
    const verdict = assess(row).kind;
    return (row.state === 'ready' || row.state === 'error') && verdict !== 'blocked' && verdict !== 'empty-name';
  });

  async function uploadAll() {
    if (!base || !project || uploading) return;
    setUploading(true);
    setActionError(null);
    for (const row of uploadable) {
      setRows((current) => current.map((r) => (r.id === row.id ? { ...r, state: 'uploading', progress: 0, message: undefined } : r)));
      const form = new FormData();
      form.append('file', row.file, row.file.name);
      form.append('key', row.key.trim());
      form.append('fileName', row.file.name);
      form.append('mediaType', row.file.type || 'application/octet-stream');
      form.append('classification', classification);
      try {
        const created = await apiUpload<ArtifactVersion>(`${base}/projects/${project.id}/artifacts`, form, (fraction) =>
          setRows((current) => current.map((r) => (r.id === row.id ? { ...r, progress: fraction } : r))),
        );
        setRows((current) =>
          current.map((r) =>
            r.id === row.id ? { ...r, state: 'done', progress: 1, message: created ? `Stored as version ${created.version}.` : 'Stored.' } : r,
          ),
        );
      } catch (error) {
        setRows((current) => current.map((r) => (r.id === row.id ? { ...r, state: 'error', progress: 0, message: explainFailure(error) } : r)));
      }
    }
    setUploading(false);
    await load();
  }

  async function download(entry: ArtifactVersion) {
    if (!base) return;
    setActionError(null);
    try {
      const grant = await apiFetch<ArtifactDownload>(`${base}/artifacts/${entry.id}/download`);
      window.open(grant.downloadUrl, '_blank', 'noopener,noreferrer');
    } catch {
      setActionError(`Could not prepare the download for ${entry.key} v${entry.version}. Quarantined files cannot be downloaded.`);
    }
  }

  async function verify(entry: ArtifactVersion) {
    if (!base) return;
    try {
      const result = await apiFetch<ArtifactVerification>(`${base}/artifacts/${entry.id}/verify`);
      setVerified((current) => ({ ...current, [entry.id]: result }));
    } catch {
      setVerified((current) => ({ ...current, [entry.id]: 'error' }));
    }
  }

  const finished = rows.filter((r) => r.state === 'done').length;

  return (
    <div className="space-y-6">
      <header>
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--signal)]">Plan · Documents</p>
        <h2 className="font-display mt-1 text-2xl font-bold">Keep the documents your plan depends on</h2>
        <p className="mt-2 max-w-3xl text-sm leading-relaxed text-[var(--muted)]">
          Upload requirement documents, wireframes, meeting notes and other supporting files. Files are never overwritten:
          uploading the same name again adds a new version, and every version keeps a checksum so you can prove it has not changed.
        </p>
      </header>

      {!live ? (
        <p className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900" role="status">
          Preview mode: connect a live project to upload and view documents.
        </p>
      ) : null}

      {/* ---- Upload ---- */}
      <section className="panel rounded-2xl p-5" aria-labelledby="upload-title">
        <h3 id="upload-title" className="font-display text-lg font-bold">Add documents</h3>

        <div
          onDragOver={(e) => {
            e.preventDefault();
            if (live) setDragging(true);
          }}
          onDragLeave={() => setDragging(false)}
          onDrop={onDrop}
          className={`mt-4 flex flex-col items-center justify-center gap-2 rounded-2xl border-2 border-dashed px-6 py-10 text-center transition ${
            dragging ? 'border-[var(--signal)] bg-teal-50' : 'border-[var(--line)] bg-white/60'
          } ${live ? '' : 'opacity-60'}`}
        >
          <svg aria-hidden="true" viewBox="0 0 24 24" className="h-9 w-9 text-[var(--signal)]" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
            <path d="M12 16V4m0 0-4 4m4-4 4 4M4 16v3a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1v-3" />
          </svg>
          <p className="text-sm font-semibold">{dragging ? 'Drop to add' : 'Drag files here'}</p>
          <p className="text-xs text-[var(--muted)]">
            or{' '}
            <button type="button" className="font-semibold text-[var(--signal)] underline" disabled={!live} onClick={() => inputRef.current?.click()}>
              choose files
            </button>
          </p>
          <p className="text-xs text-[var(--muted)]">PDF, Word, Markdown, text, images and similar · up to {formatBytes(MAX_BYTES)} each</p>
          <input
            ref={inputRef}
            type="file"
            multiple
            className="sr-only"
            aria-label="Choose documents to upload"
            disabled={!live}
            onChange={(event) => {
              if (event.target.files) addFiles(event.target.files);
              event.target.value = '';
            }}
          />
        </div>

        <p className="mt-3 rounded-lg bg-slate-50 px-3 py-2 text-xs leading-relaxed text-[var(--muted)]">
          <strong className="text-[var(--ink)]">Not for source code or secrets.</strong> Code files are not accepted, and files that
          contain passwords, API keys or private keys are rejected.
        </p>

        {rows.length > 0 ? (
          <div className="mt-5 space-y-4">
            <ul className="divide-y divide-[var(--line)] overflow-hidden rounded-xl border border-[var(--line)] bg-white">
              {rows.map((row) => {
                const verdict = assess(row);
                const locked = row.state === 'uploading' || row.state === 'done';
                const tone =
                  verdict.kind === 'blocked' || verdict.kind === 'empty-name'
                    ? 'text-rose-800'
                    : verdict.kind === 'duplicate'
                      ? 'text-amber-800'
                      : 'text-[var(--muted)]';
                return (
                  <li key={row.id} className="space-y-2 px-4 py-3">
                    <div className="flex flex-wrap items-center gap-3">
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm font-semibold">{row.file.name}</p>
                        <p className="text-xs text-[var(--muted)]">{formatBytes(row.file.size)} · {row.file.type || 'unknown type'}</p>
                      </div>
                      {!locked ? (
                        <button
                          type="button"
                          className="text-xs font-semibold text-[var(--muted)] hover:text-rose-800"
                          aria-label={`Remove ${row.file.name} from the list`}
                          onClick={() => setRows((current) => current.filter((r) => r.id !== row.id))}
                        >
                          Remove
                        </button>
                      ) : null}
                    </div>

                    {row.state === 'done' ? (
                      <p className="text-xs font-semibold text-emerald-800" role="status">✓ {row.message}</p>
                    ) : (
                      <>
                        <label className="block text-xs font-semibold" htmlFor={`key-${row.id}`}>
                          Document name <span className="font-normal text-[var(--muted)]">(the same name adds a new version)</span>
                          <input
                            id={`key-${row.id}`}
                            className="field mt-1 py-1.5 text-sm font-normal"
                            value={row.key}
                            maxLength={200}
                            disabled={locked}
                            list="existing-documents"
                            onChange={(event) => setRows((current) => current.map((r) => (r.id === row.id ? { ...r, key: event.target.value } : r)))}
                          />
                        </label>
                        <p className={`text-xs ${tone}`}>{verdict.text}</p>
                      </>
                    )}

                    {row.state === 'uploading' ? (
                      <div className="h-1.5 overflow-hidden rounded-full bg-slate-100" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={Math.round(row.progress * 100)} aria-label={`Uploading ${row.file.name}`}>
                        <div className="h-full bg-[var(--signal)] transition-[width]" style={{ width: `${Math.round(row.progress * 100)}%` }} />
                      </div>
                    ) : null}
                    {row.state === 'error' ? <p className="text-xs text-rose-800" role="alert">{row.message}</p> : null}
                  </li>
                );
              })}
            </ul>
            <datalist id="existing-documents">
              {documents.map((doc) => (
                <option key={doc.key} value={doc.key} />
              ))}
            </datalist>

            <div className="flex flex-wrap items-end gap-4">
              <label className="text-xs font-semibold" htmlFor="classification">
                Who can see these
                <select id="classification" className="field mt-1 w-auto" value={classification} onChange={(e) => setClassification(e.target.value)} disabled={uploading}>
                  {CLASSIFICATIONS.map((option) => (
                    <option key={option.value} value={option.value}>{option.value} — {option.hint}</option>
                  ))}
                </select>
              </label>
              <button className="btn btn-primary" disabled={!live || uploading || uploadable.length === 0} onClick={uploadAll}>
                {uploading ? 'Uploading…' : `Upload ${uploadable.length} file${uploadable.length === 1 ? '' : 's'}`}
              </button>
              {finished > 0 && !uploading ? (
                <button className="text-sm font-semibold text-[var(--signal)] hover:underline" onClick={() => setRows((current) => current.filter((r) => r.state !== 'done'))}>
                  Clear finished
                </button>
              ) : null}
            </div>
          </div>
        ) : null}
      </section>

      {/* ---- Library ---- */}
      <section className="panel overflow-hidden rounded-2xl" aria-labelledby="library-title">
        <header className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--line)] px-5 py-4">
          <div>
            <h3 id="library-title" className="font-display text-lg font-bold">Project documents</h3>
            <p className="mt-0.5 text-xs text-[var(--muted)]">{documents.length} document{documents.length === 1 ? '' : 's'} · {versions.length} version{versions.length === 1 ? '' : 's'}</p>
          </div>
          <label className="sr-only" htmlFor="doc-filter">Filter documents</label>
          <input id="doc-filter" className="field w-full sm:w-64" placeholder="Filter by name…" value={filter} onChange={(e) => setFilter(e.target.value)} />
        </header>

        {actionError ? <p className="border-b border-[var(--line)] bg-rose-50 px-5 py-2 text-sm text-rose-800" role="alert">{actionError}</p> : null}
        {loadError ? (
          <p className="px-5 py-8 text-sm text-rose-800" role="alert">
            Could not load documents. <button className="font-semibold underline" onClick={() => void load()}>Try again</button>
          </p>
        ) : loading && documents.length === 0 ? (
          <p className="px-5 py-8 text-sm text-[var(--muted)]">Loading documents…</p>
        ) : shown.length === 0 ? (
          <p className="px-5 py-10 text-sm text-[var(--muted)]">
            {documents.length === 0 ? 'No documents yet. Drop a requirements document above to start the project’s record.' : 'No documents match that filter.'}
          </p>
        ) : (
          <ul className="divide-y divide-[var(--line)]">
            {shown.map((doc) => {
              const expanded = open === doc.key;
              return (
                <li key={doc.key}>
                  <button
                    type="button"
                    aria-expanded={expanded}
                    onClick={() => setOpen(expanded ? null : doc.key)}
                    className="flex w-full flex-wrap items-center justify-between gap-3 px-5 py-4 text-left hover:bg-white/70"
                  >
                    <span className="min-w-0">
                      <span className="block truncate text-sm font-semibold">{doc.key}</span>
                      <span className="mt-0.5 block text-xs text-[var(--muted)]">
                        {formatBytes(doc.latest.sizeInBytes)} · {classificationName(doc.latest.classification)} · updated {formatWhen(doc.latest.createdAt)}
                      </span>
                    </span>
                    <span className="flex items-center gap-2">
                      {statusName(doc.latest.status) === 'Quarantined' ? <StateBadge value="Quarantined" /> : null}
                      <span className="rounded-md bg-slate-100 px-2 py-0.5 font-mono text-[11px] font-semibold text-slate-700">v{doc.latest.version}</span>
                      <span className="text-xs text-[var(--muted)]">{doc.versions.length} version{doc.versions.length === 1 ? '' : 's'}</span>
                      <span aria-hidden="true" className="text-[var(--muted)]">{expanded ? '▾' : '▸'}</span>
                    </span>
                  </button>

                  {expanded ? (
                    <ol className="space-y-2 bg-slate-50/60 px-5 pb-4 pt-1">
                      {doc.versions.map((entry) => {
                        const check = verified[entry.id];
                        const quarantined = statusName(entry.status) === 'Quarantined';
                        return (
                          <li key={entry.id} className="rounded-xl border border-[var(--line)] bg-white px-4 py-3">
                            <div className="flex flex-wrap items-center justify-between gap-3">
                              <div className="min-w-0">
                                <p className="text-sm font-semibold">
                                  Version {entry.version}
                                  {entry.id === doc.latest.id ? <span className="ml-2 text-xs font-normal text-emerald-800">latest</span> : null}
                                </p>
                                <p className="text-xs text-[var(--muted)]">
                                  {formatBytes(entry.sizeInBytes)} · {entry.mediaType} · by {shortId(entry.creator)} · {formatWhen(entry.createdAt)}
                                </p>
                                <p className="mt-1 font-mono text-[11px] text-[var(--muted)]" title={entry.checksum}>
                                  {entry.checksumAlgorithm} {entry.checksum.slice(0, 16)}…
                                </p>
                              </div>
                              <div className="flex gap-2">
                                <button className="btn btn-ghost py-1.5" disabled={quarantined} onClick={() => void download(entry)}>Download</button>
                                <button className="btn btn-ghost py-1.5" disabled={quarantined} onClick={() => void verify(entry)}>Verify</button>
                              </div>
                            </div>
                            {check === 'error' ? (
                              <p className="mt-2 text-xs text-rose-800" role="alert">Could not run the integrity check.</p>
                            ) : check ? (
                              <p className={`mt-2 text-xs font-semibold ${check.match ? 'text-emerald-800' : 'text-rose-800'}`} role="status">
                                {check.match
                                  ? '✓ Integrity confirmed: the stored file matches its checksum from upload.'
                                  : '✗ The stored file no longer matches its upload checksum. Do not rely on it.'}
                              </p>
                            ) : null}
                          </li>
                        );
                      })}
                    </ol>
                  ) : null}
                </li>
              );
            })}
          </ul>
        )}
      </section>
    </div>
  );
}
