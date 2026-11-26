import { useState } from 'react';
import { functionalRequirementJson, humanizeKey, parseContent, toFlatFields, toFunctionalRequirement } from '../lib/content';

/**
 * Edits revision content. When the content is a flat object, or the functional-requirement
 * shape the knowledge page starts from, the person fills in labelled fields; otherwise
 * (or on request) they edit the JSON directly. The server still validates against the schema.
 */
export function ContentEditor({ value, onChange }: { value: string; onChange: (next: string) => void }) {
  const flat = toFlatFields(value);
  const functional = toFunctionalRequirement(value);
  const [mode, setMode] = useState<'form' | 'json'>('form');
  const parsed = parseContent(value);
  const useForm = mode === 'form' && (flat !== null || functional !== null);

  function setField(key: string, text: string) {
    if (!flat) return;
    onChange(JSON.stringify({ ...flat, [key]: text }, null, 2));
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between gap-3">
        <span className="text-xs font-semibold">
          Content <span className="font-normal text-[var(--muted)]">(must satisfy the item's schema)</span>
        </span>
        {flat !== null || functional !== null ? (
          <div className="inline-flex overflow-hidden rounded-lg border border-[var(--line)] text-xs font-semibold" role="group" aria-label="Editor mode">
            {(['form', 'json'] as const).map((option) => (
              <button
                key={option}
                type="button"
                aria-pressed={mode === option}
                className={`px-3 py-1.5 ${mode === option ? 'bg-[var(--signal)] text-white' : 'bg-white text-[var(--ink)]'}`}
                onClick={() => setMode(option)}
              >
                {option === 'form' ? 'Fields' : 'JSON'}
              </button>
            ))}
          </div>
        ) : null}
      </div>

      {useForm && functional ? (
        <div className="space-y-3">
          <label className="block space-y-1 text-xs font-semibold" htmlFor="field-description">
            <span>Description</span>
            <textarea
              id="field-description"
              className="field min-h-16 font-normal"
              value={functional.description}
              onChange={(event) =>
                onChange(functionalRequirementJson({ ...functional, description: event.target.value }))
              }
            />
          </label>
          {functional.acceptanceCriteria.map((criterion, index) => (
            <div key={index} className="grid gap-3 sm:grid-cols-[8rem_minmax(0,1fr)]">
              <label className="block space-y-1 text-xs font-semibold" htmlFor={`field-criterion-id-${index}`}>
                <span>Criterion id</span>
                <input
                  id={`field-criterion-id-${index}`}
                  className="field font-normal"
                  value={criterion.id}
                  onChange={(event) => {
                    const acceptanceCriteria = functional.acceptanceCriteria.map((row, rowIndex) =>
                      rowIndex === index ? { ...row, id: event.target.value } : row,
                    );
                    onChange(functionalRequirementJson({ ...functional, acceptanceCriteria }));
                  }}
                />
              </label>
              <label className="block space-y-1 text-xs font-semibold" htmlFor={`field-criterion-statement-${index}`}>
                <span>Acceptance criterion</span>
                <textarea
                  id={`field-criterion-statement-${index}`}
                  className="field min-h-16 font-normal"
                  value={criterion.statement}
                  onChange={(event) => {
                    const acceptanceCriteria = functional.acceptanceCriteria.map((row, rowIndex) =>
                      rowIndex === index ? { ...row, statement: event.target.value } : row,
                    );
                    onChange(functionalRequirementJson({ ...functional, acceptanceCriteria }));
                  }}
                />
              </label>
            </div>
          ))}
        </div>
      ) : useForm && flat ? (
        <div className="space-y-3">
          {Object.entries(flat).map(([key, text]) => (
            <label key={key} className="block space-y-1 text-xs font-semibold" htmlFor={`field-${key}`}>
              <span>{humanizeKey(key)}</span>
              <textarea
                id={`field-${key}`}
                className="field min-h-16 font-normal"
                value={text}
                onChange={(event) => setField(key, event.target.value)}
              />
            </label>
          ))}
        </div>
      ) : (
        <textarea
          aria-label="Revision content as JSON"
          className="field min-h-40 font-mono text-xs"
          spellCheck={false}
          value={value}
          onChange={(event) => onChange(event.target.value)}
        />
      )}

      {parsed.ok ? (
        <p className="text-xs text-emerald-800">Valid JSON.</p>
      ) : (
        <p className="text-xs text-rose-800" role="alert">
          Not valid JSON yet: {parsed.error}
        </p>
      )}
    </div>
  );
}
