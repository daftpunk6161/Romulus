# ================================================================
#  NKIT CONVERT – NKit→ISO Rueckkonvertierung (MF-07)
#  Dependencies: Tools.ps1, Convert.ps1
# ================================================================

function Test-NKitImage {
  <#
  .SYNOPSIS
    Prueft ob eine Datei ein NKit-Image ist.
  .PARAMETER Path
    Pfad zur Datei.
  #>
  param(
    [Parameter(Mandatory)][string]$Path
  )

  if (-not (Test-Path $Path)) { return $false }

  $ext = [System.IO.Path]::GetExtension($Path).ToLowerInvariant()
  if ($ext -notin @('.nkit.iso', '.nkit.gcz', '.nkit')) {
    # Pruefe auch Dateinamen-Pattern
    $name = [System.IO.Path]::GetFileName($Path).ToLowerInvariant()
    if ($name -notmatch '\.nkit\.' -and $ext -ne '.nkit') { return $false }
  }

  return $true
}

function Get-NKitConversionParams {
  <#
  .SYNOPSIS
    Erstellt Parameter fuer die NKit→ISO Konvertierung.
  .PARAMETER SourcePath
    Pfad zur NKit-Datei.
  .PARAMETER OutputDir
    Ausgabe-Verzeichnis.
  .PARAMETER TargetFormat
    Zielformat: ISO oder RVZ.
  #>
  param(
    [Parameter(Mandatory)][string]$SourcePath,
    [Parameter(Mandatory)][string]$OutputDir,
    [ValidateSet('ISO','RVZ')][string]$TargetFormat = 'ISO'
  )

  $baseName = [System.IO.Path]::GetFileName($SourcePath) -replace '\.nkit\.\w+$', ''
  if (-not $baseName -or $baseName -eq [System.IO.Path]::GetFileName($SourcePath)) {
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($SourcePath)
  }
  $outputExt = if ($TargetFormat -eq 'ISO') { '.iso' } else { '.rvz' }
  $outputPath = Join-Path $OutputDir ($baseName + $outputExt)

  return @{
    SourcePath   = $SourcePath
    OutputPath   = $outputPath
    TargetFormat = $TargetFormat
    Tool         = 'nkit'
    BaseName     = $baseName
  }
}

function Invoke-NKitConversion {
  <#
  .SYNOPSIS
    Fuehrt die NKit→ISO Konvertierung aus (DryRun-faehig).
  .PARAMETER SourcePath
    Pfad zur NKit-Datei.
  .PARAMETER OutputDir
    Ausgabe-Verzeichnis.
  .PARAMETER TargetFormat
    Zielformat.
  .PARAMETER Mode
    DryRun oder Move.
  #>
  param(
    [Parameter(Mandatory)][string]$SourcePath,
    [Parameter(Mandatory)][string]$OutputDir,
    [ValidateSet('ISO','RVZ')][string]$TargetFormat = 'ISO',
    [ValidateSet('DryRun','Move')][string]$Mode = 'DryRun'
  )

  if (-not (Test-NKitImage -Path $SourcePath)) {
    return @{ Status = 'Error'; Reason = 'NotNKitImage'; SourcePath = $SourcePath }
  }

  $params = Get-NKitConversionParams -SourcePath $SourcePath -OutputDir $OutputDir -TargetFormat $TargetFormat

  if ($Mode -eq 'DryRun') {
    return @{
      Status       = 'DryRun'
      SourcePath   = $params.SourcePath
      OutputPath   = $params.OutputPath
      TargetFormat = $params.TargetFormat
      Tool         = $params.Tool
    }
  }

  # Echte Konvertierung wird an Tools.ps1 delegiert
  return @{
    Status       = 'Pending'
    SourcePath   = $params.SourcePath
    OutputPath   = $params.OutputPath
    TargetFormat = $params.TargetFormat
    Tool         = $params.Tool
  }
}
