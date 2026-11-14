#Requires -Version 7.0
<#
.SYNOPSIS
  Bootstrap the Online Cake Shop pilot project (plan, agents, coordination baselines).

.DESCRIPTION
  Reuses an existing pilot tenant when docs/pilot/session.json (or -ReuseSessionPath) is present;
  otherwise creates a new tenant. Seeds plan v1, evolves (move/detach/add/revise), locks v2,
  and writes docs/pilot/session-online-cake-shop.json. Optionally updates Studio .env.local.
#>
param(
  [string]$ApiBase = "http://localhost:5080",
  [string]$TenantName = "Pilot Agency",
  [string]$OwnerEmail = "owner@pilot.devweave.local",
  [string]$RepositoryId = "devweave-pilot/online-cake-shop",
  [string]$SandboxPath = "C:\Prospectbd Software\business projects\online-cake-shop",
  [string]$Branch = "main",
  [string]$ReuseSessionPath = "",
  [switch]$WriteStudioEnv
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$outDir = Join-Path $repoRoot "docs\pilot"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
if (-not $ReuseSessionPath) {
  $ReuseSessionPath = Join-Path $outDir "session.json"
}

function Invoke-Json {
  param(
    [string]$Method,
    [string]$Url,
    [hashtable]$Headers = @{},
    $Body = $null
  )
  $params = @{ Method = $Method; Uri = $Url; Headers = $Headers }
  if ($null -ne $Body) {
    $params.ContentType = "application/json"
    $params.Body = ($Body | ConvertTo-Json -Depth 10 -Compress)
  }
  try { return Invoke-RestMethod @params }
  catch {
    $detail = $_.ErrorDetails.Message
    if (-not $detail) { $detail = $_.Exception.ToString() }
    throw "HTTP $Method $Url failed: $($_.Exception.Message)`n$detail`nBody=$($params.Body)"
  }
}

Write-Host "== Online Cake Shop pilot bootstrap ==" -ForegroundColor Cyan
Write-Host "API: $ApiBase"

$health = Invoke-RestMethod -Uri "$ApiBase/health/live"
if ($health.status -ne "healthy") { throw "API not healthy" }

$tenantId = $null
$ownerId = $null
$token = $null
if ((Test-Path $ReuseSessionPath)) {
  try {
    $prior = Get-Content $ReuseSessionPath -Raw | ConvertFrom-Json
    if ($prior.tenantId -and $prior.ownerId) {
      $mint = Invoke-Json -Method Post -Url "$ApiBase/auth/pilot/token" -Body @{
        tenantId = $prior.tenantId
        principalId = $prior.ownerId
        platformAdmin = $true
      }
      $tenantId = $prior.tenantId
      $ownerId = $prior.ownerId
      $token = $mint.accessToken
      Write-Host "Reusing tenant $tenantId (token reminted from $ReuseSessionPath)"
    }
  } catch {
    Write-Host "Could not reuse prior session; creating a new tenant. ($($_.Exception.Message))" -ForegroundColor Yellow
  }
}

if (-not $token) {
  $boot = Invoke-Json -Method Post -Url "$ApiBase/auth/pilot/bootstrap" -Body @{
    tenantName = $TenantName
    ownerEmail = $OwnerEmail
  }
  $tenantId = $boot.tenantId
  $ownerId = $boot.principalId
  $token = $boot.accessToken
  Write-Host "Created new tenant $tenantId"
}

$H = @{ Authorization = "Bearer $token" }
$who = Invoke-Json -Method Get -Url "$ApiBase/auth/whoami" -Headers $H
Write-Host "whoami: $($who.principalType) tenant=$($who.tenantId)"

function Ensure-PublishedSchema([string]$namespace, [string]$version, [string]$description, [string]$document) {
  $schemas = Invoke-Json -Method Get -Url "$ApiBase/schemas" -Headers $H
  $existing = @($schemas) | Where-Object { $_.namespace -eq $namespace -and $_.version -eq $version } | Select-Object -First 1
  if (-not $existing) {
    $schema = Invoke-Json -Method Post -Url "$ApiBase/schemas" -Headers $H -Body @{
      namespace = $namespace; version = $version; owner = "platform"
      description = $description; document = $document
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
    code = "supports"; name = "Supports"
    allowedSourceTypeIds = @("design.decision")
    allowedTargetTypeIds = @("plan.requirement")
    direction = "Forward"; cardinality = "ManyToMany"
  }
  Write-Host "Registered relation type supports"
} else {
  Write-Host "Relation type supports already present ($($supports.status))"
}

# Lifecycle profile key may already exist on a reused tenant — create a cake-specific key.
$profile = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/lifecycle-profiles" -Headers $H -Body @{
  key = "cake-delivery"
  name = "Cake Shop Delivery"
  version = "1.0"
}

$project = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/projects" -Headers $H -Body @{
  name = "Online Cake Shop"
  key = "online-cake-shop"
  description = "Pilot: signed-in customers place cake orders and list only their own orders"
  lifecycleProfileId = $profile.id
  createdBy = $ownerId
}

