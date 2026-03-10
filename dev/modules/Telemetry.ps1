# ================================================================
#  TELEMETRY (OPT-IN) – Anonyme Nutzungsstatistiken (XL-14)
#  Feature-Nutzung, populaere Konsolen, Error-Patterns
# ================================================================

function New-TelemetryConfig {
  <#
  .SYNOPSIS
    Erstellt eine Telemetrie-Konfiguration.
  .PARAMETER Enabled
    Ob Telemetrie aktiviert ist.
  .PARAMETER AnonymousId
    Anonyme Installations-ID.
  .PARAMETER CollectionLevel
    Level der Datensammlung.
  #>
  param(
    [bool]$Enabled = $false,
    [string]$AnonymousId = '',
    [ValidateSet('Minimal','Standard','Full')][string]$CollectionLevel = 'Minimal'
  )

  if (-not $AnonymousId) {
    $AnonymousId = [guid]::NewGuid().ToString('N')
  }

  return @{
    Enabled         = $Enabled
    AnonymousId     = $AnonymousId
    CollectionLevel = $CollectionLevel
    ConsentDate     = if ($Enabled) { [datetime]::UtcNow.ToString('o') } else { $null }
    Version         = '1.0'
  }
}

function New-TelemetryEvent {
  <#
  .SYNOPSIS
    Erstellt ein Telemetrie-Event.
  .PARAMETER EventName
    Name des Events.
  .PARAMETER Category
    Event-Kategorie.
  .PARAMETER Properties
    Zusaetzliche Eigenschaften.
  .PARAMETER Config
    Telemetrie-Konfiguration.
  #>
  param(
    [Parameter(Mandatory)][string]$EventName,
    [ValidateSet('Feature','Error','Performance','System')][string]$Category = 'Feature',
    [hashtable]$Properties = @{},
    [Parameter(Mandatory)][hashtable]$Config
  )

  if (-not $Config.Enabled) {
    return $null
  }

  # Sensitive Daten entfernen
  $safeProps = @{}
  foreach ($key in $Properties.Keys) {
    $val = $Properties[$key]
    # Keine Pfade, keine Namen, keine IPs
    if ($val -is [string] -and ($val -match '[\\/]' -or $val -match '\d+\.\d+\.\d+\.\d+')) {
      continue
    }
    $safeProps[$key] = $val
  }

  return @{
    EventName   = $EventName
    Category    = $Category
    Properties  = $safeProps
    AnonymousId = $Config.AnonymousId
    Timestamp   = [datetime]::UtcNow.ToString('o')
    AppVersion  = '2.0.0'
  }
}

function Get-TelemetryConsentText {
  <#
  .SYNOPSIS
    Gibt den Einwilligungstext fuer die Telemetrie zurueck.
  .PARAMETER Language
    Sprache.
  #>
  param(
    [ValidateSet('de','en')][string]$Language = 'de'
  )

  $texts = @{
    'de' = @{
      Title   = 'Anonyme Nutzungsstatistiken'
      Body    = 'RomCleanup kann anonyme Nutzungsdaten sammeln, um das Tool zu verbessern. Keine persoenlichen Daten, Dateipfade oder ROM-Namen werden uebertragen.'
      Consent = 'Ich stimme der anonymen Datensammlung zu.'
      Decline = 'Nein, danke.'
    }
    'en' = @{
      Title   = 'Anonymous Usage Statistics'
      Body    = 'RomCleanup can collect anonymous usage data to improve the tool. No personal data, file paths, or ROM names are transmitted.'
      Consent = 'I agree to anonymous data collection.'
      Decline = 'No, thanks.'
    }
  }

  return $texts[$Language]
}

function Add-TelemetryEvent {
  <#
  .SYNOPSIS
    Fuegt ein Event zur Event-Queue hinzu.
  .PARAMETER Queue
    Event-Queue (Array).
  .PARAMETER Event
    Telemetrie-Event.
  .PARAMETER MaxQueueSize
    Maximale Queue-Groesse.
  #>
  param(
    [array]$Queue = @(),
    [Parameter(Mandatory)][hashtable]$Event,
    [int]$MaxQueueSize = 500
  )

  $newQueue = @($Queue) + @($Event)

  if ($newQueue.Count -gt $MaxQueueSize) {
    $newQueue = $newQueue[($newQueue.Count - $MaxQueueSize)..($newQueue.Count - 1)]
  }

  return ,$newQueue
}

function Get-TelemetryAggregation {
  <#
  .SYNOPSIS
    Aggregiert Telemetrie-Events fuer den Upload.
  .PARAMETER Events
    Array von Telemetrie-Events.
  #>
  param(
    [Parameter(Mandatory)][array]$Events
  )

  $categories = @{}
  $features = @{}

  foreach ($e in $Events) {
    $cat = $e.Category
    if (-not $categories.ContainsKey($cat)) { $categories[$cat] = 0 }
    $categories[$cat]++

    if ($cat -eq 'Feature') {
      $name = $e.EventName
      if (-not $features.ContainsKey($name)) { $features[$name] = 0 }
      $features[$name]++
    }
  }

  return @{
    TotalEvents   = @($Events).Count
    Categories    = $categories
    TopFeatures   = $features
    TimeRange     = @{
      First = if ($Events.Count -gt 0) { $Events[0].Timestamp } else { $null }
      Last  = if ($Events.Count -gt 0) { $Events[$Events.Count - 1].Timestamp } else { $null }
    }
  }
}

function Get-TelemetryStatistics {
  <#
  .SYNOPSIS
    Gibt Statistiken ueber die Telemetrie zurueck.
  .PARAMETER Config
    Telemetrie-Konfiguration.
  .PARAMETER EventCount
    Anzahl gesammelter Events.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Config,
    [int]$EventCount = 0
  )

  return @{
    Enabled         = $Config.Enabled
    CollectionLevel = $Config.CollectionLevel
    EventCount      = $EventCount
    AnonymousId     = $Config.AnonymousId.Substring(0, [math]::Min(8, $Config.AnonymousId.Length)) + '...'
    Version         = $Config.Version
  }
}
