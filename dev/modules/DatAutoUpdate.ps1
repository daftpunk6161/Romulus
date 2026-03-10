# ================================================================
#  DAT AUTO UPDATE – Automatischer DAT-Versions-Check (MF-11)
#  Dependencies: DatSources.ps1, Dat.ps1
# ================================================================

function Test-DatUpdateAvailable {
  <#
  .SYNOPSIS
    Prueft ob eine neuere DAT-Version verfuegbar ist.
  .PARAMETER DatSource
    DAT-Quellen-Definition aus dat-catalog.json.
  .PARAMETER CurrentVersion
    Aktuell installierte Version (Datum-String oder Versions-Nr).
  #>
  param(
    [Parameter(Mandatory)][hashtable]$DatSource,
    [string]$CurrentVersion
  )

  if (-not $DatSource.ContainsKey('name')) {
    return @{ UpdateAvailable = $false; Reason = 'InvalidSource' }
  }

  if (-not $CurrentVersion) {
    return @{ UpdateAvailable = $true; Reason = 'NoCurrentVersion'; SourceName = $DatSource.name }
  }

  # Vergleich: wenn remoteVersion neuer als CurrentVersion
  $remoteVersion = $DatSource['latestVersion']
  if (-not $remoteVersion) {
    return @{ UpdateAvailable = $false; Reason = 'NoRemoteVersion'; SourceName = $DatSource.name }
  }

  $isNewer = $remoteVersion -ne $CurrentVersion
  return @{
    UpdateAvailable = $isNewer
    CurrentVersion  = $CurrentVersion
    RemoteVersion   = $remoteVersion
    SourceName      = $DatSource.name
    Reason          = if ($isNewer) { 'NewVersionAvailable' } else { 'UpToDate' }
  }
}

function Get-DatUpdateCheckResult {
  <#
  .SYNOPSIS
    Prueft mehrere DAT-Quellen auf Updates.
  .PARAMETER DatSources
    Array von DAT-Quellen-Definitionen.
  .PARAMETER InstalledVersions
    Hashtable: SourceName → InstalledVersion.
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$DatSources,
    [hashtable]$InstalledVersions
  )

  if (-not $DatSources -or $DatSources.Count -eq 0) {
    return @{ TotalSources = 0; UpdatesAvailable = 0; Results = @() }
  }

  $results = @()
  $updatesCount = 0

  foreach ($source in $DatSources) {
    $sourceName = $source.name
    $currentVer = if ($InstalledVersions -and $InstalledVersions.ContainsKey($sourceName)) { $InstalledVersions[$sourceName] } else { $null }

    $check = Test-DatUpdateAvailable -DatSource $source -CurrentVersion $currentVer
    if ($check.UpdateAvailable) { $updatesCount++ }
    $results += $check
  }

  return @{
    TotalSources     = $DatSources.Count
    UpdatesAvailable = $updatesCount
    Results          = $results
    CheckedAt        = (Get-Date).ToString('o')
  }
}

function New-DatUpdatePlan {
  <#
  .SYNOPSIS
    Erstellt einen Download-Plan fuer verfuegbare DAT-Updates.
  .PARAMETER CheckResult
    Ergebnis von Get-DatUpdateCheckResult.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$CheckResult
  )

  $pending = @($CheckResult.Results | Where-Object { $_.UpdateAvailable })

  return @{
    TotalUpdates = $pending.Count
    Downloads    = $pending
    Status       = if ($pending.Count -gt 0) { 'PendingDownload' } else { 'UpToDate' }
  }
}
