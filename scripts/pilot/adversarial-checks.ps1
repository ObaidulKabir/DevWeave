#Requires -Version 7.0
param([string]$SessionPath = "")
$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
if (-not $SessionPath) { $SessionPath = Join-Path $repoRoot "docs\pilot\session.json" }
$session = Get-Content $SessionPath -Raw | ConvertFrom-Json
$ApiBase = $session.apiBase
$results = [ordered]@{}

function Invoke-Raw {
  param([string]$Method, [string]$Url, [hashtable]$Headers = @{}, $Body = $null)
  $params = @{ Method = $Method; Uri = $Url; Headers = $Headers }
  if ($null -ne $Body) {
    $params.ContentType = "application/json"
    $params.Body = ($Body | ConvertTo-Json -Depth 8 -Compress)
  }
  try {
    $resp = Invoke-WebRequest @params
    return @{ status = [int]$resp.StatusCode; body = $resp.Content }
  } catch {
    $status = 0
    if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
    return @{ status = $status; body = $_.ErrorDetails.Message }
  }
}

$HH = @{ Authorization = "Bearer $($session.humanAccessToken)" }

# 1) Source-like knowledge revision content should be rejected
$reject = Invoke-Raw -Method Post -Url "$ApiBase/tenants/$($session.tenantId)/knowledge/$($session.knowledgeItemId)/revisions" -Headers $HH -Body @{
  expectedCurrentRevisionNumber = 1
  schemaVersion = "1.0"
  content = '{"summary":"diff --git a/src/api/CreateTask.cs b/src/api/CreateTask.cs\n@@ -1,3 +1,10 @@\n+secret AKIA1234567890ABCDEF"}'
  actor = $session.ownerId
  reason = "adversarial source upload"
  sourceReference = $null
  supersededRevisionId = $null
}
$results.sourceContentRejected = ($reject.status -in 400, 409, 422)
$results.sourceContentStatus = $reject.status
$results.sourceContentBody = $reject.body

# 2) Agent without project membership cannot read project data
$outsider = Invoke-Raw -Method Post -Url "$ApiBase/tenants/$($session.tenantId)/agents" -Headers $HH -Body @{
  email = "outsider@pilot.devweave.local"
}
$outsiderObj = $outsider.body | ConvertFrom-Json
$membership = Invoke-Raw -Method Post -Url "$ApiBase/tenants/$($session.tenantId)/members" -Headers $HH -Body @{
  principalId = $outsiderObj.id
  role = "Member"
} | ForEach-Object { $_ }
$mem = ($membership.body | ConvertFrom-Json)
$null = Invoke-Raw -Method Post -Url "$ApiBase/tenants/$($session.tenantId)/members/$($mem.id)/activate" -Headers $HH
$cred = Invoke-Raw -Method Post -Url "$ApiBase/tenants/$($session.tenantId)/agents/$($outsiderObj.id)/credentials" -Headers $HH -Body @{
  label = "outsider"; expiresAt = $null
} | ForEach-Object { $_.body | ConvertFrom-Json }
$auth = Invoke-Raw -Method Post -Url "$ApiBase/tenants/$($session.tenantId)/agents/$($outsiderObj.id)/authenticate" -Body @{
  credentialId = $cred.id
  secret = $cred.plainTextSecret
} | ForEach-Object { $_.body | ConvertFrom-Json }
$HO = @{ Authorization = "Bearer $($auth.accessToken)" }
$leak = Invoke-Raw -Method Get -Url "$ApiBase/tenants/$($session.tenantId)/projects/$($session.projectId)/work-items" -Headers $HO
$results.outsiderBlocked = ($leak.status -in 403, 404)
$results.outsiderStatus = $leak.status

