BeforeAll {
  . "$PSScriptRoot/../../modules/NasOptimization.ps1"
}

Describe 'LF-15: NasOptimization' {
  Describe 'New-NasProfile' {
    It 'erstellt Profil mit Defaults' {
      $p = New-NasProfile -Name 'MyNAS'
      $p.Name | Should -Be 'MyNAS'
      $p.MaxParallel | Should -BeGreaterThan 0
      $p.BatchSize | Should -BeGreaterThan 0
    }
  }

  Describe 'Test-NetworkPath' {
    It 'erkennt UNC-Pfad' {
      $r = Test-NetworkPath -Path '\\server\share'
      $r.IsNetwork | Should -Be $true
    }

    It 'erkennt lokalen Pfad' {
      $r = Test-NetworkPath -Path 'C:\Roms'
      $r.IsNetwork | Should -Be $false
    }
  }

  Describe 'Get-AdaptiveBatchSize' {
    It 'berechnet Batch-Groesse basierend auf Latenz' {
      $size = Get-AdaptiveBatchSize -LatencyMs 10
      $size | Should -BeGreaterThan 0
    }

    It 'berechnet kleinere Batches bei hoher Latenz' {
      $small = Get-AdaptiveBatchSize -LatencyMs 200
      $large = Get-AdaptiveBatchSize -LatencyMs 1
      $small | Should -BeLessOrEqual $large
    }
  }

  Describe 'Split-FilesIntoBatches' {
    It 'teilt Dateien in Batches' {
      $files = @(1..10 | ForEach-Object { @{ Path = "file$_.rom"; Size = 1024 } })
      $batches = Split-FilesIntoBatches -Files $files -BatchSize 3
      $batches.Count | Should -Be 4
      $batches[0].Count | Should -Be 3
    }
  }

  Describe 'Get-ThrottleDelayMs' {
    It 'liefert Delay basierend auf Throttling-Level' {
      $delay = Get-ThrottleDelayMs -Throttling 'Medium'
      $delay | Should -BeGreaterOrEqual 0
    }
  }

  Describe 'Get-NasProfileRecommendation' {
    It 'empfiehlt Profil-Einstellungen' {
      $rec = Get-NasProfileRecommendation -NetworkType 'LAN-1G'
      $rec.BatchSize | Should -BeGreaterThan 0
      $rec.MaxParallel | Should -BeGreaterThan 0
    }
  }
}
