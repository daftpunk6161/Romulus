# ================================================================
#  CLRMAMEPRO / ROMVAULT IMPORT (XL-12)
#  Datenbank-Import von anderen ROM-Management-Tools
# ================================================================

function Get-SupportedImportFormats {
  <#
  .SYNOPSIS
    Gibt die unterstuetzten Import-Formate zurueck.
  #>

  return @(
    @{
      Key         = 'clrmamepro'
      Name        = 'clrmamepro'
      Extensions  = @('.dat', '.xml')
      Description = 'clrmamepro DAT/XML-Datenbank'
    }
    @{
      Key         = 'romvault'
      Name        = 'RomVault'
      Extensions  = @('.rvdb', '.xml')
      Description = 'RomVault Datenbank'
    }
    @{
      Key         = 'romcenter'
      Name        = 'RomCenter'
      Extensions  = @('.rc2', '.xml')
      Description = 'RomCenter Datenbank'
    }
    @{
      Key         = 'logiqx'
      Name        = 'Logiqx XML'
      Extensions  = @('.dat', '.xml')
      Description = 'Logiqx-kompatibles DAT-Format'
    }
  )
}

function New-ImportConfig {
  <#
  .SYNOPSIS
    Erstellt eine Import-Konfiguration.
  .PARAMETER SourceFormat
    Quell-Format.
  .PARAMETER SourcePath
    Pfad zur Import-Datei.
  .PARAMETER MergeMode
    Zusammenfuehrungsmodus.
  #>
  param(
    [Parameter(Mandatory)][ValidateSet('clrmamepro','romvault','romcenter','logiqx')][string]$SourceFormat,
    [Parameter(Mandatory)][string]$SourcePath,
    [ValidateSet('Replace','Merge','Append')][string]$MergeMode = 'Merge'
  )

  return @{
    SourceFormat = $SourceFormat
    SourcePath   = $SourcePath
    MergeMode    = $MergeMode
    ImportDate   = [datetime]::UtcNow.ToString('o')
    Status       = 'Pending'
    Errors       = @()
  }
}

function Read-ClrmameproDat {
  <#
  .SYNOPSIS
    Parst ein clrmamepro-DAT im Textformat.
  .PARAMETER Content
    DAT-Dateiinhalt als String.
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyString()][string]$Content
  )

  $entries = @()
  if ([string]::IsNullOrEmpty($Content)) {
    return @{ Entries = $entries; EntryCount = 0; Format = 'clrmamepro' }
  }
  $lines = $Content -split "`n"
  $currentGame = $null

  foreach ($line in $lines) {
    $trimmed = $line.Trim()

    if ($trimmed -match '^game\s*\(') {
      $currentGame = @{ Name = ''; Roms = @(); Description = '' }
    }
    elseif ($currentGame -and $trimmed -match '^\)') {
      if ($currentGame.Name) {
        $entries += $currentGame
      }
      $currentGame = $null
    }
    elseif ($currentGame) {
      if ($trimmed -match '^name\s+"?([^"]+)"?') {
        $currentGame.Name = $Matches[1].Trim()
      }
      elseif ($trimmed -match '^description\s+"?([^"]+)"?') {
        $currentGame.Description = $Matches[1].Trim()
      }
      elseif ($trimmed -match '^rom\s*\(.+name\s+"?([^"]+)"?') {
        $romEntry = @{ Name = $Matches[1].Trim() }
        if ($trimmed -match 'sha1\s+([0-9a-fA-F]+)') {
          $romEntry.SHA1 = $Matches[1]
        }
        if ($trimmed -match 'md5\s+([0-9a-fA-F]+)') {
          $romEntry.MD5 = $Matches[1]
        }
        if ($trimmed -match 'size\s+(\d+)') {
          $romEntry.Size = [long]$Matches[1]
        }
        $currentGame.Roms += $romEntry
      }
    }
  }

  return @{
    Entries    = $entries
    EntryCount = $entries.Count
    Format     = 'clrmamepro'
  }
}

function ConvertTo-RomCleanupIndex {
  <#
  .SYNOPSIS
    Konvertiert importierte Eintraege in das RomCleanup-Index-Format.
  .PARAMETER ImportResult
    Import-Ergebnis.
  .PARAMETER ConsoleKey
    Konsolen-Key.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$ImportResult,
    [string]$ConsoleKey = 'Unknown'
  )

  $index = @{}

  foreach ($entry in $ImportResult.Entries) {
    $key = $entry.Name
    $index[$key] = @{
      Name        = $entry.Name
      Description = $entry.Description
      ConsoleKey  = $ConsoleKey
      RomCount    = @($entry.Roms).Count
      Source       = $ImportResult.Format
    }
  }

  return @{
    Index      = $index
    EntryCount = $index.Count
    ConsoleKey = $ConsoleKey
    Source     = $ImportResult.Format
  }
}

function Get-ImportStatistics {
  <#
  .SYNOPSIS
    Gibt Statistiken ueber den Import zurueck.
  .PARAMETER ImportResult
    Import-Ergebnis.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$ImportResult
  )

  $totalRoms = 0
  foreach ($entry in $ImportResult.Entries) {
    $totalRoms += @($entry.Roms).Count
  }

  return @{
    Format      = $ImportResult.Format
    GameCount   = $ImportResult.EntryCount
    TotalRoms   = $totalRoms
    AvgRomsPerGame = if ($ImportResult.EntryCount -gt 0) { [math]::Round($totalRoms / $ImportResult.EntryCount, 1) } else { 0 }
  }
}
