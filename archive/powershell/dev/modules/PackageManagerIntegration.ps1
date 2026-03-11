# ================================================================
#  PACKAGE MANAGER INTEGRATION – Winget/Scoop-Paket (XL-05)
#  Automatische Installation und Updates via Paketmanager
# ================================================================

function New-WingetManifest {
  <#
  .SYNOPSIS
    Erstellt eine Winget-Manifest-Konfiguration.
  .PARAMETER PackageId
    Winget-Paket-ID.
  .PARAMETER Version
    Paket-Version.
  .PARAMETER InstallerUrl
    URL zum Installer.
  .PARAMETER InstallerHash
    SHA256-Hash des Installers.
  #>
  param(
    [string]$PackageId = 'RomCleanup.RomCleanup',
    [string]$Version = '2.0.0',
    [string]$InstallerUrl = '',
    [string]$InstallerHash = ''
  )

  return @{
    PackageIdentifier = $PackageId
    PackageVersion    = $Version
    PackageName       = 'RomCleanup'
    Publisher         = 'RomCleanup Team'
    License           = 'MIT'
    ShortDescription  = 'ROM Collection Management Tool'
    InstallerType     = 'zip'
    InstallerUrl      = $InstallerUrl
    InstallerSha256   = $InstallerHash
    ManifestType      = 'singleton'
    ManifestVersion   = '1.6.0'
    Commands          = @('Invoke-RomCleanup', 'romcleanup')
  }
}

function New-ScoopManifest {
  <#
  .SYNOPSIS
    Erstellt eine Scoop-Manifest-Konfiguration (JSON-Format).
  .PARAMETER Version
    Paket-Version.
  .PARAMETER Url
    Download-URL.
  .PARAMETER Hash
    SHA256-Hash.
  .PARAMETER Bin
    Ausfuehrbare Dateien.
  #>
  param(
    [string]$Version = '2.0.0',
    [string]$Url = '',
    [string]$Hash = '',
    [string[]]$Bin = @('Invoke-RomCleanup.ps1')
  )

  return @{
    version     = $Version
    description = 'ROM Collection Management Tool – Region Dedupe, Format Conversion, DAT Verification'
    homepage    = 'https://github.com/romcleanup/romcleanup'
    license     = 'MIT'
    url         = $Url
    hash        = $Hash
    bin         = $Bin
    checkver    = @{ github = 'https://github.com/romcleanup/romcleanup' }
    autoupdate  = @{
      url = $Url -replace [regex]::Escape($Version), '$version'
    }
  }
}

function Test-PackageManagerAvailable {
  <#
  .SYNOPSIS
    Prueft welche Paketmanager auf dem System verfuegbar sind.
  #>

  $result = @{
    Winget = $false
    Scoop  = $false
    Choco  = $false
  }

  # Pruefe Winget
  try {
    $wingetCmd = Get-Command 'winget' -ErrorAction SilentlyContinue
    if ($wingetCmd) { $result.Winget = $true }
  } catch { }

  # Pruefe Scoop
  try {
    $scoopCmd = Get-Command 'scoop' -ErrorAction SilentlyContinue
    if ($scoopCmd) { $result.Scoop = $true }
  } catch { }

  # Pruefe Chocolatey
  try {
    $chocoCmd = Get-Command 'choco' -ErrorAction SilentlyContinue
    if ($chocoCmd) { $result.Choco = $true }
  } catch { }

  return $result
}

function Get-PackageUpdateCommand {
  <#
  .SYNOPSIS
    Gibt den Update-Befehl fuer den jeweiligen Paketmanager zurueck.
  .PARAMETER Manager
    Paketmanager-Name.
  .PARAMETER PackageName
    Name des Pakets.
  #>
  param(
    [Parameter(Mandatory)][ValidateSet('Winget','Scoop','Choco')][string]$Manager,
    [string]$PackageName = 'RomCleanup'
  )

  switch ($Manager) {
    'Winget' { return @{ Command = "winget upgrade $PackageName"; Manager = 'Winget' } }
    'Scoop'  { return @{ Command = "scoop update $PackageName";   Manager = 'Scoop'  } }
    'Choco'  { return @{ Command = "choco upgrade $PackageName";  Manager = 'Choco'  } }
  }
}

function Get-PackageManagerStatistics {
  <#
  .SYNOPSIS
    Gibt Statistiken ueber die Paketmanager-Integration zurueck.
  .PARAMETER WingetManifest
    Winget-Manifest-Hashtable.
  .PARAMETER ScoopManifest
    Scoop-Manifest-Hashtable.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$WingetManifest,
    [Parameter(Mandatory)][hashtable]$ScoopManifest
  )

  return @{
    WingetPackageId = $WingetManifest.PackageIdentifier
    ScoopVersion    = $ScoopManifest.version
    CommandCount    = @($WingetManifest.Commands).Count
    BinCount        = @($ScoopManifest.bin).Count
    SupportedManagers = @('Winget', 'Scoop', 'Choco')
    ManagerCount    = 3
  }
}
