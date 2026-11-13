import type { ReactNode } from 'react';
import { ApiError } from '../lib/api';
import type { SpecParameter } from '../lib/types';

export const SUPPORT_TONE: Record<string, string> = {
  recommended: 'bg-emerald-50 text-emerald-800',
  supported: 'bg-slate-100 text-slate-700',
  discouraged: 'bg-amber-50 text-amber-900',
  deprecated: 'bg-rose-50 text-rose-800',
};

export function Chip({ tone, children }: { tone: string; children: ReactNode }) {
  return <span className={`inline-flex rounded-md px-2 py-0.5 text-[11px] font-semibold uppercase tracking-wide ${tone}`}>{children}</span>;
}

export interface SpecProblem {
  message: string;
  issues: { path: string; message: string }[];
}

/** Plain-language explanation of a failed specification request. */
export function explainSpecError(error: unknown): SpecProblem {
  if (error instanceof ApiError) {
    try {
      const body = JSON.parse(error.body ?? '{}') as { message?: string; issues?: { path: string; message: string }[] };
      if (error.status === 422) return { message: 'That does not fit the catalog rules.', issues: body.issues ?? [] };
      if (error.status === 409) return { message: body.message ?? 'The platform has locked this topic.', issues: [] };
      if (error.status === 403 || error.status === 404) return { message: 'You do not have permission to propose changes here.', issues: [] };
    } catch {
      /* fall through */
    }
  }
  return { message: 'Could not send the request. Check the connection and try again.', issues: [] };
}

export function ProblemBox({ problem }: { problem: SpecProblem }) {
  return (
    <div role="alert" className="rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-800">
      <p className="font-semibold">{problem.message}</p>
      {problem.issues.length > 0 ? (
        <ul className="mt-1 list-disc pl-5">
          {problem.issues.map((i) => (
            <li key={`${i.path}${i.message}`}>{i.path.replace('/decision/', '').replace(/^\//, '')}: {i.message}</li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}

/** One typed parameter input: a select for closed value sets and booleans, a number or text box otherwise. */
export function ParameterField({
  parameter,
  value,
  onChange,
  idPrefix = 'param',
}: {
  parameter: SpecParameter;
  value: string;
  onChange: (value: string) => void;
  idPrefix?: string;
}) {
  const id = `${idPrefix}-${parameter.name}`;
  return (
    <label className="block text-sm font-semibold" htmlFor={id}>
      {parameter.name}
      {parameter.required ? null : <span className="ml-1 text-xs font-normal text-[var(--muted)]">optional</span>}
      {parameter.kind === 'boolean' ? (
        <select id={id} className="field mt-1" value={value} onChange={(e) => onChange(e.target.value)}>
          <option value="">Choose…</option>
          <option value="true">Yes</option>
          <option value="false">No</option>
        </select>
      ) : parameter.allowedValues.length > 0 ? (
        <select id={id} className="field mt-1" value={value} onChange={(e) => onChange(e.target.value)}>
          <option value="">Choose…</option>
          {parameter.allowedValues.map((option) => (
            <option key={option} value={option}>{option}</option>
          ))}
        </select>
      ) : (
        <input
          id={id}
          className="field mt-1"
          inputMode={parameter.kind === 'integer' ? 'numeric' : undefined}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={parameter.kind === 'integer' ? 'A whole number' : 'Text'}
        />
      )}
    </label>
  );
}
