BeforeAll {
  . "$PSScriptRoot/../../modules/Accessibility.ps1"
}

Describe 'LF-13: Accessibility' {
  Describe 'Get-AccessibilityDefaults' {
    It 'liefert Standard-Einstellungen' {
      $defaults = Get-AccessibilityDefaults
      $defaults.FontScale | Should -Be 1.0
      $defaults.HighContrast | Should -Be $false
      $defaults.ReducedMotion | Should -Be $false
    }
  }

  Describe 'Get-ScaledFontSize' {
    It 'skaliert Schriftgroesse' {
      $scaled = Get-ScaledFontSize -BaseSizePt 14 -Scale 1.5
      $scaled | Should -Be 21
    }

    It 'clipped auf Minimum' {
      $scaled = Get-ScaledFontSize -BaseSizePt 14 -Scale 0.1
      $scaled | Should -BeGreaterOrEqual 8
    }
  }

  Describe 'Get-AccessibleColorPair' {
    It 'gibt Farb-Paar zurueck' {
      $pair = Get-AccessibleColorPair -Theme 'Dark'
      $pair.Foreground | Should -Not -BeNullOrEmpty
      $pair.Background | Should -Not -BeNullOrEmpty
    }
  }

  Describe 'New-AriaLabel' {
    It 'erzeugt Label fuer Element' {
      $label = New-AriaLabel -ElementName 'btnStart' -Description 'Start Scan'
      $label | Should -Not -BeNullOrEmpty
      $label.Label | Should -BeLike '*Start Scan*'
    }
  }

  Describe 'Get-FocusOrder' {
    It 'gibt sortierte Reihenfolge zurueck' {
      $elements = @('txtPath', 'btnStart', 'btnStop')
      $order = Get-FocusOrder -Elements $elements
      $order[0].Name | Should -Be 'txtPath'
      $order[1].Name | Should -Be 'btnStart'
      $order[2].Name | Should -Be 'btnStop'
    }
  }

  Describe 'Test-ContrastRatio' {
    It 'bestaetigt ausreichendes Kontrastverhaeltnis' {
      # White luminance ~1.0, Black luminance ~0.0
      $result = Test-ContrastRatio -LuminanceFg 1.0 -LuminanceBg 0.0
      $result.Ratio | Should -BeGreaterThan 4.5
      $result.PassAA | Should -Be $true
    }

    It 'erkennt unzureichendes Kontrastverhaeltnis' {
      $result = Test-ContrastRatio -LuminanceFg 0.3 -LuminanceBg 0.35
      $result.PassAA | Should -Be $false
    }
  }
}
