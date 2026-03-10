# ================================================================
#  WINDOWS CONTEXT MENU – Shell-Extension (XL-03)
#  Rechtsklick auf Ordner -> "Mit RomCleanup scannen/sortieren"
# ================================================================

function New-ContextMenuEntry {
  <#
  .SYNOPSIS
    Erstellt eine Kontextmenue-Eintrags-Definition.
  .PARAMETER Label
    Angezeigter Text im Kontextmenue.
  .PARAMETER Command
    Auszufuehrender Befehl.
  .PARAMETER Icon
    Pfad zum Icon.
  .PARAMETER Position
    Position im Menue (Top/Bottom).
  #>
  param(
    [Parameter(Mandatory)][string]$Label,
    [Parameter(Mandatory)][string]$Command,
    [string]$Icon = '',
    [ValidateSet('Top','Bottom','')][string]$Position = ''
  )

  return @{
    Label    = $Label
    Command  = $Command
    Icon     = $Icon
    Position = $Position
    Type     = 'Directory'
  }
}

function Get-DefaultContextMenuEntries {
  <#
  .SYNOPSIS
    Gibt die Standard-Kontextmenue-Eintraege fuer RomCleanup zurueck.
  .PARAMETER ScriptPath
    Pfad zum RomCleanup-Script.
  #>
  param(
    [string]$ScriptPath = 'C:\RomCleanup\Invoke-RomCleanup.ps1'
  )

  $safePath = $ScriptPath -replace "'", "''"

  return @(
    (New-ContextMenuEntry -Label 'Mit RomCleanup scannen (DryRun)' `
      -Command "pwsh -NoProfile -File `"$safePath`" -Roots `"%V`" -Mode DryRun" `
      -Position 'Top')
    (New-ContextMenuEntry -Label 'Mit RomCleanup sortieren (Move)' `
      -Command "pwsh -NoProfile -File `"$safePath`" -Roots `"%V`" -Mode Move")
    (New-ContextMenuEntry -Label 'RomCleanup GUI oeffnen' `
      -Command "pwsh -NoProfile -File `"$safePath`" -GUI")
  )
}

function ConvertTo-RegistryCommands {
  <#
  .SYNOPSIS
    Konvertiert Kontextmenue-Eintraege in Registry-Befehle.
  .PARAMETER Entries
    Array von Kontextmenue-Eintraegen.
  .PARAMETER RootKey
    Registry-Root-Key.
  #>
  param(
    [Parameter(Mandatory)][array]$Entries,
    [string]$RootKey = 'HKCU:\Software\Classes\Directory\shell'
  )

  $commands = @()
  $counter = 0

  foreach ($entry in $Entries) {
    $counter++
    $subKey = "RomCleanup_$counter"
    $keyPath = "$RootKey\$subKey"
    $cmdPath = "$keyPath\command"

    $regEntry = @{
      KeyPath     = $keyPath
      CommandPath = $cmdPath
      Label       = $entry.Label
      Command     = $entry.Command
      Properties  = @{}
    }

    if ($entry.Icon) {
      $regEntry.Properties['Icon'] = $entry.Icon
    }
    if ($entry.Position) {
      $regEntry.Properties['Position'] = $entry.Position
    }

    $commands += $regEntry
  }

  return $commands
}

function Get-ContextMenuUninstallCommands {
  <#
  .SYNOPSIS
    Gibt die Registry-Pfade zum Entfernen der Kontextmenue-Eintraege zurueck.
  .PARAMETER Count
    Anzahl der registrierten Eintraege.
  .PARAMETER RootKey
    Registry-Root-Key.
  #>
  param(
    [int]$Count = 3,
    [string]$RootKey = 'HKCU:\Software\Classes\Directory\shell'
  )

  $paths = @()
  for ($i = 1; $i -le $Count; $i++) {
    $paths += "$RootKey\RomCleanup_$i"
  }
  return $paths
}

function Test-ContextMenuInstalled {
  <#
  .SYNOPSIS
    Prueft ob die Kontextmenue-Eintraege bereits installiert sind (Logik-Pruefung).
  .PARAMETER RootKey
    Registry-Root-Key.
  #>
  param(
    [string]$RootKey = 'HKCU:\Software\Classes\Directory\shell'
  )

  # Gibt einen Status zurueck - tatsaechliche Registry-Pruefung erfolgt beim Aufruf
  return @{
    ExpectedKeyPattern = "$RootKey\RomCleanup_*"
    CheckRequired      = $true
    RootKey            = $RootKey
  }
}

function Get-ContextMenuStatistics {
  <#
  .SYNOPSIS
    Gibt Statistiken ueber die Kontextmenue-Konfiguration zurueck.
  .PARAMETER Entries
    Array von Kontextmenue-Eintraegen.
  #>
  param(
    [Parameter(Mandatory)][array]$Entries
  )

  $withIcons = @($Entries | Where-Object { $_.Icon -ne '' }).Count

  return @{
    EntryCount     = @($Entries).Count
    WithIconCount  = $withIcons
    TargetType     = 'Directory'
    RegistryScope  = 'CurrentUser'
  }
}
