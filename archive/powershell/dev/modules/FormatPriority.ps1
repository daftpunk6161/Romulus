# ================================================================
#  FORMAT PRIORITY – Konfigurierbare Zielformat-Hierarchie (MF-10)
#  Dependencies: FormatScoring.ps1, Settings.ps1
# ================================================================

function Get-DefaultFormatPriority {
  <#
  .SYNOPSIS
    Gibt die Standard-Format-Prioritaeten pro Konsole zurueck.
  #>
  return @{
    PS1       = @('CHD', 'BIN/CUE', 'PBP', 'CSO', 'ISO')
    PS2       = @('CHD', 'ISO')
    PSP       = @('CHD', 'CSO', 'ISO')
    GC        = @('RVZ', 'ISO', 'NKit')
    Wii       = @('RVZ', 'ISO', 'NKit')
    Saturn    = @('CHD', 'BIN/CUE', 'ISO')
    Dreamcast = @('CHD', 'GDI', 'ISO')
    NES       = @('ZIP', '7Z', 'NES')
    SNES      = @('ZIP', '7Z', 'SFC', 'SMC')
    GBA       = @('ZIP', '7Z', 'GBA')
    N64       = @('ZIP', '7Z', 'Z64', 'N64')
    MegaDrive = @('ZIP', '7Z', 'MD', 'BIN')
  }
}

function Get-FormatPriority {
  <#
  .SYNOPSIS
    Gibt die Format-Prioritaet fuer eine bestimmte Konsole zurueck.
  .PARAMETER ConsoleKey
    Konsolen-Schluessel.
  .PARAMETER UserPriority
    Optionale User-definierte Prioritaeten (ueberschreibt Default).
  #>
  param(
    [Parameter(Mandatory)][string]$ConsoleKey,
    [hashtable]$UserPriority
  )

  $defaults = Get-DefaultFormatPriority

  if ($UserPriority -and $UserPriority.ContainsKey($ConsoleKey)) {
    return @($UserPriority[$ConsoleKey])
  }

  if ($defaults.ContainsKey($ConsoleKey)) {
    return @($defaults[$ConsoleKey])
  }

  # Fallback: generische Sortierung
  return @('ZIP', '7Z')
}

function Get-FormatPriorityScore {
  <#
  .SYNOPSIS
    Berechnet einen Score basierend auf der Position in der Prioritaetsliste.
  .PARAMETER Format
    Dateiformat (z.B. 'CHD', 'ISO').
  .PARAMETER ConsoleKey
    Konsolen-Schluessel.
  .PARAMETER UserPriority
    Optionale User-definierte Prioritaeten.
  #>
  param(
    [Parameter(Mandatory)][string]$Format,
    [Parameter(Mandatory)][string]$ConsoleKey,
    [hashtable]$UserPriority
  )

  $priority = Get-FormatPriority -ConsoleKey $ConsoleKey -UserPriority $UserPriority
  $formatUpper = $Format.ToUpperInvariant()

  $index = -1
  for ($i = 0; $i -lt $priority.Count; $i++) {
    if ($priority[$i].ToUpperInvariant() -eq $formatUpper) {
      $index = $i
      break
    }
  }

  if ($index -lt 0) {
    return 0  # Format nicht in Liste = niedrigster Score
  }

  # Hoechste Prioritaet = hoechster Score
  return ($priority.Count - $index) * 100
}

function Test-FormatPreferred {
  <#
  .SYNOPSIS
    Prueft ob Format A gegenueber Format B bevorzugt wird.
  .PARAMETER FormatA
    Erstes Format.
  .PARAMETER FormatB
    Zweites Format.
  .PARAMETER ConsoleKey
    Konsolen-Schluessel.
  .PARAMETER UserPriority
    Optionale User-definierte Prioritaeten.
  #>
  param(
    [Parameter(Mandatory)][string]$FormatA,
    [Parameter(Mandatory)][string]$FormatB,
    [Parameter(Mandatory)][string]$ConsoleKey,
    [hashtable]$UserPriority
  )

  $scoreA = Get-FormatPriorityScore -Format $FormatA -ConsoleKey $ConsoleKey -UserPriority $UserPriority
  $scoreB = Get-FormatPriorityScore -Format $FormatB -ConsoleKey $ConsoleKey -UserPriority $UserPriority

  return @{
    Preferred  = if ($scoreA -ge $scoreB) { $FormatA } else { $FormatB }
    ScoreA     = $scoreA
    ScoreB     = $scoreB
    APreferred = ($scoreA -ge $scoreB)
  }
}

function Merge-FormatPriority {
  <#
  .SYNOPSIS
    Merged User-Prioritaeten mit Default-Prioritaeten.
  .PARAMETER UserPriority
    User-definierte Prioritaeten.
  #>
  param(
    [hashtable]$UserPriority
  )

  $defaults = Get-DefaultFormatPriority
  $merged = @{}

  foreach ($key in $defaults.Keys) {
    $merged[$key] = $defaults[$key]
  }

  if ($UserPriority) {
    foreach ($key in $UserPriority.Keys) {
      $merged[$key] = $UserPriority[$key]
    }
  }

  return $merged
}
