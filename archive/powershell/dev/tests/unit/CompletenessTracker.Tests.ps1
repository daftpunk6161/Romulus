BeforeAll {
  . "$PSScriptRoot/../../modules/CompletenessTracker.ps1"
}

Describe 'MF-04: CompletenessTracker' {
  Describe 'Get-CompletenessReport' {
    It 'berechnet Completeness-Prozent korrekt' {
      $goal = @{ name = 'Full SNES'; console = 'SNES'; region = $null; genre = $null }
      $datIndex = @{
        'H1' = @{ Name = 'G1' }
        'H2' = @{ Name = 'G2' }
        'H3' = @{ Name = 'G3' }
        'H4' = @{ Name = 'G4' }
        'H5' = @{ Name = 'G5' }
      }
      $foundHashes = @{ 'H1' = $true; 'H2' = $true; 'H3' = $true }
      $result = Get-CompletenessReport -Goal $goal -DatIndex $datIndex -FoundHashes $foundHashes
      $result.Completeness | Should -Be 60.0
      $result.Found | Should -Be 3
      $result.Total | Should -Be 5
    }

    It 'filtert nach Region' {
      $goal = @{ name = 'EU SNES'; console = 'SNES'; region = 'EU'; genre = $null }
      $datIndex = @{
        'H1' = @{ Name = 'G1 (EU)' }
        'H2' = @{ Name = 'G2 (US)' }
      }
      $foundHashes = @{ 'H1' = $true }
      $result = Get-CompletenessReport -Goal $goal -DatIndex $datIndex -FoundHashes $foundHashes
      $result.Completeness | Should -Be 100.0
    }

    It 'behandelt leere DAT' {
      $goal = @{ name = 'Test'; console = 'SNES'; region = $null; genre = $null }
      $result = Get-CompletenessReport -Goal $goal -DatIndex @{} -FoundHashes @{}
      $result.Completeness | Should -Be 100.0
      $result.Total | Should -Be 0
    }
  }

  Describe 'Get-MultiGoalReport' {
    It 'verarbeitet mehrere Ziele' {
      $goals = @(
        @{ name = 'A'; console = 'SNES'; region = $null; genre = $null }
        @{ name = 'B'; console = 'NES'; region = $null; genre = $null }
      )
      $datIndex = @{
        'H1' = @{ Name = 'G1' }
        'H2' = @{ Name = 'G2' }
      }
      $foundHashes = @{ 'H1' = $true }
      $result = Get-MultiGoalReport -Goals $goals -DatIndex $datIndex -FoundHashes $foundHashes
      $result.Count | Should -Be 2
    }
  }
}
