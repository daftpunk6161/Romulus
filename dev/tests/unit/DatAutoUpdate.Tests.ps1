BeforeAll {
  . "$PSScriptRoot/../../modules/DatAutoUpdate.ps1"
}

Describe 'MF-11: DatAutoUpdate' {
  Describe 'Test-DatUpdateAvailable' {
    It 'erkennt neue Version' {
      $source = @{ name = 'No-Intro SNES'; latestVersion = '2026-03-10' }
      $result = Test-DatUpdateAvailable -DatSource $source -CurrentVersion '2026-03-01'
      $result.UpdateAvailable | Should -BeTrue
      $result.Reason | Should -Be 'NewVersionAvailable'
    }

    It 'erkennt Up-to-Date' {
      $source = @{ name = 'No-Intro SNES'; latestVersion = '2026-03-01' }
      $result = Test-DatUpdateAvailable -DatSource $source -CurrentVersion '2026-03-01'
      $result.UpdateAvailable | Should -BeFalse
      $result.Reason | Should -Be 'UpToDate'
    }

    It 'erkennt fehlende aktuelle Version als Update' {
      $source = @{ name = 'No-Intro SNES'; latestVersion = '2026-03-10' }
      $result = Test-DatUpdateAvailable -DatSource $source -CurrentVersion $null
      $result.UpdateAvailable | Should -BeTrue
    }

    It 'behandelt ungueltige Quelle' {
      $result = Test-DatUpdateAvailable -DatSource @{} -CurrentVersion '1.0'
      $result.UpdateAvailable | Should -BeFalse
      $result.Reason | Should -Be 'InvalidSource'
    }
  }

  Describe 'Get-DatUpdateCheckResult' {
    It 'prueft mehrere Quellen' {
      $sources = @(
        @{ name = 'Source1'; latestVersion = '2.0' }
        @{ name = 'Source2'; latestVersion = '1.0' }
      )
      $installed = @{ 'Source1' = '1.0'; 'Source2' = '1.0' }
      $result = Get-DatUpdateCheckResult -DatSources $sources -InstalledVersions $installed
      $result.TotalSources | Should -Be 2
      $result.UpdatesAvailable | Should -Be 1
    }

    It 'behandelt leere Quellen' {
      $result = Get-DatUpdateCheckResult -DatSources @() -InstalledVersions @{}
      $result.TotalSources | Should -Be 0
    }
  }

  Describe 'New-DatUpdatePlan' {
    It 'erstellt Plan fuer ausstehende Updates' {
      $check = @{
        Results = @(
          @{ UpdateAvailable = $true; SourceName = 'S1' }
          @{ UpdateAvailable = $false; SourceName = 'S2' }
        )
      }
      $plan = New-DatUpdatePlan -CheckResult $check
      $plan.TotalUpdates | Should -Be 1
      $plan.Status | Should -Be 'PendingDownload'
    }
  }
}
