BeforeAll {
  . "$PSScriptRoot/../../modules/PackageManagerIntegration.ps1"
}

Describe 'PackageManagerIntegration (XL-05)' {

  Describe 'New-WingetManifest' {
    It 'erstellt Standard-Winget-Manifest' {
      $m = New-WingetManifest
      $m.PackageIdentifier | Should -Be 'RomCleanup.RomCleanup'
      $m.PackageVersion | Should -Be '2.0.0'
      $m.InstallerType | Should -Be 'zip'
      $m.ManifestType | Should -Be 'singleton'
      @($m.Commands).Count | Should -BeGreaterThan 0
    }

    It 'akzeptiert benutzerdefinierte Version' {
      $m = New-WingetManifest -Version '3.0.0'
      $m.PackageVersion | Should -Be '3.0.0'
    }
  }

  Describe 'New-ScoopManifest' {
    It 'erstellt Standard-Scoop-Manifest' {
      $m = New-ScoopManifest
      $m.version | Should -Be '2.0.0'
      $m.license | Should -Be 'MIT'
      $m.homepage | Should -Not -BeNullOrEmpty
      @($m.bin).Count | Should -BeGreaterThan 0
    }

    It 'enthaelt checkver und autoupdate' {
      $m = New-ScoopManifest
      $m.checkver | Should -Not -BeNullOrEmpty
      $m.autoupdate | Should -Not -BeNullOrEmpty
    }
  }

  Describe 'Test-PackageManagerAvailable' {
    It 'gibt Ergebnis-Hashtable zurueck' {
      $result = Test-PackageManagerAvailable
      $result.ContainsKey('Winget') | Should -Be $true
      $result.ContainsKey('Scoop') | Should -Be $true
      $result.ContainsKey('Choco') | Should -Be $true
    }
  }

  Describe 'Get-PackageUpdateCommand' {
    It 'gibt Winget-Update-Befehl zurueck' {
      $cmd = Get-PackageUpdateCommand -Manager 'Winget'
      $cmd.Command | Should -BeLike 'winget upgrade*'
      $cmd.Manager | Should -Be 'Winget'
    }

    It 'gibt Scoop-Update-Befehl zurueck' {
      $cmd = Get-PackageUpdateCommand -Manager 'Scoop'
      $cmd.Command | Should -BeLike 'scoop update*'
    }

    It 'gibt Choco-Update-Befehl zurueck' {
      $cmd = Get-PackageUpdateCommand -Manager 'Choco'
      $cmd.Command | Should -BeLike 'choco upgrade*'
    }
  }

  Describe 'Get-PackageManagerStatistics' {
    It 'gibt korrekte Statistiken' {
      $wm = New-WingetManifest
      $sm = New-ScoopManifest
      $stats = Get-PackageManagerStatistics -WingetManifest $wm -ScoopManifest $sm
      $stats.WingetPackageId | Should -Be 'RomCleanup.RomCleanup'
      $stats.ManagerCount | Should -Be 3
      $stats.CommandCount | Should -BeGreaterThan 0
    }
  }
}
