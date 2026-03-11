# ================================================================
#  PARALLEL HASHING – Multi-threaded Hash-Berechnung (MF-14)
#  Dependencies: Dat.ps1
# ================================================================

function Get-OptimalThreadCount {
  <#
  .SYNOPSIS
    Ermittelt die optimale Thread-Anzahl fuer Parallel-Hashing.
  .PARAMETER MaxThreads
    Maximale Anzahl (Default: 8).
  #>
  param(
    [int]$MaxThreads = 8
  )

  $cpuCount = [Environment]::ProcessorCount
  return [math]::Min($cpuCount, $MaxThreads)
}

function Split-FileListIntoChunks {
  <#
  .SYNOPSIS
    Teilt eine Dateiliste in gleich grosse Chunks auf.
  .PARAMETER Files
    Array von Dateipfaden.
  .PARAMETER ChunkSize
    Groesse jedes Chunks.
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Files,
    [int]$ChunkSize = 100
  )

  if (-not $Files -or $Files.Count -eq 0) { return ,@() }

  $chunks = @()
  for ($i = 0; $i -lt $Files.Count; $i += $ChunkSize) {
    $end = [math]::Min($i + $ChunkSize, $Files.Count)
    $chunk = @($Files[$i..($end - 1)])
    $chunks += ,@($chunk)
  }

  return ,$chunks
}

function Get-FileHashSafe {
  <#
  .SYNOPSIS
    Berechnet den Hash einer einzelnen Datei mit Fehlerbehandlung.
  .PARAMETER Path
    Dateipfad.
  .PARAMETER Algorithm
    Hash-Algorithmus.
  #>
  param(
    [Parameter(Mandatory)][string]$Path,
    [ValidateSet('SHA1','SHA256','MD5')][string]$Algorithm = 'SHA1'
  )

  if (-not (Test-Path $Path)) {
    return @{ Path = $Path; Hash = $null; Error = 'FileNotFound' }
  }

  try {
    $hash = (Get-FileHash -Path $Path -Algorithm $Algorithm).Hash
    return @{ Path = $Path; Hash = $hash; Error = $null }
  } catch {
    return @{ Path = $Path; Hash = $null; Error = $_.Exception.Message }
  }
}

function Invoke-ParallelHashing {
  <#
  .SYNOPSIS
    Berechnet Hashes fuer viele Dateien parallel via RunspacePool.
    Fallback auf single-threaded bei Fehler.
  .PARAMETER Files
    Array von Dateipfaden.
  .PARAMETER Algorithm
    Hash-Algorithmus.
  .PARAMETER MaxThreads
    Maximale Thread-Anzahl.
  .PARAMETER Progress
    Optionaler Progress-Callback.
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Files,
    [ValidateSet('SHA1','SHA256','MD5')][string]$Algorithm = 'SHA1',
    [int]$MaxThreads = 8,
    [scriptblock]$Progress
  )

  if (-not $Files -or $Files.Count -eq 0) {
    return @{ Results = @(); TotalFiles = 0; Errors = 0; Method = 'None' }
  }

  $threadCount = Get-OptimalThreadCount -MaxThreads $MaxThreads

  # Versuche RunspacePool
  try {
    $results = Invoke-RunspacePoolHashing -Files $Files -Algorithm $Algorithm -ThreadCount $threadCount -Progress $Progress
    return @{ Results = $results; TotalFiles = $Files.Count; Errors = @($results | Where-Object { $_.Error }).Count; Method = 'Parallel' }
  } catch {
    # Fallback auf single-threaded
    $results = Invoke-SingleThreadHashing -Files $Files -Algorithm $Algorithm -Progress $Progress
    return @{ Results = $results; TotalFiles = $Files.Count; Errors = @($results | Where-Object { $_.Error }).Count; Method = 'SingleThread' }
  }
}

function Invoke-RunspacePoolHashing {
  <#
  .SYNOPSIS
    Fuehrt Hashing via RunspacePool durch.
  #>
  param(
    [Parameter(Mandatory)][string[]]$Files,
    [string]$Algorithm = 'SHA1',
    [int]$ThreadCount = 4,
    [scriptblock]$Progress
  )

  $scriptBlock = {
    param($FilePath, $Algo)
    try {
      $hash = (Get-FileHash -Path $FilePath -Algorithm $Algo).Hash
      @{ Path = $FilePath; Hash = $hash; Error = $null }
    } catch {
      @{ Path = $FilePath; Hash = $null; Error = $_.Exception.Message }
    }
  }

  $pool = [runspacefactory]::CreateRunspacePool(1, $ThreadCount)
  $pool.Open()

  $jobs = @()
  foreach ($file in $Files) {
    $ps = [powershell]::Create().AddScript($scriptBlock).AddArgument($file).AddArgument($Algorithm)
    $ps.RunspacePool = $pool
    $jobs += @{ Pipe = $ps; Handle = $ps.BeginInvoke() }
  }

  $results = @()
  $completed = 0
  foreach ($job in $jobs) {
    $result = $job.Pipe.EndInvoke($job.Handle)
    $results += $result
    $job.Pipe.Dispose()
    $completed++
    if ($Progress) {
      & $Progress @{ Completed = $completed; Total = $Files.Count; Percent = [math]::Round(($completed / $Files.Count) * 100, 1) }
    }
  }

  $pool.Close()
  $pool.Dispose()

  return $results
}

function Invoke-SingleThreadHashing {
  <#
  .SYNOPSIS
    Fallback: Single-Thread-Hashing.
  #>
  param(
    [Parameter(Mandatory)][string[]]$Files,
    [string]$Algorithm = 'SHA1',
    [scriptblock]$Progress
  )

  $results = @()
  $completed = 0

  foreach ($file in $Files) {
    $results += Get-FileHashSafe -Path $file -Algorithm $Algorithm
    $completed++
    if ($Progress) {
      & $Progress @{ Completed = $completed; Total = $Files.Count; Percent = [math]::Round(($completed / $Files.Count) * 100, 1) }
    }
  }

  return $results
}
