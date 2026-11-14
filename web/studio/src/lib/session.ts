export interface StudioSession {
  accessToken: string;
  tenantId: string;
  principalId: string;
  /** Unix ms when the access token expires. */
  expiresAt: number;
}

const SESSION_KEY = 'devweave.studio.session';
/** After logout, ignore VITE_API_TOKEN until the next successful login (same tab). */
const SUPPRESS_ENV_KEY = 'devweave.studio.suppressEnvAuth';

function readRaw(): StudioSession | null {
  if (typeof window === 'undefined') return null;
  try {
    const raw = window.localStorage.getItem(SESSION_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<StudioSession>;
    if (
      typeof parsed.accessToken !== 'string' ||
      typeof parsed.tenantId !== 'string' ||
      typeof parsed.principalId !== 'string' ||
      typeof parsed.expiresAt !== 'number'
    ) {
      return null;
    }
    return {
      accessToken: parsed.accessToken,
      tenantId: parsed.tenantId,
      principalId: parsed.principalId,
      expiresAt: parsed.expiresAt,
    };
  } catch {
    return null;
  }
}

export function isSessionValid(session: StudioSession | null | undefined): session is StudioSession {
  if (!session?.accessToken || !session.tenantId || !session.principalId) return false;
  // Allow a small skew so refresh doesn't race the exact second of expiry.
  return session.expiresAt > Date.now() + 5_000;
}

export function getSession(): StudioSession | null {
  const session = readRaw();
  if (!isSessionValid(session)) {
    if (session) clearSession();
    return null;
  }
  return session;
}

export function saveSession(session: StudioSession): void {
  if (typeof window === 'undefined') return;
  window.localStorage.setItem(SESSION_KEY, JSON.stringify(session));
  window.sessionStorage.removeItem(SUPPRESS_ENV_KEY);
}

export function clearSession(): void {
  if (typeof window === 'undefined') return;
  window.localStorage.removeItem(SESSION_KEY);
}

export function suppressEnvAuthFallback(): void {
  if (typeof window === 'undefined') return;
  window.sessionStorage.setItem(SUPPRESS_ENV_KEY, '1');
}

export function isEnvAuthSuppressed(): boolean {
  if (typeof window === 'undefined') return false;
  return window.sessionStorage.getItem(SUPPRESS_ENV_KEY) === '1';
}

export function getEnvTenantId(): string | undefined {
  const value = (import.meta.env.VITE_TENANT_ID as string | undefined)?.trim();
  return value || undefined;
}

export function getEnvAccessToken(): string | undefined {
  const value = (import.meta.env.VITE_API_TOKEN as string | undefined)?.trim();
  return value || undefined;
}

/** Session token preferred; VITE_API_TOKEN as local/dev fallback unless logout suppressed it. */
export function getAccessToken(): string | undefined {
  const session = getSession();
  if (session) return session.accessToken;
  if (isEnvAuthSuppressed()) return undefined;
  return getEnvAccessToken();
}

export function getActiveTenantId(): string | undefined {
  const session = getSession();
  if (session) return session.tenantId;
  if (isEnvAuthSuppressed()) return undefined;
  return getEnvTenantId();
}

export function hasAuthCredentials(): boolean {
  return Boolean(getAccessToken() && getActiveTenantId());
}

export interface PilotTokenResponse {
  tenantId: string;
  principalId: string;
  accessToken: string;
  tokenType: string;
  expiresInSeconds: number;
}

export async function mintPilotOwnerToken(
  tenantId: string,
  principalId: string,
): Promise<StudioSession> {
  const baseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? '';
  const response = await fetch(`${baseUrl}/auth/pilot/token`, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      tenantId,
      principalId,
      platformAdmin: true,
    }),
  });

  if (!response.ok) {
    const body = await response.text().catch(() => undefined);
    if (response.status === 404) {
      throw new Error(
        body?.includes('Not Found') || !body
          ? 'Owner not found for that tenant, or pilot auth is disabled.'
          : body,
      );
    }
    if (response.status === 400) {
      throw new Error(body || 'Invalid tenant or owner identity.');
    }
    throw new Error(
      response.status === 0
        ? 'Could not reach the API. Is the control plane running?'
        : `Login failed (${response.status}). Check tenant and owner IDs.`,
    );
  }

  const payload = (await response.json()) as PilotTokenResponse;
  if (!payload.accessToken || !payload.tenantId || !payload.principalId) {
    throw new Error('Pilot token response was incomplete.');
  }

  const expiresInSeconds = payload.expiresInSeconds > 0 ? payload.expiresInSeconds : 8 * 60 * 60;
  return {
    accessToken: payload.accessToken,
    tenantId: payload.tenantId,
    principalId: payload.principalId,
    expiresAt: Date.now() + expiresInSeconds * 1000,
  };
}
