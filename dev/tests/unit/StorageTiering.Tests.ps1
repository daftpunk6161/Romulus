BeforeAll {
  . "$PSScriptRoot/../../modules/StorageTiering.ps1"
}

Describe 'LF-08: StorageTiering' {
  Describe 'Get-StorageTierConfig' {
    It 'erstellt Config mit korrekten Werten' {
      $cfg = Get-StorageTierConfig -HotPath 'D:\Hot' -ColdPath 'E:\Cold' -HotThresholdDays 14
      $cfg.HotPath | Should -Be 'D:\Hot'
      $cfg.ColdPath | Should -Be 'E:\Cold'
      $cfg.HotThresholdDays | Should -Be 14
    }
  }

  Describe 'Get-FileAccessScore' {
    It 'gibt hoeheren Score fuer kuerzlich zugegriffene Dateien' {
      $now = Get-Date
      $scoreRecent = Get-FileAccessScore -LastAccess $now.AddDays(-1) -AccessCount 5 -Now $now
      $scoreOld = Get-FileAccessScore -LastAccess $now.AddDays(-100) -AccessCount 1 -Now $now
      $scoreRecent | Should -BeGreaterThan $scoreOld
    }
  }

  Describe 'Invoke-StorageTierAnalysis' {
    It 'ordnet Dateien korrekt Tiers zu' {
      $now = Get-Date
      $files = @(
        @{ Name = 'recent.sfc'; Path = 'D:\recent.sfc'; Size = 1MB; LastAccess = $now.AddDays(-5) }
        @{ Name = 'old.sfc'; Path = 'D:\old.sfc'; Size = 2MB; LastAccess = $now.AddDays(-90) }
      )
      $cfg = Get-StorageTierConfig -HotPath 'D:\Hot' -ColdPath 'E:\Cold' -HotThresholdDays 30
      $results = Invoke-StorageTierAnalysis -Files $files -Config $cfg
      $results.Count | Should -Be 2
      ($results | Where-Object { $_.Name -eq 'recent.sfc' }).Tier | Should -Be 'Hot'
      ($results | Where-Object { $_.Name -eq 'old.sfc' }).Tier | Should -Be 'Cold'
    }
  }

  Describe 'Get-TierMigrationPlan' {
    It 'plant Migrationen korrekt' {
      $cfg = Get-StorageTierConfig -HotPath 'D:\Hot' -ColdPath 'E:\Cold'
      $analysis = @(
        @{ Name = 'f1'; Path = 'E:\Cold\f1'; Tier = 'Hot'; Size = 1MB }
        @{ Name = 'f2'; Path = 'D:\Hot\f2'; Tier = 'Cold'; Size = 2MB }
      )
      $plan = Get-TierMigrationPlan -Analysis $analysis -Config $cfg
      $plan.MoveToHotCount | Should -Be 1
      $plan.MoveToColdCount | Should -Be 1
    }
  }

  Describe 'Get-TierStatistics' {
    It 'zaehlt Hot/Cold korrekt' {
      $analysis = @(
        @{ Tier = 'Hot'; Size = 1MB }
        @{ Tier = 'Hot'; Size = 2MB }
        @{ Tier = 'Cold'; Size = 5MB }
      )
      $stats = Get-TierStatistics -Analysis $analysis
      $stats.HotCount | Should -Be 2
      $stats.ColdCount | Should -Be 1
      $stats.Total | Should -Be 3
    }
  }
}
