BeforeAll {
  . "$PSScriptRoot/../../modules/ThemeEngine.ps1"
}

Describe 'LF-20: ThemeEngine' {
  BeforeAll {
    $testColors = @{
      Background  = '#1A1A2E'
      Surface     = '#16213E'
      Primary     = '#0F3460'
      Accent      = '#E94560'
      TextPrimary = '#EAEAEA'
    }
  }

  Describe 'Get-BuiltinThemes' {
    It 'liefert mindestens 2 Themes' {
      $themes = Get-BuiltinThemes
      $themes.Count | Should -BeGreaterOrEqual 2
    }

    It 'enthaelt Retro Dark-Theme' {
      $themes = Get-BuiltinThemes
      $dark = $themes | Where-Object { $_.Name -eq 'Retro Dark' }
      $dark | Should -Not -BeNullOrEmpty
    }
  }

  Describe 'New-CustomTheme' {
    It 'erstellt Theme mit Farben' {
      $t = New-CustomTheme -Name 'Retro' -Colors $testColors
      $t.Name | Should -Be 'Retro'
      $t.Colors.Background | Should -Be '#1A1A2E'
      $t.Colors.Accent | Should -Be '#E94560'
    }
  }

  Describe 'ConvertTo-ResourceDictionary' {
    It 'erzeugt ResourceDictionary-Daten' {
      $t = New-CustomTheme -Name 'Test' -Colors $testColors
      $rd = ConvertTo-ResourceDictionary -Theme $t
      $rd.Entries.Count | Should -BeGreaterThan 0
      $rd.ThemeKey | Should -BeLike 'custom-*'
    }
  }

  Describe 'Test-ThemeValid' {
    It 'akzeptiert gueltiges Theme' {
      $t = New-CustomTheme -Name 'Valid' -Colors $testColors
      $r = Test-ThemeValid -Theme $t
      $r.Valid | Should -Be $true
    }

    It 'lehnt Theme ohne Name ab' {
      $t = @{ Name = ''; Colors = $testColors }
      $r = Test-ThemeValid -Theme $t
      $r.Valid | Should -Be $false
    }
  }

  Describe 'Export/Import-ThemeJson' {
    It 'roundtripped korrekt' {
      $t = New-CustomTheme -Name 'Exported' -Colors $testColors -Author 'Tester'
      $json = Export-ThemeJson -Theme $t
      $imported = Import-ThemeJson -JsonString $json
      $imported.Name | Should -Be 'Exported'
      $imported.Colors.Accent | Should -Be '#E94560'
    }
  }
}
