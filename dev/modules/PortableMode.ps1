# ================================================================
#  PORTABLE MODE – Settings/Logs relativ zum Programmordner (QW-12)
#  Dependencies: Settings.ps1
# ================================================================

function Test-PortableMode {
  <#
  .SYNOPSIS
    Prueft ob der Portable-Modus aktiv ist.
    Portable-Modus ist aktiv wenn:
    - Explizit via $Portable Parameter gesetzt
    - Eine .portable Marker-Datei im Programmordner existiert
  .PARAMETER ProgramRoot
    Wurzelverzeichnis des Programms.
  .PARAMETER Portable
    Explizit gesetzter Portable-Modus.
  #>
  param(
    [string]$ProgramRoot,
    [bool]$Portable = $false
  )

  if ($Portable) { return $true }

  if (-not [string]::IsNullOrWhiteSpace($ProgramRoot)) {
    $markerPath = Join-Path $ProgramRoot '.portable'
    if (Test-Path -LiteralPath $markerPath -PathType Leaf) {
      return $true
    }
  }

  return $false
}

function Get-PortableSettingsRoot {
  <#
  .SYNOPSIS
    Ermittelt das Settings-Wurzelverzeichnis basierend auf Portable-Modus.
  .PARAMETER ProgramRoot
    Wurzelverzeichnis des Programms.
  .PARAMETER Portable
    Ob Portable-Modus aktiv ist.
  #>
  param(
    [Parameter(Mandatory)][string]$ProgramRoot,
    [bool]$Portable = $false
  )

  $isPortable = Test-PortableMode -ProgramRoot $ProgramRoot -Portable $Portable

  if ($isPortable) {
    $portableRoot = Join-Path $ProgramRoot '.romcleanup'

    # Schreibrecht pruefen
    try {
      if (-not (Test-Path -LiteralPath $portableRoot -PathType Container)) {
        New-Item -ItemType Directory -Path $portableRoot -Force -ErrorAction Stop | Out-Null
      }
      # Test-Schreibzugriff
      $testFile = Join-Path $portableRoot '.writetest'
      [System.IO.File]::WriteAllText($testFile, 'test')
      Remove-Item -LiteralPath $testFile -Force -ErrorAction SilentlyContinue
      return $portableRoot
    } catch {
      throw ("Portable-Modus: Kein Schreibzugriff auf Programmordner: {0}" -f $ProgramRoot)
    }
  }

  # Standard: %APPDATA%
  if (Get-Command Resolve-RomCleanupUserDataRoot -ErrorAction SilentlyContinue) {
    return (Join-Path (Resolve-RomCleanupUserDataRoot) 'RomCleanupRegionDedupe')
  }

  $appData = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::ApplicationData)
  return (Join-Path $appData 'RomCleanupRegionDedupe')
}

function Get-PortablePath {
  <#
  .SYNOPSIS
    Loest einen relativen Pfad basierend auf Portable-Modus auf.
  .PARAMETER ProgramRoot
    Wurzelverzeichnis des Programms.
  .PARAMETER SubPath
    Unterpfad (z.B. 'settings.json', 'logs', 'cache').
  .PARAMETER Portable
    Ob Portable-Modus aktiv ist.
  #>
  param(
    [Parameter(Mandatory)][string]$ProgramRoot,
    [Parameter(Mandatory)][string]$SubPath,
    [bool]$Portable = $false
  )

  $root = Get-PortableSettingsRoot -ProgramRoot $ProgramRoot -Portable $Portable
  return (Join-Path $root $SubPath)
}
