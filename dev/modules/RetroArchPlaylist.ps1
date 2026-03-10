# ================================================================
#  RETROARCH PLAYLIST – .lpl-Playlist-Export (QW-16)
#  Dependencies: Classification.ps1
# ================================================================

# Standard-Core-Mapping (Konsole → RetroArch Core + DB-Name)
$script:RETROARCH_CORE_MAP = @{
  'NES'    = @{ Core = 'mesen_libretro';       DB = 'Nintendo - Nintendo Entertainment System' }
  'SNES'   = @{ Core = 'snes9x_libretro';      DB = 'Nintendo - Super Nintendo Entertainment System' }
  'N64'    = @{ Core = 'mupen64plus_next_libretro'; DB = 'Nintendo - Nintendo 64' }
  'GB'     = @{ Core = 'gambatte_libretro';     DB = 'Nintendo - Game Boy' }
  'GBC'    = @{ Core = 'gambatte_libretro';     DB = 'Nintendo - Game Boy Color' }
  'GBA'    = @{ Core = 'mgba_libretro';         DB = 'Nintendo - Game Boy Advance' }
  'NDS'    = @{ Core = 'desmume_libretro';      DB = 'Nintendo - Nintendo DS' }
  'GC'     = @{ Core = 'dolphin_libretro';      DB = 'Nintendo - GameCube' }
  'WII'    = @{ Core = 'dolphin_libretro';      DB = 'Nintendo - Wii' }
  'MD'     = @{ Core = 'genesis_plus_gx_libretro'; DB = 'Sega - Mega Drive - Genesis' }
  'SMS'    = @{ Core = 'genesis_plus_gx_libretro'; DB = 'Sega - Master System - Mark III' }
  'GG'     = @{ Core = 'genesis_plus_gx_libretro'; DB = 'Sega - Game Gear' }
  'SAT'    = @{ Core = 'mednafen_saturn_libretro'; DB = 'Sega - Saturn' }
  'DC'     = @{ Core = 'flycast_libretro';      DB = 'Sega - Dreamcast' }
  'PS1'    = @{ Core = 'swanstation_libretro';  DB = 'Sony - PlayStation' }
  'PS2'    = @{ Core = 'pcsx2_libretro';        DB = 'Sony - PlayStation 2' }
  'PSP'    = @{ Core = 'ppsspp_libretro';       DB = 'Sony - PlayStation Portable' }
  'PCE'    = @{ Core = 'mednafen_pce_libretro'; DB = 'NEC - PC Engine - TurboGrafx 16' }
  'PCECD'  = @{ Core = 'mednafen_pce_libretro'; DB = 'NEC - PC Engine CD - TurboGrafx-CD' }
  'NEOGEO' = @{ Core = 'fbneo_libretro';        DB = 'SNK - Neo Geo' }
  'ARCADE' = @{ Core = 'fbneo_libretro';        DB = 'MAME' }
  'LYNX'   = @{ Core = 'handy_libretro';        DB = 'Atari - Lynx' }
  'JAGUAR' = @{ Core = 'virtualjaguar_libretro'; DB = 'Atari - Jaguar' }
  'WSWAN'  = @{ Core = 'mednafen_wswan_libretro'; DB = 'Bandai - WonderSwan' }
  'WSWANC' = @{ Core = 'mednafen_wswan_libretro'; DB = 'Bandai - WonderSwan Color' }
  'NGP'    = @{ Core = 'mednafen_ngp_libretro'; DB = 'SNK - Neo Geo Pocket' }
  'NGPC'   = @{ Core = 'mednafen_ngp_libretro'; DB = 'SNK - Neo Geo Pocket Color' }
  'VECTREX' = @{ Core = 'vecx_libretro';        DB = 'GCE - Vectrex' }
  'SCD'    = @{ Core = 'genesis_plus_gx_libretro'; DB = 'Sega - Mega-CD - Sega CD' }
  '3DO'    = @{ Core = 'opera_libretro';        DB = 'The 3DO Company - 3DO' }
}

function Get-RetroArchCoreMapping {
  <#
  .SYNOPSIS
    Gibt das RetroArch-Core-Mapping fuer eine Konsole zurueck.
  .PARAMETER ConsoleKey
    Konsolen-Schluessel (z.B. SNES, PS1).
  .PARAMETER CustomMappings
    Optionale benutzerdefinierte Mappings (ueberschreiben Standard).
  #>
  param(
    [Parameter(Mandatory)][string]$ConsoleKey,
    [hashtable]$CustomMappings
  )

  $key = $ConsoleKey.Trim().ToUpperInvariant()

  if ($CustomMappings -and $CustomMappings.ContainsKey($key)) {
    return $CustomMappings[$key]
  }

  if ($script:RETROARCH_CORE_MAP.ContainsKey($key)) {
    return $script:RETROARCH_CORE_MAP[$key]
  }

  return @{ Core = 'DETECT'; DB = 'DETECT' }
}