$route = "$ApiBase/tenants/$tenantId/projects/$($project.id)"
$sourceRef = "docs/application-planning-guide.md#online-cake-shop"

function New-KnowledgeEntry {
  param(
    [string]$Key, [string]$Title, [string]$Description = "",
    [string]$TypeId, [string]$SchemaVersion, [string]$Content, [string]$Reason
  )
  $body = @{
    key = $Key; title = $Title; classification = "Internal"
    representationKind = "StructuredData"; typeId = $TypeId
    schemaVersion = $SchemaVersion; createdBy = $ownerId
  }
  if ($Description) { $body.description = $Description }
  $item = Invoke-Json -Method Post -Url "$route/knowledge" -Headers $H -Body $body
  $rev = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/knowledge/$($item.id)/revisions" -Headers $H -Body @{
    expectedCurrentRevisionNumber = 0
    schemaVersion = $SchemaVersion
    content = $Content
    actor = $ownerId
    reason = $Reason
    sourceReference = $sourceRef
    supersededRevisionId = $null
  }
  $null = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/knowledge/$($item.id)/approve" -Headers $H -Body @{
    revisionId = $rev.id
    comment = "Approved during Online Cake Shop pilot bootstrap"
  }
  return [pscustomobject]@{
    itemId = $item.id; key = $Key; typeId = $TypeId
    revisionId = $rev.id; revisionNumber = $rev.revisionNumber
  }
}

$k01 = New-KnowledgeEntry -Key "K-01" -Title "Customers manage their cake orders" `
  -Description "Application outcome for Online Cake Shop: signed-in customers place and list only their own orders." `
  -TypeId "plan.goal" -SchemaVersion "1.0" `
  -Content '{"statement":"A signed-in customer can place a cake order and list only their own orders.","users":"Authenticated cake customers","source":"Product outcome for Online Cake Shop MVP"}' `
  -Reason "Capture application outcome as outline root"
$kOrd = New-KnowledgeEntry -Key "K-ORD" -Title "Orders API module" `
  -Description "HTTP module that owns place-order and list-orders with customer filtering; storefront UI and payments stay out of scope." `
  -TypeId "plan.module" -SchemaVersion "1.0" `
  -Content '{"name":"Orders API","purpose":"HTTP place and list endpoints for customer-scoped cake orders","inScope":["POST place-order","GET list-orders","customer filtering"],"outOfScope":["Storefront UI","payments","delivery tracking"],"source":"MVP module decomposition"}' `
  -Reason "Define orders module under the application goal"
$k02 = New-KnowledgeEntry -Key "K-02" -Title "Place order with cake type and quantity" `
  -Description "Place-order must require a cake type and quantity of at least 1 so every order is fulfillable." `
  -TypeId "plan.requirement" -SchemaVersion "1.0" `
  -Content '{"statement":"Place a cake order with a cake type and quantity; reject missing type or quantity below 1.","acceptance":"A valid order appears in the customer list; invalid payloads are rejected.","rationale":"Cake type and quantity are the minimum fulfillable order attributes.","source":"MVP acceptance"}' `
  -Reason "Capture place-order requirement"
