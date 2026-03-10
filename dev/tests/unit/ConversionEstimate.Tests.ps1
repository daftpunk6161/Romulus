BeforeAll {
  $root = $PSScriptRoot
  while ($root -and -not (Test-Path (Join-Path $root 'simple_sort.ps1'))) {
    $root = Split-Path -Parent $root
  }
  . (Join-Path $root 'dev\modules\ConversionEstimate.ps1')
}

Describe 'ConversionEstimate (QW-04)' {

  Context 'Get-CompressionRatio' {

    It 'gibt korrekte Ratio fuer BIN→CHD' {
      $r = Get-CompressionRatio -SourceExtension '.bin' -TargetExtension '.chd'
      $r | Should -Be 0.50
    }

    It 'gibt korrekte Ratio fuer ISO→RVZ' {
      $r = Get-CompressionRatio -SourceExtension '.iso' -TargetExtension '.rvz'
      $r | Should -Be 0.40
    }

    It 'gibt Fallback-Ratio fuer unbekannte Kombination' {
      $r = Get-CompressionRatio -SourceExtension '.xyz' -TargetExtension '.abc'
      $r | Should -Be 0.75
    }

    It 'funktioniert case-insensitive' {
      $r1 = Get-CompressionRatio -SourceExtension '.BIN' -TargetExtension '.CHD'
      $r2 = Get-CompressionRatio -SourceExtension '.bin' -TargetExtension '.chd'
      $r1 | Should -Be $r2
    }
  }

  Context 'Get-ConversionSavingsEstimate' {

    It 'berechnet korrekte Einsparungen' {
      $files = @(
        @{ Name = 'game1.bin'; Size = 700MB; Extension = '.bin' }
        @{ Name = 'game2.bin'; Size = 300MB; Extension = '.bin' }
      )

      $r = Get-ConversionSavingsEstimate -Files $files -TargetFormat '.chd'
      $r.TotalSourceSizeBytes | Should -Be (1000MB)
      $r.EstimatedTargetSizeBytes | Should -Be (500MB)
      $r.EstimatedSavingsBytes | Should -Be (500MB)
      $r.FileCount | Should -Be 2
    }

    It 'behandelt leere Dateiliste' {
      $r = Get-ConversionSavingsEstimate -Files @() -TargetFormat '.chd'
      $r.FileCount | Should -Be 0
      $r.TotalSourceSizeBytes | Should -Be 0
    }

    It 'zaehlt uebersprungene Dateien gleicher Extension' {
      $files = @(
        @{ Name = 'game.bin'; Size = 100MB; Extension = '.bin' }
        @{ Name = 'game.chd'; Size = 50MB; Extension = '.chd' }
      )

      $r = Get-ConversionSavingsEstimate -Files $files -TargetFormat '.chd'
      $r.FileCount | Should -Be 1
      $r.SkippedCount | Should -Be 1
    }

    It 'Ratio liegt zwischen 0 und 1' {
      $files = @(@{ Name = 'g.bin'; Size = 1GB; Extension = '.bin' })
      $r = Get-ConversionSavingsEstimate -Files $files -TargetFormat '.chd'
      $r.Ratio | Should -BeGreaterOrEqual 0
      $r.Ratio | Should -BeLessOrEqual 1
    }
  }
}
