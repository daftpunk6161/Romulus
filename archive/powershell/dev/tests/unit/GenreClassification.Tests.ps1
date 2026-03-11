BeforeAll {
  . "$PSScriptRoot/../../modules/GenreClassification.ps1"
}

Describe 'LF-02: GenreClassification' {
  Describe 'Get-GenreTaxonomy' {
    It 'gibt Standard-Genres zurueck' {
      $tax = Get-GenreTaxonomy
      $tax.Count | Should -BeGreaterThan 5
      $tax[0].Key | Should -Be 'action'
    }
  }

  Describe 'New-GenreTag' {
    It 'erstellt Tag mit korrekten Werten' {
      $tag = New-GenreTag -GameName 'Zelda' -Genre 'rpg' -Source 'manual'
      $tag.GameName | Should -Be 'Zelda'
      $tag.Genre | Should -Be 'rpg'
    }
  }

  Describe 'Find-GenreByKeyword' {
    It 'erkennt Racing-Genre' {
      Find-GenreByKeyword -GameName 'Super Mario Kart' | Should -Be 'racing'
    }
    It 'erkennt Sports-Genre' {
      Find-GenreByKeyword -GameName 'FIFA 2024' | Should -Be 'sports'
    }
    It 'gibt other bei unbekannt' {
      Find-GenreByKeyword -GameName 'Xyzzy Unknown' | Should -Be 'other'
    }
  }

  Describe 'Set-GameGenre' {
    It 'setzt Genre in Map' {
      $map = @{}
      Set-GameGenre -GenreMap $map -GameName 'Zelda' -Genre 'rpg' | Out-Null
      $map['Zelda'].Genre | Should -Be 'rpg'
    }
  }

  Describe 'Get-GenreStatistics' {
    It 'zaehlt Genres korrekt' {
      $map = @{
        'G1' = @{ Genre = 'rpg' }
        'G2' = @{ Genre = 'rpg' }
        'G3' = @{ Genre = 'action' }
      }
      $stats = Get-GenreStatistics -GenreMap $map
      $stats.Total | Should -Be 3
      $stats.TopGenre | Should -Be 'rpg'
    }
  }

  Describe 'Invoke-AutoGenreClassification' {
    It 'klassifiziert mehrere Spiele' {
      $map = Invoke-AutoGenreClassification -GameNames @('Mario Kart', 'NBA Jam', 'Tetris')
      $map.Count | Should -Be 3
      $map['Mario Kart'].Genre | Should -Be 'racing'
      $map['Tetris'].Genre | Should -Be 'puzzle'
    }
  }
}