$k03 = New-KnowledgeEntry -Key "K-03" -Title "List only the authenticated customer orders" `
  -Description "List path must enforce per-customer isolation so one customer never sees another customer's orders." `
  -TypeId "plan.requirement" -SchemaVersion "1.0" `
  -Content '{"statement":"List only the authenticated customer orders.","acceptance":"Customer A cannot read customer B orders.","rationale":"Per-customer isolation is required for personal order history.","source":"MVP acceptance"}' `
  -Reason "Capture list-isolation requirement"
$k04 = New-KnowledgeEntry -Key "K-04" -Title "Persist orders in PostgreSQL with customer identity" `
  -Description "Store each order with customer identity in Postgres so list isolation is durable and indexable." `
  -TypeId "design.decision" -SchemaVersion "1.1" `
  -Content '{"summary":"Durable per-customer order storage","decision":"Persist orders in PostgreSQL with customer identity on each row.","rationale":"Durable storage and indexed customer filtering for list isolation; avoids ephemeral in-memory loss.","alternatives":["In-memory store","Shared flat file"],"source":"Architecture choice for MVP"}' `
  -Reason "Record storage decision that supports isolation"
$k05 = New-KnowledgeEntry -Key "K-05" -Title "No source code or secrets in DevWeave" `
  -Description "Cake shop code stays in the external repo; DevWeave keeps only planning evidence and change-set refs." `
  -TypeId "plan.constraint" -SchemaVersion "1.0" `
  -Content '{"statement":"Online Cake Shop implementation stays in the external repository; DevWeave stores refs and planning evidence only.","impact":"Change-sets and messages must carry branch/PR/commit refs and path metadata, never diffs or credentials.","source":"DevWeave source-exclusion policy"}' `
  -Reason "Record control-plane constraint"

# Plan outline v1
$null = Invoke-Json -Method Post -Url "$route/plan/outline" -Headers $H -Body @{ itemId = $k01.itemId; parentItemId = $null; position = 0 }
$null = Invoke-Json -Method Post -Url "$route/plan/outline" -Headers $H -Body @{ itemId = $kOrd.itemId; parentItemId = $k01.itemId; position = 0 }
$null = Invoke-Json -Method Post -Url "$route/plan/outline" -Headers $H -Body @{ itemId = $k02.itemId; parentItemId = $kOrd.itemId; position = 0 }
$null = Invoke-Json -Method Post -Url "$route/plan/outline" -Headers $H -Body @{ itemId = $k03.itemId; parentItemId = $kOrd.itemId; position = 1 }
$null = Invoke-Json -Method Post -Url "$route/plan/outline" -Headers $H -Body @{ itemId = $k04.itemId; parentItemId = $kOrd.itemId; position = 2 }
$null = Invoke-Json -Method Post -Url "$route/plan/outline" -Headers $H -Body @{ itemId = $k05.itemId; parentItemId = $k01.itemId; position = 1 }
Write-Host "Plan outline v1: K-01 / K-ORD / {K-02,K-03,K-04} + K-05"

$relation = Invoke-Json -Method Post -Url "$route/relationships" -Headers $H -Body @{
  sourceItemId = $k04.itemId; targetItemId = $k03.itemId
  relationTypeCode = "supports"; createdBy = $ownerId
}
Write-Host "Semantic relation $($k04.key) -supports-> $($k03.key)"

$milestone = Invoke-Json -Method Post -Url "$route/milestones" -Headers $H -Body @{
  key = "M-01"
  title = "Cake Shop MVP: place, list, and isolate orders"
  dueAt = (Get-Date).ToUniversalTime().AddDays(7).ToString("o")
}

$w01 = Invoke-Json -Method Post -Url "$route/work-items" -Headers $H -Body @{
  key = "W-01"; title = "Implement place-order API"
  description = @"
Deliverable: POST place-order with cake type and quantity validation.
Governed basis: Plan Work link to $($k02.key).
Acceptance: valid order is created; missing type or qty < 1 is rejected.
"@
  milestoneId = $milestone.id; ownerId = $ownerId; dueAt = $null; priority = "High"; dependsOn = @()
}
$w02 = Invoke-Json -Method Post -Url "$route/work-items" -Headers $H -Body @{
  key = "W-02"; title = "Implement customer-scoped list-orders API"
  description = @"
Deliverable: GET list-orders filtered by authenticated customer.
Depends on W-01 for shared persistence.
Governed basis: Plan Work links to $($k03.key) and $($k04.key).
Acceptance: customer A cannot read customer B orders.
"@
  milestoneId = $milestone.id; ownerId = $ownerId; dueAt = $null; priority = "High"; dependsOn = @($w01.id)
}

