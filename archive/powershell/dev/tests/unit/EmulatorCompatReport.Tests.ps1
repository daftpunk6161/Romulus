BeforeAll {
  . "$PSScriptRoot/../../modules/EmulatorCompatReport.ps1"
}

Describe 'EmulatorCompatReport (XL-07)' {

  Describe 'New-EmulatorProfile' {
    It 'erstellt Profil mit Pflichtfeldern' {
      $p = New-EmulatorProfile -Name 'RetroArch' -SupportedConsoles @('SNES','NES')
      $p.Name | Should -Be 'RetroArch'
      @($p.SupportedConsoles).Count | Should -Be 2
      $p.Platform | Should -Be 'Multi'
      $p.CompatEntries.Count | Should -Be 0
    }

    It 'akzeptiert Platform-Parameter' {
      $p = New-EmulatorProfile -Name 'PCSX2' -SupportedConsoles @('PS2') -Platform 'Windows'
      $p.Platform | Should -Be 'Windows'
    }
  }

  Describe 'Add-CompatibilityEntry' {
    It 'fuegt Eintrag zum Profil hinzu' {
      $p = New-EmulatorProfile -Name 'Test' -SupportedConsoles @('NES')
      $entry = Add-CompatibilityEntry -Profile $p -GameKey 'super_mario' -Status 'Perfect'
      $entry.GameKey | Should -Be 'super_mario'
      $entry.Status | Should -Be 'Perfect'
      $entry.Emulator | Should -Be 'Test'
      $p.CompatEntries.ContainsKey('super_mario') | Should -Be $true
    }

    It 'Standard-Status ist Untested' {
      $p = New-EmulatorProfile -Name 'Test' -SupportedConsoles @('NES')
      $entry = Add-CompatibilityEntry -Profile $p -GameKey 'test_game'
      $entry.Status | Should -Be 'Untested'
    }
  }

  Describe 'Get-CompatibilityScore' {
    It 'gibt 100 fuer Perfect' {
      Get-CompatibilityScore -Status 'Perfect' | Should -Be 100
    }

    It 'gibt 80 fuer Playable' {
      Get-CompatibilityScore -Status 'Playable' | Should -Be 80
    }

    It 'gibt 0 fuer Nothing' {
      Get-CompatibilityScore -Status 'Nothing' | Should -Be 0
    }

    It 'gibt -1 fuer Untested' {
      Get-CompatibilityScore -Status 'Untested' | Should -Be -1
    }
  }

  Describe 'Get-CompatibilityMatrix' {
    BeforeAll {
      $script:profiles = @(
        (New-EmulatorProfile -Name 'Emu1' -SupportedConsoles @('NES','SNES')),
        (New-EmulatorProfile -Name 'Emu2' -SupportedConsoles @('NES'))
      )
      Add-CompatibilityEntry -Profile $profiles[0] -GameKey 'mario' -Status 'Perfect' | Out-Null
      Add-CompatibilityEntry -Profile $profiles[1] -GameKey 'mario' -Status 'Playable' | Out-Null
    }

    It 'erstellt Matrix fuer Konsole' {
      $matrix = Get-CompatibilityMatrix -Profiles $profiles -ConsoleKey 'NES'
      $matrix.ConsoleKey | Should -Be 'NES'
      @($matrix.Emulators).Count | Should -Be 2
    }

    It 'filtert irrelevante Emulatoren' {
      $matrix = Get-CompatibilityMatrix -Profiles $profiles -ConsoleKey 'SNES'
      @($matrix.Emulators).Count | Should -Be 1
      $matrix.Emulators | Should -Contain 'Emu1'
    }
  }

  Describe 'Get-BestEmulatorForGame' {
    BeforeAll {
      $script:testProfiles = @(
        (New-EmulatorProfile -Name 'EmuA' -SupportedConsoles @('GBA')),
        (New-EmulatorProfile -Name 'EmuB' -SupportedConsoles @('GBA'))
      )
      Add-CompatibilityEntry -Profile $testProfiles[0] -GameKey 'zelda' -Status 'Playable' | Out-Null
      Add-CompatibilityEntry -Profile $testProfiles[1] -GameKey 'zelda' -Status 'Perfect' | Out-Null
      $script:matrix = Get-CompatibilityMatrix -Profiles $testProfiles -ConsoleKey 'GBA'
    }

    It 'findet besten Emulator' {
      $best = Get-BestEmulatorForGame -Matrix $matrix -GameKey 'zelda'
      $best.BestEmulator | Should -Be 'EmuB'
      $best.BestStatus | Should -Be 'Perfect'
      $best.Score | Should -Be 100
    }

    It 'gibt Untested fuer unbekanntes Spiel' {
      $best = Get-BestEmulatorForGame -Matrix $matrix -GameKey 'unknown_game'
      $best.BestEmulator | Should -BeNullOrEmpty
      $best.BestStatus | Should -Be 'Untested'
    }
  }

  Describe 'Get-EmulatorCompatStatistics' {
    It 'berechnet Statistiken korrekt' {
      $profiles = @(
        (New-EmulatorProfile -Name 'E1' -SupportedConsoles @('NES','SNES')),
        (New-EmulatorProfile -Name 'E2' -SupportedConsoles @('NES'))
      )
      $stats = Get-EmulatorCompatStatistics -Profiles $profiles
      $stats.EmulatorCount | Should -Be 2
      $stats.ConsoleCount | Should -Be 2
    }
  }
}
