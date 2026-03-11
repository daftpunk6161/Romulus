BeforeAll {
  . "$PSScriptRoot/../../modules/VirtualFolderPreview.ps1"
}

Describe 'LF-12: VirtualFolderPreview' {
  BeforeAll {
    $testFiles = @(
      @{ Path = 'Roms/SNES/game1.sfc'; Size = 1048576; Console = 'snes'; Region = 'EU' }
      @{ Path = 'Roms/SNES/game2.sfc'; Size = 2097152; Console = 'snes'; Region = 'US' }
      @{ Path = 'Roms/NES/game3.nes'; Size = 524288; Console = 'nes'; Region = 'EU' }
    )
  }

  Describe 'Build-TreemapData' {
    It 'baut Treemap-Struktur' {
      $tm = Build-TreemapData -Files $testFiles
      $tm | Should -Not -BeNullOrEmpty
      $tm.Children.Count | Should -BeGreaterThan 0
    }
  }

  Describe 'Build-SunburstData' {
    It 'baut Sunburst-Struktur' {
      $sb = Build-SunburstData -Files $testFiles
      $sb | Should -Not -BeNullOrEmpty
      $sb.Children.Count | Should -BeGreaterThan 0
    }
  }

  Describe 'Get-DirectorySizeMap' {
    It 'berechnet Groessen pro Verzeichnis' {
      $filesWithDir = @(
        @{ Directory = 'SNES'; Size = 1048576 }
        @{ Directory = 'SNES'; Size = 2097152 }
        @{ Directory = 'NES'; Size = 524288 }
      )
      $map = Get-DirectorySizeMap -Files $filesWithDir
      $map.Keys.Count | Should -Be 2
    }
  }

  Describe 'ConvertTo-SizeLabel' {
    It 'formatiert Bytes als KB/MB/GB' {
      ConvertTo-SizeLabel -Bytes 0 | Should -Be '0 B'
      ConvertTo-SizeLabel -Bytes 1024 | Should -Be '1 KB'
      ConvertTo-SizeLabel -Bytes 1048576 | Should -Be '1 MB'
      ConvertTo-SizeLabel -Bytes 1073741824 | Should -Be '1 GB'
    }
  }

  Describe 'Get-TreemapStatistics' {
    It 'gibt Statistiken zurueck' {
      $tm = Build-TreemapData -Files $testFiles
      $stats = Get-TreemapStatistics -TreemapData $tm
      $stats.TotalFiles | Should -Be 3
      $stats.Groups | Should -BeGreaterOrEqual 1
    }
  }
}
