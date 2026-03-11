BeforeAll {
  . "$PSScriptRoot/../../modules/DatDiffViewer.ps1"
}

Describe 'MF-12: DatDiffViewer' {
  Describe 'Compare-DatVersions' {
    It 'findet hinzugefuegte Eintraege' {
      $old = @{ 'Game A' = @{ hash = 'H1' } }
      $new = @{ 'Game A' = @{ hash = 'H1' }; 'Game B' = @{ hash = 'H2' } }
      $diff = Compare-DatVersions -OldIndex $old -NewIndex $new
      $diff.Added | Should -Contain 'Game B'
      $diff.Count.Added | Should -Be 1
    }

    It 'findet entfernte Eintraege' {
      $old = @{ 'Game A' = @{ hash = 'H1' }; 'Game B' = @{ hash = 'H2' } }
      $new = @{ 'Game A' = @{ hash = 'H1' } }
      $diff = Compare-DatVersions -OldIndex $old -NewIndex $new
      $diff.Removed | Should -Contain 'Game B'
    }

    It 'erkennt Umbenennungen via Hash' {
      $old = @{ 'Game A (U)' = @{ hash = 'H1' } }
      $new = @{ 'Game A (USA)' = @{ hash = 'H1' } }
      $diff = Compare-DatVersions -OldIndex $old -NewIndex $new
      $diff.Renamed.Count | Should -Be 1
      $diff.Renamed[0].Old | Should -Be 'Game A (U)'
      $diff.Renamed[0].New | Should -Be 'Game A (USA)'
    }

    It 'identische Indizes ergeben leeren Diff' {
      $index = @{ 'Game A' = @{ hash = 'H1' } }
      $diff = Compare-DatVersions -OldIndex $index -NewIndex $index
      $diff.Count.Total | Should -Be 0
    }
  }

  Describe 'Get-DatDiffSummary' {
    It 'erstellt Summary mit korrektem Text' {
      $diff = @{ Count = @{ Added = 3; Removed = 1; Renamed = 2; Total = 6 } }
      $summary = Get-DatDiffSummary -Diff $diff -SourceName 'No-Intro SNES'
      $summary.HasChanges | Should -BeTrue
      $summary.Summary | Should -BeLike '*No-Intro SNES*'
    }
  }

  Describe 'Compare-DatEntryDetail' {
    It 'erkennt geaenderte Felder' {
      $old = @{ Name = 'Game A'; Size = '1024' }
      $new = @{ Name = 'Game A'; Size = '2048' }
      $detail = Compare-DatEntryDetail -OldEntry $old -NewEntry $new
      $detail.HasChanges | Should -BeTrue
      $detail.Changes[0].Field | Should -Be 'Size'
    }
  }
}