$linkW01 = Invoke-Json -Method Post -Url "$route/plan/work-links" -Headers $H -Body @{
  workItemId = $w01.id; knowledgeItemId = $k02.itemId; revisionId = $k02.revisionId
}
$linkW02a = Invoke-Json -Method Post -Url "$route/plan/work-links" -Headers $H -Body @{
  workItemId = $w02.id; knowledgeItemId = $k03.itemId; revisionId = $k03.revisionId
}
$linkW02b = Invoke-Json -Method Post -Url "$route/plan/work-links" -Headers $H -Body @{
  workItemId = $w02.id; knowledgeItemId = $k04.itemId; revisionId = $k04.revisionId
}
Write-Host "Plan Work links: W-01->$($k02.key); W-02->$($k03.key),$($k04.key)"

function New-PilotAgent([string]$email, [string]$label) {
  $agent = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/agents" -Headers $H -Body @{ email = $email }
  $membership = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/members" -Headers $H -Body @{
    principalId = $agent.id; role = "Member"
  }
  if ($membership.status -ne "Active") {
    $null = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/members/$($membership.id)/activate" -Headers $H
  }
  $null = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/projects/$($project.id)/members" -Headers $H -Body @{
    principalId = $agent.id; role = "Member"
  }
  $cred = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/agents/$($agent.id)/credentials" -Headers $H -Body @{
    label = $label; expiresAt = $null
  }
  foreach ($cap in @("ReadKnowledge", "AssembleContext", "ExecuteWorkflow")) {
    $grant = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/agent-grants" -Headers $H -Body @{
      agentId = $agent.id; projectId = $project.id; capability = $cap
      expiresAt = $null; classificationCeiling = "Internal"
    }
    $null = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/agent-grants/$($grant.id)/approve" -Headers $H -Body @{
      approver = $ownerId
    }
  }
  return [pscustomobject]@{
    agentId = $agent.id; email = $email; credentialId = $cred.id; secret = $cred.plainTextSecret
  }
}

$agentA = New-PilotAgent "cake-agent-a@pilot.devweave.local" "cake-runtime-a"
$agentB = New-PilotAgent "cake-agent-b@pilot.devweave.local" "cake-runtime-b"

if (-not (Test-Path $SandboxPath)) { throw "Sandbox path missing: $SandboxPath" }
Push-Location $SandboxPath
try { $baseSha = (git rev-parse HEAD).Trim() } finally { Pop-Location }

