BeforeAll {
  . "$PSScriptRoot/../../modules/TosecDatSupport.ps1"
}

Describe 'MF-13: TosecDatSupport' {
  Describe 'Test-TosecDatFormat' {
    It 'erkennt TOSEC-Format' {
      $xml = '<datafile><header><name>TOSEC - Test</name></header></datafile>'
      Test-TosecDatFormat -XmlContent $xml | Should -BeTrue
    }

    It 'lehnt Nicht-TOSEC ab' {
      $xml = '<datafile><header><name>No-Intro - SNES</name></header></datafile>'
      Test-TosecDatFormat -XmlContent $xml | Should -BeFalse
    }
  }

  Describe 'ConvertFrom-TosecName' {
    It 'parst Titel korrekt' {
      $result = ConvertFrom-TosecName -TosecName 'Super Game (1995)(Nintendo)(EU)'
      $result.Title | Should -Be 'Super Game'
      $result.Year | Should -Be '1995'
      $result.Publisher | Should -Be 'Nintendo'
      $result.Region | Should -Be 'EU'
    }

    It 'parst Titel ohne Publisher' {
      $result = ConvertFrom-TosecName -TosecName 'Test Game (2001)(EU)'
      $result.Title | Should -Be 'Test Game'
      $result.Year | Should -Be '2001'
    }

    It 'parst Titel nur mit Jahr' {
      $result = ConvertFrom-TosecName -TosecName 'Simple (1990)'
      $result.Title | Should -Be 'Simple'
      $result.Year | Should -Be '1990'
    }
  }

  Describe 'ConvertFrom-TosecDat' {
    It 'parst gueltige TOSEC-DAT-Datei' {
      $xmlContent = @'
<?xml version="1.0"?>
<datafile>
  <header><name>TOSEC - Test</name></header>
  <game name="Test Game (1995)(Pub)(EU)">
    <rom name="test.bin" size="1024" crc="AABBCCDD" sha1="1234567890ABCDEF1234567890ABCDEF12345678"/>
  </game>
</datafile>
'@
      $datFile = Join-Path $TestDrive 'test.dat'
      Set-Content -Path $datFile -Value $xmlContent -Encoding UTF8
      $result = ConvertFrom-TosecDat -Path $datFile
      $result.Status | Should -Be 'OK'
      $result.Count | Should -Be 1
    }

    It 'meldet Fehler bei fehlender Datei' {
      $result = ConvertFrom-TosecDat -Path 'C:\nonexistent.dat'
      $result.Status | Should -Be 'Error'
    }
  }

  Describe 'Merge-TosecWithDatIndex' {
    It 'fuegt neue Eintraege hinzu' {
      $existing = @{ 'Game A' = @{ Name = 'Game A' } }
      $tosec = @{ 'Game B' = @{ Name = 'Game B'; Source = 'TOSEC' } }
      $result = Merge-TosecWithDatIndex -ExistingIndex $existing -TosecIndex $tosec
      $result.TotalCount | Should -Be 2
      $result.Added | Should -Be 1
    }

    It 'ueberspringt bestehende ohne Overwrite' {
      $existing = @{ 'Game A' = @{ Name = 'Game A'; Source = 'NoIntro' } }
      $tosec = @{ 'Game A' = @{ Name = 'Game A'; Source = 'TOSEC' } }
      $result = Merge-TosecWithDatIndex -ExistingIndex $existing -TosecIndex $tosec -OverwriteExisting $false
      $result.Skipped | Should -Be 1
      $result.MergedIndex['Game A'].Source | Should -Be 'NoIntro'
    }
  }
}
