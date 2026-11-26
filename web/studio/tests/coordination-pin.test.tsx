import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CoordinationPage } from '../src/pages/CoordinationPage';

vi.mock('../src/lib/api', () => ({
  apiFetch: vi.fn(async () => []),
}));

vi.mock('../src/workspace/WorkspaceContext', () => ({
  useWorkspace: () => ({
    tenantId: '06f04633-3323-4ec8-8e4c-8e965c6ad884',
    project: { id: '01c081ad-c101-464d-b59c-91e26cf1d793', key: 'mvp-close', name: 'MVP Close' },
    live: true,
    busy: false,
    run: async (action: () => Promise<void>) => {
      await action();
    },
    campaigns: [
      {
        id: '704a3970-1132-4a1b-92ac-e578fe1a6289',
        objective: 'Pinned campaign',
        executionMode: 'Mediated',
        status: 'Proposed',
        baselineId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        createdAt: '2026-11-26T09:00:00Z',
        pins: [
          {
            codebaseTargetId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
            provider: 'github',
            repositoryId: 'acme/r1a',
            targetBranch: 'main',
            baseCommitSha: 'abc1234',
          },
        ],
      },
    ],
    mcp: null,
    setCampaigns: () => undefined,
    refresh: async () => undefined,
  }),
}));

describe('campaign pin', () => {
  afterEach(() => {
    cleanup();
  });

  it('shows the pinned commit on the campaign row', async () => {
    render(
      <MemoryRouter>
        <CoordinationPage />
      </MemoryRouter>,
    );

    expect(await screen.findByText('Pinned campaign')).toBeTruthy();
    expect(screen.getByText('Mediated · acme/r1a@abc1234')).toBeTruthy();
  });
});
