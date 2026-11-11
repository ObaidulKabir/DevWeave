#Requires -Version 7.0
<#
.SYNOPSIS
  Bootstrap the TaskBoard Lite real-case pilot tenant, plan, agents, and coordination baseline.
#>
param(
  [string]$ApiBase = "http://localhost:5080",
  [string]$TenantName = "Pilot Agency",
  [string]$OwnerEmail = "owner@pilot.devweave.local",
  [string]$RepositoryId = "devweave-pilot/taskboard-lite",
  [string]$SandboxPath = "C:\Prospectbd Software\business projects\taskboard-lite",
  [string]$Branch = "main"
)

$ErrorActionPreference = "Stop"
$outDir = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "docs\pilot"
# Prefer repo-relative docs/pilot next to scripts/
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$outDir = Join-Path $repoRoot "docs\pilot"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function Invoke-Json {
  param(
    [string]$Method,
    [string]$Url,
    [hashtable]$Headers = @{},
    $Body = $null
  )
  $params = @{
    Method = $Method
    Uri = $Url
    Headers = $Headers
  }
  if ($null -ne $Body) {
    $params.ContentType = "application/json"
    $params.Body = ($Body | ConvertTo-Json -Depth 10 -Compress)
  }
  try {
    return Invoke-RestMethod @params
  } catch {
    $detail = $_.ErrorDetails.Message
    if (-not $detail) { $detail = $_.Exception.ToString() }
    throw "HTTP $Method $Url failed: $($_.Exception.Message)`n$detail`nBody=$($params.Body)"
  }
}

Write-Host "== Pilot bootstrap ==" -ForegroundColor Cyan
Write-Host "API: $ApiBase"

$health = Invoke-RestMethod -Uri "$ApiBase/health/live"
if ($health.status -ne "healthy") { throw "API not healthy" }

$boot = Invoke-Json -Method Post -Url "$ApiBase/auth/pilot/bootstrap" -Body @{
  tenantName = $TenantName
  ownerEmail = $OwnerEmail
}
$tenantId = $boot.tenantId
$ownerId = $boot.principalId
$token = $boot.accessToken
$H = @{ Authorization = "Bearer $token" }

$who = Invoke-Json -Method Get -Url "$ApiBase/auth/whoami" -Headers $H
Write-Host "whoami: $($who.principalType) tenant=$($who.tenantId)"

# Knowledge schemas + relation type from docs/application-planning-guide.md (TaskBoard Lite example)
function Ensure-PublishedSchema([string]$namespace, [string]$version, [string]$description, [string]$document) {
  $schemas = Invoke-Json -Method Get -Url "$ApiBase/schemas" -Headers $H
  $existing = @($schemas) | Where-Object { $_.namespace -eq $namespace -and $_.version -eq $version } | Select-Object -First 1
  if (-not $existing) {
    $schema = Invoke-Json -Method Post -Url "$ApiBase/schemas" -Headers $H -Body @{
      namespace = $namespace
      version = $version
      owner = "platform"
      description = $description
      document = $document
    }
    $null = Invoke-Json -Method Post -Url "$ApiBase/schemas/$($schema.id)/publish" -Headers $H
    Write-Host "Published schema $namespace@$version"
    return
  }
  if ($existing.status -ne "Published") {
    $null = Invoke-Json -Method Post -Url "$ApiBase/schemas/$($existing.id)/publish" -Headers $H
    Write-Host "Published existing schema $namespace@$version"
  } else {
    Write-Host "Schema $namespace@$version already published"
  }
}

Ensure-PublishedSchema "plan.goal" "1.0" "Planning goal statement" `
  '{"type":"object","required":["statement","users"],"properties":{"statement":{"type":"string"},"users":{"type":"string"},"source":{"type":"string"}}}'
Ensure-PublishedSchema "plan.module" "1.0" "Application module or capability slice" `
  '{"type":"object","required":["name","purpose"],"properties":{"name":{"type":"string"},"purpose":{"type":"string"},"inScope":{"type":"array","items":{"type":"string"}},"outOfScope":{"type":"array","items":{"type":"string"}},"source":{"type":"string"}}}'
