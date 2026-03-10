# ================================================================
#  SPLIT PANEL PREVIEW – Norton-Commander-Style Vorschau (MF-16)
#  Dependencies: DryRun-Ergebnis
# ================================================================

function ConvertTo-SplitPanelData {
  <#
  .SYNOPSIS
    Konvertiert DryRun-Ergebnis in ein Split-Panel-Datenmodell (Quelle/Ziel).
  .PARAMETER DryRunItems
    Array von DryRun-Ergebnis-Items.
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$DryRunItems
  )

  if (-not $DryRunItems -or $DryRunItems.Count -eq 0) {
    return @{ SourcePanel = @(); TargetPanel = @(); TotalItems = 0 }
  }

  $sourcePanel = @()
  $targetPanel = @()

  foreach ($item in $DryRunItems) {
    $sourcePath = if ($item.ContainsKey('OldPath')) { $item.OldPath } elseif ($item.ContainsKey('SourcePath')) { $item.SourcePath } else { '' }
    $targetPath = if ($item.ContainsKey('NewPath')) { $item.NewPath } elseif ($item.ContainsKey('TargetPath')) { $item.TargetPath } else { '' }
    $action = if ($item.ContainsKey('Action')) { $item.Action } else { 'Move' }

    $sourcePanel += @{
      Path       = $sourcePath
      FileName   = [System.IO.Path]::GetFileName($sourcePath)
      Directory  = [System.IO.Path]::GetDirectoryName($sourcePath)
      Action     = $action
    }

    $targetPanel += @{
      Path      = $targetPath
      FileName  = [System.IO.Path]::GetFileName($targetPath)
      Directory = [System.IO.Path]::GetDirectoryName($targetPath)
      Action    = $action
    }
  }

  return @{
    SourcePanel = $sourcePanel
    TargetPanel = $targetPanel
    TotalItems  = $DryRunItems.Count
  }
}

function Group-SplitPanelByDirectory {
  <#
  .SYNOPSIS
    Gruppiert Panel-Items nach Verzeichnis fuer Tree-Ansicht.
  .PARAMETER PanelItems
    Array von Panel-Items.
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$PanelItems
  )

  if (-not $PanelItems -or $PanelItems.Count -eq 0) {
    return @{ Groups = @{}; DirectoryCount = 0 }
  }

  $groups = @{}

  foreach ($item in $PanelItems) {
    $dir = $item.Directory
    if (-not $dir) { $dir = '(Root)' }

    if (-not $groups.ContainsKey($dir)) {
      $groups[$dir] = @()
    }
    $groups[$dir] += $item
  }

  return @{
    Groups         = $groups
    DirectoryCount = $groups.Count
  }
}

function Get-SplitPanelStatistics {
  <#
  .SYNOPSIS
    Berechnet Statistiken fuer die Split-Panel-Ansicht.
  .PARAMETER SplitData
    Ergebnis von ConvertTo-SplitPanelData.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$SplitData
  )

  $actions = @{}
  foreach ($item in $SplitData.SourcePanel) {
    $action = $item.Action
    if (-not $actions.ContainsKey($action)) {
      $actions[$action] = 0
    }
    $actions[$action]++
  }

  $sourceDirectories = @($SplitData.SourcePanel | ForEach-Object { $_.Directory } | Sort-Object -Unique)
  $targetDirectories = @($SplitData.TargetPanel | ForEach-Object { $_.Directory } | Sort-Object -Unique)

  return @{
    TotalItems           = $SplitData.TotalItems
    ActionCounts         = $actions
    SourceDirectoryCount = $sourceDirectories.Count
    TargetDirectoryCount = $targetDirectories.Count
  }
}
