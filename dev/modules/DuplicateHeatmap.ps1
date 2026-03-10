# ================================================================
#  DUPLICATE HEATMAP – Duplikat-Verteilung nach Konsole (QW-09)
#  Dependencies: (standalone, pure logic)
# ================================================================

function Get-DuplicateHeatmapData {
  <#
  .SYNOPSIS
    Aggregiert Duplikat-Zahlen pro Konsole fuer Heatmap-Visualisierung.
  .PARAMETER DedupeResults
    Array von Dedupe-Ergebnis-Eintraegen.
    Erwartet Objekte mit mindestens: Console/ConsoleType, Action (KEEP/MOVE/JUNK).
  #>
  param(
    [Parameter(Mandatory)][object[]]$DedupeResults
  )

  $consoleStats = [hashtable]::new([StringComparer]::OrdinalIgnoreCase)
  $totalDupes = 0

  foreach ($entry in $DedupeResults) {
    $console = $null
    $action = $null

    if ($entry -is [hashtable]) {
      $console = if ($entry.ContainsKey('Console')) { $entry.Console }
                 elseif ($entry.ContainsKey('ConsoleType')) { $entry.ConsoleType }
                 else { 'Unknown' }
      $action = if ($entry.ContainsKey('Action')) { $entry.Action } else { '' }
    } else {
      $console = if ($entry.PSObject.Properties.Name -contains 'Console') { $entry.Console }
                 elseif ($entry.PSObject.Properties.Name -contains 'ConsoleType') { $entry.ConsoleType }
                 else { 'Unknown' }
      $action = if ($entry.PSObject.Properties.Name -contains 'Action') { $entry.Action } else { '' }
    }

    if ([string]::IsNullOrWhiteSpace($console)) { $console = 'Unknown' }

    if (-not $consoleStats.ContainsKey($console)) {
      $consoleStats[$console] = @{ Total = 0; Duplicates = 0 }
    }

    $consoleStats[$console].Total++

    # MOVE = Duplikat (nicht der Winner)
    if ($action -eq 'MOVE') {
      $consoleStats[$console].Duplicates++
      $totalDupes++
    }
  }

  # In sortiertes Array umwandeln (meiste Duplikate oben)
  $heatmapData = [System.Collections.Generic.List[hashtable]]::new()

  foreach ($key in $consoleStats.Keys) {
    $stats = $consoleStats[$key]
    $pct = if ($stats.Total -gt 0) {
      [math]::Round(($stats.Duplicates / $stats.Total) * 100, 1)
    } else { 0.0 }

    [void]$heatmapData.Add(@{
      Console    = $key
      Total      = $stats.Total
      Duplicates = $stats.Duplicates
      Percent    = $pct
    })
  }

  $sorted = @($heatmapData | Sort-Object { $_.Duplicates } -Descending)

  return @{
    Data       = $sorted
    TotalDupes = $totalDupes
    ConsoleCount = $consoleStats.Count
  }
}
