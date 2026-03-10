BeforeAll {
  . "$PSScriptRoot/../../modules/CommandPalette.ps1"
}

Describe 'MF-15: CommandPalette' {
  Describe 'Get-LevenshteinDistance' {
    It 'gibt 0 fuer identische Strings' {
      Get-LevenshteinDistance -Source 'test' -Target 'test' | Should -Be 0
    }

    It 'berechnet korrekte Distanz' {
      Get-LevenshteinDistance -Source 'kitten' -Target 'sitting' | Should -Be 3
    }

    It 'behandelt leere Strings' {
      Get-LevenshteinDistance -Source '' -Target 'abc' | Should -Be 3
      Get-LevenshteinDistance -Source 'abc' -Target '' | Should -Be 3
    }
  }

  Describe 'New-PaletteCommand' {
    It 'erstellt Command mit korrekten Feldern' {
      $cmd = New-PaletteCommand -Name 'Test Befehl' -Key 'test.cmd' -Category 'Test' -Shortcut 'Ctrl+T'
      $cmd.Name | Should -Be 'Test Befehl'
      $cmd.Key | Should -Be 'test.cmd'
      $cmd.Shortcut | Should -Be 'Ctrl+T'
    }
  }

  Describe 'Search-PaletteCommands' {
    BeforeAll {
      $script:commands = @(
        (New-PaletteCommand -Name 'DryRun starten' -Key 'run.dryrun' -Category 'Run')
        (New-PaletteCommand -Name 'Konvertierung starten' -Key 'convert.start' -Category 'Convert')
        (New-PaletteCommand -Name 'Settings oeffnen' -Key 'settings.open' -Category 'Settings')
        (New-PaletteCommand -Name 'DAT-Quellen aktualisieren' -Key 'dat.update' -Category 'DAT')
      )
    }

    It 'findet via Substring' {
      $result = Search-PaletteCommands -Query 'konv' -Commands $script:commands
      $result.Count | Should -BeGreaterThan 0
      $result[0].Key | Should -Be 'convert.start'
    }

    It 'findet via Key-Match' {
      $result = Search-PaletteCommands -Query 'settings' -Commands $script:commands
      $result.Count | Should -BeGreaterThan 0
    }

    It 'gibt alle bei leerem Query' {
      $result = Search-PaletteCommands -Query '' -Commands $script:commands
      $result.Count | Should -Be 4
    }

    It 'gibt keine Ergebnisse bei xyz' {
      $result = Search-PaletteCommands -Query 'xyznonexistent' -Commands $script:commands -MaxDistance 1
      $result.Count | Should -Be 0
    }

    It 'findet via Fuzzy-Match' {
      $result = Search-PaletteCommands -Query 'staren' -Commands $script:commands -MaxDistance 3
      $result.Count | Should -BeGreaterThan 0
    }
  }

  Describe 'Get-DefaultPaletteCommands' {
    It 'gibt Standard-Commands zurueck' {
      $cmds = Get-DefaultPaletteCommands
      $cmds.Count | Should -BeGreaterOrEqual 5
    }
  }
}
