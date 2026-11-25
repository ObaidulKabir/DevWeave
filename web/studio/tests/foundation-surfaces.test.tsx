import { cleanup, render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ActivityPage } from '../src/pages/ActivityPage';
import { KnowledgePage } from '../src/pages/KnowledgePage';
import { EvidencePage } from '../src/pages/EvidencePage';
import { RelationsPage } from '../src/pages/RelationsPage';
import { ReportsPage } from '../src/pages/ReportsPage';

const apiFetch = vi.hoisted(() => vi.fn(() => Promise.resolve([])));

vi.mock('../src/lib/api', () => ({ apiFetch }));

vi.mock('../src/workspace/WorkspaceContext', () => ({
  useWorkspace: () => ({
    tenantId: 'tenant-1',
    project: { id: 'project-1', name: 'Checkout', key: 'checkout' },
    live: true,
    busy: false,
    run: async (work: () => Promise<unknown>) => work(),
    setEvidence: () => undefined,
    setKnowledge: () => undefined,
    evidence: [
      {
        id: 'evidence-1',
        type: 'test-run',
        reviewState: 'Accepted',
        relatedCriterion: 'Checkout',
        criterionRevisionId: 'abcdef1234567890',
        integrityHash: 'sha256:abc',
        recordedAt: '2026-11-23T00:00:00Z',
      },
    ],
    relationships: [
      {
        id: 'rel-1',
        sourceItemId: 'item-decision',
        targetItemId: 'item-requirement',
        relationTypeCode: 'satisfies',
        status: 'Active',
        createdBy: 'owner',
        createdAt: '2026-11-23T00:00:00Z',
      },
    ],
    knowledge: [
      {
        id: 'item-decision',
        key: 'ADR-1',
        title: 'Checkout decision',
        state: 'Approved',
        reviewState: 'Reviewed',
        verificationState: 'Unverified',
        typeId: 'architecture.decision',
        currentRevisionNumber: 1,
      },
      {
        id: 'item-requirement',
        key: 'FR-1',
        title: 'Checkout requirement',
        state: 'Approved',
        reviewState: 'Reviewed',
        verificationState: 'Unverified',
        typeId: 'requirements.functional',
        currentRevisionNumber: 1,
      },
    ],
    channels: [],
    metrics: [
      {
        key: 'closedloop.evidence_backed_completion',
        value: 0,
        unit: 'ratio',
        definitionVersion: 'closed-loop.1.0',
        includedPopulation: 'Numerator 0, denominator 1.',
        isEstimate: false,
      },
    ],
    activities: [
      {
        id: 'activity-1',
        type: 'Generated',
        status: 'Completed',
        purpose: 'Record the decision',
        actor: 'planner',
        startedAt: '2026-11-23T00:00:00Z',
        inputs: [{ itemId: 'item-requirement', revisionId: '12345678-aaaa-bbbb-cccc-ddddeeeeffff' }],
        outputs: [{ itemId: 'item-decision', revisionId: '87654321-aaaa-bbbb-cccc-ddddeeeeffff' }],
      },
    ],
  }),
}));

function renderPage(page: ReactNode) {
  return render(<MemoryRouter>{page}</MemoryRouter>);
}

describe('foundation surfaces', () => {
  afterEach(() => cleanup());

  beforeEach(() => {
    apiFetch.mockImplementation((path: unknown) => {
      const url = typeof path === 'string' ? path : '';
      if (url === '/schemas/discover') {
        return Promise.resolve([
          { namespace: 'plan.requirement', version: '1.0' },
          { namespace: 'requirements.functional', version: '1.0' },
        ]);
      }
      if (url.includes('plan-import') || url.includes('/revisions') || url.includes('/reviews')) {
        return Promise.resolve([]);
      }
      return Promise.resolve([
        {
          activityId: 'activity-1',
          inputs: [],
          outputs: [],
          agentExecution: {
            provider: 'cursor',
            model: 'grok',
            modelVersion: '1',
            configurationFingerprint: 'cfg-1',
            toolCalls: [],
            runId: 'run-1',
            authorizationRef: 'auth-1',
          },
        },
      ]);
    });
  });

  it('defaults a new knowledge item to requirements.functional', async () => {
    renderPage(<KnowledgePage />);
    screen.getByText('New knowledge item').click();
    const type = await screen.findByLabelText('Type');
    expect((type as HTMLSelectElement).value).toBe('requirements.functional@1.0');
  });

  it('explains separate closed-loop ratios on Reports', () => {
    renderPage(<ReportsPage />);
    expect(screen.getByText(/separate ratios/i)).toBeTruthy();
    expect(screen.getByText(/does not raise evidence-backed completion/i)).toBeTruthy();
    expect(screen.getByText('closedloop.evidence_backed_completion')).toBeTruthy();
  });

  it('shows relation endpoint rules', () => {
    renderPage(<RelationsPage />);
    expect(screen.getAllByText('satisfies').length).toBeGreaterThan(0);
    expect(screen.getByText('Checkout decision')).toBeTruthy();
    expect(screen.getByText('Checkout requirement')).toBeTruthy();
  });

  it('shows that a newer revision makes an evidence pin stale', () => {
    renderPage(<EvidencePage />);
    expect(screen.getByText(/current revision/i)).toBeTruthy();
    expect(screen.getByText(/pinned revision abcdef12/i)).toBeTruthy();
  });

  it('shows activity pins and recorded agent execution', async () => {
    renderPage(<ActivityPage />);
    expect(screen.getByText(/pinned to exact revisions/i)).toBeTruthy();
    expect(screen.getByText(/Used 12345678/)).toBeTruthy();
    expect(screen.getByText(/Generated 87654321/)).toBeTruthy();
    expect(await screen.findByText(/cursor \/ grok/)).toBeTruthy();
  });
});
