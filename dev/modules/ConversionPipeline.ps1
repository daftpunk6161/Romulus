# ================================================================
#  CONVERSION PIPELINE – CSO/ZSO→ISO→CHD Multi-Step (MF-06)
#  Dependencies: Convert.ps1, Tools.ps1
# ================================================================

function Test-DiskSpaceForConversion {
  <#
  .SYNOPSIS
    Prueft ob genuegend Speicherplatz fuer eine Konvertierung vorhanden ist.
  .PARAMETER SourcePath
    Pfad zur Quelldatei.
  .PARAMETER TargetDir
    Ziel-Verzeichnis.
  .PARAMETER MultiplierEstimate
    Geschaetzter Faktor (z.B. 3.0 = 3x Quellgroesse als Temp-Bedarf).
  #>
  param(
    [Parameter(Mandatory)][string]$SourcePath,
    [Parameter(Mandatory)][string]$TargetDir,
    [double]$MultiplierEstimate = 3.0
  )

  if (-not (Test-Path $SourcePath)) {
    return @{ Ok = $false; Reason = 'SourceNotFound'; Required = 0; Available = 0 }
  }

  $sourceSize = (Get-Item $SourcePath).Length
  $required = [long]($sourceSize * $MultiplierEstimate)

  $drive = [System.IO.Path]::GetPathRoot($TargetDir)
  if (-not $drive) { $drive = [System.IO.Path]::GetPathRoot((Resolve-Path $TargetDir -ErrorAction SilentlyContinue)) }

  try {
    $driveInfo = [System.IO.DriveInfo]::new($drive)
    $available = $driveInfo.AvailableFreeSpace
  } catch {
    return @{ Ok = $false; Reason = 'DriveInfoFailed'; Required = $required; Available = 0 }
  }

  return @{
    Ok        = ($available -ge $required)
    Reason    = if ($available -ge $required) { 'OK' } else { 'InsufficientSpace' }
    Required  = $required
    Available = $available
  }
}

function New-ConversionPipeline {
  <#
  .SYNOPSIS
    Erstellt eine Multi-Step-Konvertierungs-Pipeline-Definition.
  .PARAMETER SourcePath
    Quell-Datei (z.B. .cso).
  .PARAMETER Steps
    Array von Schritt-Definitionen: @{ Tool; Args; OutputExt }.
  .PARAMETER CleanupTemps
    Ob Zwischen-Dateien geloescht werden sollen.
  #>
  param(
    [Parameter(Mandatory)][string]$SourcePath,
    [Parameter(Mandatory)][object[]]$Steps,
    [bool]$CleanupTemps = $true
  )

  return @{
    Id           = [guid]::NewGuid().ToString('N').Substring(0, 8)
    SourcePath   = $SourcePath
    Steps        = $Steps
    CleanupTemps = $CleanupTemps
    Status       = 'Pending'
    Created      = (Get-Date).ToString('o')
    Results      = @()
  }
}

function Get-CsoToChdPipeline {
  <#
  .SYNOPSIS
    Gibt eine vordefinierte CSO→ISO→CHD Pipeline zurueck.
  .PARAMETER SourcePath
    Pfad zur .cso/.zso Datei.
  .PARAMETER OutputDir
    Ziel-Verzeichnis fuer die CHD-Datei.
  #>
  param(
    [Parameter(Mandatory)][string]$SourcePath,
    [Parameter(Mandatory)][string]$OutputDir
  )

  $baseName = [System.IO.Path]::GetFileNameWithoutExtension($SourcePath)
  $tempIso = Join-Path $OutputDir "$baseName.iso"
  $finalChd = Join-Path $OutputDir "$baseName.chd"

  $steps = @(
    @{ Tool = 'ciso'; Action = 'decompress'; Input = $SourcePath; Output = $tempIso; OutputExt = '.iso'; IsTemp = $true }
    @{ Tool = 'chdman'; Action = 'createcd'; Input = $tempIso; Output = $finalChd; OutputExt = '.chd'; IsTemp = $false }
  )

  return New-ConversionPipeline -SourcePath $SourcePath -Steps $steps -CleanupTemps $true
}

function Invoke-ConversionPipelineStep {
  <#
  .SYNOPSIS
    Fuehrt einen einzelnen Pipeline-Schritt aus (DryRun-faehig).
  .PARAMETER Step
    Schritt-Definition.
  .PARAMETER Mode
    DryRun oder Move.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Step,
    [ValidateSet('DryRun','Move')][string]$Mode = 'DryRun'
  )

  if ($Mode -eq 'DryRun') {
    return @{
      Status  = 'DryRun'
      Tool    = $Step.Tool
      Action  = $Step.Action
      Input   = $Step.Input
      Output  = $Step.Output
      Skipped = $true
    }
  }

  # Echte Ausfuehrung wird ueber Tools.ps1 / Convert.ps1 delegiert
  return @{
    Status  = 'Pending'
    Tool    = $Step.Tool
    Action  = $Step.Action
    Input   = $Step.Input
    Output  = $Step.Output
    Skipped = $false
  }
}
