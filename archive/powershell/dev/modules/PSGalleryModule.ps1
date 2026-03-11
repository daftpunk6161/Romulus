# ================================================================
#  PSGALLERY MODULE – Install-Module RomCleanup (XL-04)
#  Auto-Update via PowerShell Gallery
# ================================================================

function New-PSGalleryManifest {
  <#
  .SYNOPSIS
    Erstellt eine PSGallery-Modul-Manifest-Konfiguration.
  .PARAMETER ModuleName
    Name des Moduls.
  .PARAMETER Version
    Modul-Version.
  .PARAMETER Author
    Autor-Name.
  .PARAMETER Description
    Modul-Beschreibung.
  #>
  param(
    [string]$ModuleName = 'RomCleanup',
    [string]$Version = '2.0.0',
    [string]$Author = 'RomCleanup Team',
    [string]$Description = 'ROM Collection Management Tool – Region Dedupe, Format Conversion, DAT Verification'
  )

  return @{
    ModuleName       = $ModuleName
    ModuleVersion    = $Version
    Author           = $Author
    Description      = $Description
    PowerShellVersion = '5.1'
    CompatiblePSEditions = @('Desktop', 'Core')
    Tags             = @('ROM', 'Emulation', 'Retro', 'Gaming', 'Cleanup', 'Dedupe', 'DAT')
    LicenseUri       = "https://github.com/romcleanup/romcleanup/blob/main/LICENSE"
    ProjectUri       = "https://github.com/romcleanup/romcleanup"
    FunctionsToExport = @(
      'Invoke-RomCleanup',
      'Invoke-RegionDedupe',
      'ConvertTo-GameKey',
      'Get-FormatScore'
    )
    RequiredModules  = @()
  }
}

function Test-PSGalleryManifestValid {
  <#
  .SYNOPSIS
    Validiert eine PSGallery-Manifest-Konfiguration.
  .PARAMETER Manifest
    Manifest-Hashtable.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Manifest
  )

  $errors = @()

  if ([string]::IsNullOrWhiteSpace($Manifest.ModuleName)) {
    $errors += 'ModuleName ist leer'
  }
  if ([string]::IsNullOrWhiteSpace($Manifest.ModuleVersion)) {
    $errors += 'ModuleVersion ist leer'
  }
  elseif ($Manifest.ModuleVersion -notmatch '^\d+\.\d+\.\d+$') {
    $errors += 'ModuleVersion muss SemVer-Format haben (x.y.z)'
  }
  if ([string]::IsNullOrWhiteSpace($Manifest.Author)) {
    $errors += 'Author ist leer'
  }
  if ([string]::IsNullOrWhiteSpace($Manifest.Description)) {
    $errors += 'Description ist leer'
  }
  if (@($Manifest.Tags).Count -eq 0) {
    $errors += 'Mindestens ein Tag erforderlich'
  }
  if (@($Manifest.FunctionsToExport).Count -eq 0) {
    $errors += 'FunctionsToExport darf nicht leer sein'
  }

  return @{
    Valid  = ($errors.Count -eq 0)
    Errors = $errors
  }
}

function Compare-ModuleVersions {
  <#
  .SYNOPSIS
    Vergleicht zwei Modul-Versionen.
  .PARAMETER Current
    Aktuelle Version.
  .PARAMETER Available
    Verfuegbare Version.
  #>
  param(
    [Parameter(Mandatory)][string]$Current,
    [Parameter(Mandatory)][string]$Available
  )

  $currentParts = $Current -split '\.' | ForEach-Object { [int]$_ }
  $availableParts = $Available -split '\.' | ForEach-Object { [int]$_ }

  # Auf 3 Teile auffuellen
  while ($currentParts.Count -lt 3) { $currentParts += 0 }
  while ($availableParts.Count -lt 3) { $availableParts += 0 }

  $updateType = 'none'
  if ($availableParts[0] -gt $currentParts[0]) { $updateType = 'major' }
  elseif ($availableParts[0] -eq $currentParts[0] -and $availableParts[1] -gt $currentParts[1]) { $updateType = 'minor' }
  elseif ($availableParts[0] -eq $currentParts[0] -and $availableParts[1] -eq $currentParts[1] -and $availableParts[2] -gt $currentParts[2]) { $updateType = 'patch' }

  return @{
    Current       = $Current
    Available     = $Available
    UpdateType    = $updateType
    UpdateAvailable = ($updateType -ne 'none')
  }
}

function New-PublishConfig {
  <#
  .SYNOPSIS
    Erstellt eine Publish-Konfiguration fuer die PSGallery.
  .PARAMETER Manifest
    Manifest-Hashtable.
  .PARAMETER Repository
    Ziel-Repository.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Manifest,
    [string]$Repository = 'PSGallery'
  )

  return @{
    ModuleName  = $Manifest.ModuleName
    Version     = $Manifest.ModuleVersion
    Repository  = $Repository
    NuGetApiKey = '${NUGET_API_KEY}'
    Tags        = $Manifest.Tags
    PreRelease  = $false
  }
}

function Get-PSGalleryStatistics {
  <#
  .SYNOPSIS
    Gibt Statistiken ueber die PSGallery-Konfiguration zurueck.
  .PARAMETER Manifest
    Manifest-Hashtable.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Manifest
  )

  return @{
    ModuleName         = $Manifest.ModuleName
    Version            = $Manifest.ModuleVersion
    TagCount           = @($Manifest.Tags).Count
    ExportedFunctions  = @($Manifest.FunctionsToExport).Count
    CompatibleEditions = @($Manifest.CompatiblePSEditions).Count
    MinPSVersion       = $Manifest.PowerShellVersion
  }
}
