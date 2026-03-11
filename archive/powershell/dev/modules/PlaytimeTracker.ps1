#  SPIELZEIT-TRACKING IMPORT (LF-04)
#  Import von RetroAchievements/RetroArch-Spielzeiten.

function Import-RetroArchPlaytime {
  <#
  .SYNOPSIS
    Importiert Spielzeiten aus RetroArch Runtime-Log-Dateien.
  #>
  param(
    [Parameter(Mandatory)][string]$LogDir
  )

  $results = @{}

  if (-not (Test-Path -LiteralPath $LogDir)) {
    return $results
  }

  $files = Get-ChildItem -LiteralPath $LogDir -Filter '*.lrtl' -ErrorAction SilentlyContinue
  foreach ($file in $files) {
    $gameName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
    try {
      $content = Get-Content -LiteralPath $file.FullName -Raw -ErrorAction Stop
      $lines = $content -split "`n" | Where-Object { $_.Trim() -ne '' }
      $totalSeconds = 0
      foreach ($line in $lines) {
        if ($line -match '(\d+):(\d+):(\d+)') {
          $totalSeconds += ([int]$Matches[1] * 3600) + ([int]$Matches[2] * 60) + [int]$Matches[3]
        }
      }
      $results[$gameName] = @{
        GameName     = $gameName
        TotalSeconds = $totalSeconds
        TotalHours   = [math]::Round($totalSeconds / 3600, 2)
        Source       = 'RetroArch'
        Sessions     = $lines.Count
      }
    } catch {
      # Skip nicht-parsbare Dateien
    }
  }

  return $results
}

function New-PlaytimeEntry {
  <#
  .SYNOPSIS
    Erstellt einen manuellen Spielzeit-Eintrag.
  #>
  param(
    [Parameter(Mandatory)][string]$GameName,
    [Parameter(Mandatory)][double]$Hours,
    [string]$Source = 'manual',
    [int]$Sessions = 1
  )

  return @{
    GameName     = $GameName
    TotalSeconds = [int]($Hours * 3600)
    TotalHours   = $Hours
    Source       = $Source
    Sessions     = $Sessions
  }
}

function Merge-PlaytimeData {
  <#
  .SYNOPSIS
    Merged Spielzeit-Daten aus verschiedenen Quellen.
  #>
  param(
    [Parameter(Mandatory)][hashtable[]]$Sources
  )

  $merged = @{}
  foreach ($source in $Sources) {
    foreach ($key in $source.Keys) {
      if ($merged.ContainsKey($key)) {
        $merged[$key].TotalSeconds += $source[$key].TotalSeconds
        $merged[$key].TotalHours = [math]::Round($merged[$key].TotalSeconds / 3600, 2)
        $merged[$key].Sessions += $source[$key].Sessions
      } else {
        $merged[$key] = $source[$key].Clone()
      }
    }
  }

  return $merged
}

function Get-PlaytimeReport {
  <#
  .SYNOPSIS
    Erstellt eine Spielzeit-Statistik.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$PlaytimeData,
    [int]$TopN = 10
  )

  $entries = @($PlaytimeData.Values)
  $totalHours = 0.0
  foreach ($e in $entries) { $totalHours += $e.TotalHours }

  $sorted = @($entries | Sort-Object { $_.TotalHours } -Descending)
  $topGames = @($sorted | Select-Object -First $TopN)

  return @{
    TotalGames   = $entries.Count
    TotalHours   = [math]::Round($totalHours, 2)
    TopGames     = $topGames
    NeverPlayed  = @($entries | Where-Object { $_.TotalSeconds -eq 0 }).Count
    AverageHours = if ($entries.Count -gt 0) { [math]::Round($totalHours / $entries.Count, 2) } else { 0 }
  }
}

function Get-UnplayedRoms {
  <#
  .SYNOPSIS
    Gibt ROMs zurueck, die keine Spielzeit haben.
  #>
  param(
    [Parameter(Mandatory)][string[]]$AllGames,
    [Parameter(Mandatory)][hashtable]$PlaytimeData
  )

  $unplayed = [System.Collections.Generic.List[string]]::new()
  foreach ($game in $AllGames) {
    if (-not $PlaytimeData.ContainsKey($game) -or $PlaytimeData[$game].TotalSeconds -eq 0) {
      $unplayed.Add($game)
    }
  }

  return ,$unplayed.ToArray()
}