$coord = "$route/coordination"
$baseline = Invoke-Json -Method Post -Url "$coord/baselines" -Headers $H -Body @{
  goal = "A signed-in customer can place a cake order and list only their own orders"
  approvedRevisions = @(
    @{ id = $k01.itemId; revisionId = $k01.revisionId },
    @{ id = $kOrd.itemId; revisionId = $kOrd.revisionId },
    @{ id = $k02.itemId; revisionId = $k02.revisionId },
    @{ id = $k03.itemId; revisionId = $k03.revisionId },
    @{ id = $k04.itemId; revisionId = $k04.revisionId },
    @{ id = $k05.itemId; revisionId = $k05.revisionId }
  )
  dependencies = @($w01.id, $w02.id)
  acceptanceCriteria = @(
    "Valid place-order and invalid payload rejection (K-02)",
    "Customer-scoped list isolation (K-03)",
    "No source blobs in DevWeave",
    "Exclusive leases across runtimes"
  )
  expectedPlanVersion = 1
}
$target = Invoke-Json -Method Post -Url "$coord/codebase-targets" -Headers $H -Body @{
  provider = "github"; repositoryId = $RepositoryId
  targetBranch = $Branch; baseCommitSha = $baseSha
}
$campaign = Invoke-Json -Method Post -Url "$coord/campaigns" -Headers $H -Body @{
  baselineId = $baseline.id
  codebaseTargetIds = @($target.id)
  objective = "Coordinate W-01 and W-02 on online-cake-shop"
  executionMode = "Mediated"
  idempotencyKey = "cake-pilot-campaign-1"
}
$null = Invoke-Json -Method Post -Url "$coord/campaigns/$($campaign.id)/transition" -Headers $H -Body @{
  targetState = "Active"; reason = "start cake pilot"; expectedVersion = 1
  idempotencyKey = "cake-pilot-activate-1"
}
$pkg1 = Invoke-Json -Method Post -Url "$coord/packages" -Headers $H -Body @{
  campaignId = $campaign.id; baselineId = $baseline.id
  objective = "W-01 place-order API"; dependsOn = @()
  allowedScopes = @("paths:/src/api/place-order")
  acceptanceCriteria = @("valid order created", "missing type or qty < 1 rejected")
  implementsRevisionIds = @($k02.revisionId)
  workItemIds = @($w01.id)
}
$pkg2 = Invoke-Json -Method Post -Url "$coord/packages" -Headers $H -Body @{
  campaignId = $campaign.id; baselineId = $baseline.id
  objective = "W-02 customer-scoped list-orders API"; dependsOn = @($pkg1.id)
  allowedScopes = @("paths:/src/api/list-orders")
  acceptanceCriteria = @("customer-only list", "cross-customer isolation")
  implementsRevisionIds = @($k03.revisionId, $k04.revisionId)
  workItemIds = @($w02.id)
}
$channel = Invoke-Json -Method Post -Url "$coord/channels" -Headers $H -Body @{
  channelType = "Campaign"; campaignId = $campaign.id; workPackageId = $null
}

$baselineV1Id = $baseline.id
$campaignV1Id = $campaign.id
$pkg1V1Id = $pkg1.id
$pkg2V1Id = $pkg2.id

# --- Plan evolution (v2) ---
Write-Host "== Plan evolution (v2) ==" -ForegroundColor Cyan

$kAuth = New-KnowledgeEntry -Key "K-AUTH" -Title "Authentication gate module" `
  -Description "Module that rejects unauthenticated calls before place/list handlers run." `
  -TypeId "plan.module" -SchemaVersion "1.0" `
  -Content '{"name":"Authentication gate","purpose":"Require a valid caller identity on every orders API request","inScope":["Bearer token validation","401 on missing/invalid credentials"],"outOfScope":["Registration UI","password reset","OAuth providers"],"source":"v2 scope expansion"}' `
  -Reason "Add auth module for plan version 2"
$k06 = New-KnowledgeEntry -Key "K-06" -Title "Reject unauthenticated API requests" `
  -Description "Place and list must return 401 when no valid authenticated caller is present." `
  -TypeId "plan.requirement" -SchemaVersion "1.0" `
  -Content '{"statement":"Reject place-order and list-orders when the caller is not authenticated.","acceptance":"Missing or invalid credentials yield HTTP 401; no order rows are created or listed.","rationale":"Customer isolation presupposes an authenticated principal.","source":"v2 acceptance"}' `
  -Reason "Capture auth requirement"
$k07 = New-KnowledgeEntry -Key "K-07" -Title "Validate JWT bearer tokens at the API edge" `
  -Description "Use JWT bearer validation so handlers receive a stable customer identity without storing passwords in the cake shop." `
  -TypeId "design.decision" -SchemaVersion "1.1" `
  -Content '{"summary":"JWT bearer at the API edge","decision":"Validate JWT bearer tokens before place/list handlers and map subject to customer identity.","rationale":"Keeps credentials out of cake-shop storage while giving every request a stable customer id.","alternatives":["Session cookies","API keys in headers"],"source":"v2 architecture choice"}' `
  -Reason "Record auth decision that supports K-06"
