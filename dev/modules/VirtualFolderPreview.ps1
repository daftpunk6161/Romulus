#  VIRTUAL FOLDER PREVIEW / TREEMAP (LF-12)
#  Sammlungsgroesse visualisiert nach Konsole/Region/Format als Treemap-Daten.

function Build-TreemapData {
  <#
  .SYNOPSIS
    Baut hierarchische Treemap-Daten aus ROM-Sammlung.
  #>
  param(
    [Parameter(Mandatory)][array]$Files,
    [ValidateSet('Console','Region','Format')]
    [string]$GroupBy = 'Console'
  )

  $groups = @{}

  foreach ($file in $Files) {
    $key = switch ($GroupBy) {
      'Console' { if ($file.ContainsKey('Console')) { $file.Console } else { 'Unknown' } }
      'Region'  { if ($file.ContainsKey('Region'))  { $file.Region }  else { 'Unknown' } }
      'Format'  { if ($file.ContainsKey('Format'))  { $file.Format }  else { 'Unknown' } }
    }

    if (-not $groups.ContainsKey($key)) {
      $groups[$key] = @{ Name = $key; Size = [long]0; Count = 0; Children = @() }
    }
    $fileSize = if ($file.ContainsKey('Size')) { $file.Size } else { 0 }
    $groups[$key].Size += $fileSize
    $groups[$key].Count++
  }

  $children = [System.Collections.Generic.List[hashtable]]::new()
  foreach ($key in $groups.Keys) {
    $children.Add($groups[$key])
  }

  return @{
    Name     = 'Root'
    GroupBy  = $GroupBy
    Size     = ($children | ForEach-Object { $_.Size } | Measure-Object -Sum).Sum
    Count    = $Files.Count
    Children = ,$children.ToArray()
  }
}

function Build-SunburstData {
  <#
  .SYNOPSIS
    Baut zweistufige Sunburst-Daten (Console > Region).
  #>
  param(
    [Parameter(Mandatory)][array]$Files
  )

  $consoles = @{}

  foreach ($file in $Files) {
    $console = if ($file.ContainsKey('Console')) { $file.Console } else { 'Unknown' }
    $region = if ($file.ContainsKey('Region')) { $file.Region } else { 'Unknown' }
    $size = if ($file.ContainsKey('Size')) { $file.Size } else { 0 }

    if (-not $consoles.ContainsKey($console)) {
      $consoles[$console] = @{ Name = $console; Regions = @{}; Size = [long]0; Count = 0 }
    }
    $consoles[$console].Size += $size
    $consoles[$console].Count++

    if (-not $consoles[$console].Regions.ContainsKey($region)) {
      $consoles[$console].Regions[$region] = @{ Name = $region; Size = [long]0; Count = 0 }
    }
    $consoles[$console].Regions[$region].Size += $size
    $consoles[$console].Regions[$region].Count++
  }

  $result = [System.Collections.Generic.List[hashtable]]::new()
  foreach ($cKey in $consoles.Keys) {
    $c = $consoles[$cKey]
    $regionList = [System.Collections.Generic.List[hashtable]]::new()
    foreach ($rKey in $c.Regions.Keys) {
      $regionList.Add($c.Regions[$rKey])
    }
    $regionArr = ,$regionList.ToArray()
    $result.Add(@{
      Name     = $c.Name
      Size     = $c.Size
      Count    = $c.Count
      Children = $regionArr
    })
  }

  return @{
    Name     = 'Collection'
    Children = ,$result.ToArray()
    Total    = $Files.Count
  }
}

function Get-DirectorySizeMap {
  <#
  .SYNOPSIS
    Berechnet Verzeichnisgroessen fuer Treemap-Darstellung.
  #>
  param(
    [Parameter(Mandatory)][array]$Files
  )

  $dirMap = @{}

  foreach ($file in $Files) {
    $dir = if ($file.ContainsKey('Directory')) { $file.Directory } else { 'Root' }
    $size = if ($file.ContainsKey('Size')) { $file.Size } else { 0 }

    if (-not $dirMap.ContainsKey($dir)) {
      $dirMap[$dir] = @{ Name = $dir; Size = [long]0; Count = 0 }
    }
    $dirMap[$dir].Size += $size
    $dirMap[$dir].Count++
  }

  return $dirMap
}

function ConvertTo-SizeLabel {
  <#
  .SYNOPSIS
    Konvertiert Bytes in lesbares Label.
  #>
  param(
    [Parameter(Mandatory)][long]$Bytes
  )

  if ($Bytes -ge 1TB) { return "$([math]::Round($Bytes / 1TB, 2)) TB" }
  if ($Bytes -ge 1GB) { return "$([math]::Round($Bytes / 1GB, 2)) GB" }
  if ($Bytes -ge 1MB) { return "$([math]::Round($Bytes / 1MB, 2)) MB" }
  if ($Bytes -ge 1KB) { return "$([math]::Round($Bytes / 1KB, 2)) KB" }
  return "$Bytes B"
}

function Get-TreemapStatistics {
  <#
  .SYNOPSIS
    Zusammenfassung der Treemap-Daten.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$TreemapData
  )

  $children = $TreemapData.Children
  $largest = if ($children.Count -gt 0) {
    ($children | Sort-Object { $_.Size } -Descending | Select-Object -First 1).Name
  } else { '' }

  $smallest = if ($children.Count -gt 0) {
    ($children | Sort-Object { $_.Size } | Select-Object -First 1).Name
  } else { '' }

  return @{
    Groups       = $children.Count
    TotalFiles   = $TreemapData.Count
    TotalSize    = ConvertTo-SizeLabel -Bytes $TreemapData.Size
    LargestGroup = $largest
    SmallestGroup = $smallest
  }
}
