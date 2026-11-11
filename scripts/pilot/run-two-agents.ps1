#Requires -Version 7.0
<#
.SYNOPSIS
  Simulate two agent runtimes: exclusive leases, MCP replay, sandbox commits/PRs, change-set refs, evidence.
#>
param(
  [string]$SessionPath = "",
  [string]$ApiBase = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
if (-not $SessionPath) {
  $SessionPath = Join-Path $repoRoot "docs\pilot\session.json"
}
$session = Get-Content $SessionPath -Raw | ConvertFrom-Json
if (-not $ApiBase) { $ApiBase = $session.apiBase }

function Invoke-Json {
  param([string]$Method, [string]$Url, [hashtable]$Headers = @{}, $Body = $null)
  $params = @{ Method = $Method; Uri = $Url; Headers = $Headers }
  if ($null -ne $Body) {
    $params.ContentType = "application/json"
    $params.Body = ($Body | ConvertTo-Json -Depth 10 -Compress)
  }
  try { return Invoke-RestMethod @params }
  catch {
    $detail = $_.ErrorDetails.Message
    throw "HTTP $Method $Url failed: $($_.Exception.Message)`n$detail`nBody=$($params.Body)"
  }
}

function Get-AgentToken([string]$agentId, [string]$credentialId, [string]$secret) {
  $auth = Invoke-Json -Method Post -Url "$ApiBase/tenants/$($session.tenantId)/agents/$agentId/authenticate" -Body @{
    credentialId = $credentialId
    secret = $secret
  }
  return $auth.accessToken
}

Write-Host "== Two-agent pilot run ==" -ForegroundColor Cyan
$tokenA = Get-AgentToken $session.agentA.agentId $session.agentA.credentialId $session.agentA.secret
$tokenB = Get-AgentToken $session.agentB.agentId $session.agentB.credentialId $session.agentB.secret
$HA = @{ Authorization = "Bearer $tokenA" }
$HB = @{ Authorization = "Bearer $tokenB" }
$HH = @{ Authorization = "Bearer $($session.humanAccessToken)" }

$coord = "$ApiBase/tenants/$($session.tenantId)/projects/$($session.projectId)/coordination"
$mcp = "$ApiBase/tenants/$($session.tenantId)/projects/$($session.projectId)/mcp/rpc"
$sandbox = $session.sandboxPath

# Register runtimes
$runtimeA = Invoke-Json -Method Post -Url "$coord/runtimes" -Headers $HA -Body @{
  agentPrincipalId = $session.agentA.agentId
  runtimeClass = "cursor-cli-a"
  protocolVersion = "1.0"
  capabilities = @("code.write")
  attestationRef = "attest-pilot-a"
}
$runtimeB = Invoke-Json -Method Post -Url "$coord/runtimes" -Headers $HB -Body @{
  agentPrincipalId = $session.agentB.agentId
  runtimeClass = "cursor-cli-b"
  protocolVersion = "1.0"
  capabilities = @("code.write")
  attestationRef = "attest-pilot-b"
}
Write-Host "Runtimes: A=$($runtimeA.id) B=$($runtimeB.id)"

# Exclusive lease: A takes TB-1 scope; B conflicts then takes TB-2
$leaseA = Invoke-Json -Method Post -Url "$coord/leases" -Headers $HA -Body @{
  workPackageId = $session.packageTb1Id
  runtimeId = $runtimeA.id
  resourceScope = "paths:/src/api/create-task"
  expiresAt = (Get-Date).ToUniversalTime().AddHours(2).ToString("o")
  expectedFence = 0
  idempotencyKey = "pilot-lease-tb1"
}
try {
  $null = Invoke-Json -Method Post -Url "$coord/leases" -Headers $HB -Body @{
    workPackageId = $session.packageTb1Id
    runtimeId = $runtimeB.id
    resourceScope = "paths:/src/api/create-task"
    expiresAt = (Get-Date).ToUniversalTime().AddHours(2).ToString("o")
    expectedFence = 0
    idempotencyKey = "pilot-lease-tb1-conflict"
  }
  throw "Expected 409 Conflict when B claims TB-1 scope"
} catch {
  if ($_.Exception.Message -notmatch "409|Conflict") { throw }
  Write-Host "Lease conflict confirmed (B cannot claim TB-1 scope)" -ForegroundColor Green
}

# MCP: A posts status; simulate disconnect; B replays from cursor 0
$HA["X-Runtime-Id"] = "$($runtimeA.id)"
$null = Invoke-Json -Method Post -Url $mcp -Headers $HA -Body @{
  jsonrpc = "2.0"; id = 1; method = "initialize"; params = $null
}
$null = Invoke-Json -Method Post -Url $mcp -Headers $HA -Body @{
  jsonrpc = "2.0"; id = 2; method = "tools/call"
  params = @{
    name = "send_project_message"
    arguments = @{
      channelId = $session.channelId
      messageType = "Status"
      payload = @{ note = "TB-1 create-task branch ready"; package = "TB-1" }
      idempotencyKey = "pilot-msg-tb1-ready"
    }
  }
}

# --- Agent A implements create-task in sandbox ---
Push-Location $sandbox
try {
  git checkout main | Out-Null
  git checkout -B agent-a/tb1-create-task
  $createTask = @'
namespace TaskBoard.Api;

public sealed record CreateTaskRequest(string Title);
public sealed record TaskResponse(Guid Id, string Title, string Status, DateTimeOffset CreatedAt);

public static class TaskStore
{
    private static readonly List<TaskResponse> Items = new();
    private static readonly object Gate = new();

    public static TaskResponse Create(CreateTaskRequest request)
    {
        var item = new TaskResponse(Guid.NewGuid(), request.Title, "Open", DateTimeOffset.UtcNow);
        lock (Gate) { Items.Add(item); }
        return item;
    }

    public static IReadOnlyList<TaskResponse> List()
    {
        lock (Gate) { return Items.ToList(); }
    }
}

public static class CreateTaskEndpoint
{
    public static TaskResponse Handle(CreateTaskRequest request) => TaskStore.Create(request);
}
'@
  Set-Content -Path "src\api\CreateTask.cs" -Value $createTask -Encoding utf8
  git add src/api/CreateTask.cs
  git commit -m "feat(TB-1): add create-task API"
  $shaA = (git rev-parse HEAD).Trim()
  # Local PR-shaped ref (GitHub push optional; DevWeave stores refs only)
  $prA = "github:pr/1"
} finally { Pop-Location }

$changeA = Invoke-Json -Method Post -Url "$coord/change-sets" -Headers $HA -Body @{
  workPackageId = $session.packageTb1Id
  codebaseTargetId = $session.codebaseTargetId
  providerRef = $prA
  commitSha = $shaA
  changedPathMetadata = @("src/api/CreateTask.cs")
}
$null = Invoke-Json -Method Post -Url "$coord/leases/$($leaseA.id)/renew" -Headers $HA -Body @{
  expectedFence = $leaseA.fence
  expiresAt = (Get-Date).ToUniversalTime().AddHours(3).ToString("o")
}

# Evidence reference for TB-1 (hash only, not source)
$claimA = Invoke-Json -Method Post -Url "$ApiBase/tenants/$($session.tenantId)/projects/$($session.projectId)/activities" -Headers $HH -Body @{
  type = "Claim"
  actor = $session.ownerId
  purpose = "TB-1 create-task verified by unit summary"
  status = "Completed"
  startedAt = (Get-Date).ToUniversalTime().ToString("o")
  endedAt = (Get-Date).ToUniversalTime().ToString("o")
  correlationId = $null
  reason = $null
  reference = $null
  inputs = @()
  outputs = @(@{ itemId = $session.knowledgeItemId; revisionId = $session.revisionId; position = 0 })
}
$evidenceA = Invoke-Json -Method Post -Url "$ApiBase/tenants/$($session.tenantId)/claims/$($claimA.id)/evidence" -Headers $HH -Body @{
  type = "test-summary"
  integrityHash = "sha256:tb1-create-task-pilot"
  relatedArtifactId = $null
  relatedCriterion = "AC-TB-1"
}
$null = Invoke-Json -Method Post -Url "$ApiBase/tenants/$($session.tenantId)/evidence/$($evidenceA.id)/review" -Headers $HH -Body @{
  state = "Accepted"
  reviewer = $session.ownerId
}

# Agent B recovers via MCP cursor after "disconnect", then leases TB-2
$HB["X-Runtime-Id"] = "$($runtimeB.id)"
$replay = Invoke-Json -Method Post -Url $mcp -Headers $HB -Body @{
  jsonrpc = "2.0"; id = 3; method = "tools/call"
  params = @{
    name = "read_messages_after_cursor"
    arguments = @{ channelId = $session.channelId; afterSequence = 0; limit = 10 }
  }
}
$replayText = ($replay | ConvertTo-Json -Depth 10 -Compress)
if ($replayText -notmatch "TB-1 create-task branch ready") {
  throw "MCP cursor replay did not return Agent A status message"
}
Write-Host "MCP cursor recovery confirmed" -ForegroundColor Green

$leaseB = Invoke-Json -Method Post -Url "$coord/leases" -Headers $HB -Body @{
  workPackageId = $session.packageTb2Id
  runtimeId = $runtimeB.id
  resourceScope = "paths:/src/api/list-tasks"
  expiresAt = (Get-Date).ToUniversalTime().AddHours(2).ToString("o")
  expectedFence = 0
  idempotencyKey = "pilot-lease-tb2"
}

Push-Location $sandbox
try {
  git checkout main | Out-Null
  git checkout -B agent-b/tb2-list-tasks
  # B depends on A's create-task store; cherry-pick or recreate list endpoint referencing TaskStore
  git checkout agent-a/tb1-create-task -- src/api/CreateTask.cs
  $listTask = @'
namespace TaskBoard.Api;

public static class ListTasksEndpoint
{
    public static IReadOnlyList<TaskResponse> Handle() => TaskStore.List();
}
'@
  Set-Content -Path "src\api\ListTasks.cs" -Value $listTask -Encoding utf8
  git add src/api/CreateTask.cs src/api/ListTasks.cs
  git commit -m "feat(TB-2): add list-tasks API"
  $shaB = (git rev-parse HEAD).Trim()
  $prB = "github:pr/2"
} finally { Pop-Location }

$changeB = Invoke-Json -Method Post -Url "$coord/change-sets" -Headers $HB -Body @{
  workPackageId = $session.packageTb2Id
  codebaseTargetId = $session.codebaseTargetId
  providerRef = $prB
  commitSha = $shaB
  changedPathMetadata = @("src/api/ListTasks.cs")
}

$claimB = Invoke-Json -Method Post -Url "$ApiBase/tenants/$($session.tenantId)/projects/$($session.projectId)/activities" -Headers $HH -Body @{
  type = "Claim"
  actor = $session.ownerId
  purpose = "TB-2 list-tasks verified by unit summary"
  status = "Completed"
  startedAt = (Get-Date).ToUniversalTime().ToString("o")
  endedAt = (Get-Date).ToUniversalTime().ToString("o")
  correlationId = $null
  reason = $null
  reference = $null
  inputs = @()
  outputs = @(@{ itemId = $session.knowledgeItemId; revisionId = $session.revisionId; position = 0 })
}
$evidenceB = Invoke-Json -Method Post -Url "$ApiBase/tenants/$($session.tenantId)/claims/$($claimB.id)/evidence" -Headers $HH -Body @{
  type = "test-summary"
  integrityHash = "sha256:tb2-list-tasks-pilot"
  relatedArtifactId = $null
  relatedCriterion = "AC-TB-2"
}
$null = Invoke-Json -Method Post -Url "$ApiBase/tenants/$($session.tenantId)/evidence/$($evidenceB.id)/review" -Headers $HH -Body @{
  state = "Accepted"
  reviewer = $session.ownerId
}

# Owner: approve/verify knowledge, report work, integration queue, project report
$knowledgeItem = Invoke-Json -Method Get -Url "$ApiBase/tenants/$($session.tenantId)/knowledge/$($session.knowledgeItemId)" -Headers $HH
$null = Invoke-Json -Method Post -Url "$ApiBase/tenants/$($session.tenantId)/knowledge/$($session.knowledgeItemId)/approve" -Headers $HH -Body @{
  revisionId = $knowledgeItem.currentRevisionId
  comment = "Approved during two-agent pilot"
}
$null = Invoke-Json -Method Post -Url "$ApiBase/tenants/$($session.tenantId)/knowledge/$($session.knowledgeItemId)/verify" -Headers $HH

$wi1 = Invoke-Json -Method Get -Url "$ApiBase/tenants/$($session.tenantId)/projects/$($session.projectId)/work-items" -Headers $HH
$tb1 = @($wi1) | Where-Object { $_.key -eq "TB-1" } | Select-Object -First 1
$tb2 = @($wi1) | Where-Object { $_.key -eq "TB-2" } | Select-Object -First 1
$null = Invoke-Json -Method Post -Url "$ApiBase/tenants/$($session.tenantId)/projects/$($session.projectId)/work-items/$($tb1.id)/report" -Headers $HH -Body @{
  state = "Completed"; reason = "create-task landed"; expectedVersion = $tb1.version
}
$null = Invoke-Json -Method Post -Url "$ApiBase/tenants/$($session.tenantId)/projects/$($session.projectId)/work-items/$($tb2.id)/report" -Headers $HH -Body @{
  state = "Completed"; reason = "list-tasks landed"; expectedVersion = $tb2.version
}

$queueA = Invoke-Json -Method Post -Url "$coord/integration-queue" -Headers $HH -Body @{
  changeSetId = $changeA.id
  idempotencyKey = "pilot-queue-tb1"
}
$queueB = Invoke-Json -Method Post -Url "$coord/integration-queue" -Headers $HH -Body @{
  changeSetId = $changeB.id
  idempotencyKey = "pilot-queue-tb2"
}
$null = Invoke-Json -Method Post -Url "$coord/integration-queue/$($queueA.id)/decide" -Headers $HH -Body @{ expectedFence = 1 }
$null = Invoke-Json -Method Post -Url "$coord/integration-queue/$($queueB.id)/decide" -Headers $HH -Body @{ expectedFence = 1 }

$report = Invoke-Json -Method Post -Url "$ApiBase/tenants/$($session.tenantId)/projects/$($session.projectId)/reports" -Headers $HH -Body @{
  declaredPurpose = "Pilot readout for TaskBoard Lite"
}

$result = [ordered]@{
  runtimeAId = $runtimeA.id
  runtimeBId = $runtimeB.id
  leaseAId = $leaseA.id
  leaseBId = $leaseB.id
  changeSetA = @{ id = $changeA.id; providerRef = $prA; commitSha = $shaA }
  changeSetB = @{ id = $changeB.id; providerRef = $prB; commitSha = $shaB }
  evidenceAId = $evidenceA.id
  evidenceBId = $evidenceB.id
  mcpReplayOk = $true
  leaseConflictOk = $true
  reportId = $report.id
  completedAt = (Get-Date).ToUniversalTime().ToString("o")
}
$out = Join-Path $repoRoot "docs\pilot\two-agents-result.json"
$result | ConvertTo-Json -Depth 6 | Set-Content $out -Encoding utf8
Write-Host "Wrote $out" -ForegroundColor Green
$result | ConvertTo-Json -Depth 6
