BeforeAll {
  $root = $PSScriptRoot
  while ($root -and -not (Test-Path (Join-Path $root 'simple_sort.ps1'))) {
    $root = Split-Path -Parent $root
  }
  . (Join-Path $root 'dev\modules\DuplicateHeatmap.ps1')
}

Describe 'DuplicateHeatmap (QW-09)' {

  Context 'Get-DuplicateHeatmapData' {

    It 'aggregiert Duplikate pro Konsole' {
      $dedupeResults = @(
        @{ Console = 'SNES'; Action = 'KEEP' }
        @{ Console = 'SNES'; Action = 'MOVE' }
        @{ Console = 'SNES'; Action = 'MOVE' }
        @{ Console = 'NES'; Action = 'KEEP' }
        @{ Console = 'NES'; Action = 'KEEP' }
      )

      $r = Get-DuplicateHeatmapData -DedupeResults $dedupeResults
      $r.TotalDupes | Should -Be 2
      $r.ConsoleCount | Should -Be 2

      $snes = $r.Data | Where-Object { $_.Console -eq 'SNES' }
      $snes.Duplicates | Should -Be 2
      $snes.Total | Should -Be 3
    }

    It 'sortiert nach Duplikat-Anzahl absteigend' {
      $dedupeResults = @(
        @{ Console = 'NES'; Action = 'MOVE' }
        @{ Console = 'SNES'; Action = 'MOVE' }
        @{ Console = 'SNES'; Action = 'MOVE' }
        @{ Console = 'SNES'; Action = 'MOVE' }
      )

      $r = Get-DuplicateHeatmapData -DedupeResults $dedupeResults
      $r.Data[0].Console | Should -Be 'SNES'
      $r.Data[0].Duplicates | Should -Be 3
    }

    It 'berechnet korrekte Prozentwerte' {
      $dedupeResults = @(
        @{ Console = 'SNES'; Action = 'KEEP' }
        @{ Console = 'SNES'; Action = 'MOVE' }
      )

      $r = Get-DuplicateHeatmapData -DedupeResults $dedupeResults
      $snes = $r.Data | Where-Object { $_.Console -eq 'SNES' }
      $snes.Percent | Should -Be 50.0
    }

    It 'behandelt Unknown-Konsole bei fehlendem Feld' {
      $dedupeResults = @(@{ Action = 'MOVE' })

      $r = Get-DuplicateHeatmapData -DedupeResults $dedupeResults
      $unknown = $r.Data | Where-Object { $_.Console -eq 'Unknown' }
      $unknown | Should -Not -BeNullOrEmpty
    }

    It 'unterstuetzt ConsoleType als Feld-Alternative' {
      $dedupeResults = @(
        @{ ConsoleType = 'GBA'; Action = 'MOVE' }
        @{ ConsoleType = 'GBA'; Action = 'KEEP' }
      )

      $r = Get-DuplicateHeatmapData -DedupeResults $dedupeResults
      $gba = $r.Data | Where-Object { $_.Console -eq 'GBA' }
      $gba | Should -Not -BeNullOrEmpty
      $gba.Total | Should -Be 2
    }
  }
}
