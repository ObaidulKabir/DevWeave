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

export interface PilotBootstrapResponse extends PilotTokenResponse {
  email?: string;
  note?: string;
}

const LAST_IDENTITY_KEY = 'devweave.studio.lastIdentity';

export interface LastIdentity {
  tenantId: string;
  principalId: string;
  email?: string;
  tenantName?: string;
}

export function getLastIdentity(): LastIdentity | null {
  if (typeof window === 'undefined') return null;
  try {
    const raw = window.localStorage.getItem(LAST_IDENTITY_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<LastIdentity>;
    if (typeof parsed.tenantId !== 'string' || typeof parsed.principalId !== 'string') return null;
    return {
      tenantId: parsed.tenantId,
      principalId: parsed.principalId,
      email: typeof parsed.email === 'string' ? parsed.email : undefined,
      tenantName: typeof parsed.tenantName === 'string' ? parsed.tenantName : undefined,
    };
  } catch {
    return null;
  }
}

export function saveLastIdentity(identity: LastIdentity): void {
  if (typeof window === 'undefined') return;
  window.localStorage.setItem(LAST_IDENTITY_KEY, JSON.stringify(identity));
}

function sessionFromPilotPayload(payload: PilotTokenResponse): StudioSession {
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

async function readPilotError(response: Response, fallback: string): Promise<never> {
  const body = await response.text().catch(() => undefined);
  if (response.status === 404) {
    throw new Error(
      body?.includes('Not Found') || !body
        ? 'Pilot auth is unavailable (API not in Development, or identity not found).'
        : body,
    );
  }
  if (response.status === 400) {
    throw new Error(body || 'Invalid registration or login details.');
  }
  throw new Error(
    response.status === 0
      ? 'Could not reach the API. Is the control plane running?'
      : `${fallback} (${response.status}).`,
  );
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
    await readPilotError(response, 'Login failed');
  }

  const payload = (await response.json()) as PilotTokenResponse;
  const session = sessionFromPilotPayload(payload);
  saveLastIdentity({ tenantId: session.tenantId, principalId: session.principalId });
  return session;
}

export interface TenantRegistrationResult {
  tenantId: string;
  principalId: string;
  email?: string;
  /** Present only when the API is issuing development pilot sessions. */
  session: StudioSession | null;
}

/** Creates a tenant and an active Owner. No verification email is sent. */
export async function registerTenant(
  tenantName: string,
  ownerEmail: string,
): Promise<TenantRegistrationResult> {
  const baseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? '';
  const response = await fetch(`${baseUrl}/auth/register`, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      tenantName: tenantName.trim(),
      ownerEmail: ownerEmail.trim(),
    }),
  });

  if (!response.ok) {
    await readPilotError(response, 'Registration failed');
  }

  const payload = (await response.json()) as PilotBootstrapResponse;
  const session = payload.accessToken ? sessionFromPilotPayload(payload) : null;
  saveLastIdentity({
    tenantId: payload.tenantId,
    principalId: payload.principalId,
    email: payload.email,
    tenantName: tenantName.trim(),
  });
  return {
    tenantId: payload.tenantId,
    principalId: payload.principalId,
    email: payload.email,
    session,
  };
}