Ensure-PublishedSchema "plan.requirement" "1.0" "Testable requirement with acceptance" `
  '{"type":"object","required":["statement","acceptance"],"properties":{"statement":{"type":"string"},"acceptance":{"type":"string"},"rationale":{"type":"string"},"source":{"type":"string"}}}'
Ensure-PublishedSchema "design.decision" "1.1" "Design decision with rationale" `
  '{"type":"object","required":["decision","rationale"],"properties":{"decision":{"type":"string"},"rationale":{"type":"string"},"summary":{"type":"string"},"alternatives":{"type":"array","items":{"type":"string"}},"source":{"type":"string"}}}'
Ensure-PublishedSchema "plan.constraint" "1.0" "Non-negotiable constraint" `
  '{"type":"object","required":["statement","impact"],"properties":{"statement":{"type":"string"},"impact":{"type":"string"},"source":{"type":"string"}}}'

$relationTypes = Invoke-Json -Method Get -Url "$ApiBase/relation-types" -Headers $H
$supports = @($relationTypes) | Where-Object { $_.code -eq "supports" } | Select-Object -First 1
if (-not $supports) {
  $supports = Invoke-Json -Method Post -Url "$ApiBase/relation-types" -Headers $H -Body @{
    code = "supports"
    name = "Supports"
    allowedSourceTypeIds = @("design.decision")
    allowedTargetTypeIds = @("plan.requirement")
    direction = "Forward"
    cardinality = "ManyToMany"
  }
  Write-Host "Registered relation type supports"
} else {
  Write-Host "Relation type supports already present ($($supports.status))"
}

$profile = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/lifecycle-profiles" -Headers $H -Body @{
  key = "delivery"
  name = "Delivery"
  version = "1.0"
}

$project = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/projects" -Headers $H -Body @{
  name = "TaskBoard Lite"
  key = "taskboard-lite"
  description = "Real-case pilot: signed-in users create and list their own tasks"
  lifecycleProfileId = $profile.id
  createdBy = $ownerId
}

$route = "$ApiBase/tenants/$tenantId/projects/$($project.id)"

function New-KnowledgeEntry {
  param(
    [string]$Key,
    [string]$Title,
    [string]$TypeId,
    [string]$SchemaVersion,
    [string]$Content,
    [string]$Reason
  )
  $item = Invoke-Json -Method Post -Url "$route/knowledge" -Headers $H -Body @{
    key = $Key
    title = $Title
    classification = "Internal"
    representationKind = "StructuredData"
    typeId = $TypeId
    schemaVersion = $SchemaVersion
    createdBy = $ownerId
  }
  $rev = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/knowledge/$($item.id)/revisions" -Headers $H -Body @{
    expectedCurrentRevisionNumber = 0
    schemaVersion = $SchemaVersion
    content = $Content
    actor = $ownerId
    reason = $Reason
    sourceReference = "docs/application-planning-guide.md#taskboard-lite"
    supersededRevisionId = $null
  }
  $null = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/knowledge/$($item.id)/approve" -Headers $H -Body @{
    revisionId = $rev.id
    comment = "Approved during pilot bootstrap"
  }
  return [pscustomobject]@{
    itemId = $item.id
    key = $Key
    typeId = $TypeId
    revisionId = $rev.id
    revisionNumber = $rev.revisionNumber
  }
}

# TaskBoard Lite sample redesigned for Plan outline + semantic Relations + Plan Work links
$k01 = New-KnowledgeEntry -Key "K-01" -Title "Users manage personal tasks" `
  -TypeId "plan.goal" -SchemaVersion "1.0" `
  -Content '{"statement":"A signed-in user can create and list their own tasks.","users":"Authenticated task owners","source":"Product outcome for TaskBoard Lite MVP"}' `
  -Reason "Capture application outcome as outline root"
