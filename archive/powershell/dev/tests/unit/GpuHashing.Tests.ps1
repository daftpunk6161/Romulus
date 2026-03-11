BeforeAll {
  . "$PSScriptRoot/../../modules/GpuHashing.ps1"
}

Describe 'GpuHashing (XL-09)' {

  Describe 'Test-GpuHashingAvailable' {
    It 'gibt strukturiertes Ergebnis zurueck' {
      $result = Test-GpuHashingAvailable
      $result.ContainsKey('OpenCL') | Should -Be $true
      $result.ContainsKey('CUDA') | Should -Be $true
      $result.ContainsKey('Available') | Should -Be $true
    }
  }

  Describe 'New-GpuHashJob' {
    It 'erstellt Job mit Standard-Werten' {
      $job = New-GpuHashJob -FilePath 'C:\test.rom'
      $job.FilePath | Should -Be 'C:\test.rom'
      $job.Algorithm | Should -Be 'SHA1'
      $job.Backend | Should -Be 'CPU'
      $job.Status | Should -Be 'Pending'
      $job.Hash | Should -BeNullOrEmpty
    }

    It 'akzeptiert SHA256' {
      $job = New-GpuHashJob -FilePath 'test.rom' -Algorithm 'SHA256'
      $job.Algorithm | Should -Be 'SHA256'
    }

    It 'akzeptiert CUDA-Backend' {
      $job = New-GpuHashJob -FilePath 'test.rom' -Backend 'CUDA'
      $job.Backend | Should -Be 'CUDA'
    }
  }

  Describe 'Get-GpuHashBatchPlan' {
    It 'erstellt Plan mit CPU-Fallback' {
      $gpu = @{ OpenCL = $false; CUDA = $false; Available = $false }
      $plan = Get-GpuHashBatchPlan -Files @('a.rom','b.rom') -GpuAvailability $gpu
      $plan.Backend | Should -Be 'CPU'
      $plan.TotalFiles | Should -Be 2
      $plan.EstimatedSpeedup | Should -BeLike '*kein GPU*'
    }

    It 'bevorzugt CUDA ueber OpenCL' {
      $gpu = @{ OpenCL = $true; CUDA = $true; Available = $true }
      $plan = Get-GpuHashBatchPlan -Files @('a.rom') -GpuAvailability $gpu
      $plan.Backend | Should -Be 'CUDA'
    }

    It 'verwendet OpenCL wenn kein CUDA' {
      $gpu = @{ OpenCL = $true; CUDA = $false; Available = $true }
      $plan = Get-GpuHashBatchPlan -Files @('a.rom') -GpuAvailability $gpu
      $plan.Backend | Should -Be 'OpenCL'
    }

    It 'erstellt Jobs fuer alle Dateien' {
      $gpu = @{ OpenCL = $false; CUDA = $false; Available = $false }
      $plan = Get-GpuHashBatchPlan -Files @('a.rom','b.rom','c.rom') -GpuAvailability $gpu
      @($plan.Jobs).Count | Should -Be 3
    }
  }

  Describe 'Complete-GpuHashJob' {
    It 'markiert Job als abgeschlossen' {
      $job = New-GpuHashJob -FilePath 'test.rom'
      $completed = Complete-GpuHashJob -Job $job -Hash 'abc123' -ElapsedMs 42
      $completed.Status | Should -Be 'Completed'
      $completed.Hash | Should -Be 'abc123'
      $completed.ElapsedMs | Should -Be 42
    }
  }

  Describe 'Get-GpuHashStatistics' {
    It 'berechnet Statistiken korrekt' {
      $gpu = @{ OpenCL = $false; CUDA = $false; Available = $false }
      $plan = Get-GpuHashBatchPlan -Files @('a.rom','b.rom') -GpuAvailability $gpu
      Complete-GpuHashJob -Job $plan.Jobs[0] -Hash 'h1' -ElapsedMs 10 | Out-Null
      $stats = Get-GpuHashStatistics -Plan $plan
      $stats.TotalFiles | Should -Be 2
      $stats.CompletedFiles | Should -Be 1
      $stats.PendingFiles | Should -Be 1
    }
  }
}
