# Point-in-time restore drill for a scratch DevWeave stack.
# This script does not run a restore and does not invent RPO/RTO.
# An operator records measured values in docs/ops/backup-restore-drill.md after a scratch run.

param(
    [string]$BackupPath = "devweave-pitr.dump",
    [string]$RecoveryTarget = ""
)

$ErrorActionPreference = "Stop"

Write-Host "Scratch PITR procedure. Do not point this at a customer volume."
Write-Host "1. Stop api, workers, and studio."
Write-Host "2. pg_dump -Fc the scratch database to $BackupPath."
Write-Host "3. Restore into an empty scratch database with pg_restore --clean --if-exists."
if ($RecoveryTarget) {
    Write-Host "4. Recovery target time requested: $RecoveryTarget. Enable WAL archiving before treating this as point-in-time."
} else {
    Write-Host "4. No recovery target was supplied. The current runbook drill is dump/restore, not WAL point-in-time."
}
Write-Host "5. Restore the object-storage volume taken at the same time as the dump."
Write-Host "6. Start the app profile and require GET /health/ready to return ready."
Write-Host "7. Record the measured RPO and RTO in docs/ops/backup-restore-drill.md."
Write-Host "Point-in-time recovery is not enabled until WAL archiving and a timed drill are recorded."
exit 2
