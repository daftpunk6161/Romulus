BeforeAll {
  . "$PSScriptRoot/../../modules/ParallelHashing.ps1"
}

Describe 'MF-14: ParallelHashing' {
  Describe 'Get-OptimalThreadCount' {
    It 'gibt maximal 8 zurueck' {
      $count = Get-OptimalThreadCount -MaxThreads 8
      $count | Should -BeLessOrEqual 8
      $count | Should -BeGreaterThan 0
    }

    It 'respektiert MaxThreads-Limit' {
      $count = Get-OptimalThreadCount -MaxThreads 2
      $count | Should -BeLessOrEqual 2
    }
  }

  Describe 'Split-FileListIntoChunks' {
    It 'teilt Liste in Chunks auf' {
      $files = 1..250 | ForEach-Object { "file$_.txt" }
      $chunks = Split-FileListIntoChunks -Files $files -ChunkSize 100
      $chunks.Count | Should -Be 3
    }

    It 'behandelt leere Liste' {
      $chunks = Split-FileListIntoChunks -Files @()
      $chunks | Should -HaveCount 0
    }

    It 'behandelt Liste kleiner als ChunkSize' {
      $chunks = Split-FileListIntoChunks -Files @('a.txt', 'b.txt') -ChunkSize 100
      $chunks.Count | Should -Be 1
    }
  }

  Describe 'Get-FileHashSafe' {
    It 'berechnet Hash korrekt' {
      $tempFile = Join-Path $TestDrive 'hashtest.txt'
      Set-Content -Path $tempFile -Value 'TestContent'
      $result = Get-FileHashSafe -Path $tempFile -Algorithm SHA256
      $result.Hash | Should -Not -BeNullOrEmpty
      $result.Error | Should -BeNullOrEmpty
    }

    It 'gibt Fehler bei fehlender Datei' {
      $result = Get-FileHashSafe -Path 'C:\nonexistent.xyz' -Algorithm SHA1
      $result.Hash | Should -BeNullOrEmpty
      $result.Error | Should -Not -BeNullOrEmpty
    }
  }

  Describe 'Invoke-ParallelHashing' {
    It 'hasht mehrere Dateien' {
      $f1 = Join-Path $TestDrive 'ph1.txt'; Set-Content -Path $f1 -Value 'data1'
      $f2 = Join-Path $TestDrive 'ph2.txt'; Set-Content -Path $f2 -Value 'data2'
      $result = Invoke-ParallelHashing -Files @($f1, $f2) -Algorithm SHA256
      $result.TotalFiles | Should -Be 2
      $result.Errors | Should -Be 0
    }

    It 'behandelt leere Liste' {
      $result = Invoke-ParallelHashing -Files @()
      $result.TotalFiles | Should -Be 0
      $result.Method | Should -Be 'None'
    }
  }
}
