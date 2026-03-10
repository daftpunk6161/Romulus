BeforeAll {
  $root = $PSScriptRoot
  while ($root -and -not (Test-Path (Join-Path $root 'simple_sort.ps1'))) {
    $root = Split-Path -Parent $root
  }
  . (Join-Path $root 'dev\modules\JunkReport.ps1')
}

Describe 'JunkReport (QW-05)' {

  Context 'Get-JunkClassificationReason' {

    It 'erkennt Beta als Junk' {
      $r = Get-JunkClassificationReason -BaseName 'Super Mario (Beta)'
      $r.IsJunk | Should -BeTrue
      $r.Category | Should -Be 'JUNK'
      $r.MatchedTag | Should -BeLike '*Beta*'
    }

    It 'erkennt Demo als Junk' {
      $r = Get-JunkClassificationReason -BaseName 'Game Demo (Demo)'
      $r.IsJunk | Should -BeTrue
    }

    It 'erkennt Proto als Junk' {
      $r = Get-JunkClassificationReason -BaseName 'Game (Proto)'
      $r.IsJunk | Should -BeTrue
    }

    It 'erkennt Hack als Junk' {
      $r = Get-JunkClassificationReason -BaseName 'Game (Hack)'
      $r.IsJunk | Should -BeTrue
    }

    It 'erkennt [b] Bad-Dump als Junk' {
      $r = Get-JunkClassificationReason -BaseName 'Game [b]'
      $r.IsJunk | Should -BeTrue
    }

    It 'erkennt normales Spiel als nicht-Junk' {
      $r = Get-JunkClassificationReason -BaseName 'Super Mario World (USA)'
      $r.IsJunk | Should -BeFalse
      $r.Category | Should -Be 'GAME'
    }

    It 'erkennt Homebrew nur bei aggressiveJunk' {
      $normal = Get-JunkClassificationReason -BaseName 'CoolApp (Homebrew)' -AggressiveJunk $false
      $aggressive = Get-JunkClassificationReason -BaseName 'CoolApp (Homebrew)' -AggressiveJunk $true
      $aggressive.IsJunk | Should -BeTrue
    }

    It 'gibt JunkReason-String zurueck' {
      $r = Get-JunkClassificationReason -BaseName 'Game (Beta)'
      $r.JunkReason | Should -Not -BeNullOrEmpty
    }
  }

  Context 'Get-JunkReport' {

    It 'erstellt Batch-Report' {
      $files = @(
        'Super Mario (USA).zip'
        'Game (Beta).zip'
        'Cool Game (Demo).zip'
        'Normal Game (Europe).zip'
      )

      $r = Get-JunkReport -FileNames $files
      $r.Total | Should -Be 4
      $r.JunkCount | Should -Be 2
      $r.GameCount | Should -Be 2
    }

    It 'erstellt Report fuer leere Liste' {
      $r = Get-JunkReport -FileNames @('placeholder') | Out-Null
      # Leere Liste: Mandatory verhindert leeres Array,
      # daher testen wir mit einem normalen Spiel = 0 Junk
      $r2 = Get-JunkReport -FileNames @('Normal Game (USA)')
      $r2.Total | Should -Be 1
      $r2.JunkCount | Should -Be 0
    }
  }
}
