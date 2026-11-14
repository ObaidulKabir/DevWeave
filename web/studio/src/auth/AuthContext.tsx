import {
  createContext,
  ReactNode,
  useCallback,
  useContext,
  useMemo,
  useState,
} from 'react';
import {
  clearSession,
  getActiveTenantId,
  getEnvAccessToken,
  getEnvTenantId,
  getSession,
  hasAuthCredentials,
  isEnvAuthSuppressed,
  mintPilotOwnerToken,
  saveSession,
  StudioSession,
  suppressEnvAuthFallback,
} from '../lib/session';

interface AuthValue {
  authenticated: boolean;
  session: StudioSession | null;
  tenantId?: string;
  principalId?: string;
  source: 'session' | 'env' | null;
  login: (tenantId: string, principalId: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthValue | null>(null);

function resolveSource(): 'session' | 'env' | null {
  if (getSession()) return 'session';
  if (!isEnvAuthSuppressed() && getEnvAccessToken() && getEnvTenantId()) return 'env';
  return null;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [revision, setRevision] = useState(0);
  const [session, setSession] = useState<StudioSession | null>(() => getSession());
  const [source, setSource] = useState<'session' | 'env' | null>(() => resolveSource());

  const login = useCallback(async (tenantId: string, principalId: string) => {
    const next = await mintPilotOwnerToken(tenantId.trim(), principalId.trim());
    saveSession(next);
    setSession(next);
    setSource('session');
    setRevision((n) => n + 1);
  }, []);

  const logout = useCallback(() => {
    clearSession();
    suppressEnvAuthFallback();
    setSession(null);
    setSource(null);
    setRevision((n) => n + 1);
  }, []);

  const value = useMemo<AuthValue>(() => {
    const activeSession = getSession();
    return {
      authenticated: hasAuthCredentials(),
      session: activeSession,
      tenantId: getActiveTenantId(),
      principalId: activeSession?.principalId,
      source,
      login,
      logout,
    };
  }, [session, source, login, logout, revision]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used within AuthProvider');
  return value;
}
