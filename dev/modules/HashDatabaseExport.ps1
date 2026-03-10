#  HASH DATABASE EXPORT (LF-11)
#  Alle Hashes als portable SQLite-DB oder JSON exportieren.

function New-HashDatabase {
  <#
  .SYNOPSIS
    Erstellt eine neue Hash-Datenbank (in-memory als Hashtable).
  #>
  param(
    [string]$Name = 'RomCleanup',
    [string]$HashType = 'SHA1'
  )

  return @{
    Name      = $Name
    HashType  = $HashType
    Created   = (Get-Date).ToString('o')
    Entries   = @{}
    Version   = '1.0'
  }
}

function Add-HashEntry {
  <#
  .SYNOPSIS
    Fuegt einen Hash-Eintrag hinzu.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Database,
    [Parameter(Mandatory)][string]$FilePath,
    [Parameter(Mandatory)][string]$Hash,
    [long]$Size = 0,
    [string]$Console = '',
    [string]$GameName = ''
  )

  $Database.Entries[$Hash] = @{
    FilePath = $FilePath
    Hash     = $Hash
    Size     = $Size
    Console  = $Console
    GameName = $GameName
    Added    = (Get-Date).ToString('o')
  }
  return $Database
}

function Export-HashDatabaseJson {
  <#
  .SYNOPSIS
    Exportiert die Hash-DB als JSON-String.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Database
  )

  $export = @{
    name     = $Database.Name
    hashType = $Database.HashType
    created  = $Database.Created
    version  = $Database.Version
    count    = $Database.Entries.Count
    entries  = @()
  }

  $entryList = [System.Collections.Generic.List[hashtable]]::new()
  foreach ($key in $Database.Entries.Keys) {
    $e = $Database.Entries[$key]
    $entryList.Add(@{
      hash     = $e.Hash
      file     = $e.FilePath
      size     = $e.Size
      console  = $e.Console
      game     = $e.GameName
    })
  }

  $export.entries = $entryList.ToArray()
  return ($export | ConvertTo-Json -Depth 5 -Compress)
}

function Export-HashDatabaseCsv {
  <#
  .SYNOPSIS
    Exportiert die Hash-DB als CSV-String (injection-safe).
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Database
  )

  $sb = [System.Text.StringBuilder]::new()
  [void]$sb.AppendLine('Hash,FilePath,Size,Console,GameName')

  foreach ($key in $Database.Entries.Keys) {
    $e = $Database.Entries[$key]
    # CSV-Injection-Schutz: fuehrende dangerous chars escapen
    $safePath = $e.FilePath
    $safeName = $e.GameName
    if ($safePath -match '^[=+\-@]') { $safePath = "'$safePath" }
    if ($safeName -match '^[=+\-@]') { $safeName = "'$safeName" }

    [void]$sb.AppendLine("$($e.Hash),`"$safePath`",$($e.Size),$($e.Console),`"$safeName`"")
  }

  return $sb.ToString()
}

function Import-HashDatabaseJson {
  <#
  .SYNOPSIS
    Importiert eine Hash-DB aus einem JSON-String.
  #>
  param(
    [Parameter(Mandatory)][string]$JsonString
  )

  $data = $JsonString | ConvertFrom-Json
  $db = New-HashDatabase -Name $data.name -HashType $data.hashType

  foreach ($entry in $data.entries) {
    [void](Add-HashEntry -Database $db -FilePath $entry.file -Hash $entry.hash -Size $entry.size -Console $entry.console -GameName $entry.game)
  }

  return $db
}

function Get-HashDatabaseStatistics {
  <#
  .SYNOPSIS
    Statistik ueber die Hash-Datenbank.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Database
  )

  $entries = @($Database.Entries.Values)
  $byConsole = @{}
  foreach ($e in $entries) {
    $c = if ($e.Console) { $e.Console } else { 'Unknown' }
    if (-not $byConsole.ContainsKey($c)) { $byConsole[$c] = 0 }
    $byConsole[$c]++
  }

  $totalSize = ($entries | ForEach-Object { $_.Size } | Measure-Object -Sum).Sum

  return @{
    TotalEntries = $entries.Count
    TotalSize    = $totalSize
    ByConsole    = $byConsole
    HashType     = $Database.HashType
  }
}
