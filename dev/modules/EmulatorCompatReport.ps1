# ================================================================
#  EMULATOR COMPATIBILITY REPORT – ROM-Emulator-Matrix (XL-07)
#  Kompatibilitaetsmatrix basierend auf Community-Listen
# ================================================================

function New-EmulatorProfile {
  <#
  .SYNOPSIS
    Erstellt ein Emulator-Profil.
  .PARAMETER Name
    Emulator-Name.
  .PARAMETER SupportedConsoles
    Unterstuetzte Konsolen-Keys.
  .PARAMETER SupportedFormats
    Unterstuetzte Dateiformate.
  .PARAMETER Platform
    Plattform (Windows/Linux/macOS/Android/Multi).
  #>
  param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][string[]]$SupportedConsoles,
    [string[]]$SupportedFormats = @(),
    [ValidateSet('Windows','Linux','macOS','Android','Multi')][string]$Platform = 'Multi'
  )

  return @{
    Name              = $Name
    SupportedConsoles = $SupportedConsoles
    SupportedFormats  = $SupportedFormats
    Platform          = $Platform
    CompatEntries     = @{}
  }
}

function Add-CompatibilityEntry {
  <#
  .SYNOPSIS
    Fuegt einen Kompatibilitaetseintrag zu einem Emulator-Profil hinzu.
  .PARAMETER Profile
    Emulator-Profil.
  .PARAMETER GameKey
    Game-Key des ROMs.
  .PARAMETER Status
    Kompatibilitaets-Status.
  .PARAMETER Notes
    Zusaetzliche Hinweise.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Profile,
    [Parameter(Mandatory)][string]$GameKey,
    [ValidateSet('Perfect','Playable','InGame','Menu','Loadable','Nothing','Untested')]
    [string]$Status = 'Untested',
    [string]$Notes = ''
  )

  $entry = @{
    GameKey   = $GameKey
    Status    = $Status
    Notes     = $Notes
    Emulator  = $Profile.Name
    Timestamp = [datetime]::UtcNow.ToString('o')
  }

  $Profile.CompatEntries[$GameKey] = $entry
  return $entry
}

function Get-CompatibilityMatrix {
  <#
  .SYNOPSIS
    Erstellt eine Kompatibilitaetsmatrix fuer eine Konsole.
  .PARAMETER Profiles
    Array von Emulator-Profilen.
  .PARAMETER ConsoleKey
    Konsolen-Key zum Filtern.
  #>
  param(
    [Parameter(Mandatory)][array]$Profiles,
    [Parameter(Mandatory)][string]$ConsoleKey
  )

  $relevantEmulators = @($Profiles | Where-Object { $_.SupportedConsoles -contains $ConsoleKey })

  $matrix = @{
    ConsoleKey  = $ConsoleKey
    Emulators   = @($relevantEmulators | ForEach-Object { $_.Name })
    Entries     = @{}
  }

  foreach ($emu in $relevantEmulators) {
    foreach ($key in $emu.CompatEntries.Keys) {
      if (-not $matrix.Entries.ContainsKey($key)) {
        $matrix.Entries[$key] = @{}
      }
      $matrix.Entries[$key][$emu.Name] = $emu.CompatEntries[$key].Status
    }
  }

  return $matrix
}

function Get-CompatibilityScore {
  <#
  .SYNOPSIS
    Berechnet einen numerischen Kompatibilitaets-Score.
  .PARAMETER Status
    Kompatibilitaets-Status.
  #>
  param(
    [Parameter(Mandatory)][ValidateSet('Perfect','Playable','InGame','Menu','Loadable','Nothing','Untested')]
    [string]$Status
  )

  $scores = @{
    'Perfect'  = 100
    'Playable' = 80
    'InGame'   = 60
    'Menu'     = 40
    'Loadable' = 20
    'Nothing'  = 0
    'Untested' = -1
  }

  return $scores[$Status]
}

function Get-BestEmulatorForGame {
  <#
  .SYNOPSIS
    Findet den besten Emulator fuer ein bestimmtes Spiel.
  .PARAMETER Matrix
    Kompatibilitaetsmatrix.
  .PARAMETER GameKey
    Game-Key.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Matrix,
    [Parameter(Mandatory)][string]$GameKey
  )

  if (-not $Matrix.Entries.ContainsKey($GameKey)) {
    return @{ GameKey = $GameKey; BestEmulator = $null; BestStatus = 'Untested'; Score = -1 }
  }

  $gameEntries = $Matrix.Entries[$GameKey]
  $bestEmu = $null
  $bestScore = -2

  foreach ($emu in $gameEntries.Keys) {
    $score = Get-CompatibilityScore -Status $gameEntries[$emu]
    if ($score -gt $bestScore) {
      $bestScore = $score
      $bestEmu = $emu
    }
  }

  return @{
    GameKey      = $GameKey
    BestEmulator = $bestEmu
    BestStatus   = if ($bestEmu) { $gameEntries[$bestEmu] } else { 'Untested' }
    Score        = $bestScore
  }
}

function Get-EmulatorCompatStatistics {
  <#
  .SYNOPSIS
    Gibt Statistiken ueber die Emulator-Kompatibilitaet zurueck.
  .PARAMETER Profiles
    Array von Emulator-Profilen.
  #>
  param(
    [Parameter(Mandatory)][array]$Profiles
  )

  $totalEntries = 0
  $consoles = @{}

  foreach ($p in $Profiles) {
    $totalEntries += $p.CompatEntries.Count
    foreach ($c in $p.SupportedConsoles) {
      if (-not $consoles.ContainsKey($c)) { $consoles[$c] = 0 }
      $consoles[$c]++
    }
  }

  return @{
    EmulatorCount  = @($Profiles).Count
    TotalEntries   = $totalEntries
    ConsoleCount   = $consoles.Count
    ConsoleCoverage = $consoles
  }
}