$kApi = New-KnowledgeEntry -Key "K-API" -Title "Personal tasks API module" `
  -TypeId "plan.module" -SchemaVersion "1.0" `
  -Content '{"name":"Personal tasks API","purpose":"HTTP create and list endpoints for owner-scoped tasks","inScope":["POST create-task","GET list-tasks","owner filtering"],"outOfScope":["UI shell","sharing","notifications"],"source":"MVP module decomposition"}' `
  -Reason "Define module under the application goal"
$k02 = New-KnowledgeEntry -Key "K-02" -Title "Create a task with a non-empty title" `
  -TypeId "plan.requirement" -SchemaVersion "1.0" `
  -Content '{"statement":"Create a task with a title; reject an empty title.","acceptance":"A valid task appears in the owner list and an empty title is rejected.","rationale":"Titles are the minimum identifiable task attribute.","source":"MVP acceptance"}' `
  -Reason "Capture create-task requirement"
$k03 = New-KnowledgeEntry -Key "K-03" -Title "List only the authenticated user tasks" `
  -TypeId "plan.requirement" -SchemaVersion "1.0" `
  -Content '{"statement":"List only the authenticated user tasks.","acceptance":"User A cannot read user B tasks.","rationale":"Per-user isolation is required for personal task boards.","source":"MVP acceptance"}' `
  -Reason "Capture list-isolation requirement"
$k04 = New-KnowledgeEntry -Key "K-04" -Title "Persist tasks in PostgreSQL with owner identity" `
  -TypeId "design.decision" -SchemaVersion "1.1" `
  -Content '{"summary":"Durable per-user task storage","decision":"Persist tasks in PostgreSQL with owner identity on each row.","rationale":"Durable storage and indexed owner filtering for list isolation; avoids ephemeral in-memory loss.","alternatives":["In-memory store","Shared flat file"],"source":"Architecture choice for MVP"}' `
  -Reason "Record storage decision that supports isolation"
