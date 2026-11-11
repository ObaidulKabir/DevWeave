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
import { ApiError, apiFetch, hasLiveConfig } from '../lib/api';
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

export function WorkspaceProvider({ children }: { children: ReactNode }) {
  const tenantId = import.meta.env.VITE_TENANT_ID as string | undefined;
  const [connection, setConnection] = useState<'checking' | 'online' | 'unreachable'>('checking');
  const [projects, setProjects] = useState<Project[]>(DEMO_PROJECTS);
  const [projectId, setProjectId] = useState(DEMO_PROJECTS[0].id);
  const [metrics, setMetrics] = useState<Metric[]>([]);
  const [knowledge, setKnowledge] = useState<KnowledgeItem[]>([]);
  const [relationships, setRelationships] = useState<Relationship[]>([]);
  const [workItems, setWorkItems] = useState<WorkItem[]>([]);
  const [milestones, setMilestones] = useState<Milestone[]>([]);
  const [outline, setOutline] = useState<PlanOutline>({ nodes: [], links: [], proposals: [] });
  const [outlineError, setOutlineError] = useState(false);
  const [activities, setActivities] = useState<Activity[]>([]);
  const [evidence, setEvidence] = useState<EvidenceItem[]>([]);
  const [members, setMembers] = useState<Membership[]>([]);
  const [campaigns, setCampaigns] = useState<Campaign[]>([]);
  const [runtimes, setRuntimes] = useState<Runtime[]>([]);
  const [mcp, setMcp] = useState<McpDirectory | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const project = useMemo(
    () => projects.find((item) => item.id === projectId) ?? projects[0],
    [projects, projectId],
  );
  const live = Boolean(tenantId) && hasLiveConfig() && !project?.id.startsWith('demo-');

  const refresh = useCallback(async () => {
    if (!tenantId || !project || project.id.startsWith('demo-')) {
      setMetrics([]);
      setKnowledge([]);
      setRelationships([]);
      setWorkItems([]);
      setMilestones([]);
      setOutline({ nodes: [], links: [], proposals: [] });
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
      apiFetch<Metric[]>(`${base}/metrics`).catch(() => []),
      apiFetch<KnowledgeItem[]>(`${base}/knowledge`).catch(() => []),
      apiFetch<Relationship[]>(`${base}/relationships`).catch(() => []),
      apiFetch<WorkItem[]>(`${base}/work-items`).catch(() => []),
      apiFetch<Milestone[]>(`${base}/milestones`).catch(() => []),
      apiFetch<Activity[]>(`${base}/activities`).catch(() => []),
      apiFetch<EvidenceItem[]>(`${base}/evidence`).catch(() => []),
      apiFetch<Membership[]>(`/tenants/${tenantId}/members`).catch(() => []),
      apiFetch<Campaign[]>(`${base}/coordination/campaigns`).catch(() => []),
      apiFetch<Runtime[]>(`${base}/coordination/runtimes`).catch(() => []),
      apiFetch<McpDirectory>(`${base}/coordination/mcp`).catch(() => null),
      apiFetch<PlanOutline>(`${base}/plan/`).catch(() => {
        setOutlineError(true);
        return { nodes: [], links: [], proposals: [] };
      }),
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
    setError(null);
  }, [tenantId, project]);

  useEffect(() => {
    let cancelled = false;
    apiFetch<{ status: string }>('/health/live')
      .then(async () => {
        if (cancelled) return;
        setConnection('online');
        if (!tenantId || !hasLiveConfig()) return;
        const liveProjects = await apiFetch<Project[]>(`/tenants/${tenantId}/projects`);
        if (!cancelled && liveProjects.length) {
          setProjects(liveProjects);
          setProjectId(liveProjects[0].id);
        }
      })
      .catch(() => {
        if (!cancelled) setConnection('unreachable');
      });
    return () => {
      cancelled = true;
    };
  }, [tenantId]);

  useEffect(() => {
    refresh().catch((err: unknown) => {
      setError(err instanceof ApiError ? err.message : 'Failed to load workspace');
    });
  }, [refresh]);

  const run = useCallback(async <T,>(action: () => Promise<T>) => {
    setBusy(true);
    setError(null);
    try {
      return await action();
    } catch (err) {
      setError(err instanceof ApiError ? `${err.message}${err.body ? ` — ${err.body}` : ''}` : 'Request failed');
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
