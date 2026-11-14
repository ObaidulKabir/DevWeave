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
  description?: string | null;
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

export interface PlanDrift {
  hasDrift: boolean;
  lastBaselineId?: string | null;
  lastPlanVersion?: number | null;
  addedKeys: string[];
  removedKeys: string[];
  changedKeys: string[];
  staleWorkLinkKeys: string[];
  activeCampaignIdsOnPriorBaselines: string[];
}

export interface PlanLockPreview {
  canLock: boolean;
  items: PlanLockItem[];
  blockers: { key: string; reason: string }[];
  currentPlanVersion: number;
  nextPlanVersion: number;
  drift: PlanDrift;
}

export interface PlanBaselineSummary {
  id: string;
  goal: string;
  planVersion: number;
  revisionCount: number;
  createdAt: string;
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

export interface PlanStatusCampaign {
  id: string;
  baselineId: string;
  planVersion: number;
  onCurrentPlan: boolean;
  status: string;
  objective: string;
}

export interface PlanStatus {
  currentPlanVersion: number;
  currentBaselineId: string | null;
  lockedAt: string | null;
  campaigns: PlanStatusCampaign[];
}

export interface Workthrough {
  id: string;
  workPackageId: string;
  recordedBy: string;
  kind: 'Succeeded' | 'Blocked' | string;
  outcome: string;
  whatSucceeded: string;
  blockedReason: string | null;
  steps: string[];
  changeSetId: string | null;
  recordedAt: string;
}

export interface WorkPackageStatus {
  id: string;
  campaignId: string;
  baselineId: string;
  planVersion: number;
  onCurrentPlan: boolean;
  key: string;
  objective: string;
  status: string;
  canProgress: boolean;
  blockedReason: string | null;
  implementsRevisionIds: string[];
  workItemIds: string[];
  specTopics: string[];
  unscoped: boolean;
  latestWorkthrough?: Workthrough | null;
}

export interface Runtime {
  id: string;
  principalId: string;
  runtimeClass: string;
  status: string;
  lastHeartbeatAt?: string | null;
}

/** Active exclusive hold on a work-package path scope. */
export interface Lease {
  id: string;
  workPackageId: string;
  runtimeId: string;
  resourceScope: string;
  fence: number;
  expiresAt: string;
  state: string;
  createdAt?: string;
}

export interface CoordinationChannel {
  id: string;
  campaignId?: string | null;
  workPackageId?: string | null;
  channelType: string;
  visibility: string;
  sequenceHighWatermark: number;
  createdAt: string;
}

export interface CoordinationMessage {
  id: string;
  channelId: string;
  sequence: number;
  senderId: string;
  runtimeId: string;
  messageType: string;
  schemaVersion: string;
  allowedMetadataJson: string;
  createdAt: string;
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

export interface SpecParameter {
  name: string;
  kind: 'string' | 'integer' | 'boolean';
  required: boolean;
  allowedValues: string[];
}

export interface SpecChoice {
  id: string;
  label: string;
  support: 'recommended' | 'supported' | 'discouraged' | 'deprecated';
  source: string;
  custom: boolean;
  parameters: SpecParameter[];
}

export interface SpecTopic {
  id: string;
  title: string;
  description: string;
  domain: string;
  allowCustom: boolean;
  default?: string | null;
  source: string;
  choices: SpecChoice[];
  constraintRules: string[];
  rules: { whenChoice: string; otherTopic: string; allowedOtherChoices: string[] }[];
}

export interface SpecDecisionView {
  topic: string;
  scope: 'platform' | 'tenant' | 'project';
  choice: string;
  params: Record<string, string>;
  constraints: { rule: string; value: string }[];
  locked: boolean;
  itemId?: string | null;
  revisionId?: string | null;
  revisionNumber?: number | null;
  definedAt: string;
}

export interface SpecResolutionView {
  topic: string;
  decided: boolean;
  origin?: 'predefined' | 'firstapproved' | 'override' | null;
  binding?: SpecDecisionView | null;
  conflicts: { reason: string; binding: SpecDecisionView; other: SpecDecisionView }[];
}

export interface SpecEffective {
  decisions: SpecResolutionView[];
  incompatibilities: { path: string; message: string }[];
  undecidedTopics: string[];
}

export interface SpecPackStatus {
  id: string;
  version: string;
  description: string;
  enabled: boolean;
  required: boolean;
  latestVersion: string;
  pinned: boolean;
  updateAvailable: boolean;
}

export interface SpecProfile {
  id: string;
  label: string;
  description: string;
  projectType: string;
  source: string;
  decisions: { topic: string; choice: string; params: Record<string, string> }[];
}

export interface SpecCatalog {
  packs: SpecPackStatus[];
  topics: SpecTopic[];
  profiles: SpecProfile[];
  problems: { source: string; issues: { path: string; message: string }[] }[];
}

export interface SpecUpgrade {
  pack: string;
  from: string;
  to: string;
  topicsAdded: string[];
  topicsRemoved: string[];
  choiceChanges: { topic: string; choice: string; change: string }[];
  defaultChanges: { topic: string; from?: string | null; to?: string | null }[];
  decisionChanges: { topic: string; fromChoice?: string | null; toChoice?: string | null; lockChanged: boolean }[];
  profilesAdded: string[];
  profilesRemoved: string[];
  affectedDecisions: string[];
}
