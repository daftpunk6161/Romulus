BeforeAll {
  $root = $PSScriptRoot
  while ($root -and -not (Test-Path (Join-Path $root 'simple_sort.ps1'))) {
    $root = Split-Path -Parent $root
  }
  . (Join-Path $root 'dev\modules\RetroArchPlaylist.ps1')
}

Describe 'RetroArchPlaylist (QW-16)' {

  Context 'Get-RetroArchCoreMapping' {

    It 'gibt korrektes Mapping fuer SNES' {
      $m = Get-RetroArchCoreMapping -ConsoleKey 'SNES'
      $m.Core | Should -Be 'snes9x_libretro'
      $m.DB | Should -BeLike '*Super Nintendo*'
    }

    It 'gibt korrektes Mapping fuer PS1' {
      $m = Get-RetroArchCoreMapping -ConsoleKey 'PS1'
      $m.Core | Should -Be 'swanstation_libretro'
    }

    It 'gibt DETECT fuer unbekannte Konsole' {
      $m = Get-RetroArchCoreMapping -ConsoleKey 'UNKNOWN_CONSOLE'
      $m.Core | Should -Be 'DETECT'
    }

    It 'Mapping ist case-insensitive' {
      $m = Get-RetroArchCoreMapping -ConsoleKey 'snes'
      $m.Core | Should -Be 'snes9x_libretro'
    }

    It 'Custom-Mapping ueberschreibt Standard' {
      $custom = @{ 'SNES' = @{ Core = 'bsnes_libretro'; DB = 'CustomDB' } }
      $m = Get-RetroArchCoreMapping -ConsoleKey 'SNES' -CustomMappings $custom
      $m.Core | Should -Be 'bsnes_libretro'
    }
  }

  Context 'Export-RetroArchPlaylist' {

    It 'exportiert eine LPL-Datei' {
      $tmpFile = Join-Path ([System.IO.Path]::GetTempPath()) "lpl_test_$(Get-Random).lpl"
      try {
        $items = @(
          @{ Path = 'C:\roms\snes\mario.zip'; Console = 'SNES'; Name = 'Super Mario World' }
          @{ Path = 'C:\roms\nes\zelda.zip'; Console = 'NES'; Name = 'Zelda' }
        )

        $r = Export-RetroArchPlaylist -Items $items -OutputPath $tmpFile
        $r.Status | Should -Be 'Success'
        $r.ItemCount | Should -Be 2
        (Test-Path -LiteralPath $tmpFile) | Should -BeTrue

        $content = Get-Content -LiteralPath $tmpFile -Raw
        $json = $content | ConvertFrom-Json
        $json.version | Should -Be '1.5'
        $json.items.Count | Should -Be 2
      } finally {
        Remove-Item -LiteralPath $tmpFile -Force -ErrorAction SilentlyContinue
      }
    }

    It 'behandelt leere Items-Liste' {
      $r = Export-RetroArchPlaylist -Items @() -OutputPath 'C:\dummy.lpl'
      $r.Status | Should -Be 'Empty'
      $r.ItemCount | Should -Be 0
    }

    It 'warnt bei unbekanntem Konsolen-Mapping' {
      $tmpFile = Join-Path ([System.IO.Path]::GetTempPath()) "lpl_warn_$(Get-Random).lpl"
      try {
        $items = @(@{ Path = 'C:\roms\game.bin'; Console = 'UNKNOWN_CONSOLE_XYZ'; Name = 'Game' })
        $r = Export-RetroArchPlaylist -Items $items -OutputPath $tmpFile
        $r.Warnings.Count | Should -BeGreaterThan 0
      } finally {
        Remove-Item -LiteralPath $tmpFile -Force -ErrorAction SilentlyContinue
      }
    }

    It 'verwendet Dateinamen als Label wenn Name fehlt' {
      $tmpFile = Join-Path ([System.IO.Path]::GetTempPath()) "lpl_noname_$(Get-Random).lpl"
      try {
        $items = @(@{ Path = 'C:\roms\cool_game.zip'; Console = 'SNES' })
        $r = Export-RetroArchPlaylist -Items $items -OutputPath $tmpFile
        $r.Status | Should -Be 'Success'

        $json = Get-Content -LiteralPath $tmpFile -Raw | ConvertFrom-Json
        $json.items[0].label | Should -Be 'cool_game'
      } finally {
        Remove-Item -LiteralPath $tmpFile -Force -ErrorAction SilentlyContinue
      }
    }

    It 'ueberspringt Items ohne Pfad' {
      $tmpFile = Join-Path ([System.IO.Path]::GetTempPath()) "lpl_skip_$(Get-Random).lpl"
      try {
        $items = @(
          @{ Console = 'SNES'; Name = 'NoPath' }
          @{ Path = 'C:\roms\game.zip'; Console = 'SNES'; Name = 'WithPath' }
        )

        $r = Export-RetroArchPlaylist -Items $items -OutputPath $tmpFile
        $r.ItemCount | Should -Be 1
      } finally {
        Remove-Item -LiteralPath $tmpFile -Force -ErrorAction SilentlyContinue
      }
    }
  }
}