function Export-RetroArchPlaylist {
  <#
  .SYNOPSIS
    Exportiert eine ROM-Sammlung als RetroArch .lpl-Playlist.
  .PARAMETER Items
    Array von ROM-Eintraegen (braucht Path/MainPath, Console/ConsoleType, Name).
  .PARAMETER OutputPath
    Pfad fuer die Ausgabe .lpl-Datei.
  .PARAMETER CustomCoreMappings
    Optionale benutzerdefinierte Core-Mappings.
  .PARAMETER Log
    Optionaler Logging-Callback.
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Items,
    [Parameter(Mandatory)][string]$OutputPath,
    [hashtable]$CustomCoreMappings,
    [scriptblock]$Log
  )

  $result = @{
    OutputPath = $OutputPath
    ItemCount  = 0
    Warnings   = [System.Collections.Generic.List[string]]::new()
    Status     = 'Unknown'
  }

  if ($Items.Count -eq 0) {
    $result.Status = 'Empty'
    return $result
  }

  $playlistItems = [System.Collections.Generic.List[hashtable]]::new()

  foreach ($item in $Items) {
    # Pfad extrahieren
    $path = ''
    $console = ''
    $label = ''

    if ($item -is [hashtable]) {
      $path = if ($item.ContainsKey('Path')) { $item.Path }
              elseif ($item.ContainsKey('MainPath')) { $item.MainPath }
              else { '' }
      $console = if ($item.ContainsKey('Console')) { $item.Console }
                 elseif ($item.ContainsKey('ConsoleType')) { $item.ConsoleType }
                 else { '' }
      $label = if ($item.ContainsKey('Name')) { $item.Name }
               elseif ($item.ContainsKey('Label')) { $item.Label }
               else { '' }
    } else {
      $path = if ($item.PSObject.Properties.Name -contains 'Path') { $item.Path }
              elseif ($item.PSObject.Properties.Name -contains 'MainPath') { $item.MainPath }
              else { '' }
      $console = if ($item.PSObject.Properties.Name -contains 'Console') { $item.Console }
                 elseif ($item.PSObject.Properties.Name -contains 'ConsoleType') { $item.ConsoleType }
                 else { '' }
      $label = if ($item.PSObject.Properties.Name -contains 'Name') { $item.Name }
               elseif ($item.PSObject.Properties.Name -contains 'Label') { $item.Label }
               else { '' }
    }

    if ([string]::IsNullOrWhiteSpace($path)) { continue }

    if ([string]::IsNullOrWhiteSpace($label)) {
      $label = [System.IO.Path]::GetFileNameWithoutExtension($path)
    }

    $coreMapping = Get-RetroArchCoreMapping -ConsoleKey $console -CustomMappings $CustomCoreMappings
    $dbName = $coreMapping.DB + '.lpl'

    if ($coreMapping.Core -eq 'DETECT' -and -not [string]::IsNullOrWhiteSpace($console)) {
      [void]$result.Warnings.Add("Kein Core-Mapping fuer Konsole: $console")
    }

    [void]$playlistItems.Add(@{
      path      = [string]$path
      label     = [string]$label
      core_path = 'DETECT'
      core_name = 'DETECT'
      crc32     = 'DETECT'
      db_name   = $dbName
    })
  }

  $result.ItemCount = $playlistItems.Count

  # RetroArch LPL JSON-Format
  $playlist = @{
    version           = '1.5'
    default_core_path = ''
    default_core_name = ''
    items             = @($playlistItems)
  }

  try {
    $dir = Split-Path -Parent $OutputPath
    if ($dir -and -not (Test-Path -LiteralPath $dir -PathType Container)) {
      New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    $json = $playlist | ConvertTo-Json -Depth 5
    $json | Out-File -LiteralPath $OutputPath -Encoding utf8 -Force

    $result.Status = 'Success'
    if ($Log) { & $Log ("RetroArch-Playlist exportiert: {0} ({1} Eintraege)" -f $OutputPath, $playlistItems.Count) }
  } catch {
    $result.Status = 'Error'
    if ($Log) { & $Log ("RetroArch-Export Fehler: {0}" -f $_.Exception.Message) }
  }

  return $result
}
