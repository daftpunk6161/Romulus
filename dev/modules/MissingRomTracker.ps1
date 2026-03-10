# ================================================================
#  MISSING ROM TRACKER – Fehlende Spiele laut DAT (MF-01)
#  Dependencies: Dat.ps1
# ================================================================

function Get-DatMissingGames {
  <#
  .SYNOPSIS
    Ermittelt welche Spiele laut DAT-Index fehlen.
  .PARAMETER DatIndex
    DAT-Index Hashtable (Hash → GameName).
  .PARAMETER FoundHashes
    Hashtable der gefundenen Datei-Hashes (Hash → $true).
  .PARAMETER FilterRegions
    Optionale Regions-Filter (nur Missing aus diesen Regionen).
  #>
  param(
    [Parameter(Mandatory)][hashtable]$DatIndex,
    [Parameter(Mandatory)][hashtable]$FoundHashes,
    [string[]]$FilterRegions
  )

  $missing = [System.Collections.Generic.List[hashtable]]::new()
  $regionPattern = '\(([A-Za-z, ]+)\)'

  foreach ($hash in $DatIndex.Keys) {
    if ($FoundHashes.ContainsKey($hash)) { continue }

    $entry = $DatIndex[$hash]
    $name = if ($entry -is [hashtable] -and $entry.ContainsKey('Name')) {
      $entry.Name
    } elseif ($entry -is [string]) {
      $entry
    } else {
      [string]$entry
    }

    # Region extrahieren
    $region = ''
    if ($name -match $regionPattern) {
      $region = $Matches[1].Trim()
    }

    # Optionaler Regions-Filter
    if ($FilterRegions -and $FilterRegions.Count -gt 0) {
      $matchesFilter = $false
      foreach ($fr in $FilterRegions) {
        if ($region -like "*$fr*") { $matchesFilter = $true; break }
      }
      if (-not $matchesFilter) { continue }
    }

    $size = if ($entry -is [hashtable] -and $entry.ContainsKey('Size')) { $entry.Size } else { 0 }
    $source = if ($entry -is [hashtable] -and $entry.ContainsKey('Source')) { $entry.Source } else { '' }

    $missing.Add(@{
      Name      = $name
      Hash      = $hash
      Region    = $region
      Size      = $size
      DatSource = $source
    })
  }

  # Sortieren nach Name
  $sorted = $missing | Sort-Object { $_.Name }
  return ,@($sorted)
}

function Get-MissingReport {
  <#
  .SYNOPSIS
    Erstellt einen strukturierten Missing-Report pro Konsole.
  .PARAMETER DatIndex
    DAT-Index.
  .PARAMETER FoundHashes
    Gefundene Hashes.
  .PARAMETER ConsoleKey
    Konsolen-Schluessel.
  .PARAMETER FilterRegions
    Optionale Filter-Regionen.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$DatIndex,
    [Parameter(Mandatory)][hashtable]$FoundHashes,
    [string]$ConsoleKey = '',
    [string[]]$FilterRegions
  )

  $missing = Get-DatMissingGames -DatIndex $DatIndex -FoundHashes $FoundHashes -FilterRegions $FilterRegions

  $total = $DatIndex.Count
  $found = ($DatIndex.Keys | Where-Object { $FoundHashes.ContainsKey($_) }).Count
  $missingCount = $missing.Count
  $pct = if ($total -gt 0) { [math]::Round(($found / $total) * 100, 1) } else { 0 }

  return @{
    ConsoleKey   = $ConsoleKey
    TotalInDat   = $total
    Found        = $found
    Missing      = $missingCount
    Completeness = $pct
    MissingGames = $missing
  }
}
