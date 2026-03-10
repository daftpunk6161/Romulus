# ================================================================
#  GPU ACCELERATED HASHING – SHA1/SHA256 via GPU (XL-09)
#  OpenCL/CUDA fuer massive Sammlungen
# ================================================================

function Test-GpuHashingAvailable {
  <#
  .SYNOPSIS
    Prueft ob GPU-beschleunigtes Hashing verfuegbar ist.
  #>

  $result = @{
    OpenCL    = $false
    CUDA      = $false
    Available = $false
    Reason    = ''
  }

  # Pruefe OpenCL-DLLs
  $openclPaths = @(
    "$env:SystemRoot\System32\OpenCL.dll",
    "$env:SystemRoot\SysWOW64\OpenCL.dll"
  )
  foreach ($p in $openclPaths) {
    if ([System.IO.File]::Exists($p)) {
      $result.OpenCL = $true
      break
    }
  }

  # Pruefe CUDA (nvidia-smi)
  try {
    $nvCmd = Get-Command 'nvidia-smi' -ErrorAction SilentlyContinue
    if ($nvCmd) { $result.CUDA = $true }
  } catch { }

  $result.Available = $result.OpenCL -or $result.CUDA
  if (-not $result.Available) {
    $result.Reason = 'Weder OpenCL noch CUDA verfuegbar'
  }

  return $result
}

function New-GpuHashJob {
  <#
  .SYNOPSIS
    Erstellt einen GPU-Hash-Job.
  .PARAMETER FilePath
    Pfad zur Datei.
  .PARAMETER Algorithm
    Hash-Algorithmus.
  .PARAMETER Backend
    GPU-Backend.
  #>
  param(
    [Parameter(Mandatory)][string]$FilePath,
    [ValidateSet('SHA1','SHA256','MD5')][string]$Algorithm = 'SHA1',
    [ValidateSet('OpenCL','CUDA','CPU')][string]$Backend = 'CPU'
  )

  return @{
    FilePath   = $FilePath
    Algorithm  = $Algorithm
    Backend    = $Backend
    Status     = 'Pending'
    Hash       = $null
    StartTime  = $null
    EndTime    = $null
    ElapsedMs  = 0
  }
}

function Get-GpuHashBatchPlan {
  <#
  .SYNOPSIS
    Erstellt einen Batch-Plan fuer GPU-Hashing.
  .PARAMETER Files
    Array von Dateipfaden.
  .PARAMETER Algorithm
    Hash-Algorithmus.
  .PARAMETER GpuAvailability
    Ergebnis von Test-GpuHashingAvailable.
  .PARAMETER BatchSizeMB
    Batch-Groesse in MB.
  #>
  param(
    [Parameter(Mandatory)][string[]]$Files,
    [ValidateSet('SHA1','SHA256','MD5')][string]$Algorithm = 'SHA1',
    [Parameter(Mandatory)][hashtable]$GpuAvailability,
    [int]$BatchSizeMB = 256
  )

  $backend = 'CPU'
  if ($GpuAvailability.CUDA) { $backend = 'CUDA' }
  elseif ($GpuAvailability.OpenCL) { $backend = 'OpenCL' }

  $jobs = @()
  foreach ($f in $Files) {
    $jobs += New-GpuHashJob -FilePath $f -Algorithm $Algorithm -Backend $backend
  }

  $batchCount = [math]::Ceiling($Files.Count / [math]::Max(1, $BatchSizeMB))
  if ($batchCount -lt 1) { $batchCount = 1 }

  return @{
    Backend     = $backend
    Algorithm   = $Algorithm
    TotalFiles  = $Files.Count
    BatchCount  = $batchCount
    BatchSizeMB = $BatchSizeMB
    Jobs        = $jobs
    EstimatedSpeedup = switch ($backend) {
      'CUDA'   { '10-50x' }
      'OpenCL' { '5-20x' }
      'CPU'    { '1x (kein GPU)' }
    }
  }
}

function Complete-GpuHashJob {
  <#
  .SYNOPSIS
    Markiert einen GPU-Hash-Job als abgeschlossen.
  .PARAMETER Job
    GPU-Hash-Job.
  .PARAMETER Hash
    Berechneter Hash.
  .PARAMETER ElapsedMs
    Dauer in Millisekunden.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Job,
    [Parameter(Mandatory)][string]$Hash,
    [long]$ElapsedMs = 0
  )

  $Job.Status = 'Completed'
  $Job.Hash = $Hash
  $Job.EndTime = [datetime]::UtcNow.ToString('o')
  $Job.ElapsedMs = $ElapsedMs

  return $Job
}

function Get-GpuHashStatistics {
  <#
  .SYNOPSIS
    Gibt Statistiken ueber das GPU-Hashing zurueck.
  .PARAMETER Plan
    Batch-Plan.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Plan
  )

  $completedJobs = @($Plan.Jobs | Where-Object { $_.Status -eq 'Completed' })
  $totalMs = ($completedJobs | ForEach-Object { $_.ElapsedMs } | Measure-Object -Sum).Sum

  return @{
    Backend          = $Plan.Backend
    Algorithm        = $Plan.Algorithm
    TotalFiles       = $Plan.TotalFiles
    CompletedFiles   = $completedJobs.Count
    PendingFiles     = $Plan.TotalFiles - $completedJobs.Count
    TotalElapsedMs   = $totalMs
    EstimatedSpeedup = $Plan.EstimatedSpeedup
  }
}
