#  CUSTOM DAT EDITOR (LF-09)
#  Eigene DAT-Dateien erstellen/editieren fuer private Sammlungen/Homebrew.

function New-CustomDat {
  <#
  .SYNOPSIS
    Erstellt ein neues leeres Custom-DAT.
  #>
  param(
    [Parameter(Mandatory)][string]$Name,
    [string]$Description = '',
    [string]$Author = '',
    [string]$Version = '1.0'
  )

  return @{
    Header = @{
      Name        = $Name
      Description = $Description
      Author      = $Author
      Version     = $Version
      Date        = (Get-Date).ToString('yyyy-MM-dd')
    }
    Games = @{}
  }
}

function Add-CustomDatEntry {
  <#
  .SYNOPSIS
    Fuegt einen Eintrag zum Custom-DAT hinzu.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Dat,
    [Parameter(Mandatory)][string]$GameName,
    [Parameter(Mandatory)][string]$FileName,
    [string]$Hash = '',
    [string]$HashType = 'SHA1',
    [long]$Size = 0,
    [string]$Region = '',
    [string]$Description = ''
  )

  $entry = @{
    GameName    = $GameName
    FileName    = $FileName
    Hash        = $Hash
    HashType    = $HashType
    Size        = $Size
    Region      = $Region
    Description = $Description
  }

  $key = if ($Hash) { $Hash } else { $FileName }
  $Dat.Games[$key] = $entry
  return $Dat
}

function Remove-CustomDatEntry {
  <#
  .SYNOPSIS
    Entfernt einen Eintrag aus dem Custom-DAT.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Dat,
    [Parameter(Mandatory)][string]$Key
  )

  if ($Dat.Games.ContainsKey($Key)) {
    $Dat.Games.Remove($Key)
  }
  return $Dat
}

function Find-CustomDatEntry {
  <#
  .SYNOPSIS
    Sucht Eintraege im Custom-DAT nach Name oder Hash.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Dat,
    [string]$Query = ''
  )

  if (-not $Query) {
    return ,@($Dat.Games.Values)
  }

  $queryLower = $Query.ToLowerInvariant()
  $results = [System.Collections.Generic.List[hashtable]]::new()

  foreach ($key in $Dat.Games.Keys) {
    $entry = $Dat.Games[$key]
    if ($entry.GameName.ToLowerInvariant() -like "*$queryLower*" -or
        $entry.FileName.ToLowerInvariant() -like "*$queryLower*" -or
        $key -like "*$queryLower*") {
      $results.Add($entry)
    }
  }

  return ,$results.ToArray()
}

function ConvertTo-DatXml {
  <#
  .SYNOPSIS
    Konvertiert Custom-DAT in XML-String (Logiqx-Format).
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Dat
  )

  $sb = [System.Text.StringBuilder]::new()
  [void]$sb.AppendLine('<?xml version="1.0" encoding="UTF-8"?>')
  [void]$sb.AppendLine('<datafile>')
  [void]$sb.AppendLine("  <header>")
  [void]$sb.AppendLine("    <name>$([System.Security.SecurityElement]::Escape($Dat.Header.Name))</name>")
  [void]$sb.AppendLine("    <description>$([System.Security.SecurityElement]::Escape($Dat.Header.Description))</description>")
  [void]$sb.AppendLine("    <version>$([System.Security.SecurityElement]::Escape($Dat.Header.Version))</version>")
  [void]$sb.AppendLine("    <author>$([System.Security.SecurityElement]::Escape($Dat.Header.Author))</author>")
  [void]$sb.AppendLine("    <date>$($Dat.Header.Date)</date>")
  [void]$sb.AppendLine("  </header>")

  foreach ($key in $Dat.Games.Keys) {
    $game = $Dat.Games[$key]
    $safeName = [System.Security.SecurityElement]::Escape($game.GameName)
    $safeFile = [System.Security.SecurityElement]::Escape($game.FileName)
    [void]$sb.AppendLine("  <game name=`"$safeName`">")
    [void]$sb.AppendLine("    <rom name=`"$safeFile`" size=`"$($game.Size)`" sha1=`"$($game.Hash)`"/>")
    [void]$sb.AppendLine("  </game>")
  }

  [void]$sb.AppendLine('</datafile>')
  return $sb.ToString()
}

function Get-CustomDatStatistics {
  <#
  .SYNOPSIS
    Statistik ueber ein Custom-DAT.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Dat
  )

  $entries = @($Dat.Games.Values)
  $withHash = @($entries | Where-Object { $_.Hash -ne '' }).Count
  $totalSize = ($entries | ForEach-Object { $_.Size } | Measure-Object -Sum).Sum

  $regions = @{}
  foreach ($e in $entries) {
    $r = if ($e.Region) { $e.Region } else { 'Unknown' }
    if (-not $regions.ContainsKey($r)) { $regions[$r] = 0 }
    $regions[$r]++
  }

  return @{
    TotalEntries   = $entries.Count
    WithHash       = $withHash
    WithoutHash    = $entries.Count - $withHash
    TotalSizeBytes = $totalSize
    ByRegion       = $regions
  }
}
