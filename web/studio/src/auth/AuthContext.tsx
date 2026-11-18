import {
  createContext,
  ReactNode,
  useCallback,
  useContext,
  useMemo,
  useState,
} from 'react';
import {
  registerTenant,
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
  register: (tenantName: string, ownerEmail: string) => Promise<'signed-in' | 'created'>;
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

  const applySession = useCallback((next: StudioSession) => {
    saveSession(next);
    setSession(next);
    setSource('session');
    setRevision((n) => n + 1);
  }, []);

  const login = useCallback(async (tenantId: string, principalId: string) => {
    const next = await mintPilotOwnerToken(tenantId.trim(), principalId.trim());
    applySession(next);
  }, [applySession]);

  const register = useCallback(async (tenantName: string, ownerEmail: string) => {
    const result = await registerTenant(tenantName, ownerEmail);
    if (!result.session) return 'created' as const;
    applySession(result.session);
    return 'signed-in' as const;
  }, [applySession]);

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
      register,
      logout,
    };
  }, [session, source, login, register, logout, revision]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used within AuthProvider');
  return value;
}