$kDraft = New-KnowledgeEntry -Key "K-DRAFT" -Title "Shared wedding registry orders (deferred)" `
  -Description "Parked idea: multi-customer shared wedding registry orders. Detached from the outline so it stays Knowledge without governing the locked plan." `
  -TypeId "plan.requirement" -SchemaVersion "1.0" `
  -Content '{"statement":"Allow two customers to share a wedding registry order list.","acceptance":"Both customers see the same orders.","rationale":"Useful later; out of MVP/v2 scope.","source":"Deferred product idea"}' `
  -Reason "Capture deferred idea for detach demo"

$null = Invoke-Json -Method Post -Url "$route/plan/outline" -Headers $H -Body @{ itemId = $kAuth.itemId; parentItemId = $k01.itemId; position = 2 }
$null = Invoke-Json -Method Post -Url "$route/plan/outline" -Headers $H -Body @{ itemId = $k06.itemId; parentItemId = $kAuth.itemId; position = 0 }
$null = Invoke-Json -Method Post -Url "$route/plan/outline" -Headers $H -Body @{ itemId = $k07.itemId; parentItemId = $kAuth.itemId; position = 1 }
$null = Invoke-Json -Method Post -Url "$route/plan/outline" -Headers $H -Body @{ itemId = $kDraft.itemId; parentItemId = $k01.itemId; position = 3 }
Write-Host "Attached K-AUTH/{K-06,K-07} and temporary K-DRAFT"

$null = Invoke-Json -Method Post -Url "$route/plan/outline/move" -Headers $H -Body @{
  itemId = $k05.itemId; newParentItemId = $kOrd.itemId; position = 3
}
Write-Host "Moved K-05 under K-ORD"

$null = Invoke-Json -Method Post -Url "$route/plan/outline/detach" -Headers $H -Body @{ itemId = $kDraft.itemId }
Write-Host "Detached K-DRAFT (unplaced Knowledge)"

$k03Rev2 = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/knowledge/$($k03.itemId)/revisions" -Headers $H -Body @{
  expectedCurrentRevisionNumber = $k03.revisionNumber
  schemaVersion = "1.0"
  content = '{"statement":"List only the authenticated customer orders.","acceptance":"Customer A cannot read customer B orders; unauthenticated callers receive 401 before any list runs.","rationale":"Per-customer isolation requires an authenticated principal (see K-06).","source":"MVP acceptance + v2 auth gate"}'
  actor = $ownerId
  reason = "Tie list isolation to the authentication gate"
  sourceReference = $sourceRef
  supersededRevisionId = $k03.revisionId
}
$null = Invoke-Json -Method Post -Url "$ApiBase/tenants/$tenantId/knowledge/$($k03.itemId)/approve" -Headers $H -Body @{
  revisionId = $k03Rev2.id; comment = "Approved for plan version 2"
}
$k03PriorRevisionId = $k03.revisionId
$k03 = [pscustomobject]@{
  itemId = $k03.itemId; key = $k03.key; typeId = $k03.typeId
  revisionId = $k03Rev2.id; revisionNumber = $k03Rev2.revisionNumber
}
Write-Host "Revised+approved $($k03.key) r$($k03.revisionNumber)"

$relationAuth = Invoke-Json -Method Post -Url "$route/relationships" -Headers $H -Body @{
  sourceItemId = $k07.itemId; targetItemId = $k06.itemId
  relationTypeCode = "supports"; createdBy = $ownerId
}

$w03 = Invoke-Json -Method Post -Url "$route/work-items" -Headers $H -Body @{
  key = "W-03"; title = "Enforce JWT auth on place and list"
  description = @"
Deliverable: reject unauthenticated place/list with 401.
Governed basis: Plan Work links to $($k06.key) and $($k07.key).
Depends on W-01/W-02 handlers existing.
Acceptance: missing/invalid bearer → 401; valid token reaches owner-scoped behaviour.
"@
  milestoneId = $milestone.id; ownerId = $ownerId; dueAt = $null; priority = "High"
  dependsOn = @($w01.id, $w02.id)
}
$linkW03 = Invoke-Json -Method Post -Url "$route/plan/work-links" -Headers $H -Body @{
  workItemId = $w03.id; knowledgeItemId = $k06.itemId; revisionId = $k06.revisionId
}
$linkW03b = Invoke-Json -Method Post -Url "$route/plan/work-links" -Headers $H -Body @{
  workItemId = $w03.id; knowledgeItemId = $k07.itemId; revisionId = $k07.revisionId
}
$linkW02c = Invoke-Json -Method Post -Url "$route/plan/work-links" -Headers $H -Body @{
  workItemId = $w02.id; knowledgeItemId = $k03.itemId; revisionId = $k03.revisionId
}

