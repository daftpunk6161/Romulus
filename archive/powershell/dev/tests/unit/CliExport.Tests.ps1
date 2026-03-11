BeforeAll {
  $root = $PSScriptRoot
  while ($root -and -not (Test-Path (Join-Path $root 'simple_sort.ps1'))) {
    $root = Split-Path -Parent $root
  }
  . (Join-Path $root 'dev\modules\CliExport.ps1')
}

Describe 'CliExport (QW-10)' {

  Context 'Export-CliCommand' {

    It 'generiert Basis-Kommando' {
      $settings = @{ Roots = @('D:\Roms'); Mode = 'DryRun' }
      $cmd = Export-CliCommand -Settings $settings
      $cmd | Should -Not -BeNullOrEmpty
      $cmd | Should -BeLike '*-Roots*'
      $cmd | Should -BeLike '*DryRun*'
    }

    It 'quotet Pfade mit Leerzeichen' {
      $settings = @{ Roots = @('D:\My Roms') }
      $cmd = Export-CliCommand -Settings $settings
      $cmd | Should -BeLike '*"D:\My Roms"*'
    }

    It 'enthaelt PreferRegions' {
      $settings = @{ PreferRegions = @('EU','US','JP') }
      $cmd = Export-CliCommand -Settings $settings
      $cmd | Should -BeLike '*-PreferRegions*'
      $cmd | Should -BeLike '*EU*US*JP*'
    }

    It 'enthaelt AggressiveJunk-Flag' {
      $settings = @{ AggressiveJunk = $true }
      $cmd = Export-CliCommand -Settings $settings
      $cmd | Should -BeLike '*-AggressiveJunk*'
    }

    It 'enthaelt UseDat-Flag' {
      $settings = @{ dat = @{ useDat = $true } }
      $cmd = Export-CliCommand -Settings $settings
      $cmd | Should -BeLike '*-UseDat*'
    }
  }

  Context 'ConvertTo-SafeCliArg' {

    It 'gibt einfachen Wert unveraendert zurueck' {
      $r = ConvertTo-SafeCliArg -Value 'D:\Roms'
      $r | Should -Be 'D:\Roms'
    }

    It 'quotet Wert mit Leerzeichen' {
      $r = ConvertTo-SafeCliArg -Value 'D:\My Roms'
      $r | Should -Be '"D:\My Roms"'
    }

    It 'escaped innere Anfuehrungszeichen' {
      $r = ConvertTo-SafeCliArg -Value 'path "with" quotes'
      $r | Should -BeLike '*\"*'
    }
  }

  Context 'ConvertFrom-CliCommand - Round-Trip' {

    It 'parst Roots zurueck' {
      $cmd = 'pwsh -NoProfile -File ./Invoke-RomCleanup.ps1 -Roots D:\Roms -Mode DryRun'
      $s = ConvertFrom-CliCommand -Command $cmd
      $s.Roots | Should -Contain 'D:\Roms'
      $s.Mode | Should -Be 'DryRun'
    }

    It 'parst AggressiveJunk-Flag' {
      $cmd = 'pwsh -NoProfile -File ./Invoke-RomCleanup.ps1 -Roots D:\Roms -AggressiveJunk'
      $s = ConvertFrom-CliCommand -Command $cmd
      $s.AggressiveJunk | Should -BeTrue
    }

    It 'parst PreferRegions zurueck' {
      $cmd = 'pwsh -NoProfile -File ./Invoke-RomCleanup.ps1 -PreferRegions EU,US,JP'
      $s = ConvertFrom-CliCommand -Command $cmd
      $s.PreferRegions | Should -HaveCount 3
    }
  }
}
