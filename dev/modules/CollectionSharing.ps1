# ================================================================
#  COLLECTION SHARING – Sammlungsliste als HTML/JSON (XL-08)
#  Export ohne ROMs, nur Metadaten – teilbar
# ================================================================

function New-CollectionExportConfig {
  <#
  .SYNOPSIS
    Erstellt eine Export-Konfiguration fuer die Sammlungsliste.
  .PARAMETER Title
    Titel der Sammlung.
  .PARAMETER Owner
    Besitzer-Name.
  .PARAMETER Format
    Export-Format.
  .PARAMETER IncludeStats
    Ob Statistiken eingebunden werden sollen.
  #>
  param(
    [string]$Title = 'Meine ROM-Sammlung',
    [string]$Owner = '',
    [ValidateSet('HTML','JSON','Markdown')][string]$Format = 'HTML',
    [bool]$IncludeStats = $true
  )

  return @{
    Title        = $Title
    Owner        = $Owner
    Format       = $Format
    IncludeStats = $IncludeStats
    ExportDate   = [datetime]::UtcNow.ToString('o')
    Version      = '1.0'
    Privacy      = @{
      IncludePaths  = $false
      IncludeHashes = $false
      IncludeSizes  = $true
    }
  }
}

function New-CollectionEntry {
  <#
  .SYNOPSIS
    Erstellt einen Sammlungs-Eintrag (ohne Dateipfade/Hashes).
  .PARAMETER GameName
    Name des Spiels.
  .PARAMETER ConsoleKey
    Konsolen-Key.
  .PARAMETER Region
    Region-Tag.
  .PARAMETER Format
    Dateiformat.
  .PARAMETER SizeBytes
    Dateigroesse in Bytes.
  #>
  param(
    [Parameter(Mandatory)][string]$GameName,
    [Parameter(Mandatory)][string]$ConsoleKey,
    [string]$Region = '',
    [string]$Format = '',
    [long]$SizeBytes = 0
  )

  return @{
    GameName   = $GameName
    ConsoleKey = $ConsoleKey
    Region     = $Region
    Format     = $Format
    SizeBytes  = $SizeBytes
  }
}

function ConvertTo-CollectionHtml {
  <#
  .SYNOPSIS
    Konvertiert eine Sammlungsliste in HTML.
  .PARAMETER Config
    Export-Konfiguration.
  .PARAMETER Entries
    Array von Sammlungs-Eintraegen.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Config,
    [Parameter(Mandatory)][array]$Entries
  )

  $safeTitle = [System.Net.WebUtility]::HtmlEncode($Config.Title)
  $safeOwner = [System.Net.WebUtility]::HtmlEncode($Config.Owner)

  $lines = @()
  $lines += '<!DOCTYPE html>'
  $lines += '<html lang="de"><head><meta charset="utf-8">'
  $lines += "<title>$safeTitle</title>"
  $lines += '<meta http-equiv="Content-Security-Policy" content="default-src ''none''; style-src ''unsafe-inline''">'
  $lines += '<style>body{font-family:monospace;background:#1a1a2e;color:#eee}table{border-collapse:collapse;width:100%}th,td{border:1px solid #333;padding:6px;text-align:left}th{background:#0f3460}</style>'
  $lines += "</head><body><h1>$safeTitle</h1>"

  if ($safeOwner) { $lines += "<p>Von: $safeOwner</p>" }

  $lines += '<table><tr><th>Spiel</th><th>Konsole</th><th>Region</th><th>Format</th></tr>'

  foreach ($entry in $Entries) {
    $safeName = [System.Net.WebUtility]::HtmlEncode($entry.GameName)
    $safeConsole = [System.Net.WebUtility]::HtmlEncode($entry.ConsoleKey)
    $safeRegion = [System.Net.WebUtility]::HtmlEncode($entry.Region)
    $safeFormat = [System.Net.WebUtility]::HtmlEncode($entry.Format)
    $lines += "<tr><td>$safeName</td><td>$safeConsole</td><td>$safeRegion</td><td>$safeFormat</td></tr>"
  }

  $lines += '</table></body></html>'

  return @{
    Content    = $lines -join "`n"
    Format     = 'HTML'
    EntryCount = @($Entries).Count
  }
}

function ConvertTo-CollectionJson {
  <#
  .SYNOPSIS
    Konvertiert eine Sammlungsliste in JSON.
  .PARAMETER Config
    Export-Konfiguration.
  .PARAMETER Entries
    Array von Sammlungs-Eintraegen.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Config,
    [Parameter(Mandatory)][array]$Entries
  )

  $export = @{
    title      = $Config.Title
    owner      = $Config.Owner
    exportDate = $Config.ExportDate
    version    = $Config.Version
    entries    = $Entries
    stats      = @{
      totalGames = @($Entries).Count
    }
  }

  return @{
    Content    = ($export | ConvertTo-Json -Depth 5)
    Format     = 'JSON'
    EntryCount = @($Entries).Count
  }
}

function Get-CollectionSharingStatistics {
  <#
  .SYNOPSIS
    Gibt Statistiken ueber die Sammlungsliste zurueck.
  .PARAMETER Entries
    Array von Sammlungs-Eintraegen.
  #>
  param(
    [Parameter(Mandatory)][array]$Entries
  )

  $consoles = @{}
  $regions = @{}
  $totalSize = [long]0

  foreach ($e in $Entries) {
    if ($e.ConsoleKey) {
      if (-not $consoles.ContainsKey($e.ConsoleKey)) { $consoles[$e.ConsoleKey] = 0 }
      $consoles[$e.ConsoleKey]++
    }
    if ($e.Region) {
      if (-not $regions.ContainsKey($e.Region)) { $regions[$e.Region] = 0 }
      $regions[$e.Region]++
    }
    $totalSize += $e.SizeBytes
  }

  return @{
    TotalGames     = @($Entries).Count
    ConsoleCount   = $consoles.Count
    RegionCount    = $regions.Count
    TotalSizeBytes = $totalSize
    TopConsoles    = $consoles
    TopRegions     = $regions
  }
}