$previewV2 = Invoke-Json -Method Get -Url "$coord/baselines/preview" -Headers $H
Write-Host ("Drift before v2 lock: hasDrift={0} added=[{1}] changed=[{2}] stale=[{3}]" -f `
  $previewV2.drift.hasDrift, `
  (($previewV2.drift.addedKeys) -join ","), `
  (($previewV2.drift.changedKeys) -join ","), `
  (($previewV2.drift.staleWorkLinkKeys) -join ","))

if (-not $previewV2.canLock) {
  throw "Cannot lock plan v2: $($previewV2.blockers | ConvertTo-Json -Compress)"
}

$baselineV2 = Invoke-Json -Method Post -Url "$coord/baselines" -Headers $H -Body @{
  goal = "A signed-in customer can place a cake order and list only their own orders; unauthenticated requests are rejected"
  approvedRevisions = @($previewV2.items | ForEach-Object {
    @{ id = $_.itemId; revisionId = $_.revisionId }
  })
  dependencies = @($w01.id, $w02.id, $w03.id)
  acceptanceCriteria = @(
    "Valid place-order and invalid payload rejection (K-02)",
    "Customer-scoped list isolation (K-03)",
    "Unauthenticated place/list → 401 (K-06)",
    "No source blobs in DevWeave (K-05)",
    "Exclusive leases across runtimes"
  )
  expectedPlanVersion = [int]$previewV2.nextPlanVersion
}
Write-Host "Locked PlanBaseline v$($previewV2.nextPlanVersion) ($($baselineV2.id))"

$campaignV2 = Invoke-Json -Method Post -Url "$coord/campaigns" -Headers $H -Body @{
  baselineId = $baselineV2.id
  codebaseTargetIds = @($target.id)
  objective = "Coordinate W-01/W-02/W-03 on online-cake-shop (plan v2)"
  executionMode = "Mediated"
  idempotencyKey = "cake-pilot-campaign-2"
}
$null = Invoke-Json -Method Post -Url "$coord/campaigns/$($campaignV2.id)/transition" -Headers $H -Body @{
  targetState = "Active"; reason = "start cake pilot on plan v2"; expectedVersion = 1
  idempotencyKey = "cake-pilot-activate-2"
}
$pkg1 = Invoke-Json -Method Post -Url "$coord/packages" -Headers $H -Body @{
  campaignId = $campaignV2.id; baselineId = $baselineV2.id
  objective = "W-01 place-order API"; dependsOn = @()
  allowedScopes = @("paths:/src/api/place-order")
  acceptanceCriteria = @("valid order created", "missing type or qty < 1 rejected")
  implementsRevisionIds = @($k02.revisionId)
  workItemIds = @($w01.id)
}
$pkg2 = Invoke-Json -Method Post -Url "$coord/packages" -Headers $H -Body @{
  campaignId = $campaignV2.id; baselineId = $baselineV2.id
  objective = "W-02 customer-scoped list-orders API"; dependsOn = @($pkg1.id)
  allowedScopes = @("paths:/src/api/list-orders")
  acceptanceCriteria = @("customer-only list", "cross-customer isolation")
  implementsRevisionIds = @($k03.revisionId, $k04.revisionId)
  workItemIds = @($w02.id)
}
$pkg3 = Invoke-Json -Method Post -Url "$coord/packages" -Headers $H -Body @{
  campaignId = $campaignV2.id; baselineId = $baselineV2.id
  objective = "W-03 JWT auth gate on place/list"; dependsOn = @($pkg1.id, $pkg2.id)
  allowedScopes = @("paths:/src/api")
  acceptanceCriteria = @("missing/invalid bearer → 401", "valid token reaches customer-scoped handlers")
  implementsRevisionIds = @($k06.revisionId, $k07.revisionId)
  workItemIds = @($w03.id)
}
$channelV2 = Invoke-Json -Method Post -Url "$coord/channels" -Headers $H -Body @{
  channelType = "Campaign"; campaignId = $campaignV2.id; workPackageId = $null
}
Write-Host "Active campaign on baseline v2; packages W-01/W-02/W-03 ready"
Write-Host "Final outline: K-01 → K-ORD → {K-02,K-03,K-04,K-05} + K-AUTH → {K-06,K-07}; K-DRAFT unplaced"

