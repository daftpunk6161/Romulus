BeforeAll {
  . "$PSScriptRoot/../../modules/ToolImport.ps1"
}

Describe 'ToolImport (XL-12)' {

  Describe 'Get-SupportedImportFormats' {
    It 'gibt mehrere Formate zurueck' {
      $formats = Get-SupportedImportFormats
      @($formats).Count | Should -BeGreaterOrEqual 4
    }

    It 'enthaelt clrmamepro' {
      $formats = Get-SupportedImportFormats
      $cmp = @($formats | Where-Object { $_.Key -eq 'clrmamepro' })
      $cmp.Count | Should -Be 1
      @($cmp[0].Extensions) | Should -Contain '.dat'
    }

    It 'enthaelt romvault' {
      $formats = Get-SupportedImportFormats
      $rv = @($formats | Where-Object { $_.Key -eq 'romvault' })
      $rv.Count | Should -Be 1
    }
  }

  Describe 'New-ImportConfig' {
    It 'erstellt Import-Konfiguration' {
      $cfg = New-ImportConfig -SourceFormat 'clrmamepro' -SourcePath 'C:\dat\test.dat'
      $cfg.SourceFormat | Should -Be 'clrmamepro'
      $cfg.SourcePath | Should -Be 'C:\dat\test.dat'
      $cfg.MergeMode | Should -Be 'Merge'
      $cfg.Status | Should -Be 'Pending'
    }

    It 'akzeptiert Replace-Modus' {
      $cfg = New-ImportConfig -SourceFormat 'romvault' -SourcePath 'x' -MergeMode 'Replace'
      $cfg.MergeMode | Should -Be 'Replace'
    }
  }

  Describe 'Read-ClrmameproDat' {
    It 'parst einfaches DAT' {
      $content = @"
clrmamepro (
  name "Test"
  description "Test DAT"
)

game (
  name "Super Mario Bros"
  description "Super Mario Bros"
  rom ( name "Super Mario Bros.nes" size 40976 sha1 abc123 )
)

game (
  name "Zelda"
  description "Legend of Zelda"
  rom ( name "Zelda.nes" size 131088 sha1 def456 )
  rom ( name "Zelda.sav" size 8192 md5 789abc )
)
"@
      $result = Read-ClrmameproDat -Content $content
      $result.Format | Should -Be 'clrmamepro'
      $result.EntryCount | Should -Be 2
      $result.Entries[0].Name | Should -Be 'Super Mario Bros'
      @($result.Entries[0].Roms).Count | Should -Be 1
      @($result.Entries[1].Roms).Count | Should -Be 2
    }

    It 'extrahiert SHA1-Hashes' {
      $content = @"
game (
  name "TestGame"
  rom ( name "test.rom" size 1024 sha1 aabbccdd )
)
"@
      $result = Read-ClrmameproDat -Content $content
      $result.Entries[0].Roms[0].SHA1 | Should -Be 'aabbccdd'
    }

    It 'behandelt leeres DAT' {
      $result = Read-ClrmameproDat -Content ''
      $result.EntryCount | Should -Be 0
    }
  }

  Describe 'ConvertTo-RomCleanupIndex' {
    It 'konvertiert Import-Ergebnis' {
      $importResult = @{
        Entries = @(
          @{ Name = 'Game1'; Description = 'Desc1'; Roms = @(@{ Name = 'g1.rom' }) },
          @{ Name = 'Game2'; Description = 'Desc2'; Roms = @() }
        )
        EntryCount = 2
        Format = 'clrmamepro'
      }
      $index = ConvertTo-RomCleanupIndex -ImportResult $importResult -ConsoleKey 'NES'
      $index.EntryCount | Should -Be 2
      $index.ConsoleKey | Should -Be 'NES'
      $index.Index['Game1'].RomCount | Should -Be 1
    }
  }

  Describe 'Get-ImportStatistics' {
    It 'berechnet Statistiken' {
      $importResult = @{
        Entries = @(
          @{ Name = 'G1'; Roms = @(@{},@{}) },
          @{ Name = 'G2'; Roms = @(@{}) }
        )
        EntryCount = 2
        Format = 'logiqx'
      }
      $stats = Get-ImportStatistics -ImportResult $importResult
      $stats.GameCount | Should -Be 2
      $stats.TotalRoms | Should -Be 3
      $stats.AvgRomsPerGame | Should -Be 1.5
    }
  }
}
