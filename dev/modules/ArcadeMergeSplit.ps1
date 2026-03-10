#  ARCADE ROM MERGE/SPLIT (LF-07)
#  Non-Merged / Split / Merged-Set-Konvertierung fuer MAME/FBNEO.

function Get-ArcadeSetTypes {
  <#
  .SYNOPSIS
    Gibt die unterstuetzten Arcade-Set-Typen zurueck.
  #>
  return @(
    @{ Key = 'non-merged'; Name = 'Non-Merged'; Description = 'Jedes ROM-Set enthaelt alle benoetigten Dateien' }
    @{ Key = 'split';      Name = 'Split';      Description = 'Clones enthalten nur eigene Dateien, Parent separat' }
    @{ Key = 'merged';     Name = 'Merged';     Description = 'Parent + Clones in einer ZIP-Datei' }
  )
}

function Read-ArcadeDatParentClone {
  <#
  .SYNOPSIS
    Parsed Parent/Clone-Beziehungen aus einem DAT-Index.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$DatIndex
  )

  $parentMap = @{}
  $cloneMap  = @{}

  foreach ($key in $DatIndex.Keys) {
    $entry = $DatIndex[$key]
    $parent = if ($entry.ContainsKey('CloneOf')) { $entry.CloneOf } else { '' }

    if ($parent -and $parent -ne $key) {
      # Clone
      if (-not $cloneMap.ContainsKey($parent)) { $cloneMap[$parent] = [System.Collections.Generic.List[string]]::new() }
      $cloneMap[$parent].Add($key)
    } else {
      # Parent
      if (-not $parentMap.ContainsKey($key)) { $parentMap[$key] = $entry }
    }
  }

  return @{
    Parents  = $parentMap
    Clones   = $cloneMap
    ParentCount = $parentMap.Count
    CloneCount  = ($cloneMap.Values | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum
  }
}

function Get-ArcadeSetInfo {
  <#
  .SYNOPSIS
    Analysiert ein ZIP-Archiv und gibt die ROM-Dateien darin zurueck.
  #>
  param(
    [Parameter(Mandatory)][string]$ZipPath
  )

  if (-not (Test-Path -LiteralPath $ZipPath)) {
    return @{ Valid = $false; Error = 'Datei nicht gefunden'; Files = @() }
  }

  $name = [System.IO.Path]::GetFileNameWithoutExtension($ZipPath)
  return @{
    Valid    = $true
    SetName  = $name
    Path     = $ZipPath
    SizeBytes = (Get-Item -LiteralPath $ZipPath).Length
    Files    = @()  # In der echten Implementierung: ZIP-Inhalt lesen
  }
}

function New-MergeOperation {
  <#
  .SYNOPSIS
    Erstellt eine Merge-Operation fuer Arcade-Sets.
  #>
  param(
    [Parameter(Mandatory)][string]$SourceType,
    [Parameter(Mandatory)][string]$TargetType,
    [Parameter(Mandatory)][array]$Sets,
    [hashtable]$ParentCloneMap = @{}
  )

  return @{
    SourceType     = $SourceType
    TargetType     = $TargetType
    Sets           = $Sets
    ParentCloneMap = $ParentCloneMap
    Status         = 'Pending'
    Processed      = 0
    Errors         = [System.Collections.Generic.List[string]]::new()
  }
}

function Get-MergePlan {
  <#
  .SYNOPSIS
    Erstellt einen Merge/Split-Plan basierend auf DAT-Daten.
  #>
  param(
    [Parameter(Mandatory)][string]$SourceType,
    [Parameter(Mandatory)][string]$TargetType,
    [Parameter(Mandatory)][array]$Sets,
    [Parameter(Mandatory)][hashtable]$DatIndex
  )

  $pcMap = Read-ArcadeDatParentClone -DatIndex $DatIndex

  $actions = [System.Collections.Generic.List[hashtable]]::new()

  foreach ($set in $Sets) {
    $setName = $set.SetName
    $isParent = $pcMap.Parents.ContainsKey($setName)
    $isClone = $false
    $parentName = ''

    foreach ($pKey in $pcMap.Clones.Keys) {
      if ($pcMap.Clones[$pKey] -contains $setName) {
        $isClone = $true
        $parentName = $pKey
        break
      }
    }

    $actions.Add(@{
      SetName    = $setName
      IsParent   = $isParent
      IsClone    = $isClone
      ParentName = $parentName
      Action     = "$SourceType->$TargetType"
    })
  }

  return @{
    Actions     = ,$actions.ToArray()
    SourceType  = $SourceType
    TargetType  = $TargetType
    TotalSets   = $Sets.Count
    ParentSets  = @($actions | Where-Object { $_.IsParent }).Count
    CloneSets   = @($actions | Where-Object { $_.IsClone }).Count
  }
}
