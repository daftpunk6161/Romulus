BeforeAll {
  . "$PSScriptRoot/../../modules/LauncherIntegration.ps1"
}

Describe 'LF-03: LauncherIntegration' {
  Describe 'Get-SupportedLauncherFormats' {
    It 'gibt Formate zurueck' {
      $formats = Get-SupportedLauncherFormats
      $formats.Count | Should -Be 4
      ($formats | Where-Object { $_.Key -eq 'retroarch' }) | Should -Not -BeNullOrEmpty
    }
  }

  Describe 'New-LauncherEntry' {
    It 'erstellt Entry mit korrekten Feldern' {
      $e = New-LauncherEntry -Name 'Super Mario' -Path 'C:\roms\mario.sfc' -Console 'snes'
      $e.Name | Should -Be 'Super Mario'
      $e.Console | Should -Be 'snes'
    }
  }

  Describe 'Get-DefaultCoreMapping' {
    It 'enthaelt Core fuer NES' {
      $map = Get-DefaultCoreMapping
      $map['nes'] | Should -BeLike '*libretro*'
    }
  }

  Describe 'ConvertTo-RetroArchPlaylist' {
    It 'erstellt gueltige Playlist-Struktur' {
      $entries = @(
        (New-LauncherEntry -Name 'TestGame' -Path 'C:\rom.sfc' -Console 'snes')
      )
      $pl = ConvertTo-RetroArchPlaylist -Entries $entries -PlaylistName 'Test'
      $pl.version | Should -Be '1.5'
      $pl.items.Count | Should -Be 1
      $pl.items[0].label | Should -Be 'TestGame'
    }
  }

  Describe 'ConvertTo-EmulationStationGamelist' {
    It 'konvertiert Eintraege' {
      $entries = @(
        (New-LauncherEntry -Name 'Game1' -Path 'C:\g1.sfc' -Console 'snes')
      )
      $games = ConvertTo-EmulationStationGamelist -Entries $entries
      $games.Count | Should -Be 1
      $games[0].name | Should -Be 'Game1'
    }
  }

  Describe 'Export-LauncherData' {
    It 'exportiert im RetroArch-Format' {
      $entries = @(
        (New-LauncherEntry -Name 'T1' -Path 'p1' -Console 'nes')
      )
      $data = Export-LauncherData -Entries $entries -Format 'retroarch'
      $data.version | Should -Be '1.5'
    }

    It 'exportiert im EmulationStation-Format' {
      $entries = @(
        (New-LauncherEntry -Name 'T1' -Path 'p1' -Console 'nes')
      )
      $data = Export-LauncherData -Entries $entries -Format 'emulationstation'
      $data.Games | Should -Not -BeNullOrEmpty
    }
  }
}
