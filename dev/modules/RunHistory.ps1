# ================================================================
#  RUN HISTORY – Run-History-Browser (QW-14)
#  Dependencies: RunIndex.ps1
# ================================================================

function Get-RunHistory {
  <#
  .SYNOPSIS
    Gibt die Liste aller bisherigen Runs zurueck, sortiert nach Datum (neueste zuerst).
  .PARAMETER ReportsDir
    Verzeichnis mit den Report/Move-Plan-Dateien.
  .PARAMETER MaxEntries
    Maximale Anzahl der zurueckgegebenen Eintraege.
  #>
  param(
    [Parameter(Mandatory)][string]$ReportsDir,
    [int]$MaxEntries = 100
  )

  $history = [System.Collections.Generic.List[hashtable]]::new()

  if (-not (Test-Path -LiteralPath $ReportsDir -PathType Container)) {
    return @{
      Entries = @()
      Total   = 0
    }
  }

  # Move-Plan-JSONs finden
  $planFiles = @(Get-ChildItem -LiteralPath $ReportsDir -Filter 'move-plan-*.json' `
                  -File -ErrorAction SilentlyContinue |
                  Sort-Object LastWriteTime -Descending |
                  Select-Object -First $MaxEntries)

  foreach ($planFile in $planFiles) {
    $entry = @{
      Id           = $planFile.BaseName
      FileName     = $planFile.Name
      FilePath     = $planFile.FullName
      Date         = $planFile.LastWriteTime
      DateFormatted = $planFile.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss')
      SizeBytes    = $planFile.Length
      Roots        = @()
      Mode         = ''
      Status       = ''
      FileCount    = 0
    }

    # Metadaten aus JSON lesen
    try {
      $content = Get-Content -LiteralPath $planFile.FullName -Raw -ErrorAction Stop
      $data = $content | ConvertFrom-Json -ErrorAction Stop

      if ($data.Roots) { $entry.Roots = @($data.Roots) }
      if ($data.Mode) { $entry.Mode = [string]$data.Mode }
      if ($data.Status) { $entry.Status = [string]$data.Status }
      if ($data.Moves) { $entry.FileCount = @($data.Moves).Count }
      elseif ($data.TotalFiles) { $entry.FileCount = [int]$data.TotalFiles }
    } catch {
      $entry.Status = 'ParseError'
    }

    [void]$history.Add($entry)
  }

  return @{
    Entries = @($history)
    Total   = $history.Count
  }
}

function Get-RunDetail {
  <#
  .SYNOPSIS
    Gibt die Detail-Informationen eines bestimmten Runs zurueck.
  .PARAMETER PlanFilePath
    Pfad zur Move-Plan-JSON-Datei.
  #>
  param(
    [Parameter(Mandatory)][string]$PlanFilePath
  )

  if (-not (Test-Path -LiteralPath $PlanFilePath -PathType Leaf)) {
    return @{ Status = 'FileNotFound' }
  }

  try {
    $content = Get-Content -LiteralPath $PlanFilePath -Raw -ErrorAction Stop
    $data = $content | ConvertFrom-Json -ErrorAction Stop

    return @{
      Status  = 'OK'
      Data    = $data
      Path    = $PlanFilePath
      Name    = [System.IO.Path]::GetFileNameWithoutExtension($PlanFilePath)
    }
  } catch {
    return @{
      Status = 'ParseError'
      Error  = $_.Exception.Message
    }
  }
}
