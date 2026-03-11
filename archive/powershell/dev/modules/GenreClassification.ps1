#  GENRE/TAG CLASSIFICATION (LF-02)
#  Automatische Genre-Erkennung aus DAT-Metadaten oder manuellen Tags.

function Get-GenreTaxonomy {
  <#
  .SYNOPSIS
    Gibt die Standard-Genre-Taxonomie zurueck.
  #>
  return @(
    @{ Key = 'action';      Name = 'Action';           Parent = '' }
    @{ Key = 'platformer';  Name = 'Platformer';       Parent = 'action' }
    @{ Key = 'shooter';     Name = 'Shooter';          Parent = 'action' }
    @{ Key = 'rpg';         Name = 'RPG';              Parent = '' }
    @{ Key = 'jrpg';        Name = 'JRPG';             Parent = 'rpg' }
    @{ Key = 'wrpg';        Name = 'Western RPG';      Parent = 'rpg' }
    @{ Key = 'strategy';    Name = 'Strategy';         Parent = '' }
    @{ Key = 'puzzle';      Name = 'Puzzle';           Parent = '' }
    @{ Key = 'racing';      Name = 'Racing';           Parent = '' }
    @{ Key = 'sports';      Name = 'Sports';           Parent = '' }
    @{ Key = 'adventure';   Name = 'Adventure';        Parent = '' }
    @{ Key = 'fighting';    Name = 'Fighting';         Parent = '' }
    @{ Key = 'simulation';  Name = 'Simulation';       Parent = '' }
    @{ Key = 'other';       Name = 'Other';            Parent = '' }
  )
}

function New-GenreTag {
  <#
  .SYNOPSIS
    Erstellt ein Genre-Tag-Objekt.
  #>
  param(
    [Parameter(Mandatory)][string]$GameName,
    [Parameter(Mandatory)][string]$Genre,
    [string]$Source = 'manual',
    [string[]]$Tags = @()
  )

  return @{
    GameName = $GameName
    Genre    = $Genre
    Source   = $Source
    Tags     = $Tags
  }
}

function Find-GenreByKeyword {
  <#
  .SYNOPSIS
    Erkennt Genre aus Spielname via Keyword-Matching.
  #>
  param(
    [Parameter(Mandatory)][string]$GameName
  )

  $nameLower = $GameName.ToLowerInvariant()

  $patterns = @(
    @{ Genre = 'rpg';       Keywords = @('rpg', 'quest', 'fantasy', 'dragon') }
    @{ Genre = 'racing';    Keywords = @('race', 'racing', 'rally', 'kart', 'grand prix') }
    @{ Genre = 'sports';    Keywords = @('soccer', 'football', 'baseball', 'basketball', 'tennis', 'golf', 'fifa', 'nba', 'nfl', 'nhl') }
    @{ Genre = 'fighting';  Keywords = @('fighter', 'fighting', 'street fighter', 'mortal kombat', 'tekken') }
    @{ Genre = 'puzzle';    Keywords = @('puzzle', 'tetris', 'columns') }
    @{ Genre = 'shooter';   Keywords = @('shooter', 'gun', 'strike', 'warfare') }
    @{ Genre = 'platformer';Keywords = @('mario', 'sonic', 'jump', 'platform') }
  )

  foreach ($p in $patterns) {
    foreach ($kw in $p.Keywords) {
      if ($nameLower -like "*$kw*") {
        return $p.Genre
      }
    }
  }

  return 'other'
}

function Set-GameGenre {
  <#
  .SYNOPSIS
    Setzt oder aktualisiert das Genre fuer ein Spiel in einer Genre-Map.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$GenreMap,
    [Parameter(Mandatory)][string]$GameName,
    [Parameter(Mandatory)][string]$Genre,
    [string]$Source = 'manual',
    [string[]]$Tags = @()
  )

  $GenreMap[$GameName] = New-GenreTag -GameName $GameName -Genre $Genre -Source $Source -Tags $Tags
  return $GenreMap
}

function Get-GenreStatistics {
  <#
  .SYNOPSIS
    Statistik ueber Genre-Verteilung.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$GenreMap
  )

  $stats = @{}
  foreach ($key in $GenreMap.Keys) {
    $genre = $GenreMap[$key].Genre
    if (-not $stats.ContainsKey($genre)) {
      $stats[$genre] = 0
    }
    $stats[$genre]++
  }

  return @{
    Total      = $GenreMap.Count
    ByGenre    = $stats
    TopGenre   = if ($stats.Count -gt 0) {
      ($stats.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1).Key
    } else { '' }
  }
}

function Invoke-AutoGenreClassification {
  <#
  .SYNOPSIS
    Klassifiziert eine Liste von Spielen automatisch per Keyword-Analyse.
  #>
  param(
    [Parameter(Mandatory)][string[]]$GameNames
  )

  $map = @{}
  foreach ($name in $GameNames) {
    $genre = Find-GenreByKeyword -GameName $name
    $map[$name] = New-GenreTag -GameName $name -Genre $genre -Source 'auto-keyword'
  }

  return $map
}
