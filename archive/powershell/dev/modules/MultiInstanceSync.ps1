# ================================================================
#  MULTI-INSTANCE COORDINATION (XL-13)
#  Mehrere RomCleanup-Instanzen synchron halten
# ================================================================

function New-InstanceIdentity {
  <#
  .SYNOPSIS
    Erstellt eine eindeutige Instanz-Identitaet.
  .PARAMETER MachineName
    Rechnername.
  .PARAMETER InstanceName
    Optionaler Instanzname.
  #>
  param(
    [string]$MachineName = $env:COMPUTERNAME,
    [string]$InstanceName = 'default'
  )

  $id = [guid]::NewGuid().ToString('N').Substring(0, 12)

  return @{
    InstanceId   = $id
    MachineName  = $MachineName
    InstanceName = $InstanceName
    CreatedAt    = [datetime]::UtcNow.ToString('o')
    LastSeen     = [datetime]::UtcNow.ToString('o')
    Status       = 'Active'
  }
}

function New-SyncManifest {
  <#
  .SYNOPSIS
    Erstellt ein Synchronisations-Manifest.
  .PARAMETER Identity
    Instanz-Identitaet.
  .PARAMETER SyncRoot
    Gemeinsamer Sync-Ordner.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Identity,
    [Parameter(Mandatory)][string]$SyncRoot
  )

  return @{
    InstanceId  = $Identity.InstanceId
    MachineName = $Identity.MachineName
    SyncRoot    = $SyncRoot
    Version     = 1
    Timestamp   = [datetime]::UtcNow.ToString('o')
    Operations  = @()
    Checksum    = ''
  }
}

function Add-SyncOperation {
  <#
  .SYNOPSIS
    Fuegt eine Sync-Operation zum Manifest hinzu.
  .PARAMETER Manifest
    Sync-Manifest.
  .PARAMETER OperationType
    Typ der Operation.
  .PARAMETER SourcePath
    Quellpfad (relativ).
  .PARAMETER TargetPath
    Zielpfad (relativ).
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Manifest,
    [Parameter(Mandatory)][ValidateSet('Move','Copy','Delete','Rename','SettingsChange')][string]$OperationType,
    [string]$SourcePath = '',
    [string]$TargetPath = ''
  )

  $op = @{
    OperationType = $OperationType
    SourcePath    = $SourcePath
    TargetPath    = $TargetPath
    Timestamp     = [datetime]::UtcNow.ToString('o')
    Status        = 'Pending'
    InstanceId    = $Manifest.InstanceId
  }

  $Manifest.Operations += $op
  $Manifest.Version++
  $Manifest.Timestamp = [datetime]::UtcNow.ToString('o')

  return $op
}

function Test-SyncConflict {
  <#
  .SYNOPSIS
    Prueft ob Sync-Konflikte zwischen zwei Manifesten bestehen.
  .PARAMETER LocalManifest
    Lokales Manifest.
  .PARAMETER RemoteManifest
    Remote-Manifest.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$LocalManifest,
    [Parameter(Mandatory)][hashtable]$RemoteManifest
  )

  $conflicts = @()

  $localFiles = @{}
  foreach ($op in $LocalManifest.Operations) {
    if ($op.SourcePath) { $localFiles[$op.SourcePath] = $op }
  }

  foreach ($op in $RemoteManifest.Operations) {
    if ($op.SourcePath -and $localFiles.ContainsKey($op.SourcePath)) {
      $localOp = $localFiles[$op.SourcePath]
      if ($localOp.OperationType -ne $op.OperationType -or $localOp.TargetPath -ne $op.TargetPath) {
        $conflicts += @{
          FilePath     = $op.SourcePath
          LocalAction  = $localOp.OperationType
          RemoteAction = $op.OperationType
          LocalTarget  = $localOp.TargetPath
          RemoteTarget = $op.TargetPath
        }
      }
    }
  }

  return @{
    HasConflicts  = ($conflicts.Count -gt 0)
    Conflicts     = $conflicts
    ConflictCount = $conflicts.Count
  }
}

function Merge-SyncManifests {
  <#
  .SYNOPSIS
    Fuehrt zwei Sync-Manifeste zusammen (ohne Konflikte).
  .PARAMETER LocalManifest
    Lokales Manifest.
  .PARAMETER RemoteManifest
    Remote-Manifest.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$LocalManifest,
    [Parameter(Mandatory)][hashtable]$RemoteManifest
  )

  $conflictCheck = Test-SyncConflict -LocalManifest $LocalManifest -RemoteManifest $RemoteManifest

  if ($conflictCheck.HasConflicts) {
    return @{
      Merged     = $false
      Reason     = 'Konflikte vorhanden'
      Conflicts  = $conflictCheck.Conflicts
    }
  }

  # Merge Remote-Ops die lokal nicht existieren
  $localPaths = @{}
  foreach ($op in $LocalManifest.Operations) {
    if ($op.SourcePath) { $localPaths[$op.SourcePath] = $true }
  }

  $newOps = @()
  foreach ($op in $RemoteManifest.Operations) {
    if ($op.SourcePath -and -not $localPaths.ContainsKey($op.SourcePath)) {
      $newOps += $op
    }
  }

  return @{
    Merged        = $true
    NewOperations = $newOps
    MergedCount   = $newOps.Count
  }
}

function Get-MultiInstanceStatistics {
  <#
  .SYNOPSIS
    Gibt Statistiken ueber die Multi-Instanz-Koordination zurueck.
  .PARAMETER Manifests
    Array von Sync-Manifesten.
  #>
  param(
    [Parameter(Mandatory)][array]$Manifests
  )

  $totalOps = 0
  $instances = @{}
  foreach ($m in $Manifests) {
    $totalOps += @($m.Operations).Count
    $instances[$m.InstanceId] = $m.MachineName
  }

  return @{
    ManifestCount   = @($Manifests).Count
    TotalOperations = $totalOps
    UniqueInstances = $instances.Count
    Instances       = $instances
  }
}
