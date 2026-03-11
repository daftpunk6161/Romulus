BeforeAll {
  $root = $PSScriptRoot
  while ($root -and -not (Test-Path (Join-Path $root 'simple_sort.ps1'))) {
    $root = Split-Path -Parent $root
  }
  . (Join-Path $root 'dev\modules\ThemeManager.ps1')
}

Describe 'ThemeManager (QW-07)' {

  Context 'Resolve-EffectiveTheme' {

    It 'gibt dark zurueck bei dark Setting' {
      $theme = Resolve-EffectiveTheme -ThemeSetting 'dark'
      $theme | Should -Be 'dark'
    }

    It 'gibt light zurueck bei light Setting' {
      $theme = Resolve-EffectiveTheme -ThemeSetting 'light'
      $theme | Should -Be 'light'
    }

    It 'gibt dark oder light zurueck bei auto' {
      $theme = Resolve-EffectiveTheme -ThemeSetting 'auto'
      $theme | Should -BeIn @('dark','light')
    }
  }

  Context 'Get-ThemeColors' {

    It 'gibt Dark-Farbpalette zurueck' {
      $colors = Get-ThemeColors -Theme 'dark'
      $colors | Should -BeOfType [hashtable]
      $colors.Background | Should -Not -BeNullOrEmpty
      $colors.Accent | Should -Be '#00D4AA'
    }

    It 'gibt Light-Farbpalette zurueck' {
      $colors = Get-ThemeColors -Theme 'light'
      $colors.Background | Should -Be '#FAFAFA'
    }

    It 'alle Farbwerte sind Hex-Codes' {
      $colors = Get-ThemeColors -Theme 'dark'
      foreach ($key in $colors.Keys) {
        $colors[$key] | Should -Match '^#[0-9A-Fa-f]{6}$'
      }
    }

    It 'Dark und Light haben unterschiedliche Backgrounds' {
      $dark = Get-ThemeColors -Theme 'dark'
      $light = Get-ThemeColors -Theme 'light'
      $dark.Background | Should -Not -Be $light.Background
    }
  }

  Context 'Get-ThemeResourceDictionary' {

    It 'gibt gueltiges XAML zurueck' {
      $xaml = Get-ThemeResourceDictionary -Theme 'dark'
      $xaml | Should -Not -BeNullOrEmpty
      $xaml | Should -BeLike '*ResourceDictionary*'
      $xaml | Should -BeLike '*BackgroundBrush*'
      $xaml | Should -BeLike '*AccentBrush*'
    }

    It 'setzt korrekte Farbwerte' {
      $xaml = Get-ThemeResourceDictionary -Theme 'dark'
      $xaml | Should -BeLike '*#0F0F23*'  # Dark Background
    }
  }
}
