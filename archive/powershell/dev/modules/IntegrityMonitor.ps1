# ================================================================
#  INTEGRITY MONITOR – Bit-Rot-Erkennung (MF-24)
#  Dependencies: Dat.ps1 (Hash-Cache), ParallelHashing.ps1
# ================================================================

function New-IntegrityBaseline {
  <#
  .SYNOPSIS
    Erstellt eine Hash-Baseline fuer eine Sammlung von Dateien.
  .PARAMETER Files
    Array von Dateipfaden.
  .PARAMETER Algorithm
    Hash-Algorithmus.
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Files,
    [ValidateSet('SHA1','SHA256','MD5')][string]$Algorithm = 'SHA256'
  )

  if (-not $Files -or $Files.Count -eq 0) {
    return @{ Entries = @{}; Created = (Get-Date).ToString('o'); Algorithm = $Algorithm; Count = 0 }
  }

  $entries = @{}
  foreach ($file in $Files) {
    if (-not (Test-Path $file)) { continue }

    try {
      $hash = (Get-FileHash -Path $file -Algorithm $Algorithm).Hash
      $fileInfo = Get-Item $file
      $entries[$file] = @{
        Hash         = $hash
        Size         = $fileInfo.Length
        LastModified = $fileInfo.LastWriteTime.ToString('o')
        RecordedAt   = (Get-Date).ToString('o')
      }
    } catch {
      $entries[$file] = @{ Hash = $null; Error = $_.Exception.Message }
    }
  }

  return @{
    Entries   = $entries
    Created   = (Get-Date).ToString('o')
    Algorithm = $Algorithm
    Count     = $entries.Count
  }
}

function Test-IntegrityAgainstBaseline {
  <#
  .SYNOPSIS
    Prueft aktuelle Dateien gegen eine gespeicherte Baseline.
  .PARAMETER Baseline
    Baseline-Objekt von New-IntegrityBaseline.
  .PARAMETER Algorithm
    Hash-Algorithmus (muss mit Baseline uebereinstimmen).
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Baseline,
    [ValidateSet('SHA1','SHA256','MD5')][string]$Algorithm = 'SHA256'
  )

  $changed = @()
  $missing = @()
  $intact = @()
  $errors = @()

  foreach ($path in $Baseline.Entries.Keys) {
    $entry = $Baseline.Entries[$path]

    if (-not (Test-Path $path)) {
      $missing += @{ Path = $path; OriginalHash = $entry.Hash }
      continue
    }

    try {
      $currentHash = (Get-FileHash -Path $path -Algorithm $Algorithm).Hash
      if ($currentHash -ne $entry.Hash) {
        $changed += @{
          Path         = $path
          OriginalHash = $entry.Hash
          CurrentHash  = $currentHash
          RecordedAt   = $entry.RecordedAt
        }
      } else {
        $intact += $path
      }
    } catch {
      $errors += @{ Path = $path; Error = $_.Exception.Message }
    }
  }

  return @{
    Changed  = $changed
    Missing  = $missing
    Intact   = $intact
    Errors   = $errors
    Summary  = @{
      Total       = $Baseline.Entries.Count
      Intact      = $intact.Count
      Changed     = $changed.Count
      Missing     = $missing.Count
      Errors      = $errors.Count
      BitRotRisk  = ($changed.Count -gt 0)
    }
    CheckedAt = (Get-Date).ToString('o')
  }
}

function Save-IntegrityBaseline {
  <#
  .SYNOPSIS
    Speichert eine Baseline als JSON.
  .PARAMETER Baseline
    Baseline-Objekt.
  .PARAMETER Path
    Speicherpfad.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Baseline,
    [Parameter(Mandatory)][string]$Path
  )

  $dir = [System.IO.Path]::GetDirectoryName($Path)
  if ($dir -and -not (Test-Path $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
  }

  $Baseline | ConvertTo-Json -Depth 5 | Set-Content -Path $Path -Encoding UTF8
  return @{ Status = 'Saved'; Path = $Path; Count = $Baseline.Count }
}

function Get-IntegrityReport {
  <#
  .SYNOPSIS
    Erstellt einen Integritaets-Bericht.
  .PARAMETER CheckResult
    Ergebnis von Test-IntegrityAgainstBaseline.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$CheckResult
  )

  $s = $CheckResult.Summary

  return @{
    Status   = if ($s.BitRotRisk) { 'Warning' } elseif ($s.Missing -gt 0) { 'Warning' } else { 'OK' }
    Summary  = $s
    Changed  = $CheckResult.Changed
    Missing  = $CheckResult.Missing
    Message  = if ($s.BitRotRisk) {
      "$($s.Changed) Datei(en) haben sich unerwartet geaendert (moeglicher Bit-Rot)"
    } elseif ($s.Missing -gt 0) {
      "$($s.Missing) Datei(en) fehlen"
    } else {
      "Alle $($s.Total) Dateien intakt"
    }
  }
}
