#  CLOUD SETTINGS SYNC (LF-17)
#  Sammlungs-Metadaten (nicht ROMs!) via OneDrive/Dropbox synchronisieren.

function New-CloudSyncConfig {
  <#
  .SYNOPSIS
    Erstellt eine Cloud-Sync-Konfiguration.
  #>
  param(
    [Parameter(Mandatory)][ValidateSet('OneDrive','Dropbox','GoogleDrive','Custom')]
    [string]$Provider,
    [Parameter(Mandatory)][string]$SyncPath,
    [switch]$AutoSync,
    [int]$SyncIntervalMinutes = 60
  )

  return @{
    Provider            = $Provider
    SyncPath            = $SyncPath
    AutoSync            = [bool]$AutoSync
    SyncIntervalMinutes = $SyncIntervalMinutes
    LastSync            = $null
    SyncEnabled         = $true
  }
}

function Get-CloudSyncPath {
  <#
  .SYNOPSIS
    Erkennt den Standard-Cloud-Ordner fuer einen Provider.
  #>
  param(
    [Parameter(Mandatory)][ValidateSet('OneDrive','Dropbox','GoogleDrive')]
    [string]$Provider
  )

  switch ($Provider) {
    'OneDrive' {
      $path = Join-Path $env:USERPROFILE 'OneDrive'
      if (Test-Path $path) { return $path }
      # Fallback: Registry
      try {
        $regPath = 'HKCU:\Software\Microsoft\OneDrive'
        if (Test-Path $regPath) {
          $val = (Get-ItemProperty $regPath -Name 'UserFolder' -ErrorAction SilentlyContinue).UserFolder
          if ($val -and (Test-Path $val)) { return $val }
        }
      } catch {}
      return ''
    }
    'Dropbox' {
      $path = Join-Path $env:USERPROFILE 'Dropbox'
      if (Test-Path $path) { return $path }
      return ''
    }
    'GoogleDrive' {
      $path = Join-Path $env:USERPROFILE 'Google Drive'
      if (Test-Path $path) { return $path }
      return ''
    }
  }
}

function New-SyncManifest {
  <#
  .SYNOPSIS
    Erstellt ein Sync-Manifest mit Metadaten zu synchronisierenden Dateien.
  #>
  param(
    [Parameter(Mandatory)][string]$MachineName,
    [Parameter(Mandatory)][array]$Files
  )

  $entries = [System.Collections.Generic.List[hashtable]]::new()
  foreach ($f in $Files) {
    $entries.Add(@{
      Name     = $f.Name
      Hash     = if ($f.ContainsKey('Hash')) { $f.Hash } else { '' }
      Modified = if ($f.ContainsKey('Modified')) { $f.Modified } else { (Get-Date).ToString('o') }
      Size     = if ($f.ContainsKey('Size')) { $f.Size } else { 0 }
    })
  }

  return @{
    MachineName = $MachineName
    Created     = (Get-Date).ToString('o')
    Files       = ,$entries.ToArray()
    Version     = '1.0'
  }
}

function Compare-SyncManifests {
  <#
  .SYNOPSIS
    Vergleicht zwei Sync-Manifeste und findet Unterschiede.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Local,
    [Parameter(Mandatory)][hashtable]$Remote
  )

  $localIndex = @{}
  foreach ($f in $Local.Files) { $localIndex[$f.Name] = $f }

  $remoteIndex = @{}
  foreach ($f in $Remote.Files) { $remoteIndex[$f.Name] = $f }

  $toUpload   = [System.Collections.Generic.List[string]]::new()
  $toDownload = [System.Collections.Generic.List[string]]::new()
  $conflicts  = [System.Collections.Generic.List[string]]::new()

  foreach ($name in $localIndex.Keys) {
    if (-not $remoteIndex.ContainsKey($name)) {
      $toUpload.Add($name)
    } elseif ($localIndex[$name].Hash -ne $remoteIndex[$name].Hash) {
      $conflicts.Add($name)
    }
  }

  foreach ($name in $remoteIndex.Keys) {
    if (-not $localIndex.ContainsKey($name)) {
      $toDownload.Add($name)
    }
  }

  return @{
    ToUpload   = ,$toUpload.ToArray()
    ToDownload = ,$toDownload.ToArray()
    Conflicts  = ,$conflicts.ToArray()
    InSync     = ($toUpload.Count -eq 0 -and $toDownload.Count -eq 0 -and $conflicts.Count -eq 0)
  }
}

function Get-SyncStatus {
  <#
  .SYNOPSIS
    Gibt den aktuellen Sync-Status zurueck.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Config
  )

  return @{
    Provider  = $Config.Provider
    Enabled   = $Config.SyncEnabled
    AutoSync  = $Config.AutoSync
    LastSync  = $Config.LastSync
    SyncPath  = $Config.SyncPath
    PathExists = (Test-Path -LiteralPath $Config.SyncPath -ErrorAction SilentlyContinue)
  }
}
