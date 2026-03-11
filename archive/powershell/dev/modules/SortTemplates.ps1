# ================================================================
#  SORT TEMPLATES – Ordnerstruktur-Vorlagen (MF-22)
#  Dependencies: ConsoleSort.ps1, consoles.json
# ================================================================

function Get-DefaultSortTemplates {
  <#
  .SYNOPSIS
    Gibt die vordefinierten Ordnerstruktur-Vorlagen zurueck.
  #>
  return @{
    RetroArch = @{
      Name     = 'RetroArch'
      Pattern  = '{console}/{filename}'
      ConsoleMappings = @{
        SNES       = 'Nintendo - Super Nintendo Entertainment System'
        NES        = 'Nintendo - Nintendo Entertainment System'
        GBA        = 'Nintendo - Game Boy Advance'
        GB         = 'Nintendo - Game Boy'
        GBC        = 'Nintendo - Game Boy Color'
        N64        = 'Nintendo - Nintendo 64'
        GC         = 'Nintendo - GameCube'
        Wii        = 'Nintendo - Wii'
        MegaDrive  = 'Sega - Mega Drive - Genesis'
        Saturn     = 'Sega - Saturn'
        Dreamcast  = 'Sega - Dreamcast'
        PS1        = 'Sony - PlayStation'
        PS2        = 'Sony - PlayStation 2'
        PSP        = 'Sony - PlayStation Portable'
      }
    }
    EmulationStation = @{
      Name     = 'EmulationStation'
      Pattern  = 'roms/{console_lower}/{filename}'
      ConsoleMappings = @{
        SNES      = 'snes'
        NES       = 'nes'
        GBA       = 'gba'
        GB        = 'gb'
        GBC       = 'gbc'
        N64       = 'n64'
        GC        = 'gc'
        Wii       = 'wii'
        MegaDrive = 'megadrive'
        Saturn    = 'saturn'
        Dreamcast = 'dreamcast'
        PS1       = 'psx'
        PS2       = 'ps2'
        PSP       = 'psp'
      }
    }
    LaunchBox = @{
      Name     = 'LaunchBox'
      Pattern  = 'Games/{console}/{filename}'
      ConsoleMappings = @{
        SNES      = 'Super Nintendo Entertainment System'
        NES       = 'Nintendo Entertainment System'
        GBA       = 'Nintendo Game Boy Advance'
        N64       = 'Nintendo 64'
        PS1       = 'Sony Playstation'
        PS2       = 'Sony Playstation 2'
        MegaDrive = 'Sega Genesis'
        Dreamcast = 'Sega Dreamcast'
      }
    }
    Batocera = @{
      Name     = 'Batocera'
      Pattern  = 'share/roms/{console_lower}/{filename}'
      ConsoleMappings = @{
        SNES      = 'snes'
        NES       = 'nes'
        GBA       = 'gba'
        N64       = 'n64'
        PS1       = 'psx'
        PS2       = 'ps2'
        MegaDrive = 'megadrive'
        Dreamcast = 'dreamcast'
        Saturn    = 'saturn'
      }
    }
    Flat = @{
      Name     = 'Flat (alles in einem Ordner)'
      Pattern  = '{filename}'
      ConsoleMappings = @{}
    }
  }
}

function Resolve-SortTemplatePath {
  <#
  .SYNOPSIS
    Loest einen Template-Pattern-Pfad fuer eine konkrete Datei auf.
  .PARAMETER TemplateName
    Name des Templates (RetroArch, EmulationStation, etc.).
  .PARAMETER ConsoleKey
    Konsolen-Schluessel.
  .PARAMETER FileName
    Dateiname.
  .PARAMETER OutputRoot
    Basis-Ausgabeverzeichnis.
  .PARAMETER CustomTemplates
    Optionale benutzerdefinierte Templates.
  #>
  param(
    [Parameter(Mandatory)][string]$TemplateName,
    [Parameter(Mandatory)][string]$ConsoleKey,
    [Parameter(Mandatory)][string]$FileName,
    [Parameter(Mandatory)][string]$OutputRoot,
    [hashtable]$CustomTemplates
  )

  $templates = Get-DefaultSortTemplates
  if ($CustomTemplates) {
    foreach ($key in $CustomTemplates.Keys) {
      $templates[$key] = $CustomTemplates[$key]
    }
  }

  if (-not $templates.ContainsKey($TemplateName)) {
    return @{ Status = 'Error'; Reason = "Template '$TemplateName' nicht gefunden" }
  }

  $template = $templates[$TemplateName]
  $pattern = $template.Pattern

  # Console-Mapping aufloesen
  $consoleName = $ConsoleKey
  if ($template.ConsoleMappings.ContainsKey($ConsoleKey)) {
    $consoleName = $template.ConsoleMappings[$ConsoleKey]
  }

  # Platzhalter ersetzen
  $resolved = $pattern -replace '\{console\}', $consoleName
  $resolved = $resolved -replace '\{console_lower\}', ($consoleName.ToLowerInvariant())
  $resolved = $resolved -replace '\{filename\}', $FileName

  $fullPath = Join-Path $OutputRoot $resolved

  return @{
    Status      = 'OK'
    Path        = $fullPath
    Template    = $TemplateName
    ConsoleKey  = $ConsoleKey
    ConsoleName = $consoleName
  }
}

function Get-TemplateNames {
  <#
  .SYNOPSIS
    Gibt alle verfuegbaren Template-Namen zurueck.
  .PARAMETER CustomTemplates
    Optionale benutzerdefinierte Templates.
  #>
  param(
    [hashtable]$CustomTemplates
  )

  $templates = Get-DefaultSortTemplates
  if ($CustomTemplates) {
    foreach ($key in $CustomTemplates.Keys) {
      $templates[$key] = $CustomTemplates[$key]
    }
  }

  return @($templates.Keys | Sort-Object)
}

function Test-TemplateMappingComplete {
  <#
  .SYNOPSIS
    Prueft ob alle verwendeten Konsolen ein Mapping im Template haben.
  .PARAMETER TemplateName
    Name des Templates.
  .PARAMETER ConsoleKeys
    Array der verwendeten Konsolen-Keys.
  #>
  param(
    [Parameter(Mandatory)][string]$TemplateName,
    [Parameter(Mandatory)][string[]]$ConsoleKeys
  )

  $templates = Get-DefaultSortTemplates
  if (-not $templates.ContainsKey($TemplateName)) {
    return @{ Complete = $false; Missing = $ConsoleKeys; Reason = 'TemplateNotFound' }
  }

  $mappings = $templates[$TemplateName].ConsoleMappings
  $missing = @($ConsoleKeys | Where-Object { -not $mappings.ContainsKey($_) })

  return @{
    Complete = ($missing.Count -eq 0)
    Missing  = $missing
    Mapped   = @($ConsoleKeys | Where-Object { $mappings.ContainsKey($_) })
  }
}
