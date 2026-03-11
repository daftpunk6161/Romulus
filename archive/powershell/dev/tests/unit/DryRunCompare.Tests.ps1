BeforeAll {
  . "$PSScriptRoot/../../modules/DryRunCompare.ps1"
}

Describe 'MF-21: DryRunCompare' {
  Describe 'Compare-DryRunResults' {
    It 'erkennt identische Items' {
      $a = @( @{ OldPath = 'C:\a.zip'; NewPath = 'D:\a.zip'; Action = 'Move' } )
      $b = @( @{ OldPath = 'C:\a.zip'; NewPath = 'D:\a.zip'; Action = 'Move' } )
      $result = Compare-DryRunResults -ResultA $a -ResultB $b
      $result.Identical.Count | Should -Be 1
      $result.Different.Count | Should -Be 0
      $result.OnlyInA.Count | Should -Be 0
      $result.OnlyInB.Count | Should -Be 0
    }

    It 'erkennt unterschiedliche Ziele' {
      $a = @( @{ OldPath = 'C:\a.zip'; NewPath = 'D:\a.zip'; Action = 'Move' } )
      $b = @( @{ OldPath = 'C:\a.zip'; NewPath = 'E:\a.zip'; Action = 'Move' } )
      $result = Compare-DryRunResults -ResultA $a -ResultB $b
      $result.Different.Count | Should -Be 1
      $result.Identical.Count | Should -Be 0
    }

    It 'erkennt Items die nur in A vorhanden sind' {
      $a = @( @{ OldPath = 'C:\a.zip'; NewPath = 'D:\a.zip'; Action = 'Move' } )
      $b = @()
      $result = Compare-DryRunResults -ResultA $a -ResultB $b
      $result.OnlyInA.Count | Should -Be 1
      $result.OnlyInB.Count | Should -Be 0
    }

    It 'behandelt leere Results' {
      $result = Compare-DryRunResults -ResultA @() -ResultB @()
      $result.Summary.TotalKeys | Should -Be 0
    }
  }

  Describe 'Get-DryRunComparisonSummary' {
    It 'erstellt lesbaren Summary-Text' {
      $comp = @{
        Summary = @{ Identical = 5; Different = 2; OnlyA = 1; OnlyB = 0 }
      }
      $result = Get-DryRunComparisonSummary -Comparison $comp -LabelA 'Run1' -LabelB 'Run2'
      $result.Text | Should -BeLike '*Run1*'
      $result.Text | Should -BeLike '*Run2*'
      $result.HasChanges | Should -BeTrue
    }

    It 'erkennt keine Aenderungen' {
      $comp = @{
        Summary = @{ Identical = 5; Different = 0; OnlyA = 0; OnlyB = 0 }
      }
      $result = Get-DryRunComparisonSummary -Comparison $comp
      $result.HasChanges | Should -BeFalse
    }
  }
}
