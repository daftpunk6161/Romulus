# ================================================================
#  COMPLETENESS TRACKER – Sammlung-Completeness-Ziel (MF-04)
#  Dependencies: Dat.ps1
# ================================================================

function Get-CompletenessReport {
  <#
  .SYNOPSIS
    Berechnet den Completeness-Fortschritt fuer ein definiertes Ziel.
  .PARAMETER DatIndex
    DAT-Index Hashtable.
  .PARAMETER FoundHashes
    Hashtable der gefundenen Datei-Hashes.
  .PARAMETER Goal
    Ziel-Definition: @{ name; console; region; genre }.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$DatIndex,
    [Parameter(Mandatory)][hashtable]$FoundHashes,
    [Parameter(Mandatory)][hashtable]$Goal
  )

  $regionFilter = if ($Goal.region) { @($Goal.region) } else { @() }

  # Alle DAT-Eintraege, die zum Ziel passen
  $targetEntries = @{}
  foreach ($hash in $DatIndex.Keys) {
    $entry = $DatIndex[$hash]
    $name = if ($entry -is [hashtable] -and $entry.ContainsKey('Name')) { $entry.Name }
            elseif ($entry -is [string]) { $entry }
            else { [string]$entry }

    # Regions-Filter
    if ($regionFilter.Count -gt 0) {
      $matchesRegion = $false
      foreach ($r in $regionFilter) {
        if ($name -match [regex]::Escape($r)) { $matchesRegion = $true; break }
      }
      if (-not $matchesRegion) { continue }
    }

    $targetEntries[$hash] = $name
  }

  $total = $targetEntries.Count
  $found = 0
  $missing = [System.Collections.Generic.List[hashtable]]::new()

  foreach ($hash in $targetEntries.Keys) {
    if ($FoundHashes.ContainsKey($hash)) {
      $found++
    } else {
      $missing.Add(@{ Name = $targetEntries[$hash]; Hash = $hash })
    }
  }

  $pct = if ($total -gt 0) { [math]::Round(($found / $total) * 100, 1) } else { 100.0 }

  return @{
    GoalName     = $Goal.name
    Console      = $Goal.console
    Region       = $Goal.region
    Total        = $total
    Found        = $found
    Missing      = $missing.Count
    Completeness = $pct
    MissingGames = ,@($missing.ToArray())
  }
}

function Get-MultiGoalReport {
  <#
  .SYNOPSIS
    Berechnet Completeness fuer mehrere Ziele gleichzeitig.
  .PARAMETER DatIndex
    DAT-Index.
  .PARAMETER FoundHashes
    Gefundene Hashes.
  .PARAMETER Goals
    Array von Ziel-Definitionen.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$DatIndex,
    [Parameter(Mandatory)][hashtable]$FoundHashes,
    [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Goals
  )

  $reports = [System.Collections.Generic.List[hashtable]]::new()

  foreach ($goal in $Goals) {
    $report = Get-CompletenessReport -DatIndex $DatIndex -FoundHashes $FoundHashes -Goal $goal
    $reports.Add($report)
  }

  return ,@($reports.ToArray())
}
