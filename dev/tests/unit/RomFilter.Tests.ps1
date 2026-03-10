BeforeAll {
  $root = $PSScriptRoot
  while ($root -and -not (Test-Path (Join-Path $root 'simple_sort.ps1'))) {
    $root = Split-Path -Parent $root
  }
  . (Join-Path $root 'dev\modules\RomFilter.ps1')
}

Describe 'RomFilter (QW-08)' {

  BeforeAll {
    $testItems = @(
      @{ Name = 'Super Mario World'; Console = 'SNES'; Region = 'USA'; Category = 'GAME'; Format = 'zip'; Path = 'C:\roms\snes\smw.zip' }
      @{ Name = 'Sonic the Hedgehog'; Console = 'MD'; Region = 'EU'; Category = 'GAME'; Format = 'zip'; Path = 'C:\roms\md\sonic.zip' }
      @{ Name = 'Zelda - A Link to the Past'; Console = 'SNES'; Region = 'JP'; Category = 'GAME'; Format = '7z'; Path = 'C:\roms\snes\zelda.7z' }
      @{ Name = 'Test Hack (Beta)'; Console = 'NES'; Region = 'USA'; Category = 'JUNK'; Format = 'zip'; Path = 'C:\roms\nes\hack.zip' }
    )
  }

  Context 'Search-RomCollection' {

    It 'findet per Name-Suche' {
      $r = Search-RomCollection -Items $testItems -SearchText 'Zelda'
      $r.Count | Should -Be 1
      $r[0].Name | Should -BeLike '*Zelda*'
    }

    It 'findet per Konsole' {
      $r = Search-RomCollection -Items $testItems -SearchText 'SNES'
      $r.Count | Should -Be 2
    }

    It 'Suche ist case-insensitive' {
      $r = Search-RomCollection -Items $testItems -SearchText 'mario'
      $r.Count | Should -Be 1
    }

    It 'leerer Suchtext gibt alle zurueck' {
      $r = Search-RomCollection -Items $testItems -SearchText ''
      $r.Count | Should -Be 4
    }

    It 'null Suchtext gibt alle zurueck' {
      $r = Search-RomCollection -Items $testItems -SearchText $null
      $r.Count | Should -Be 4
    }

    It 'filtert auf bestimmtes Feld' {
      $r = Search-RomCollection -Items $testItems -SearchText 'SNES' -Field 'Console'
      $r.Count | Should -Be 2
    }

    It 'gibt leeres Array zurueck bei keinem Treffer' {
      $r = Search-RomCollection -Items $testItems -SearchText 'PlayStation'
      $r.Count | Should -Be 0
    }

    It 'findet per Region' {
      $r = Search-RomCollection -Items $testItems -SearchText 'JP' -Field 'Region'
      $r.Count | Should -Be 1
    }

    It 'findet per Kategorie' {
      $r = Search-RomCollection -Items $testItems -SearchText 'JUNK' -Field 'Category'
      $r.Count | Should -Be 1
    }
  }

  Context 'New-RomFilterPredicate' {

    It 'erstellt funktionierendes Predicate' {
      $pred = New-RomFilterPredicate -SearchText 'Mario'
      $match = & $pred $testItems[0]
      $match | Should -BeTrue
    }

    It 'Predicate gibt false fuer Nicht-Treffer' {
      $pred = New-RomFilterPredicate -SearchText 'PlayStation'
      $match = & $pred $testItems[0]
      $match | Should -BeFalse
    }

    It 'leerer Suchtext ergibt always-true Predicate' {
      $pred = New-RomFilterPredicate -SearchText ''
      $match = & $pred $testItems[0]
      $match | Should -BeTrue
    }
  }
}
