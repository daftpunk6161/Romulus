# ================================================================
#  COMMAND PALETTE – Fuzzy-Search Command-Palette (MF-15)
#  Dependencies: keine (standalone)
# ================================================================

function Get-LevenshteinDistance {
  <#
  .SYNOPSIS
    Berechnet die Levenshtein-Distanz zwischen zwei Strings.
  .PARAMETER Source
    Quell-String.
  .PARAMETER Target
    Ziel-String.
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyString()][string]$Source,
    [Parameter(Mandatory)][AllowEmptyString()][string]$Target
  )

  if ($Source.Length -eq 0) { return $Target.Length }
  if ($Target.Length -eq 0) { return $Source.Length }

  $sLower = $Source.ToLowerInvariant()
  $tLower = $Target.ToLowerInvariant()

  $d = New-Object 'int[,]' ($sLower.Length + 1), ($tLower.Length + 1)

  for ($i = 0; $i -le $sLower.Length; $i++) { $d[$i, 0] = $i }
  for ($j = 0; $j -le $tLower.Length; $j++) { $d[0, $j] = $j }

  for ($i = 1; $i -le $sLower.Length; $i++) {
    for ($j = 1; $j -le $tLower.Length; $j++) {
      $cost = if ($sLower[$i - 1] -eq $tLower[$j - 1]) { 0 } else { 1 }
      $d[$i, $j] = [math]::Min(
        [math]::Min($d[($i - 1), $j] + 1, $d[$i, ($j - 1)] + 1),
        $d[($i - 1), ($j - 1)] + $cost
      )
    }
  }

  return $d[$sLower.Length, $tLower.Length]
}

function New-PaletteCommand {
  <#
  .SYNOPSIS
    Erstellt einen Command-Palette-Eintrag.
  .PARAMETER Name
    Anzeigename des Befehls.
  .PARAMETER Key
    Eindeutiger Schluessel.
  .PARAMETER Category
    Kategorie (z.B. 'Run', 'Settings', 'Convert').
  .PARAMETER Shortcut
    Optionaler Tastenkombination-String.
  #>
  param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][string]$Key,
    [string]$Category = 'General',
    [string]$Shortcut
  )

  return @{
    Name     = $Name
    Key      = $Key
    Category = $Category
    Shortcut = $Shortcut
  }
}

function Search-PaletteCommands {
  <#
  .SYNOPSIS
    Sucht Commands anhand eines Suchbegriffs (Substring + Levenshtein).
  .PARAMETER Query
    Suchbegriff.
  .PARAMETER Commands
    Array von Command-Eintraegen.
  .PARAMETER MaxDistance
    Maximale Levenshtein-Distanz fuer Fuzzy-Match.
  .PARAMETER MaxResults
    Maximale Anzahl Ergebnisse.
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyString()][string]$Query,
    [Parameter(Mandatory)][object[]]$Commands,
    [int]$MaxDistance = 3,
    [int]$MaxResults = 10
  )

  if (-not $Query -or $Query.Length -eq 0) {
    return @($Commands | Select-Object -First $MaxResults)
  }

  $queryLower = $Query.ToLowerInvariant()
  $scored = @()

  foreach ($cmd in $Commands) {
    $nameLower = $cmd.Name.ToLowerInvariant()

    # Exakter Substring-Match: hoechste Prioritaet
    if ($nameLower -like "*$queryLower*") {
      $scored += @{ Command = $cmd; Score = 0; MatchType = 'Substring' }
      continue
    }

    # Key-Match
    $keyLower = $cmd.Key.ToLowerInvariant()
    if ($keyLower -like "*$queryLower*") {
      $scored += @{ Command = $cmd; Score = 1; MatchType = 'KeyMatch' }
      continue
    }

    # Fuzzy-Match via Levenshtein auf einzelne Woerter
    $words = $nameLower -split '\s+'
    $minDist = [int]::MaxValue
    foreach ($word in $words) {
      $dist = Get-LevenshteinDistance -Source $queryLower -Target $word
      if ($dist -lt $minDist) { $minDist = $dist }
    }

    if ($minDist -le $MaxDistance) {
      $scored += @{ Command = $cmd; Score = 2 + $minDist; MatchType = 'Fuzzy' }
    }
  }

  $sorted = @($scored | Sort-Object { $_.Score } | Select-Object -First $MaxResults)
  $results = @($sorted | ForEach-Object { $_.Command })
  return ,$results
}

function Get-DefaultPaletteCommands {
  <#
  .SYNOPSIS
    Gibt die Standard-Command-Palette-Eintraege zurueck.
  #>
  return @(
    (New-PaletteCommand -Name 'DryRun starten' -Key 'run.dryrun' -Category 'Run' -Shortcut 'Ctrl+Shift+D')
    (New-PaletteCommand -Name 'Move-Modus starten' -Key 'run.move' -Category 'Run' -Shortcut 'Ctrl+Shift+M')
    (New-PaletteCommand -Name 'Konvertierung starten' -Key 'convert.start' -Category 'Convert')
    (New-PaletteCommand -Name 'Settings oeffnen' -Key 'settings.open' -Category 'Settings' -Shortcut 'Ctrl+,')
    (New-PaletteCommand -Name 'DAT-Quellen aktualisieren' -Key 'dat.update' -Category 'DAT')
    (New-PaletteCommand -Name 'Sammlung exportieren' -Key 'export.collection' -Category 'Export')
    (New-PaletteCommand -Name 'Report anzeigen' -Key 'report.show' -Category 'Report')
    (New-PaletteCommand -Name 'Run-History anzeigen' -Key 'history.show' -Category 'History')
    (New-PaletteCommand -Name 'Abbrechen' -Key 'run.cancel' -Category 'Run' -Shortcut 'Escape')
    (New-PaletteCommand -Name 'Hilfe anzeigen' -Key 'help.show' -Category 'Help' -Shortcut 'F1')
  )
}
