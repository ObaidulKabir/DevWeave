import {
  createContext,
  Dispatch,
  ReactNode,
  SetStateAction,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from 'react';
import { useAuth } from '../auth/AuthContext';
import { ApiError, apiFetch, hasLiveConfig } from '../lib/api';
import { getActiveTenantId } from '../lib/session';
import type {
  Activity,
  Campaign,
  EvidenceItem,
  KnowledgeItem,
  Membership,
  Metric,
  McpDirectory,
  Milestone,
  PlanOutline,
  Project,
  Relationship,
  Runtime,
  WorkItem,
} from '../lib/types';

interface WorkspaceValue {
  tenantId?: string;
  live: boolean;
  connection: 'checking' | 'online' | 'unreachable';
  projects: Project[];
  project?: Project;
  setProjectId: (id: string) => void;
  metrics: Metric[];
  knowledge: KnowledgeItem[];
  relationships: Relationship[];
  workItems: WorkItem[];
  milestones: Milestone[];
  outline: PlanOutline;
  outlineError: boolean;
  activities: Activity[];
  evidence: EvidenceItem[];
  members: Membership[];
  campaigns: Campaign[];
  runtimes: Runtime[];
  mcp: McpDirectory | null;
  error: string | null;
  busy: boolean;
  refresh: () => Promise<void>;
  refreshProjects: () => Promise<Project[]>;
  run: <T>(action: () => Promise<T>) => Promise<T | undefined>;
  setKnowledge: Dispatch<SetStateAction<KnowledgeItem[]>>;
  setWorkItems: Dispatch<SetStateAction<WorkItem[]>>;
  setMilestones: Dispatch<SetStateAction<Milestone[]>>;
  setCampaigns: Dispatch<SetStateAction<Campaign[]>>;
  setEvidence: Dispatch<SetStateAction<EvidenceItem[]>>;
  setActivities: Dispatch<SetStateAction<Activity[]>>;
  setRelationships: Dispatch<SetStateAction<Relationship[]>>;
}

const WorkspaceContext = createContext<WorkspaceValue | null>(null);

const DEMO_PROJECTS: Project[] = [
  {
    id: 'demo-1',
    name: 'Atlas Commerce API',
    key: 'atlas-api',
    description: 'Preview project used when no live tenant is configured.',
    status: 'Active',
    updatedAt: new Date().toISOString(),
  },
];

const PROJECT_STORAGE_KEY = 'devweave.studio.projectId';
const EMPTY_OUTLINE: PlanOutline = { nodes: [], links: [], proposals: [] };
const AUTH_HELP =
  'Owner token was rejected (401/403). Sign out and sign in again from /login to mint a fresh pilot Owner token.';

function isAuthFailure(err: unknown): boolean {
  return err instanceof ApiError && (err.status === 401 || err.status === 403);
}

function asList<T>(payload: unknown): T[] {
  if (Array.isArray(payload)) return payload as T[];
  if (payload && typeof payload === 'object') {
    const record = payload as { items?: T[]; Items?: T[] };
    return record.items ?? record.Items ?? [];
  }
  return [];
}

function normalizeOutline(payload: unknown): PlanOutline {
  if (!payload || typeof payload !== 'object') return EMPTY_OUTLINE;
  const record = payload as Partial<PlanOutline>;
  return {
    nodes: Array.isArray(record.nodes) ? record.nodes : [],
    links: Array.isArray(record.links) ? record.links : [],
    proposals: Array.isArray(record.proposals) ? record.proposals : [],
  };
}

function pickPreferredProjectId(liveProjects: Project[]): string {
  const preferred =
    (import.meta.env.VITE_PROJECT_ID as string | undefined)?.trim() ||
    (typeof window !== 'undefined' ? window.localStorage.getItem(PROJECT_STORAGE_KEY) ?? undefined : undefined);
  if (preferred) {
    const match = liveProjects.find((item) => item.id === preferred || item.key === preferred);
    if (match) return match.id;
  }
  const taskboard = liveProjects.find((item) => item.key === 'taskboard-lite');
  return taskboard?.id ?? liveProjects[0].id;
}

export function WorkspaceProvider({ children }: { children: ReactNode }) {
  const { authenticated, tenantId: authTenantId } = useAuth();
  const tenantId = authTenantId ?? getActiveTenantId();
  const [connection, setConnection] = useState<'checking' | 'online' | 'unreachable'>('checking');
  const [projects, setProjects] = useState<Project[]>(DEMO_PROJECTS);
  const [projectId, setProjectIdState] = useState(DEMO_PROJECTS[0].id);
  const [metrics, setMetrics] = useState<Metric[]>([]);
  const [knowledge, setKnowledge] = useState<KnowledgeItem[]>([]);
  const [relationships, setRelationships] = useState<Relationship[]>([]);
  const [workItems, setWorkItems] = useState<WorkItem[]>([]);
  const [milestones, setMilestones] = useState<Milestone[]>([]);
  const [outline, setOutline] = useState<PlanOutline>(EMPTY_OUTLINE);
  const [outlineError, setOutlineError] = useState(false);
  const [activities, setActivities] = useState<Activity[]>([]);
  const [evidence, setEvidence] = useState<EvidenceItem[]>([]);
  const [members, setMembers] = useState<Membership[]>([]);
  const [campaigns, setCampaigns] = useState<Campaign[]>([]);
  const [runtimes, setRuntimes] = useState<Runtime[]>([]);
  const [mcp, setMcp] = useState<McpDirectory | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const setProjectId = useCallback((id: string) => {
    setProjectIdState(id);
    if (typeof window !== 'undefined' && id && !id.startsWith('demo-')) {
      window.localStorage.setItem(PROJECT_STORAGE_KEY, id);
    }
  }, []);

  const project = useMemo(
    () => projects.find((item) => item.id === projectId) ?? projects[0],
    [projects, projectId],
  );
  const live = Boolean(authenticated && tenantId) && hasLiveConfig() && !project?.id.startsWith('demo-');

  const refreshProjects = useCallback(async () => {
    if (!authenticated || !tenantId || !hasLiveConfig()) return [] as Project[];
    const liveProjects = asList<Project>(await apiFetch<Project[]>(`/tenants/${tenantId}/projects`));
    if (liveProjects.length) {
      setProjects(liveProjects);
      setProjectIdState((current) => {
        if (liveProjects.some((item) => item.id === current)) return current;
        return pickPreferredProjectId(liveProjects);
      });
    }
    return liveProjects;
  }, [authenticated, tenantId]);

  const refresh = useCallback(async () => {
    if (!authenticated || !tenantId || !project || project.id.startsWith('demo-')) {
      setMetrics([]);
      setKnowledge([]);
      setRelationships([]);
      setWorkItems([]);
      setMilestones([]);
      setOutline(EMPTY_OUTLINE);
      setOutlineError(false);
      setActivities([]);
      setEvidence([]);
      setMembers([]);
      setCampaigns([]);
      setRuntimes([]);
      setMcp(null);
      return;
    }

    const base = `/tenants/${tenantId}/projects/${project.id}`;
    setOutlineError(false);
    let authFailed = false;
    const soft = async <T,>(path: string, fallback: T, onError?: () => void): Promise<T> => {
      try {
        return await apiFetch<T>(path);
      } catch (err) {
        if (isAuthFailure(err)) authFailed = true;
        onError?.();
        return fallback;
      }
    };

    const [
      nextMetrics,
      nextKnowledge,
      nextRelationships,
      nextWork,
      nextMilestones,
      nextActivities,
      nextEvidence,
      nextMembers,
      nextCampaigns,
      nextRuntimes,
      nextMcp,
      nextOutline,
    ] = await Promise.all([
      soft(`${base}/metrics`, [] as Metric[]).then(asList<Metric>),
      soft(`${base}/knowledge`, [] as KnowledgeItem[]).then(asList<KnowledgeItem>),
      soft(`${base}/relationships`, [] as Relationship[]).then(asList<Relationship>),
      soft(`${base}/work-items`, [] as WorkItem[]).then(asList<WorkItem>),
      soft(`${base}/milestones`, [] as Milestone[]).then(asList<Milestone>),
      soft(`${base}/activities`, [] as Activity[]).then(asList<Activity>),
      soft(`${base}/evidence`, [] as EvidenceItem[]).then(asList<EvidenceItem>),
      soft(`/tenants/${tenantId}/members`, [] as Membership[]).then(asList<Membership>),
      soft(`${base}/coordination/campaigns`, [] as Campaign[]).then(asList<Campaign>),
      soft(`${base}/coordination/runtimes`, [] as Runtime[]).then(asList<Runtime>),
      soft<McpDirectory | null>(`${base}/coordination/mcp`, null),
      soft(`${base}/plan/`, EMPTY_OUTLINE, () => setOutlineError(true)).then(normalizeOutline),
    ]);

    setMetrics(nextMetrics);
    setKnowledge(nextKnowledge);
    setRelationships(nextRelationships);
    setWorkItems(nextWork);
    setMilestones(nextMilestones);
    setActivities(nextActivities);
    setEvidence(nextEvidence);
    setMembers(nextMembers);
    setCampaigns(nextCampaigns);
    setRuntimes(nextRuntimes);
    setMcp(nextMcp);
    setOutline(nextOutline);
    setError(authFailed ? AUTH_HELP : null);
  }, [authenticated, tenantId, project]);

  useEffect(() => {
    let cancelled = false;
    apiFetch<{ status: string }>('/health/live')
      .then(async () => {
        if (cancelled) return;
        setConnection('online');
        if (!authenticated || !tenantId || !hasLiveConfig()) return;
        try {
          const liveProjects = await refreshProjects();
          if (!cancelled && liveProjects.length) {
            setProjectId(pickPreferredProjectId(liveProjects));
          }
        } catch (err) {
          if (cancelled) return;
          setError(isAuthFailure(err) ? AUTH_HELP : err instanceof ApiError ? err.message : 'Failed to load projects');
        }
      })
      .catch(() => {
        if (!cancelled) setConnection('unreachable');
      });
    return () => {
      cancelled = true;
    };
  }, [authenticated, tenantId, setProjectId, refreshProjects]);

  useEffect(() => {
    if (!authenticated) return;
    refresh().catch((err: unknown) => {
      setError(isAuthFailure(err) ? AUTH_HELP : err instanceof ApiError ? err.message : 'Failed to load workspace');
    });
  }, [authenticated, refresh]);

  const run = useCallback(async <T,>(action: () => Promise<T>) => {
    setBusy(true);
    setError(null);
    try {
      return await action();
    } catch (err) {
      setError(
        isAuthFailure(err)
          ? AUTH_HELP
          : err instanceof ApiError
            ? `${err.message}${err.body ? ` — ${err.body}` : ''}`
            : 'Request failed',
      );
      return undefined;
    } finally {
      setBusy(false);
    }
  }, []);

  const value: WorkspaceValue = {
    tenantId,
    live,
    connection,
    projects,
    project,
    setProjectId,
    metrics,
    knowledge,
    relationships,
    workItems,
    milestones,
    activities,
    evidence,
    members,
    campaigns,
    runtimes,
    mcp,
    error,
    busy,
    refresh,
    refreshProjects,
    run,
    setKnowledge,
    setWorkItems,
    setMilestones,
    outline,
    outlineError,
    setCampaigns,
    setEvidence,
    setActivities,
    setRelationships,
  };

  return <WorkspaceContext.Provider value={value}>{children}</WorkspaceContext.Provider>;
}

export function useWorkspace() {
  const value = useContext(WorkspaceContext);
  if (!value) throw new Error('useWorkspace must be used within WorkspaceProvider');
  return value;
}
