#  ROM COVER/THUMBNAIL SCRAPER (LF-01)
#  Boxart/Screenshots via ScreenScraper.fr oder IGDB-API herunterladen.

function New-CoverScraperConfig {
  <#
  .SYNOPSIS
    Erstellt eine Scraper-Konfiguration.
  #>
  param(
    [Parameter(Mandatory)][string]$Provider,
    [string]$ApiKey = '',
    [string]$CacheDir = '',
    [ValidateSet('box-2d','box-3d','screenshot','title','wheel')]
    [string]$ImageType = 'box-2d',
    [int]$MaxWidth = 400,
    [int]$TimeoutSeconds = 30
  )

  return @{
    Provider       = $Provider
    ApiKey         = $ApiKey
    CacheDir       = $CacheDir
    ImageType      = $ImageType
    MaxWidth       = $MaxWidth
    TimeoutSeconds = $TimeoutSeconds
    RateLimit      = 1.0
  }
}

function Get-CoverCachePath {
  <#
  .SYNOPSIS
    Gibt den Cache-Pfad fuer ein bestimmtes ROM-Cover zurueck.
  #>
  param(
    [Parameter(Mandatory)][string]$CacheDir,
    [Parameter(Mandatory)][string]$ConsoleKey,
    [Parameter(Mandatory)][string]$GameName,
    [string]$ImageType = 'box-2d'
  )

  $safeName = $GameName -replace '[\\/:*?"<>|]', '_'
  $fileName = "$($safeName)_$($ImageType).jpg"
  return Join-Path $CacheDir (Join-Path $ConsoleKey $fileName)
}

function Test-CoverCached {
  <#
  .SYNOPSIS
    Prueft ob ein Cover bereits im Cache liegt.
  #>
  param(
    [Parameter(Mandatory)][string]$CacheDir,
    [Parameter(Mandatory)][string]$ConsoleKey,
    [Parameter(Mandatory)][string]$GameName,
    [string]$ImageType = 'box-2d'
  )

  $path = Get-CoverCachePath -CacheDir $CacheDir -ConsoleKey $ConsoleKey -GameName $GameName -ImageType $ImageType
  return (Test-Path -LiteralPath $path)
}

function New-CoverScrapeRequest {
  <#
  .SYNOPSIS
    Erstellt ein Scrape-Request-Objekt.
  #>
  param(
    [Parameter(Mandatory)][string]$GameName,
    [Parameter(Mandatory)][string]$ConsoleKey,
    [string]$Hash = '',
    [string]$Region = ''
  )

  return @{
    GameName   = $GameName
    ConsoleKey = $ConsoleKey
    Hash       = $Hash
    Region     = $Region
    Status     = 'Pending'
    ResultPath = ''
    Error      = ''
  }
}

function Invoke-CoverScrape {
  <#
  .SYNOPSIS
    Fuehrt einen Batch-Scrape aus (simuliert, keine echten API-Calls).
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Config,
    [Parameter(Mandatory)][array]$Requests
  )

  $results = [System.Collections.Generic.List[hashtable]]::new()

  foreach ($req in $Requests) {
    $entry = $req.Clone()

    # Pruefe Cache
    if ($Config.CacheDir -and (Test-CoverCached -CacheDir $Config.CacheDir -ConsoleKey $req.ConsoleKey -GameName $req.GameName -ImageType $Config.ImageType)) {
      $entry.Status = 'Cached'
      $entry.ResultPath = Get-CoverCachePath -CacheDir $Config.CacheDir -ConsoleKey $req.ConsoleKey -GameName $req.GameName -ImageType $Config.ImageType
    } else {
      # Im echten Betrieb: API-Call an Provider
      # Hier nur Markierung fuer Simulation
      $entry.Status = 'NotFound'
    }

    $results.Add($entry)
  }

  return ,$results.ToArray()
}

function Get-CoverScrapeReport {
  <#
  .SYNOPSIS
    Zusammenfassung eines Scrape-Durchlaufs.
  #>
  param(
    [Parameter(Mandatory)][array]$Results
  )

  $cached   = @($Results | Where-Object { $_.Status -eq 'Cached' }).Count
  $found    = @($Results | Where-Object { $_.Status -eq 'Found' }).Count
  $notFound = @($Results | Where-Object { $_.Status -eq 'NotFound' }).Count
  $errors   = @($Results | Where-Object { $_.Status -eq 'Error' }).Count

  return @{
    Total    = $Results.Count
    Cached   = $cached
    Found    = $found
    NotFound = $notFound
    Errors   = $errors
  }
}
