#  PLUGIN MARKETPLACE UI (LF-18)
#  In-App-Browser fuer Community-Plugins mit Install/Update/Bewertung.

function New-PluginMarketplaceConfig {
  <#
  .SYNOPSIS
    Erstellt eine Marketplace-Konfiguration.
  #>
  param(
    [string]$CatalogUrl = '',
    [string]$InstallDir = '',
    [switch]$AllowUntrusted
  )

  return @{
    CatalogUrl     = $CatalogUrl
    InstallDir     = $InstallDir
    AllowUntrusted = [bool]$AllowUntrusted
    InstalledPlugins = @{}
    LastRefresh    = $null
  }
}

function New-PluginListing {
  <#
  .SYNOPSIS
    Erstellt einen Plugin-Katalogeintrag.
  #>
  param(
    [Parameter(Mandatory)][string]$Id,
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][string]$Version,
    [string]$Author = '',
    [string]$Description = '',
    [ValidateSet('operation','report','console','theme')]
    [string]$Type = 'operation',
    [double]$Rating = 0.0,
    [int]$Downloads = 0,
    [switch]$Trusted,
    [switch]$Signed
  )

  return @{
    Id          = $Id
    Name        = $Name
    Version     = $Version
    Author      = $Author
    Description = $Description
    Type        = $Type
    Rating      = $Rating
    Downloads   = $Downloads
    Trusted     = [bool]$Trusted
    Signed      = [bool]$Signed
    Compatible  = $true
  }
}

function Search-PluginCatalog {
  <#
  .SYNOPSIS
    Sucht im Plugin-Katalog.
  #>
  param(
    [Parameter(Mandatory)][array]$Catalog,
    [string]$Query = '',
    [string]$Type = '',
    [ValidateSet('Name','Rating','Downloads','Recent')]
    [string]$SortBy = 'Name'
  )

  $results = $Catalog

  if ($Query) {
    $qLower = $Query.ToLowerInvariant()
    $results = @($results | Where-Object {
      $_.Name.ToLowerInvariant() -like "*$qLower*" -or
      $_.Description.ToLowerInvariant() -like "*$qLower*" -or
      $_.Author.ToLowerInvariant() -like "*$qLower*"
    })
  }

  if ($Type) {
    $results = @($results | Where-Object { $_.Type -eq $Type })
  }

  $sorted = switch ($SortBy) {
    'Rating'    { @($results | Sort-Object { $_.Rating } -Descending) }
    'Downloads' { @($results | Sort-Object { $_.Downloads } -Descending) }
    default     { @($results | Sort-Object { $_.Name }) }
  }

  return ,$sorted
}

function Test-PluginInstalled {
  <#
  .SYNOPSIS
    Prueft ob ein Plugin bereits installiert ist.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Config,
    [Parameter(Mandatory)][string]$PluginId
  )

  return $Config.InstalledPlugins.ContainsKey($PluginId)
}

function Test-PluginUpdateAvailable {
  <#
  .SYNOPSIS
    Prueft ob ein Plugin-Update verfuegbar ist.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Config,
    [Parameter(Mandatory)][hashtable]$Listing
  )

  if (-not $Config.InstalledPlugins.ContainsKey($Listing.Id)) {
    return @{ Available = $false; Reason = 'NotInstalled' }
  }

  $installed = $Config.InstalledPlugins[$Listing.Id]
  $hasUpdate = $Listing.Version -ne $installed.Version

  return @{
    Available      = $hasUpdate
    InstalledVersion = $installed.Version
    AvailableVersion = $Listing.Version
  }
}

function Get-PluginCatalogStatistics {
  <#
  .SYNOPSIS
    Statistik ueber den Plugin-Katalog.
  #>
  param(
    [Parameter(Mandatory)][array]$Catalog
  )

  $byType = @{}
  foreach ($p in $Catalog) {
    if (-not $byType.ContainsKey($p.Type)) { $byType[$p.Type] = 0 }
    $byType[$p.Type]++
  }

  $trusted = @($Catalog | Where-Object { $_.Trusted }).Count
  $signed  = @($Catalog | Where-Object { $_.Signed }).Count

  return @{
    Total   = $Catalog.Count
    ByType  = $byType
    Trusted = $trusted
    Signed  = $signed
    AvgRating = if ($Catalog.Count -gt 0) {
      [math]::Round(($Catalog | ForEach-Object { $_.Rating } | Measure-Object -Average).Average, 2)
    } else { 0 }
  }
}
