#  NAS/SMB OPTIMIERUNG (LF-15)
#  Adaptive Batch-Groessen, parallele SMB-Transfers, Retry/Throttling.

function New-NasProfile {
  <#
  .SYNOPSIS
    Erstellt ein NAS-Profil mit Optimierungs-Einstellungen.
  #>
  param(
    [Parameter(Mandatory)][string]$Name,
    [int]$BatchSize = 50,
    [int]$MaxParallel = 4,
    [int]$RetryCount = 3,
    [int]$RetryDelayMs = 2000,
    [int]$TimeoutSeconds = 120,
    [ValidateSet('None','Light','Medium','Heavy')]
    [string]$Throttling = 'Medium'
  )

  return @{
    Name           = $Name
    BatchSize      = $BatchSize
    MaxParallel    = $MaxParallel
    RetryCount     = $RetryCount
    RetryDelayMs   = $RetryDelayMs
    TimeoutSeconds = $TimeoutSeconds
    Throttling     = $Throttling
  }
}

function Test-NetworkPath {
  <#
  .SYNOPSIS
    Prueft ob ein Pfad ein Netzwerk-/UNC-Pfad ist.
  #>
  param(
    [Parameter(Mandatory)][string]$Path
  )

  $isUnc = $Path -match '^\\\\[^\\]+\\' -or $Path -match '^//'
  $isMapped = $false

  if (-not $isUnc -and $Path.Length -ge 2 -and $Path[1] -eq ':') {
    try {
      $drive = $Path.Substring(0, 2)
      $netUse = net use $drive 2>&1
      if ($netUse -match 'OK') { $isMapped = $true }
    } catch {
      # Kein Netzlaufwerk
    }
  }

  return @{
    IsNetwork = ($isUnc -or $isMapped)
    IsUnc     = $isUnc
    IsMapped  = $isMapped
  }
}

function Get-AdaptiveBatchSize {
  <#
  .SYNOPSIS
    Berechnet adaptive Batch-Groesse basierend auf Latenz.
  #>
  param(
    [double]$LatencyMs,
    [int]$BaseBatch = 50,
    [int]$MinBatch = 5,
    [int]$MaxBatch = 500
  )

  # Hohe Latenz = kleinere Batches, niedrige Latenz = groessere
  $factor = if ($LatencyMs -le 1) { 4.0 }
            elseif ($LatencyMs -le 5) { 2.0 }
            elseif ($LatencyMs -le 20) { 1.0 }
            elseif ($LatencyMs -le 100) { 0.5 }
            else { 0.25 }

  $batch = [int]($BaseBatch * $factor)
  return [math]::Max($MinBatch, [math]::Min($MaxBatch, $batch))
}

function Split-FilesIntoBatches {
  <#
  .SYNOPSIS
    Teilt eine Dateiliste in Batches auf.
  #>
  param(
    [Parameter(Mandatory)][array]$Files,
    [int]$BatchSize = 50
  )

  $batches = [System.Collections.Generic.List[array]]::new()
  $batchSize = [math]::Max(1, $BatchSize)

  for ($i = 0; $i -lt $Files.Count; $i += $batchSize) {
    $end = [math]::Min($i + $batchSize - 1, $Files.Count - 1)
    $batch = @($Files[$i..$end])
    $batches.Add($batch)
  }

  return ,$batches.ToArray()
}

function Get-ThrottleDelayMs {
  <#
  .SYNOPSIS
    Gibt die Throttle-Verzoegerung in ms fuer ein Profil zurueck.
  #>
  param(
    [Parameter(Mandatory)][string]$Throttling
  )

  switch ($Throttling) {
    'None'   { return 0 }
    'Light'  { return 50 }
    'Medium' { return 200 }
    'Heavy'  { return 1000 }
    default  { return 200 }
  }
}

function New-TransferResult {
  <#
  .SYNOPSIS
    Erstellt ein Transfer-Ergebnis-Objekt.
  #>
  param(
    [int]$TotalFiles = 0,
    [int]$Succeeded = 0,
    [int]$Failed = 0,
    [int]$Retried = 0,
    [long]$BytesTransferred = 0,
    [double]$DurationSeconds = 0
  )

  $throughput = if ($DurationSeconds -gt 0) {
    [math]::Round($BytesTransferred / $DurationSeconds / 1MB, 2)
  } else { 0 }

  return @{
    TotalFiles       = $TotalFiles
    Succeeded        = $Succeeded
    Failed           = $Failed
    Retried          = $Retried
    BytesTransferred = $BytesTransferred
    DurationSeconds  = $DurationSeconds
    ThroughputMBps   = $throughput
  }
}

function Get-NasProfileRecommendation {
  <#
  .SYNOPSIS
    Gibt eine Profil-Empfehlung basierend auf Netzwerktyp.
  #>
  param(
    [ValidateSet('Local','LAN-1G','LAN-10G','WiFi','WAN')]
    [string]$NetworkType = 'LAN-1G'
  )

  switch ($NetworkType) {
    'Local'   { return New-NasProfile -Name 'Local'   -BatchSize 200 -MaxParallel 8 -Throttling 'None' }
    'LAN-1G'  { return New-NasProfile -Name 'LAN-1G'  -BatchSize 50  -MaxParallel 4 -Throttling 'Light' }
    'LAN-10G' { return New-NasProfile -Name 'LAN-10G' -BatchSize 100 -MaxParallel 6 -Throttling 'None' }
    'WiFi'    { return New-NasProfile -Name 'WiFi'    -BatchSize 20  -MaxParallel 2 -Throttling 'Medium' }
    'WAN'     { return New-NasProfile -Name 'WAN'     -BatchSize 10  -MaxParallel 2 -Throttling 'Heavy' -RetryCount 5 }
    default   { return New-NasProfile -Name 'Default' }
  }
}
