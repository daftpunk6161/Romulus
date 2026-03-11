#  CLONE LIST VISUALIZATION (LF-10)
#  Parent/Clone-Beziehungen als interaktiver Baum.

function Build-CloneTree {
  <#
  .SYNOPSIS
    Baut einen hierarchischen Baum aus Parent/Clone-Daten.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$DatIndex
  )

  $tree = @{}
  $orphans = [System.Collections.Generic.List[string]]::new()

  # Erst Parents identifizieren
  foreach ($key in $DatIndex.Keys) {
    $entry = $DatIndex[$key]
    $parent = if ($entry.ContainsKey('CloneOf') -and $entry.CloneOf) { $entry.CloneOf } else { '' }

    if (-not $parent -or $parent -eq $key) {
      # Ist Parent
      if (-not $tree.ContainsKey($key)) {
        $tree[$key] = @{
          Name     = if ($entry.ContainsKey('Name')) { $entry.Name } else { $key }
          Clones   = [System.Collections.Generic.List[hashtable]]::new()
          RomCount = if ($entry.ContainsKey('RomCount')) { $entry.RomCount } else { 0 }
        }
      }
    }
  }

  # Dann Clones zuordnen
  foreach ($key in $DatIndex.Keys) {
    $entry = $DatIndex[$key]
    $parent = if ($entry.ContainsKey('CloneOf') -and $entry.CloneOf) { $entry.CloneOf } else { '' }

    if ($parent -and $parent -ne $key) {
      if ($tree.ContainsKey($parent)) {
        $tree[$parent].Clones.Add(@{
          Key      = $key
          Name     = if ($entry.ContainsKey('Name')) { $entry.Name } else { $key }
          RomCount = if ($entry.ContainsKey('RomCount')) { $entry.RomCount } else { 0 }
        })
      } else {
        $orphans.Add($key)
      }
    }
  }

  return @{
    Tree       = $tree
    Orphans    = ,$orphans.ToArray()
    ParentCount = $tree.Count
    CloneCount  = ($tree.Values | ForEach-Object { $_.Clones.Count } | Measure-Object -Sum).Sum
    OrphanCount = $orphans.Count
  }
}

function Get-CloneTreeFlat {
  <#
  .SYNOPSIS
    Flacht den Clone-Tree fuer tabellarische Darstellung ab.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$TreeData
  )

  $rows = [System.Collections.Generic.List[hashtable]]::new()

  foreach ($parentKey in $TreeData.Tree.Keys) {
    $parent = $TreeData.Tree[$parentKey]
    $rows.Add(@{
      Key      = $parentKey
      Name     = $parent.Name
      Type     = 'Parent'
      ParentOf = ''
      Depth    = 0
      Clones   = $parent.Clones.Count
    })

    foreach ($clone in $parent.Clones) {
      $rows.Add(@{
        Key      = $clone.Key
        Name     = $clone.Name
        Type     = 'Clone'
        ParentOf = $parentKey
        Depth    = 1
        Clones   = 0
      })
    }
  }

  return ,$rows.ToArray()
}

function Search-CloneTree {
  <#
  .SYNOPSIS
    Sucht im Clone-Tree nach einem Namen.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$TreeData,
    [Parameter(Mandatory)][string]$Query
  )

  $queryLower = $Query.ToLowerInvariant()
  $results = [System.Collections.Generic.List[hashtable]]::new()

  foreach ($parentKey in $TreeData.Tree.Keys) {
    $parent = $TreeData.Tree[$parentKey]
    $parentMatch = $parent.Name.ToLowerInvariant() -like "*$queryLower*"

    if ($parentMatch) {
      $results.Add(@{
        Key      = $parentKey
        Name     = $parent.Name
        Type     = 'Parent'
        Clones   = $parent.Clones.Count
      })
    }

    foreach ($clone in $parent.Clones) {
      if ($clone.Name.ToLowerInvariant() -like "*$queryLower*") {
        $results.Add(@{
          Key       = $clone.Key
          Name      = $clone.Name
          Type      = 'Clone'
          ParentKey = $parentKey
        })
      }
    }
  }

  return ,$results.ToArray()
}

function Get-CloneTreeStatistics {
  <#
  .SYNOPSIS
    Statistik ueber den Clone-Tree.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$TreeData
  )

  $cloneCounts = @($TreeData.Tree.Values | ForEach-Object { $_.Clones.Count })
  $maxClones = if ($cloneCounts.Count -gt 0) { ($cloneCounts | Measure-Object -Maximum).Maximum } else { 0 }
  $avgClones = if ($cloneCounts.Count -gt 0) { [math]::Round(($cloneCounts | Measure-Object -Average).Average, 2) } else { 0 }
  $standalone = @($cloneCounts | Where-Object { $_ -eq 0 }).Count

  return @{
    Parents    = $TreeData.ParentCount
    Clones     = $TreeData.CloneCount
    Orphans    = $TreeData.OrphanCount
    MaxClones  = $maxClones
    AvgClones  = $avgClones
    Standalone = $standalone
  }
}
