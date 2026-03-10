# ================================================================
#  SCHEDULER ADVANCED – Cron-artige Run-Planung (MF-23)
#  Dependencies: Scheduler.ps1, PipelineEngine.ps1
# ================================================================

function New-ScheduleEntry {
  <#
  .SYNOPSIS
    Erstellt einen neuen Schedule-Eintrag.
  .PARAMETER Name
    Name des Schedules.
  .PARAMETER CronExpression
    Cron-artiger Ausdruck (Minute Stunde Tag Monat Wochentag).
  .PARAMETER PipelineName
    Name der auszufuehrenden Pipeline.
  .PARAMETER Enabled
    Ob der Schedule aktiv ist.
  #>
  param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][string]$CronExpression,
    [string]$PipelineName = 'Default',
    [bool]$Enabled = $true
  )

  $parsed = ConvertFrom-CronExpression -Expression $CronExpression
  if (-not $parsed.Valid) {
    return @{ Status = 'Error'; Reason = "Ungueltige Cron-Expression: $($parsed.Error)" }
  }

  return @{
    Id             = [guid]::NewGuid().ToString('N').Substring(0, 8)
    Name           = $Name
    CronExpression = $CronExpression
    CronParsed     = $parsed
    PipelineName   = $PipelineName
    Enabled        = $Enabled
    LastRun        = $null
    NextRun        = $null
    Created        = (Get-Date).ToString('o')
  }
}

function ConvertFrom-CronExpression {
  <#
  .SYNOPSIS
    Parst eine Cron-Expression in ihre Bestandteile.
    Format: Minute(0-59) Stunde(0-23) Tag(1-31) Monat(1-12) Wochentag(0-6, 0=So)
  .PARAMETER Expression
    Cron-String.
  #>
  param(
    [Parameter(Mandatory)][string]$Expression
  )

  $parts = $Expression.Trim() -split '\s+'
  if ($parts.Count -ne 5) {
    return @{ Valid = $false; Error = "Erwartet 5 Felder, erhalten: $($parts.Count)" }
  }

  return @{
    Valid     = $true
    Minute    = $parts[0]
    Hour      = $parts[1]
    DayOfMonth = $parts[2]
    Month     = $parts[3]
    DayOfWeek = $parts[4]
    Raw       = $Expression
  }
}

function Test-CronMatch {
  <#
  .SYNOPSIS
    Prueft ob ein Zeitpunkt zur Cron-Expression passt.
  .PARAMETER CronParsed
    Geparstes Cron-Objekt.
  .PARAMETER DateTime
    Zu pruefender Zeitpunkt.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$CronParsed,
    [Parameter(Mandatory)][datetime]$DateTime
  )

  if (-not $CronParsed.Valid) { return $false }

  $checks = @(
    @{ Field = $CronParsed.Minute; Value = $DateTime.Minute }
    @{ Field = $CronParsed.Hour; Value = $DateTime.Hour }
    @{ Field = $CronParsed.DayOfMonth; Value = $DateTime.Day }
    @{ Field = $CronParsed.Month; Value = $DateTime.Month }
    @{ Field = $CronParsed.DayOfWeek; Value = [int]$DateTime.DayOfWeek }
  )

  foreach ($check in $checks) {
    if (-not (Test-CronFieldMatch -Field $check.Field -Value $check.Value)) {
      return $false
    }
  }

  return $true
}

function Test-CronFieldMatch {
  <#
  .SYNOPSIS
    Prueft ob ein Wert zu einem Cron-Feld passt.
  .PARAMETER Field
    Cron-Feld (z.B. '*', '0', '1,3,5', '0-23').
  .PARAMETER Value
    Aktueller Wert.
  #>
  param(
    [Parameter(Mandatory)][string]$Field,
    [Parameter(Mandatory)][int]$Value
  )

  if ($Field -eq '*') { return $true }

  # Komma-getrennte Werte
  if ($Field -match ',') {
    $values = $Field -split ',' | ForEach-Object { [int]$_.Trim() }
    return ($Value -in $values)
  }

  # Bereich (z.B. 1-5)
  if ($Field -match '^(\d+)-(\d+)$') {
    $start = [int]$Matches[1]
    $end = [int]$Matches[2]
    return ($Value -ge $start -and $Value -le $end)
  }

  # Intervall (z.B. */5)
  if ($Field -match '^\*/(\d+)$') {
    $interval = [int]$Matches[1]
    if ($interval -eq 0) { return $false }
    return (($Value % $interval) -eq 0)
  }

  # Einzelwert
  if ($Field -match '^\d+$') {
    return ($Value -eq [int]$Field)
  }

  return $false
}

function Get-NextCronOccurrence {
  <#
  .SYNOPSIS
    Berechnet den naechsten Ausfuehrungszeitpunkt.
  .PARAMETER CronParsed
    Geparstes Cron-Objekt.
  .PARAMETER After
    Zeitpunkt ab dem gesucht wird.
  .PARAMETER MaxSearchMinutes
    Maximale Suchdauer in Minuten (Default: 10080 = 1 Woche).
  #>
  param(
    [Parameter(Mandatory)][hashtable]$CronParsed,
    [datetime]$After,
    [int]$MaxSearchMinutes = 10080
  )

  if (-not $After) { $After = Get-Date }
  $current = $After.AddMinutes(1)
  # Auf ganze Minute abrunden
  $current = $current.AddSeconds(-$current.Second).AddMilliseconds(-$current.Millisecond)

  for ($i = 0; $i -lt $MaxSearchMinutes; $i++) {
    if (Test-CronMatch -CronParsed $CronParsed -DateTime $current) {
      return @{ Found = $true; NextRun = $current }
    }
    $current = $current.AddMinutes(1)
  }

  return @{ Found = $false; NextRun = $null; Reason = 'MaxSearchExceeded' }
}
