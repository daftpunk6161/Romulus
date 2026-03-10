#  EMULATOR LAUNCHER INTEGRATION (LF-03)
#  Export nach RetroArch .lpl, LaunchBox XML, EmulationStation gamelist.xml, Playnite.

function Get-SupportedLauncherFormats {
  <#
  .SYNOPSIS
    Gibt die unterstuetzten Launcher-Formate zurueck.
  #>
  return @(
    @{ Key = 'retroarch';          Name = 'RetroArch';        Extension = '.lpl';  Format = 'JSON' }
    @{ Key = 'emulationstation';   Name = 'EmulationStation'; Extension = '.xml';  Format = 'XML' }
    @{ Key = 'launchbox';          Name = 'LaunchBox';        Extension = '.xml';  Format = 'XML' }
    @{ Key = 'playnite';           Name = 'Playnite';         Extension = '.json'; Format = 'JSON' }
  )
}

function New-LauncherEntry {
  <#
  .SYNOPSIS
    Erstellt einen Launcher-Eintrag fuer ein ROM.
  #>
  param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$Console,
    [string]$Core = '',
    [string]$CoverPath = '',
    [string]$Region = ''
  )

  return @{
    Name      = $Name
    Path      = $Path
    Console   = $Console
    Core      = $Core
    CoverPath = $CoverPath
    Region    = $Region
  }
}

function Get-DefaultCoreMapping {
  <#
  .SYNOPSIS
    Standard-Core-Zuweisungen fuer RetroArch nach Konsole.
  #>
  return @{
    'nes'       = 'mesen_libretro'
    'snes'      = 'snes9x_libretro'
    'n64'       = 'mupen64plus_next_libretro'
    'gb'        = 'gambatte_libretro'
    'gba'       = 'mgba_libretro'
    'gbc'       = 'gambatte_libretro'
    'nds'       = 'melonds_libretro'
    'genesis'   = 'genesis_plus_gx_libretro'
    'megadrive' = 'genesis_plus_gx_libretro'
    'psx'       = 'mednafen_psx_hw_libretro'
    'ps2'       = 'pcsx2_libretro'
    'psp'       = 'ppsspp_libretro'
    'gc'        = 'dolphin_libretro'
    'wii'       = 'dolphin_libretro'
    'saturn'    = 'mednafen_saturn_libretro'
    'dreamcast' = 'flycast_libretro'
    'arcade'    = 'fbneo_libretro'
  }
}

function ConvertTo-RetroArchPlaylist {
  <#
  .SYNOPSIS
    Konvertiert Launcher-Eintraege in RetroArch .lpl Format.
  #>
  param(
    [Parameter(Mandatory)][array]$Entries,
    [string]$PlaylistName = 'RomCleanup',
    [hashtable]$CoreMapping = $null
  )

  if (-not $CoreMapping) { $CoreMapping = Get-DefaultCoreMapping }

  $items = [System.Collections.Generic.List[hashtable]]::new()

  foreach ($entry in $Entries) {
    $consoleKey = $entry.Console.ToLowerInvariant()
    $core = if ($CoreMapping.ContainsKey($consoleKey)) { $CoreMapping[$consoleKey] } else { 'DETECT' }

    $items.Add(@{
      path      = $entry.Path
      label     = $entry.Name
      core_path = $core
      core_name = ($core -replace '_libretro$', '')
      db_name   = "$PlaylistName.lpl"
    })
  }

  return @{
    version         = '1.5'
    default_core_path = ''
    default_core_name = ''
    label_display_mode = 0
    items           = ,$items.ToArray()
  }
}

function ConvertTo-EmulationStationGamelist {
  <#
  .SYNOPSIS
    Konvertiert Eintraege in EmulationStation gamelist.xml Format.
  #>
  param(
    [Parameter(Mandatory)][array]$Entries
  )

  $games = [System.Collections.Generic.List[hashtable]]::new()

  foreach ($entry in $Entries) {
    $games.Add(@{
      path  = "./$([System.IO.Path]::GetFileName($entry.Path))"
      name  = $entry.Name
      image = if ($entry.CoverPath) { $entry.CoverPath } else { '' }
    })
  }

  return ,$games.ToArray()
}

function ConvertTo-LaunchBoxXml {
  <#
  .SYNOPSIS
    Konvertiert Eintraege in LaunchBox XML-Struktur.
  #>
  param(
    [Parameter(Mandatory)][array]$Entries,
    [string]$Platform = ''
  )

  $games = [System.Collections.Generic.List[hashtable]]::new()

  foreach ($entry in $Entries) {
    $games.Add(@{
      Title            = $entry.Name
      ApplicationPath  = $entry.Path
      Platform         = if ($Platform) { $Platform } else { $entry.Console }
      Region           = $entry.Region
    })
  }

  return ,$games.ToArray()
}

function Export-LauncherData {
  <#
  .SYNOPSIS
    Exportiert Launcher-Daten im gewaehlten Format als Hashtable.
  #>
  param(
    [Parameter(Mandatory)][array]$Entries,
    [Parameter(Mandatory)][ValidateSet('retroarch','emulationstation','launchbox','playnite')]
    [string]$Format,
    [string]$PlaylistName = 'RomCleanup'
  )

  switch ($Format) {
    'retroarch'        { return ConvertTo-RetroArchPlaylist -Entries $Entries -PlaylistName $PlaylistName }
    'emulationstation' { return @{ Games = ConvertTo-EmulationStationGamelist -Entries $Entries } }
    'launchbox'        { return @{ Games = ConvertTo-LaunchBoxXml -Entries $Entries } }
    'playnite'         { return @{ Games = $Entries; Format = 'Playnite' } }
  }
}