$session = [ordered]@{
  example = "online-cake-shop"
  apiBase = $ApiBase
  tenantId = $tenantId
  ownerId = $ownerId
  humanAccessToken = $token
  projectId = $project.id
  milestoneId = $milestone.id
  workItemW01Id = $w01.id
  workItemW02Id = $w02.id
  workItemW03Id = $w03.id
  knowledgeItemId = $k04.itemId
  revisionId = $k04.revisionId
  knowledge = @{
    k01 = $k01; kOrd = $kOrd; k02 = $k02; k03 = $k03
    k03PriorRevisionId = $k03PriorRevisionId
    k04 = $k04; k05 = $k05; kAuth = $kAuth; k06 = $k06; k07 = $k07; kDraft = $kDraft
    supportsRelationId = $relation.id
    supportsRelationAuthId = $relationAuth.id
    workLinks = @($linkW01.id, $linkW02a.id, $linkW02b.id, $linkW02c.id, $linkW03.id, $linkW03b.id)
    outlineFinal = "K-01 → K-ORD → {K-02,K-03,K-04,K-05} + K-AUTH → {K-06,K-07}; K-DRAFT unplaced"
  }
  planEvolution = @{
    moved = @(@{ key = "K-05"; from = "K-01"; to = "K-ORD" })
    detached = @("K-DRAFT")
    addedKeys = @("K-AUTH", "K-06", "K-07")
    driftBeforeV2 = $previewV2.drift
  }
  repositoryId = $RepositoryId
  sandboxPath = $SandboxPath
  baseCommitSha = $baseSha
  baselineId = $baselineV2.id
  baselineV1Id = $baselineV1Id
  baselineV2Id = $baselineV2.id
  planVersion = [int]$previewV2.nextPlanVersion
  codebaseTargetId = $target.id
  campaignId = $campaignV2.id
  campaignV1Id = $campaignV1Id
  campaignV2Id = $campaignV2.id
  packageW01Id = $pkg1.id
  packageW02Id = $pkg2.id
  packageW03Id = $pkg3.id
  packageW01V1Id = $pkg1V1Id
  packageW02V1Id = $pkg2V1Id
  channelId = $channelV2.id
  channelV1Id = $channel.id
  agentA = $agentA
  agentB = $agentB
  createdAt = (Get-Date).ToUniversalTime().ToString("o")
}

$sessionPath = Join-Path $outDir "session-online-cake-shop.json"
$session | ConvertTo-Json -Depth 8 | Set-Content -Path $sessionPath -Encoding utf8
Write-Host ""
Write-Host "Wrote $sessionPath" -ForegroundColor Green

if ($WriteStudioEnv) {
  $studioEnv = @"
VITE_API_BASE_URL=
VITE_TENANT_ID=$tenantId
VITE_API_TOKEN=$token
"@
  $studioEnvPath = Join-Path $repoRoot "web\studio\.env.local"
  Set-Content -Path $studioEnvPath -Value $studioEnv -Encoding utf8
  Write-Host "Wrote $studioEnvPath (restart Vite; select project Online Cake Shop)" -ForegroundColor Green
} else {
  Write-Host "Studio .env.local left unchanged (pass -WriteStudioEnv to point Studio at this tenant)." -ForegroundColor Yellow
}

Write-Host "Canonical target: github:$RepositoryId@$Branch#$baseSha"
Write-Host "Plan versions: v1=$baselineV1Id → v2=$($baselineV2.id) (active campaign $($campaignV2.id))" -ForegroundColor Green
$sessionPath
