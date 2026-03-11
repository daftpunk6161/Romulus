BeforeAll {
  . "$PSScriptRoot/../../modules/SplitPanelPreview.ps1"
}

Describe 'MF-16: SplitPanelPreview' {
  Describe 'ConvertTo-SplitPanelData' {
    It 'konvertiert DryRun-Items in Panel-Daten' {
      $items = @(
        @{ OldPath = 'C:\src\game.zip'; NewPath = 'D:\dst\SNES\game.zip'; Action = 'Move' }
        @{ OldPath = 'C:\src\other.zip'; NewPath = 'D:\dst\NES\other.zip'; Action = 'Move' }
      )
      $result = ConvertTo-SplitPanelData -DryRunItems $items
      $result.TotalItems | Should -Be 2
      $result.SourcePanel.Count | Should -Be 2
      $result.TargetPanel.Count | Should -Be 2
    }

    It 'behandelt leere Items' {
      $result = ConvertTo-SplitPanelData -DryRunItems @()
      $result.TotalItems | Should -Be 0
    }
  }

  Describe 'Group-SplitPanelByDirectory' {
    It 'gruppiert nach Verzeichnis' {
      $items = @(
        @{ Path = 'C:\dir1\a.zip'; FileName = 'a.zip'; Directory = 'C:\dir1'; Action = 'Move' }
        @{ Path = 'C:\dir1\b.zip'; FileName = 'b.zip'; Directory = 'C:\dir1'; Action = 'Move' }
        @{ Path = 'C:\dir2\c.zip'; FileName = 'c.zip'; Directory = 'C:\dir2'; Action = 'Move' }
      )
      $result = Group-SplitPanelByDirectory -PanelItems $items
      $result.DirectoryCount | Should -Be 2
    }

    It 'behandelt leere Items' {
      $result = Group-SplitPanelByDirectory -PanelItems @()
      $result.DirectoryCount | Should -Be 0
    }
  }

  Describe 'Get-SplitPanelStatistics' {
    It 'berechnet Statistiken korrekt' {
      $splitData = @{
        TotalItems  = 3
        SourcePanel = @(
          @{ Action = 'Move'; Directory = 'C:\dir1' }
          @{ Action = 'Move'; Directory = 'C:\dir1' }
          @{ Action = 'Junk'; Directory = 'C:\dir2' }
        )
        TargetPanel = @(
          @{ Directory = 'D:\out1' }
          @{ Directory = 'D:\out1' }
          @{ Directory = 'D:\out2' }
        )
      }
      $stats = Get-SplitPanelStatistics -SplitData $splitData
      $stats.TotalItems | Should -Be 3
      $stats.ActionCounts['Move'] | Should -Be 2
      $stats.ActionCounts['Junk'] | Should -Be 1
    }
  }
}
