# ================================================================
#  SYSTEM TRAY – Mini-Modus / System-Tray-Integration (MF-18)
#  Dependencies: WpfApp.ps1 (System.Windows.Forms fuer NotifyIcon)
# ================================================================

function New-TrayIconConfig {
  <#
  .SYNOPSIS
    Erstellt eine System-Tray-Konfiguration.
  .PARAMETER ToolTip
    Tooltip-Text.
  .PARAMETER IconState
    Status-Icon: Idle, Running, Error, Paused.
  #>
  param(
    [string]$ToolTip = 'RomCleanup',
    [ValidateSet('Idle','Running','Error','Paused')][string]$IconState = 'Idle'
  )

  return @{
    ToolTip       = $ToolTip
    IconState     = $IconState
    Visible       = $true
    MenuItems     = @()
    BalloonTitle  = $null
    BalloonText   = $null
  }
}

function Add-TrayMenuItem {
  <#
  .SYNOPSIS
    Fuegt ein Kontextmenu-Item zur Tray-Konfiguration hinzu.
  .PARAMETER Config
    Tray-Konfiguration.
  .PARAMETER Label
    Menuepunkt-Text.
  .PARAMETER Key
    Eindeutiger Schluessel.
  .PARAMETER IsSeparator
    Ob es ein Trennstrich ist.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Config,
    [Parameter(Mandatory)][string]$Label,
    [Parameter(Mandatory)][string]$Key,
    [bool]$IsSeparator = $false
  )

  $Config.MenuItems += @{
    Label       = $Label
    Key         = $Key
    IsSeparator = $IsSeparator
  }

  return $Config
}

function Get-DefaultTrayMenu {
  <#
  .SYNOPSIS
    Erstellt das Standard-Kontextmenu fuer den System-Tray.
  #>
  $config = New-TrayIconConfig -ToolTip 'RomCleanup' -IconState 'Idle'
  $config = Add-TrayMenuItem -Config $config -Label 'Fenster anzeigen' -Key 'show'
  $config = Add-TrayMenuItem -Config $config -Label 'DryRun starten' -Key 'dryrun'
  $config = Add-TrayMenuItem -Config $config -Label '-' -Key 'sep1' -IsSeparator $true
  $config = Add-TrayMenuItem -Config $config -Label 'Status' -Key 'status'
  $config = Add-TrayMenuItem -Config $config -Label '-' -Key 'sep2' -IsSeparator $true
  $config = Add-TrayMenuItem -Config $config -Label 'Beenden' -Key 'exit'
  return $config
}

function Set-TrayIconState {
  <#
  .SYNOPSIS
    Aendert den Status des Tray-Icons.
  .PARAMETER Config
    Tray-Konfiguration.
  .PARAMETER IconState
    Neuer Status.
  .PARAMETER ToolTip
    Optionaler neuer Tooltip.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Config,
    [ValidateSet('Idle','Running','Error','Paused')][string]$IconState,
    [string]$ToolTip
  )

  $Config.IconState = $IconState
  if ($ToolTip) { $Config.ToolTip = $ToolTip }
  return $Config
}

function New-TrayBalloonNotification {
  <#
  .SYNOPSIS
    Erstellt eine Balloon-Benachrichtigung fuer den System-Tray.
  .PARAMETER Title
    Titel der Benachrichtigung.
  .PARAMETER Text
    Text der Benachrichtigung.
  .PARAMETER Icon
    Icon-Typ: Info, Warning, Error, None.
  .PARAMETER TimeoutMs
    Anzeigedauer in Millisekunden.
  #>
  param(
    [Parameter(Mandatory)][string]$Title,
    [Parameter(Mandatory)][string]$Text,
    [ValidateSet('Info','Warning','Error','None')][string]$Icon = 'Info',
    [int]$TimeoutMs = 5000
  )

  return @{
    Title     = $Title
    Text      = $Text
    Icon      = $Icon
    TimeoutMs = $TimeoutMs
  }
}
