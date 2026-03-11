BeforeAll {
  . "$PSScriptRoot/../../modules/CloneListViewer.ps1"
}

Describe 'LF-10: CloneListViewer' {
  BeforeAll {
    $testDat = @{
      'sf2'   = @{ Name = 'Street Fighter II'; CloneOf = '' }
      'sf2ce' = @{ Name = 'SF2 Champion Edition'; CloneOf = 'sf2' }
      'sf2hf' = @{ Name = 'SF2 Hyper Fighting'; CloneOf = 'sf2' }
      'mk'    = @{ Name = 'Mortal Kombat'; CloneOf = '' }
    }
  }

  Describe 'Build-CloneTree' {
    It 'baut Baum mit Parents und Clones' {
      $tree = Build-CloneTree -DatIndex $testDat
      $tree.ParentCount | Should -Be 2
      $tree.CloneCount | Should -Be 2
      $tree.Tree['sf2'].Clones.Count | Should -Be 2
    }

    It 'erkennt Orphans' {
      $dat = @{ 'clone1' = @{ Name = 'Clone'; CloneOf = 'missing_parent' } }
      $tree = Build-CloneTree -DatIndex $dat
      $tree.OrphanCount | Should -Be 1
    }
  }

  Describe 'Get-CloneTreeFlat' {
    It 'flacht Baum korrekt ab' {
      $tree = Build-CloneTree -DatIndex $testDat
      $flat = Get-CloneTreeFlat -TreeData $tree
      $flat.Count | Should -Be 4
      @($flat | Where-Object { $_.Type -eq 'Parent' }).Count | Should -Be 2
      @($flat | Where-Object { $_.Type -eq 'Clone' }).Count | Should -Be 2
    }
  }

  Describe 'Search-CloneTree' {
    It 'findet Parent und Clones' {
      $tree = Build-CloneTree -DatIndex $testDat
      $results = Search-CloneTree -TreeData $tree -Query 'Fighter'
      $results.Count | Should -BeGreaterThan 0
    }

    It 'findet nichts bei leerem Treffer' {
      $tree = Build-CloneTree -DatIndex $testDat
      $results = Search-CloneTree -TreeData $tree -Query 'zzzznonexist'
      $results.Count | Should -Be 0
    }
  }

  Describe 'Get-CloneTreeStatistics' {
    It 'berechnet korrekte Statistiken' {
      $tree = Build-CloneTree -DatIndex $testDat
      $stats = Get-CloneTreeStatistics -TreeData $tree
      $stats.Parents | Should -Be 2
      $stats.Clones | Should -Be 2
      $stats.MaxClones | Should -Be 2
    }
  }
}
