import { useState } from 'react';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { EmptyState, Panel } from '../components/Panel';
import { StateBadge } from '../components/StateBadge';
import { MetaLine } from '../components/MetaLine';
import { apiFetch } from '../lib/api';
import type { EvidenceItem } from '../lib/types';
import { useWorkspace } from '../workspace/WorkspaceContext';

export function EvidencePage() {
  const { tenantId, live, busy, run, evidence, setEvidence } = useWorkspace();
  const [reviewTarget, setReviewTarget] = useState<EvidenceItem | null>(null);

  async function acceptEvidence() {
    if (!tenantId || !reviewTarget) return;
    const updated = await run(() =>
      apiFetch<EvidenceItem>(`/tenants/${tenantId}/evidence/${reviewTarget.id}/review`, {
        method: 'POST',
        body: JSON.stringify({
          state: 'Accepted',
          reviewer: '00000000-0000-0000-0000-000000000000',
        }),
      }),
    );
    setReviewTarget(null);
    if (updated) {
      setEvidence((current) => current.map((item) => (item.id === updated.id ? updated : item)));
    }
  }

  return (
    <>
      <Panel title="Evidence review" subtitle="Reported evidence is not verification until accepted (PRO-005, UI-03).">
        {evidence.length === 0 ? (
          <EmptyState label="No evidence records loaded." />
        ) : (
          <div className="divide-y divide-[var(--line)]">
            {evidence.map((item) => (
              <div key={item.id} className="flex flex-wrap items-start justify-between gap-4 px-5 py-4">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="text-sm font-semibold">{item.type}</p>
                    <StateBadge value={item.reviewState} />
                  </div>
                  <p className="mt-2 text-xs text-[var(--muted)]">{item.relatedCriterion}</p>
                  <MetaLine source={item.integrityHash} updatedAt={item.recordedAt} />
                </div>
                <button
                  className="btn btn-ghost"
                  disabled={!live || busy || item.reviewState === 'Accepted'}
                  onClick={() => setReviewTarget(item)}
                >
                  Accept evidence
                </button>
              </div>
            ))}
          </div>
        )}
      </Panel>

      <ConfirmDialog
        open={Boolean(reviewTarget)}
        title="Accept this evidence?"
        body="Acceptance is an audited review decision. It qualifies claims for verification but is not itself delivery approval (UI-06)."
        confirmLabel="Accept"
        busy={busy}
        onCancel={() => setReviewTarget(null)}
        onConfirm={acceptEvidence}
      />
    </>
  );
}
