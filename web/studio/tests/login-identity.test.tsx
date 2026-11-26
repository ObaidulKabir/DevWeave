import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { LoginPage } from '../src/pages/LoginPage';

vi.mock('../src/auth/AuthContext', () => ({
  useAuth: () => ({ login: vi.fn() }),
}));

describe('login identity', () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it('hides the pilot GUID form when the host publishes an identity provider', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => ({
        ok: true,
        json: async () => ({
          pilotEnabled: false,
          oidcAuthority: 'https://identity.example.test',
          oidcAudience: 'devweave-api',
          studioClientId: 'devweave-studio',
        }),
      })),
    );

    render(
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>,
    );

    expect(await screen.findByRole('link', { name: 'Sign in with identity provider' })).toBeTruthy();
    expect(screen.queryByLabelText('Tenant ID')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Sign in to dashboard' })).toBeNull();
  });

  it('keeps the development GUID form when pilot sign-in is enabled', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => ({
        ok: true,
        json: async () => ({
          pilotEnabled: true,
          oidcAuthority: null,
          oidcAudience: 'devweave-api',
          studioClientId: 'devweave-studio',
        }),
      })),
    );

    render(
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>,
    );

    expect(await screen.findByRole('button', { name: 'Sign in to dashboard' })).toBeTruthy();
    expect(screen.getByLabelText('Tenant ID')).toBeTruthy();
    expect(screen.queryByRole('link', { name: 'Sign in with identity provider' })).toBeNull();
  });
});
