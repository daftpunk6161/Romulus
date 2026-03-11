BeforeAll {
  . "$PSScriptRoot/../../modules/HashDatabaseExport.ps1"
}

Describe 'LF-11: HashDatabaseExport' {
  Describe 'New-HashDatabase' {
    It 'erstellt leere DB' {
      $db = New-HashDatabase -Name 'TestDB' -HashType 'SHA256'
      $db.Name | Should -Be 'TestDB'
      $db.HashType | Should -Be 'SHA256'
      $db.Entries.Count | Should -Be 0
    }
  }

  Describe 'Add-HashEntry' {
    It 'fuegt Eintrag hinzu' {
      $db = New-HashDatabase -Name 'Test'
      Add-HashEntry -Database $db -FilePath 'rom.sfc' -Hash 'AABB' -Size 1024 -Console 'snes' | Out-Null
      $db.Entries.Count | Should -Be 1
      $db.Entries['AABB'].FilePath | Should -Be 'rom.sfc'
    }
  }

  Describe 'Export-HashDatabaseJson' {
    It 'exportiert als gueltigem JSON' {
      $db = New-HashDatabase -Name 'Test'
      Add-HashEntry -Database $db -FilePath 'r.sfc' -Hash 'H1' -Console 'snes' -GameName 'G1' | Out-Null
      $json = Export-HashDatabaseJson -Database $db
      $parsed = $json | ConvertFrom-Json
      $parsed.count | Should -Be 1
      $parsed.name | Should -Be 'Test'
    }
  }

  Describe 'Export-HashDatabaseCsv' {
    It 'exportiert als CSV mit Header' {
      $db = New-HashDatabase -Name 'Test'
      Add-HashEntry -Database $db -FilePath 'r.sfc' -Hash 'H1' -Console 'snes' -GameName 'G1' | Out-Null
      $csv = Export-HashDatabaseCsv -Database $db
      $csv | Should -BeLike '*Hash,FilePath*'
    }

    It 'schuetzt vor CSV-Injection' {
      $db = New-HashDatabase -Name 'Test'
      Add-HashEntry -Database $db -FilePath '=cmd' -Hash 'H1' -GameName '+danger' | Out-Null
      $csv = Export-HashDatabaseCsv -Database $db
      $csv | Should -BeLike "*'=cmd*"
    }
  }

  Describe 'Import-HashDatabaseJson' {
    It 'importiert aus JSON' {
      $db = New-HashDatabase -Name 'Export'
      Add-HashEntry -Database $db -FilePath 'x.sfc' -Hash 'HH' -Console 'nes' -GameName 'Gx' | Out-Null
      $json = Export-HashDatabaseJson -Database $db
      $imported = Import-HashDatabaseJson -JsonString $json
      $imported.Entries.Count | Should -Be 1
    }
  }

  Describe 'Get-HashDatabaseStatistics' {
    It 'berechnet Statistiken' {
      $db = New-HashDatabase -Name 'Test'
      Add-HashEntry -Database $db -FilePath 'a' -Hash 'H1' -Console 'snes' -Size 1024 | Out-Null
      Add-HashEntry -Database $db -FilePath 'b' -Hash 'H2' -Console 'nes' -Size 512 | Out-Null
      $stats = Get-HashDatabaseStatistics -Database $db
      $stats.TotalEntries | Should -Be 2
      $stats.TotalSize | Should -Be 1536
      $stats.ByConsole.Count | Should -Be 2
    }
  }
}
