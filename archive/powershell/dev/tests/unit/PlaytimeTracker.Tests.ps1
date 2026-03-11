BeforeAll {
  . "$PSScriptRoot/../../modules/PlaytimeTracker.ps1"
}

Describe 'LF-04: PlaytimeTracker' {
  Describe 'New-PlaytimeEntry' {
    It 'erstellt Eintrag mit korrekten Werten' {
      $e = New-PlaytimeEntry -GameName 'Zelda' -Hours 5.5
      $e.GameName | Should -Be 'Zelda'
      $e.TotalHours | Should -Be 5.5
      $e.TotalSeconds | Should -Be 19800
    }
  }

  Describe 'Merge-PlaytimeData' {
    It 'merged zwei Quellen korrekt' {
      $s1 = @{ 'G1' = (New-PlaytimeEntry -GameName 'G1' -Hours 2.0) }
      $s2 = @{ 'G1' = (New-PlaytimeEntry -GameName 'G1' -Hours 3.0) }
      $merged = Merge-PlaytimeData -Sources @($s1, $s2)
      $merged['G1'].TotalHours | Should -Be 5.0
    }

    It 'behaelt nicht-ueberlappende Eintraege' {
      $s1 = @{ 'G1' = (New-PlaytimeEntry -GameName 'G1' -Hours 1.0) }
      $s2 = @{ 'G2' = (New-PlaytimeEntry -GameName 'G2' -Hours 2.0) }
      $merged = Merge-PlaytimeData -Sources @($s1, $s2)
      $merged.Count | Should -Be 2
    }
  }

  Describe 'Get-PlaytimeReport' {
    It 'erstellt Report mit Statistiken' {
      $data = @{
        'G1' = (New-PlaytimeEntry -GameName 'G1' -Hours 10.0)
        'G2' = (New-PlaytimeEntry -GameName 'G2' -Hours 5.0)
        'G3' = (New-PlaytimeEntry -GameName 'G3' -Hours 0)
      }
      $report = Get-PlaytimeReport -PlaytimeData $data -TopN 2
      $report.TotalGames | Should -Be 3
      $report.TotalHours | Should -Be 15.0
      $report.NeverPlayed | Should -Be 1
      $report.TopGames.Count | Should -Be 2
    }
  }

  Describe 'Get-UnplayedRoms' {
    It 'findet ungespielte ROMs' {
      $allGames = @('G1', 'G2', 'G3')
      $played = @{ 'G1' = (New-PlaytimeEntry -GameName 'G1' -Hours 5.0) }
      $unplayed = Get-UnplayedRoms -AllGames $allGames -PlaytimeData $played
      $unplayed.Count | Should -Be 2
      $unplayed | Should -Contain 'G2'
    }
  }

  Describe 'Import-RetroArchPlaytime' {
    It 'gibt leeres Ergebnis bei nicht-existierendem Verzeichnis' {
      $result = Import-RetroArchPlaytime -LogDir "$TestDrive\nonexistent"
      $result.Count | Should -Be 0
    }
  }
}
