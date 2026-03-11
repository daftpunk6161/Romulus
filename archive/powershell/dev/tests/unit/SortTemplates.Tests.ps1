BeforeAll {
  . "$PSScriptRoot/../../modules/SortTemplates.ps1"
}

Describe 'MF-22: SortTemplates' {
  Describe 'Get-DefaultSortTemplates' {
    It 'gibt Standard-Templates zurueck' {
      $templates = Get-DefaultSortTemplates
      $templates.Keys | Should -Contain 'RetroArch'
      $templates.Keys | Should -Contain 'EmulationStation'
      $templates.Keys | Should -Contain 'LaunchBox'
      $templates.Keys | Should -Contain 'Batocera'
      $templates.Keys | Should -Contain 'Flat'
    }

    It 'Templates haben Name und Pattern' {
      $templates = Get-DefaultSortTemplates
      $templates.RetroArch.Name | Should -Be 'RetroArch'
      $templates.RetroArch.Pattern | Should -Not -BeNullOrEmpty
      $templates.RetroArch.ConsoleMappings | Should -Not -BeNullOrEmpty
    }
  }

  Describe 'Resolve-SortTemplatePath' {
    It 'loest RetroArch-Pfad korrekt auf' {
      $result = Resolve-SortTemplatePath -TemplateName 'RetroArch' -ConsoleKey 'SNES' -FileName 'game.zip' -OutputRoot 'D:\Output'
      $result.Status | Should -Be 'OK'
      $result.Path | Should -BeLike '*Nintendo*Super Nintendo*game.zip'
    }

    It 'loest EmulationStation-Pfad korrekt auf' {
      $result = Resolve-SortTemplatePath -TemplateName 'EmulationStation' -ConsoleKey 'SNES' -FileName 'game.zip' -OutputRoot 'D:\Output'
      $result.Status | Should -Be 'OK'
      $result.Path | Should -BeLike '*roms*snes*game.zip'
    }

    It 'loest Flat-Pfad korrekt auf' {
      $result = Resolve-SortTemplatePath -TemplateName 'Flat' -ConsoleKey 'SNES' -FileName 'game.zip' -OutputRoot 'D:\Output'
      $result.Status | Should -Be 'OK'
      $result.Path | Should -BeLike '*game.zip'
    }

    It 'gibt Fehler bei unbekanntem Template' {
      $result = Resolve-SortTemplatePath -TemplateName 'Unbekannt' -ConsoleKey 'SNES' -FileName 'test.zip' -OutputRoot 'D:\Out'
      $result.Status | Should -Be 'Error'
    }
  }

  Describe 'Get-TemplateNames' {
    It 'gibt alle Template-Namen zurueck' {
      $names = Get-TemplateNames
      $names | Should -Contain 'RetroArch'
      $names | Should -Contain 'Flat'
    }
  }

  Describe 'Test-TemplateMappingComplete' {
    It 'erkennt vollstaendiges Mapping' {
      $result = Test-TemplateMappingComplete -TemplateName 'RetroArch' -ConsoleKeys @('SNES', 'NES')
      $result.Complete | Should -BeTrue
      $result.Missing.Count | Should -Be 0
    }

    It 'erkennt fehlende Mappings' {
      $result = Test-TemplateMappingComplete -TemplateName 'LaunchBox' -ConsoleKeys @('SNES', 'PCE')
      $result.Complete | Should -BeFalse
      $result.Missing | Should -Contain 'PCE'
    }
  }
}