$k05 = New-KnowledgeEntry -Key "K-05" -Title "No source code or secrets in DevWeave" `
  -TypeId "plan.constraint" -SchemaVersion "1.0" `
  -Content '{"statement":"TaskBoard implementation stays in the external repository; DevWeave stores refs and planning evidence only.","impact":"Change-sets and messages must carry branch/PR/commit refs and path metadata, never diffs or credentials.","source":"DevWeave source-exclusion policy"}' `
  -Reason "Record control-plane constraint"

# Plan outline (structural): root goal -> module -> requirements/decision/constraint
$null = Invoke-Json -Method Post -Url "$route/plan/outline" -Headers $H -Body @{
  itemId = $k01.itemId; parentItemId = $null; position = 0
}
$null = Invoke-Json -Method Post -Url "$route/plan/outline" -Headers $H -Body @{
  itemId = $kApi.itemId; parentItemId = $k01.itemId; position = 0
}
$null = Invoke-Json -Method Post -Url "$route/plan/outline" -Headers $H -Body @{
  itemId = $k02.itemId; parentItemId = $kApi.itemId; position = 0
}
$null = Invoke-Json -Method Post -Url "$route/plan/outline" -Headers $H -Body @{
  itemId = $k03.itemId; parentItemId = $kApi.itemId; position = 1
}
$null = Invoke-Json -Method Post -Url "$route/plan/outline" -Headers $H -Body @{
  itemId = $k04.itemId; parentItemId = $kApi.itemId; position = 2
}
$null = Invoke-Json -Method Post -Url "$route/plan/outline" -Headers $H -Body @{
  itemId = $k05.itemId; parentItemId = $k01.itemId; position = 1
}
Write-Host "Plan outline: K-01 / K-API / {K-02,K-03,K-04} + K-05"

# Semantic relation (not the same as outline parent/child)
$relation = Invoke-Json -Method Post -Url "$route/relationships" -Headers $H -Body @{
  sourceItemId = $k04.itemId
  targetItemId = $k03.itemId
  relationTypeCode = "supports"
  createdBy = $ownerId
}
Write-Host "Semantic relation $($k04.key) -supports-> $($k03.key) ($($relation.id))"

$milestone = Invoke-Json -Method Post -Url "$route/milestones" -Headers $H -Body @{
  key = "M-01"
  title = "TaskBoard MVP: create, list, and isolate tasks"
  dueAt = (Get-Date).ToUniversalTime().AddDays(7).ToString("o")
}

$tb1 = Invoke-Json -Method Post -Url "$route/work-items" -Headers $H -Body @{
  key = "W-01"
  title = "Implement create-task API"
  description = @"
Deliverable: POST create-task with title validation.
Governed basis: Plan Work link to $($k02.key) (pinned revision below).
Narrative: also serves goal $($k01.key).
Acceptance: valid title creates a task; empty title is rejected.
"@
  milestoneId = $milestone.id
  ownerId = $ownerId
  dueAt = $null
  priority = "High"
  dependsOn = @()
}

$tb2 = Invoke-Json -Method Post -Url "$route/work-items" -Headers $H -Body @{
  key = "W-02"
  title = "Implement user-scoped list-tasks API"
  description = @"
Deliverable: GET list-tasks filtered by authenticated owner.
Depends on W-01 for shared persistence.
Governed basis: Plan Work links to $($k03.key) and $($k04.key).
Acceptance: user A cannot read user B tasks; list returns only owner tasks.
"@
  milestoneId = $milestone.id
  ownerId = $ownerId
  dueAt = $null
  priority = "High"
  dependsOn = @($tb1.id)
}

# Revision-pinned Plan Work links (require current Reviewed knowledge on the outline)
$linkW01 = Invoke-Json -Method Post -Url "$route/plan/work-links" -Headers $H -Body @{
  workItemId = $tb1.id; knowledgeItemId = $k02.itemId; revisionId = $k02.revisionId
}
$linkW02a = Invoke-Json -Method Post -Url "$route/plan/work-links" -Headers $H -Body @{
  workItemId = $tb2.id; knowledgeItemId = $k03.itemId; revisionId = $k03.revisionId
}
$linkW02b = Invoke-Json -Method Post -Url "$route/plan/work-links" -Headers $H -Body @{
  workItemId = $tb2.id; knowledgeItemId = $k04.itemId; revisionId = $k04.revisionId
}
Write-Host "Plan Work links: W-01->$($k02.key); W-02->$($k03.key),$($k04.key)"

# Backward-compatible aliases used by later pilot scripts
$knowledge = [pscustomobject]@{ id = $k04.itemId }
$revision = [pscustomobject]@{ id = $k04.revisionId }

# Agents
function New-PilotAgent([string]$email, [string]$label) {
  $agent = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/agents" -Headers $H -Body @{ email = $email }
  $membership = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/members" -Headers $H -Body @{
    principalId = $agent.id
    role = "Member"
  }
  if ($membership.status -ne "Active") {
    $null = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/members/$($membership.id)/activate" -Headers $H
  }
  $null = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/projects/$($project.id)/members" -Headers $H -Body @{
    principalId = $agent.id
    role = "Member"
  }
  $cred = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/agents/$($agent.id)/credentials" -Headers $H -Body @{
    label = $label
    expiresAt = $null
  }
  foreach ($cap in @("ReadKnowledge", "AssembleContext", "ExecuteWorkflow")) {
    $grant = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/agent-grants" -Headers $H -Body @{
      agentId = $agent.id
      projectId = $project.id
      capability = $cap
      expiresAt = $null
      classificationCeiling = "Internal"
    }
    $null = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/agent-grants/$($grant.id)/approve" -Headers $H -Body @{
      approver = $ownerId
    }
  }
  return [pscustomobject]@{
    agentId = $agent.id
    email = $email
    credentialId = $cred.id
    secret = $cred.plainTextSecret
  }
}

$agentA = New-PilotAgent "agent-a@pilot.devweave.local" "pilot-runtime-a"
$agentB = New-PilotAgent "agent-b@pilot.devweave.local" "pilot-runtime-b"

# Coordination freeze
if (-not (Test-Path $SandboxPath)) { throw "Sandbox path missing: $SandboxPath" }
Push-Location $SandboxPath
try {
  $baseSha = (git rev-parse HEAD).Trim()
} finally {
  Pop-Location
}

$coord = "$route/coordination"
$baseline = Invoke-Json -Method Post -Url "$coord/baselines" -Headers $H -Body @{
  goal = "A signed-in user can create and list their own tasks"
  approvedRevisions = @(
    @{ id = $k01.itemId; revisionId = $k01.revisionId },
    @{ id = $kApi.itemId; revisionId = $kApi.revisionId },
    @{ id = $k02.itemId; revisionId = $k02.revisionId },
    @{ id = $k03.itemId; revisionId = $k03.revisionId },
    @{ id = $k04.itemId; revisionId = $k04.revisionId },
    @{ id = $k05.itemId; revisionId = $k05.revisionId }
  )
  dependencies = @($tb1.id, $tb2.id)
  acceptanceCriteria = @(
    "Valid create-task and empty-title rejection (K-02)",
    "User-scoped list isolation (K-03)",
    "No source blobs in DevWeave",
    "Exclusive leases across runtimes"
  )
  expectedPlanVersion = 1
}
$target = Invoke-Json -Method Post -Url "$coord/codebase-targets" -Headers $H -Body @{
  provider = "github"
  repositoryId = $RepositoryId
  targetBranch = $Branch
  baseCommitSha = $baseSha
}
$campaign = Invoke-Json -Method Post -Url "$coord/campaigns" -Headers $H -Body @{
  baselineId = $baseline.id
  codebaseTargetIds = @($target.id)
  objective = "Coordinate TB-1 and TB-2 on taskboard-lite"
  executionMode = "Mediated"
  idempotencyKey = "pilot-campaign-1"
}
$null = Invoke-Json -Method Post -Url "$coord/campaigns/$($campaign.id)/transition" -Headers $H -Body @{
  targetState = "Active"
  reason = "start pilot"
  expectedVersion = 1
  idempotencyKey = "pilot-activate-1"
}
$pkg1 = Invoke-Json -Method Post -Url "$coord/packages" -Headers $H -Body @{
  campaignId = $campaign.id
  baselineId = $baseline.id
  objective = "W-01 create-task API"
  dependsOn = @()
  allowedScopes = @("paths:/src/api/create-task")
  acceptanceCriteria = @("valid title creates task", "empty title rejected")
}
$pkg2 = Invoke-Json -Method Post -Url "$coord/packages" -Headers $H -Body @{
  campaignId = $campaign.id
  baselineId = $baseline.id
  objective = "W-02 user-scoped list-tasks API"
  dependsOn = @($pkg1.id)
  allowedScopes = @("paths:/src/api/list-tasks")
  acceptanceCriteria = @("owner-only list", "cross-user isolation")
}
$channel = Invoke-Json -Method Post -Url "$coord/channels" -Headers $H -Body @{
  channelType = "Campaign"
  campaignId = $campaign.id
  workPackageId = $null
}

$session = [ordered]@{
  apiBase = $ApiBase
  tenantId = $tenantId
  ownerId = $ownerId
  humanAccessToken = $token
  projectId = $project.id
  milestoneId = $milestone.id
  workItemTb1Id = $tb1.id
  workItemTb2Id = $tb2.id
  knowledgeItemId = $k04.itemId
  revisionId = $k04.revisionId
  knowledge = @{
    k01 = $k01
    kApi = $kApi
    k02 = $k02
    k03 = $k03
    k04 = $k04
    k05 = $k05
    supportsRelationId = $relation.id
    workLinks = @($linkW01.id, $linkW02a.id, $linkW02b.id)
  }
  repositoryId = $RepositoryId
  sandboxPath = $SandboxPath
  baseCommitSha = $baseSha
  baselineId = $baseline.id
  codebaseTargetId = $target.id
  campaignId = $campaign.id
  packageTb1Id = $pkg1.id
  packageTb2Id = $pkg2.id
  channelId = $channel.id
  agentA = $agentA
  agentB = $agentB
  createdAt = (Get-Date).ToUniversalTime().ToString("o")
}

$sessionPath = Join-Path $outDir "session.json"
$session | ConvertTo-Json -Depth 8 | Set-Content -Path $sessionPath -Encoding utf8

$studioEnv = @"
VITE_API_BASE_URL=
VITE_TENANT_ID=$tenantId
VITE_API_TOKEN=$token
"@
$studioEnvPath = Join-Path $repoRoot "web\studio\.env.local"
Set-Content -Path $studioEnvPath -Value $studioEnv -Encoding utf8

Write-Host ""
Write-Host "Wrote $sessionPath" -ForegroundColor Green
Write-Host "Wrote $studioEnvPath (restart Vite to pick up token)" -ForegroundColor Green
Write-Host "Canonical target: github:$RepositoryId@$Branch#$baseSha"
$sessionPath
