BeforeAll {
  . "$PSScriptRoot/../../modules/FormatPriority.ps1"
}

Describe 'MF-10: FormatPriority' {
  Describe 'Get-DefaultFormatPriority' {
    It 'gibt Defaults fuer bekannte Konsolen' {
      $defaults = Get-DefaultFormatPriority
      $defaults.ContainsKey('PS1') | Should -BeTrue
      $defaults.PS1[0] | Should -Be 'CHD'
    }
  }

  Describe 'Get-FormatPriority' {
    It 'gibt Default-Liste fuer bekannte Konsole' {
      $result = Get-FormatPriority -ConsoleKey 'PS1'
      $result[0] | Should -Be 'CHD'
    }

    It 'gibt User-Prioritaet wenn vorhanden' {
      $userPrio = @{ PS1 = @('ISO', 'CHD', 'PBP') }
      $result = Get-FormatPriority -ConsoleKey 'PS1' -UserPriority $userPrio
      $result[0] | Should -Be 'ISO'
    }

    It 'gibt Fallback fuer unbekannte Konsole' {
      $result = Get-FormatPriority -ConsoleKey 'UnknownConsole'
      $result | Should -Contain 'ZIP'
    }
  }

  Describe 'Get-FormatPriorityScore' {
    It 'gibt hoechsten Score fuer erstes Format' {
      $scoreChd = Get-FormatPriorityScore -Format 'CHD' -ConsoleKey 'PS1'
      $scoreIso = Get-FormatPriorityScore -Format 'ISO' -ConsoleKey 'PS1'
      $scoreChd | Should -BeGreaterThan $scoreIso
    }

    It 'gibt 0 fuer unbekanntes Format' {
      $score = Get-FormatPriorityScore -Format 'XYZ' -ConsoleKey 'PS1'
      $score | Should -Be 0
    }
  }

  Describe 'Test-FormatPreferred' {
    It 'bevorzugt CHD ueber ISO fuer PS1' {
      $result = Test-FormatPreferred -FormatA 'CHD' -FormatB 'ISO' -ConsoleKey 'PS1'
      $result.Preferred | Should -Be 'CHD'
      $result.APreferred | Should -BeTrue
    }

    It 'bevorzugt RVZ ueber ISO fuer GC' {
      $result = Test-FormatPreferred -FormatA 'ISO' -FormatB 'RVZ' -ConsoleKey 'GC'
      $result.Preferred | Should -Be 'RVZ'
    }
  }

  Describe 'Merge-FormatPriority' {
    It 'merged User-Prioritaeten mit Defaults' {
      $user = @{ PS1 = @('ISO', 'CHD'); CustomConsole = @('ZIP') }
      $merged = Merge-FormatPriority -UserPriority $user
      $merged.PS1[0] | Should -Be 'ISO'
      $merged.ContainsKey('CustomConsole') | Should -BeTrue
      $merged.ContainsKey('SNES') | Should -BeTrue
    }
  }
}
