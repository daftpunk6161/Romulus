BeforeAll {
  . "$PSScriptRoot/../../modules/CustomDatEditor.ps1"
}

Describe 'LF-09: CustomDatEditor' {
  Describe 'New-CustomDat' {
    It 'erstellt leeres Custom-DAT' {
      $dat = New-CustomDat -Name 'MyDat' -Author 'Me'
      $dat.Header.Name | Should -Be 'MyDat'
      $dat.Games.Count | Should -Be 0
    }
  }

  Describe 'Add-CustomDatEntry' {
    It 'fuegt Eintrag hinzu' {
      $dat = New-CustomDat -Name 'Test'
      Add-CustomDatEntry -Dat $dat -GameName 'Zelda' -FileName 'zelda.nes' -Hash 'ABC123' -Size 524288 | Out-Null
      $dat.Games.Count | Should -Be 1
      $dat.Games['ABC123'].GameName | Should -Be 'Zelda'
    }
  }

  Describe 'Remove-CustomDatEntry' {
    It 'entfernt Eintrag' {
      $dat = New-CustomDat -Name 'Test'
      Add-CustomDatEntry -Dat $dat -GameName 'G1' -FileName 'g1.nes' -Hash 'H1' | Out-Null
      Remove-CustomDatEntry -Dat $dat -Key 'H1' | Out-Null
      $dat.Games.Count | Should -Be 0
    }
  }

  Describe 'Find-CustomDatEntry' {
    It 'sucht nach Name' {
      $dat = New-CustomDat -Name 'Test'
      Add-CustomDatEntry -Dat $dat -GameName 'Super Mario' -FileName 'mario.nes' -Hash 'H1' | Out-Null
      Add-CustomDatEntry -Dat $dat -GameName 'Zelda' -FileName 'zelda.nes' -Hash 'H2' | Out-Null
      $results = Find-CustomDatEntry -Dat $dat -Query 'Mario'
      $results.Count | Should -Be 1
      $results[0].GameName | Should -Be 'Super Mario'
    }
  }

  Describe 'ConvertTo-DatXml' {
    It 'erzeugt gueltiges XML mit escaped Characters' {
      $dat = New-CustomDat -Name 'Test & Dat' -Author 'Author'
      Add-CustomDatEntry -Dat $dat -GameName 'Game <1>' -FileName 'game.nes' -Hash 'H1' | Out-Null
      $xml = ConvertTo-DatXml -Dat $dat
      $xml | Should -BeLike '*<datafile>*'
      $xml | Should -BeLike '*Test &amp; Dat*'
    }
  }

  Describe 'Get-CustomDatStatistics' {
    It 'zaehlt korrekt' {
      $dat = New-CustomDat -Name 'Test'
      Add-CustomDatEntry -Dat $dat -GameName 'G1' -FileName 'g1.nes' -Hash 'H1' -Region 'EU' | Out-Null
      Add-CustomDatEntry -Dat $dat -GameName 'G2' -FileName 'g2.nes' -Hash '' -Region 'US' | Out-Null
      $stats = Get-CustomDatStatistics -Dat $dat
      $stats.TotalEntries | Should -Be 2
      $stats.WithHash | Should -Be 1
      $stats.WithoutHash | Should -Be 1
    }
  }
}
