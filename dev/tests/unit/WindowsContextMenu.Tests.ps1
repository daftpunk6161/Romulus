BeforeAll {
  . "$PSScriptRoot/../../modules/WindowsContextMenu.ps1"
}

Describe 'WindowsContextMenu (XL-03)' {

  Describe 'New-ContextMenuEntry' {
    It 'erstellt Eintrag mit Pflichtfeldern' {
      $entry = New-ContextMenuEntry -Label 'Scannen' -Command 'pwsh test.ps1'
      $entry.Label | Should -Be 'Scannen'
      $entry.Command | Should -Be 'pwsh test.ps1'
      $entry.Type | Should -Be 'Directory'
    }

    It 'akzeptiert optionale Position' {
      $entry = New-ContextMenuEntry -Label 'Test' -Command 'cmd' -Position 'Top'
      $entry.Position | Should -Be 'Top'
    }

    It 'akzeptiert optionales Icon' {
      $entry = New-ContextMenuEntry -Label 'Test' -Command 'cmd' -Icon 'C:\icon.ico'
      $entry.Icon | Should -Be 'C:\icon.ico'
    }
  }

  Describe 'Get-DefaultContextMenuEntries' {
    It 'gibt drei Standard-Eintraege zurueck' {
      $entries = Get-DefaultContextMenuEntries
      @($entries).Count | Should -Be 3
    }

    It 'erster Eintrag ist DryRun' {
      $entries = Get-DefaultContextMenuEntries
      $entries[0].Label | Should -BeLike '*DryRun*'
      $entries[0].Command | Should -BeLike '*DryRun*'
    }

    It 'dritter Eintrag oeffnet GUI' {
      $entries = Get-DefaultContextMenuEntries
      $entries[2].Label | Should -BeLike '*GUI*'
    }

    It 'verwendet benutzerdefinierten ScriptPath' {
      $entries = Get-DefaultContextMenuEntries -ScriptPath 'D:\Tools\rc.ps1'
      $entries[0].Command | Should -BeLike '*D:\Tools\rc.ps1*'
    }
  }

  Describe 'ConvertTo-RegistryCommands' {
    It 'erstellt Registry-Befehle aus Eintraegen' {
      $entries = Get-DefaultContextMenuEntries
      $cmds = ConvertTo-RegistryCommands -Entries $entries
      @($cmds).Count | Should -Be 3
      $cmds[0].KeyPath | Should -BeLike '*RomCleanup_1*'
      $cmds[1].KeyPath | Should -BeLike '*RomCleanup_2*'
    }

    It 'setzt Command-Path korrekt' {
      $entries = @((New-ContextMenuEntry -Label 'Test' -Command 'cmd /c echo'))
      $cmds = @(ConvertTo-RegistryCommands -Entries $entries)
      $cmds[0].CommandPath | Should -BeLike '*command*'
    }
  }

  Describe 'Get-ContextMenuUninstallCommands' {
    It 'gibt korrekte Anzahl Pfade zurueck' {
      $paths = Get-ContextMenuUninstallCommands -Count 3
      @($paths).Count | Should -Be 3
      $paths[0] | Should -BeLike '*RomCleanup_1*'
    }
  }

  Describe 'Test-ContextMenuInstalled' {
    It 'gibt Pruef-Status zurueck' {
      $result = Test-ContextMenuInstalled
      $result.CheckRequired | Should -Be $true
      $result.ExpectedKeyPattern | Should -BeLike '*RomCleanup*'
    }
  }

  Describe 'Get-ContextMenuStatistics' {
    It 'berechnet Statistiken korrekt' {
      $entries = Get-DefaultContextMenuEntries
      $stats = Get-ContextMenuStatistics -Entries $entries
      $stats.EntryCount | Should -Be 3
      $stats.TargetType | Should -Be 'Directory'
      $stats.RegistryScope | Should -Be 'CurrentUser'
    }
  }
}