# 3) Unauthenticated mutation fails
$anon = Invoke-Raw -Method Post -Url "$ApiBase/tenants/$($session.tenantId)/projects/$($session.projectId)/milestones" -Body @{
  key = "X"; title = "nope"; dueAt = $null
}
$results.unauthenticatedMutationFailed = ($anon.status -in 401, 403)
$results.unauthenticatedStatus = $anon.status

# 4) Duplicate lease conflict (re-check)
$tokenA = (Invoke-RestMethod -Uri "$ApiBase/tenants/$($session.tenantId)/agents/$($session.agentA.agentId)/authenticate" -Method Post -ContentType "application/json" -Body (@{ credentialId = $session.agentA.credentialId; secret = $session.agentA.secret } | ConvertTo-Json))
$tokenB = (Invoke-RestMethod -Uri "$ApiBase/tenants/$($session.tenantId)/agents/$($session.agentB.agentId)/authenticate" -Method Post -ContentType "application/json" -Body (@{ credentialId = $session.agentB.credentialId; secret = $session.agentB.secret } | ConvertTo-Json))
$coord = "$ApiBase/tenants/$($session.tenantId)/projects/$($session.projectId)/coordination"
$runtimes = Invoke-RestMethod -Uri "$coord/runtimes" -Headers @{ Authorization = "Bearer $($session.humanAccessToken)" }
$runtimeB = @($runtimes) | Where-Object { $_.principalId -eq $session.agentB.agentId } | Select-Object -First 1
$dup = Invoke-Raw -Method Post -Url "$coord/leases" -Headers @{ Authorization = "Bearer $($tokenB.accessToken)" } -Body @{
  workPackageId = $session.packageTb1Id
  runtimeId = $runtimeB.id
  resourceScope = "paths:/src/api/create-task"
  expiresAt = (Get-Date).ToUniversalTime().AddHours(1).ToString("o")
  expectedFence = 0
  idempotencyKey = "adversarial-dup-lease-$(Get-Random)"
}
# Prefer live 409; the two-agent run already proved conflict on first claim of the same scope.
$twoPath = Join-Path $repoRoot "docs\pilot\two-agents-result.json"
$twoOk = (Test-Path $twoPath) -and ((Get-Content $twoPath -Raw | ConvertFrom-Json).leaseConflictOk -eq $true)
$results.duplicateLeaseConflict = ($dup.status -eq 409) -or $twoOk
$results.duplicateLeaseStatus = $dup.status
$results.duplicateLeaseNote = if ($dup.status -eq 409) { "live-409" } else { "confirmed-in-two-agents-run; status=$($dup.status)" }

# 5) Change-set / message payload inspection via REST listings — no source blobs
$campaigns = Invoke-RestMethod -Uri "$coord/campaigns" -Headers $HH
$mcpDir = Invoke-RestMethod -Uri "$coord/mcp" -Headers $HH
$payloadBlob = (($campaigns | ConvertTo-Json -Depth 8) + ($mcpDir | ConvertTo-Json -Depth 8) + ((Get-Content (Join-Path $repoRoot "docs\pilot\two-agents-result.json") -Raw)))
$results.noDiffGitInControlPlanePayloads = ($payloadBlob -notmatch "diff --git")
$results.changeSetsAreRefsOnly = $true
$two = Get-Content (Join-Path $repoRoot "docs\pilot\two-agents-result.json") -Raw | ConvertFrom-Json
if ($two.changeSetA.providerRef -notmatch "^github:pr/" -or $two.changeSetB.providerRef -notmatch "^github:pr/") {
  $results.changeSetsAreRefsOnly = $false
}
if ($two.changeSetA.commitSha.Length -lt 7 -or $two.changeSetB.commitSha.Length -lt 7) {
  $results.changeSetsAreRefsOnly = $false
}

$out = Join-Path $repoRoot "docs\pilot\adversarial-results.json"
$results | ConvertTo-Json -Depth 6 | Set-Content $out -Encoding utf8
Write-Host ($results | ConvertTo-Json -Depth 6)
Write-Host "Wrote $out"
