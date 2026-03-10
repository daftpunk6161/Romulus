# ================================================================
#  HISTORICAL TREND ANALYSIS – Sammlungsgroesse/Qualitaet ueber Zeit (XL-06)
#  Interaktiver Graph nach jedem Run
# ================================================================

function New-TrendSnapshot {
  <#
  .SYNOPSIS
    Erstellt einen Trend-Snapshot der aktuellen Sammlung.
  .PARAMETER TotalFiles
    Gesamtanzahl der Dateien.
  .PARAMETER TotalSizeBytes
    Gesamtgroesse in Bytes.
  .PARAMETER VerifiedCount
    Anzahl verifizierter ROMs.
  .PARAMETER DuplicateCount
    Anzahl Duplikate.
  .PARAMETER JunkCount
    Anzahl Junk-Dateien.
  .PARAMETER ConsoleBreakdown
    Aufschluesselung nach Konsole.
  #>
  param(
    [Parameter(Mandatory)][int]$TotalFiles,
    [Parameter(Mandatory)][long]$TotalSizeBytes,
    [int]$VerifiedCount = 0,
    [int]$DuplicateCount = 0,
    [int]$JunkCount = 0,
    [hashtable]$ConsoleBreakdown = @{}
  )

  return @{
    Timestamp        = [datetime]::UtcNow.ToString('o')
    TotalFiles       = $TotalFiles
    TotalSizeBytes   = $TotalSizeBytes
    VerifiedCount    = $VerifiedCount
    DuplicateCount   = $DuplicateCount
    JunkCount        = $JunkCount
    ConsoleBreakdown = $ConsoleBreakdown
    QualityScore     = if ($TotalFiles -gt 0) { [math]::Round(($VerifiedCount / $TotalFiles) * 100, 1) } else { 0 }
  }
}

function Add-TrendSnapshot {
  <#
  .SYNOPSIS
    Fuegt einen Snapshot zur Trend-Historie hinzu.
  .PARAMETER History
    Bestehende Trend-Historie (Array).
  .PARAMETER Snapshot
    Neuer Snapshot.
  .PARAMETER MaxEntries
    Maximale Anzahl gespeicherter Snapshots.
  #>
  param(
    [array]$History = @(),
    [Parameter(Mandatory)][hashtable]$Snapshot,
    [int]$MaxEntries = 365
  )

  $newHistory = @($History) + @($Snapshot)

  if ($newHistory.Count -gt $MaxEntries) {
    $newHistory = $newHistory[($newHistory.Count - $MaxEntries)..($newHistory.Count - 1)]
  }

  return ,$newHistory
}

function Get-TrendDelta {
  <#
  .SYNOPSIS
    Berechnet die Veraenderung zwischen zwei Snapshots.
  .PARAMETER OldSnapshot
    Aelterer Snapshot.
  .PARAMETER NewSnapshot
    Neuerer Snapshot.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$OldSnapshot,
    [Parameter(Mandatory)][hashtable]$NewSnapshot
  )

  $fileDelta = $NewSnapshot.TotalFiles - $OldSnapshot.TotalFiles
  $sizeDelta = $NewSnapshot.TotalSizeBytes - $OldSnapshot.TotalSizeBytes
  $qualityDelta = $NewSnapshot.QualityScore - $OldSnapshot.QualityScore

  return @{
    FileDelta     = $fileDelta
    SizeDelta     = $sizeDelta
    QualityDelta  = [math]::Round($qualityDelta, 1)
    FilesTrend    = if ($fileDelta -gt 0) { 'up' } elseif ($fileDelta -lt 0) { 'down' } else { 'stable' }
    SizeTrend     = if ($sizeDelta -gt 0) { 'up' } elseif ($sizeDelta -lt 0) { 'down' } else { 'stable' }
    QualityTrend  = if ($qualityDelta -gt 0) { 'up' } elseif ($qualityDelta -lt 0) { 'down' } else { 'stable' }
  }
}

function Get-TrendChartData {
  <#
  .SYNOPSIS
    Bereitet Trend-Daten fuer Chart-Rendering vor.
  .PARAMETER History
    Trend-Historie (Array von Snapshots).
  .PARAMETER Metric
    Zu visualisierende Metrik.
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyCollection()][array]$History,
    [ValidateSet('TotalFiles','TotalSizeBytes','QualityScore','VerifiedCount','DuplicateCount','JunkCount')]
    [string]$Metric = 'TotalFiles'
  )

  $dataPoints = @()
  foreach ($snap in $History) {
    $dataPoints += @{
      X     = $snap.Timestamp
      Y     = $snap[$Metric]
      Label = "$Metric`: $($snap[$Metric])"
    }
  }

  return @{
    Metric     = $Metric
    DataPoints = $dataPoints
    PointCount = $dataPoints.Count
    Min        = if ($dataPoints.Count -gt 0) { ($dataPoints | ForEach-Object { $_.Y } | Measure-Object -Minimum).Minimum } else { 0 }
    Max        = if ($dataPoints.Count -gt 0) { ($dataPoints | ForEach-Object { $_.Y } | Measure-Object -Maximum).Maximum } else { 0 }
  }
}

function Get-TrendStatistics {
  <#
  .SYNOPSIS
    Gibt zusammenfassende Statistiken ueber die Trend-Historie zurueck.
  .PARAMETER History
    Trend-Historie (Array von Snapshots).
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyCollection()][array]$History
  )

  if ($History.Count -eq 0) {
    return @{ SnapshotCount = 0; HasData = $false }
  }

  $first = $History[0]
  $last = $History[$History.Count - 1]

  return @{
    SnapshotCount    = $History.Count
    HasData          = $true
    FirstTimestamp   = $first.Timestamp
    LastTimestamp     = $last.Timestamp
    LatestFiles      = $last.TotalFiles
    LatestQuality    = $last.QualityScore
    OverallFileDelta = $last.TotalFiles - $first.TotalFiles
    OverallSizeDelta = $last.TotalSizeBytes - $first.TotalSizeBytes
  }
}
