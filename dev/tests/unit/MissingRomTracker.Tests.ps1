BeforeAll {
  . "$PSScriptRoot/../../modules/MissingRomTracker.ps1"
}

Describe 'MF-01: MissingRomTracker' {
  Describe 'Get-DatMissingGames' {
    It 'findet fehlende Spiele korrekt' {
      $datIndex = @{
        'AAAA' = @{ Name = 'Game A (EU)' }
        'BBBB' = @{ Name = 'Game B (US)' }
        'CCCC' = @{ Name = 'Game C (JP)' }
      }
      $foundHashes = @{ 'AAAA' = $true }
      $result = Get-DatMissingGames -DatIndex $datIndex -FoundHashes $foundHashes
      $result.Count | Should -Be 2
    }

    It 'filtert nach Region' {
      $datIndex = @{
        'AAAA' = @{ Name = 'Game A (EU)' }
        'BBBB' = @{ Name = 'Game B (US)' }
        'CCCC' = @{ Name = 'Game C (JP)' }
      }
      $foundHashes = @{}
      $result = Get-DatMissingGames -DatIndex $datIndex -FoundHashes $foundHashes -FilterRegions @('EU')
      $result.Count | Should -Be 1
      $result[0].Region | Should -Be 'EU'
    }

    It 'gibt leeres Ergebnis bei leerer DAT' {
      $result = Get-DatMissingGames -DatIndex @{} -FoundHashes @{}
      $result | Should -HaveCount 0
    }

    It 'gibt leeres Ergebnis wenn alles gefunden' {
      $datIndex = @{ 'AAAA' = @{ Name = 'Game A' } }
      $foundHashes = @{ 'AAAA' = $true }
      $result = Get-DatMissingGames -DatIndex $datIndex -FoundHashes $foundHashes
      $result | Should -HaveCount 0
    }
  }

  Describe 'Get-MissingReport' {
    It 'berechnet Completeness-Prozent korrekt' {
      $datIndex = @{
        'H1' = @{ Name = 'G1' }
        'H2' = @{ Name = 'G2' }
        'H3' = @{ Name = 'G3' }
        'H4' = @{ Name = 'G4' }
      }
      $foundHashes = @{ 'H1' = $true; 'H2' = $true; 'H3' = $true }
      $result = Get-MissingReport -DatIndex $datIndex -FoundHashes $foundHashes
      $result.Completeness | Should -Be 75.0
      $result.Missing | Should -Be 1
      $result.TotalInDat | Should -Be 4
    }
  }
}
