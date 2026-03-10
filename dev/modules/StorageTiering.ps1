#  INTELLIGENT STORAGE TIERING (LF-08)
#  Haeufig genutzte ROMs auf SSD, Rest auf HDD/NAS automatisch verschieben.

function Get-StorageTierConfig {
  <#
  .SYNOPSIS
    Erstellt eine Standard-Tier-Konfiguration.
  #>
  param(
    [Parameter(Mandatory)][string]$HotPath,
    [Parameter(Mandatory)][string]$ColdPath,
    [int]$HotThresholdDays = 30,
    [long]$HotMaxSizeGB = 100
  )

  return @{
    HotPath           = $HotPath
    ColdPath          = $ColdPath
    HotThresholdDays  = $HotThresholdDays
    HotMaxSizeGB      = $HotMaxSizeGB
    HotMaxSizeBytes   = $HotMaxSizeGB * 1GB
  }
}

function Get-FileAccessScore {
  <#
  .SYNOPSIS
    Berechnet einen Access-Score basierend auf letztem Zugriff und Haeufigkeit.
  #>
  param(
    [Parameter(Mandatory)][datetime]$LastAccess,
    [int]$AccessCount = 1,
    [datetime]$Now = (Get-Date)
  )

  $daysSinceAccess = [math]::Max(1, ($Now - $LastAccess).TotalDays)
  $recency = 1000.0 / $daysSinceAccess
  $frequency = [math]::Log([math]::Max(1, $AccessCount) + 1) * 100

  return [math]::Round($recency + $frequency, 2)
}

function Invoke-StorageTierAnalysis {
  <#
  .SYNOPSIS
    Analysiert Dateien und ordnet sie Hot/Cold-Tier zu.
  #>
  param(
    [Parameter(Mandatory)][array]$Files,
    [Parameter(Mandatory)][hashtable]$Config,
    [hashtable]$PlaytimeData = @{}
  )

  $results = [System.Collections.Generic.List[hashtable]]::new()
  $now = Get-Date

  foreach ($file in $Files) {
    $lastAccess = if ($file.ContainsKey('LastAccess')) { $file.LastAccess } else { $now.AddDays(-999) }
    $accessCount = if ($PlaytimeData.ContainsKey($file.Name)) { $PlaytimeData[$file.Name].Sessions } else { 0 }

    $score = Get-FileAccessScore -LastAccess $lastAccess -AccessCount $accessCount -Now $now
    $daysSince = ($now - $lastAccess).TotalDays
    $tier = if ($daysSince -le $Config.HotThresholdDays) { 'Hot' } else { 'Cold' }

    $results.Add(@{
      Name       = $file.Name
      Path       = $file.Path
      Size       = if ($file.ContainsKey('Size')) { $file.Size } else { 0 }
      Score      = $score
      Tier       = $tier
      DaysSince  = [math]::Round($daysSince, 0)
    })
  }

  return ,$results.ToArray()
}

function Get-TierMigrationPlan {
  <#
  .SYNOPSIS
    Plant Datei-Migrationen zwischen Tiers.
  #>
  param(
    [Parameter(Mandatory)][array]$Analysis,
    [Parameter(Mandatory)][hashtable]$Config
  )

  $toHot  = [System.Collections.Generic.List[hashtable]]::new()
  $toCold = [System.Collections.Generic.List[hashtable]]::new()

  foreach ($item in $Analysis) {
    $currentlyHot = $item.Path -like "$($Config.HotPath)*"

    if ($item.Tier -eq 'Hot' -and -not $currentlyHot) {
      $toHot.Add($item)
    } elseif ($item.Tier -eq 'Cold' -and $currentlyHot) {
      $toCold.Add($item)
    }
  }

  $hotSize = ($toHot | ForEach-Object { $_.Size } | Measure-Object -Sum).Sum

  return @{
    MoveToHot      = ,$toHot.ToArray()
    MoveToCold     = ,$toCold.ToArray()
    MoveToHotCount = $toHot.Count
    MoveToColdCount= $toCold.Count
    HotSizeNeeded  = $hotSize
    Feasible       = ($hotSize -le $Config.HotMaxSizeBytes)
  }
}

function Get-TierStatistics {
  <#
  .SYNOPSIS
    Statistik ueber die Tier-Verteilung.
  #>
  param(
    [Parameter(Mandatory)][array]$Analysis
  )

  $hotCount = @($Analysis | Where-Object { $_.Tier -eq 'Hot' }).Count
  $coldCount = @($Analysis | Where-Object { $_.Tier -eq 'Cold' }).Count
  $hotSize = ($Analysis | Where-Object { $_.Tier -eq 'Hot' } | ForEach-Object { $_.Size } | Measure-Object -Sum).Sum
  $coldSize = ($Analysis | Where-Object { $_.Tier -eq 'Cold' } | ForEach-Object { $_.Size } | Measure-Object -Sum).Sum

  return @{
    HotCount  = $hotCount
    ColdCount = $coldCount
    HotSize   = $hotSize
    ColdSize  = $coldSize
    Total     = $Analysis.Count
  }
}
