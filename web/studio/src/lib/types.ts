export interface Project {
  id: string;
  name: string;
  key: string;
  description?: string | null;
  status?: string;
  updatedAt?: string | null;
  createdAt?: string;
}

export interface KnowledgeItem {
  id: string;
  key: string;
  title: string;
  state: string;
  reviewState: string;
  verificationState: string;
  currencyState?: string;
  typeId: string;
  schemaVersion: string;
  currentRevisionId?: string | null;
  currentRevisionNumber: number;
  createdAt?: string;
  createdBy?: string;
}

export interface KnowledgeRevision {
  id: string;
  knowledgeItemId?: string;
  revisionNumber: number;
  schemaVersion: string;
  content: string;
  reason: string;
  recordedAt: string;
  actor?: string;
  sourceReference?: string | null;
}

export interface ReviewDecision {
  reviewerId: string;
  decision: 'Approved' | 'Rejected';
  revisionId: string;
  revisionNumber: number;
  comment?: string | null;
  decidedAt: string;
}

export interface PlanLockItem {
  itemId: string;
  key: string;
  title: string;
  typeId: string;
  revisionId: string;
  revisionNumber: number;
  parentKey?: string | null;
}

export interface PlanLockPreview {
  canLock: boolean;
  items: PlanLockItem[];
  blockers: { key: string; reason: string }[];
}

export interface PlanOutlineNode {
  itemId: string;
  rootItemId: string;
  parentItemId: string | null;
  position: number;
}

export interface PlanWorkLink {
  id: string;
  workItemId: string;
  knowledgeItemId: string;
  revisionId: string;
}

export interface PlanExpansionProposal {
  id: string;
  parentItemId: string;
  key: string;
  title: string;
  typeId: string;
  rationale: string;
  proposedBy: string;
  status: string;
  resolvedItemId: string | null;
}

export interface PlanOutline {
  nodes: PlanOutlineNode[];
  links: PlanWorkLink[];
  proposals: PlanExpansionProposal[];
}

export interface Relationship {
  id: string;
  sourceItemId: string;
  targetItemId: string;
  relationTypeCode: string;
  status: string;
  createdAt?: string;
  createdBy?: string;
}

export interface WorkItem {
  id: string;
  key: string;
  title: string;
  description?: string;
  milestoneId?: string | null;
  dependsOn?: string[];
  plannedState: string;
  reportedState: string;
  verificationState: string;
  approvalState: string;
  version?: number;
  updatedAt?: string;
}

export interface Milestone {
  id: string;
  key: string;
  title: string;
  state: string;
  dueAt?: string | null;
  version?: number;
}

export interface Activity {
  id: string;
  type: string;
  purpose: string;
  status: string;
  startedAt: string;
  actor?: string;
  reason?: string | null;
}

export interface EvidenceItem {
  id: string;
  type: string;
  relatedCriterion: string;
  reviewState: string;
  recordedAt?: string;
  integrityHash?: string | null;
}

export interface Metric {
  key: string;
  value: number;
  unit: string;
  includedPopulation: string;
  calculatedAt?: string;
}

export interface Membership {
  id: string;
  principalId: string;
  role: string;
  status: string;
}

export interface Campaign {
  id: string;
  objective: string;
  executionMode: string;
  status: string;
  baselineId: string;
  createdAt: string;
}

export interface Runtime {
  id: string;
  principalId: string;
  runtimeClass: string;
  status: string;
  lastHeartbeatAt?: string | null;
}

export interface McpDirectory {
  serviceRef: string;
  capabilities: string[];
}

export interface SearchHit {
  itemId: string;
  revisionId: string;
  title: string;
  typeId: string;
  lifecycle: string;
  review: string;
  verification: string;
  recordedAt: string;
}

/** One immutable version of a stored document. `classification` and `status` may arrive as names or numbers. */
export interface ArtifactVersion {
  id: string;
  key: string;
  version: number;
  mediaType: string;
  sizeInBytes: number;
  checksum: string;
  checksumAlgorithm: string;
  classification: string | number;
  creator: string;
  createdAt: string;
  status: string | number;
}

export interface ArtifactVerification {
  match: boolean;
  expectedChecksum: string;
  actualChecksum: string;
  checksumAlgorithm: string;
}

export interface ArtifactDownload {
  downloadUrl: string;
  expiresAt: string;
}

export type StudioView =
  | 'overview'
  | 'knowledge'
  | 'relations'
  | 'work'
  | 'activity'
  | 'evidence'
  | 'access'
  | 'settings'
  | 'coordination'
  | 'reports';
