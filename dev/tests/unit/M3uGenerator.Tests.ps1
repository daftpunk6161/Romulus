BeforeAll {
  $root = $PSScriptRoot
  while ($root -and -not (Test-Path (Join-Path $root 'simple_sort.ps1'))) {
    $root = Split-Path -Parent $root
  }
  . (Join-Path $root 'dev\modules\M3uGenerator.ps1')
}

Describe 'M3uGenerator (QW-15)' {

  Context 'Group-MultiDiscFiles' {

    It 'gruppiert Multi-Disc-Dateien korrekt' {
      $files = @(
        'C:\roms\Final Fantasy VII (Disc 1).chd'
        'C:\roms\Final Fantasy VII (Disc 2).chd'
        'C:\roms\Final Fantasy VII (Disc 3).chd'
      )

      $groups = Group-MultiDiscFiles -Files $files
      $groups.Count | Should -Be 1
      $group = $groups.Values | Select-Object -First 1
      $group.GameName | Should -BeLike '*Final Fantasy VII*'
      $group.Discs.Count | Should -Be 3
    }

    It 'ignoriert Single-Disc-Spiele (ohne Disc-Tag)' {
      $files = @('C:\roms\Super Mario World.chd')
      $groups = Group-MultiDiscFiles -Files $files
      $groups.Count | Should -Be 0
    }

    It 'gruppiert verschiedene Spiele separat' {
      $files = @(
        'C:\roms\FF7 (Disc 1).chd'
        'C:\roms\FF7 (Disc 2).chd'
        'C:\roms\FF8 (Disc 1).chd'
        'C:\roms\FF8 (Disc 2).chd'
      )

      $groups = Group-MultiDiscFiles -Files $files
      $groups.Count | Should -Be 2
    }

    It 'erkennt Disc-Tags case-insensitive' {
      $files = @(
        'C:\roms\Game (disc 1).chd'
        'C:\roms\Game (DISC 2).chd'
      )

      $groups = Group-MultiDiscFiles -Files $files
      $groups.Count | Should -Be 1
    }
  }

  Context 'Test-DiscSequenceComplete' {

    It 'erkennt vollstaendige Sequenz' {
      $r = Test-DiscSequenceComplete -DiscNumbers @(1, 2, 3)
      $r.Complete | Should -BeTrue
      $r.Missing.Count | Should -Be 0
    }

    It 'erkennt fehlende Disc' {
      $r = Test-DiscSequenceComplete -DiscNumbers @(1, 3)
      $r.Complete | Should -BeFalse
      $r.Missing | Should -Contain 2
    }

    It 'behandelt einzelne Disc' {
      $r = Test-DiscSequenceComplete -DiscNumbers @(1)
      $r.Complete | Should -BeTrue
    }
  }

  Context 'New-M3uPlaylist' {

    It 'setzt WouldCreate im DryRun-Modus' {
      $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) "m3u_test_$(Get-Random)"
      New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
      try {
        $discs = New-Object 'System.Collections.Generic.SortedDictionary[int,string]'
        $discs.Add(1, 'C:\roms\FF7 (Disc 1).chd')
        $discs.Add(2, 'C:\roms\FF7 (Disc 2).chd')

        $r = New-M3uPlaylist -DiscFiles $discs -OutputDir $tmpDir -GameName 'Final Fantasy VII' -Mode DryRun
        $r.Status | Should -Be 'WouldCreate'
        $r.DiscCount | Should -Be 2
      } finally {
        Remove-Item -LiteralPath $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
      }
    }

    It 'erstellt M3U-Datei im Move-Modus' {
      $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) "m3u_test_$(Get-Random)"
      New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
      try {
        $discs = New-Object 'System.Collections.Generic.SortedDictionary[int,string]'
        $discs.Add(1, 'C:\roms\Game (Disc 1).chd')
        $discs.Add(2, 'C:\roms\Game (Disc 2).chd')

        $r = New-M3uPlaylist -DiscFiles $discs -OutputDir $tmpDir -GameName 'TestGame' -Mode Move
        $r.Status | Should -Be 'Created'
        (Test-Path -LiteralPath $r.M3uPath) | Should -BeTrue

        $content = Get-Content -LiteralPath $r.M3uPath -Raw
        $content | Should -BeLike '*#EXTM3U*'
        $content | Should -BeLike '*Disc 1*'
        $content | Should -BeLike '*Disc 2*'
      } finally {
        Remove-Item -LiteralPath $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
      }
    }

    It 'meldet leere Disc-Liste' {
      $discs = New-Object 'System.Collections.Generic.SortedDictionary[int,string]'
      $r = New-M3uPlaylist -DiscFiles $discs -OutputDir 'C:\tmp' -GameName 'Test'
      $r.Status | Should -Be 'NoDiscs'
    }
  }
}
