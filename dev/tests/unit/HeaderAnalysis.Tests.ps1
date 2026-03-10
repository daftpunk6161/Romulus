BeforeAll {
  . "$PSScriptRoot/../../modules/HeaderAnalysis.ps1"
}

Describe 'MF-03: HeaderAnalysis' {
  Describe 'Read-RomHeader mit NES' {
    It 'erkennt gueltigen iNES Header' {
      $tmpFile = Join-Path $TestDrive 'test.nes'
      # iNES Header: NES\x1A + PRG=2, CHR=1
      $header = [byte[]]@(0x4E, 0x45, 0x53, 0x1A, 2, 1, 0x01, 0x00, 0,0,0,0,0,0,0,0)
      $padding = [byte[]]::new(1024)
      $all = $header + $padding
      [System.IO.File]::WriteAllBytes($tmpFile, $all)

      $result = Read-RomHeader -FilePath $tmpFile
      $result.Valid | Should -BeTrue
      $result.Platform | Should -Be 'NES'
      $result.Details.PrgSizeKB | Should -Be 32
    }

    It 'erkennt ungueltigen Header' {
      $tmpFile = Join-Path $TestDrive 'bad.bin'
      $junk = [byte[]]::new(1024)
      [System.IO.File]::WriteAllBytes($tmpFile, $junk)

      $result = Read-RomHeader -FilePath $tmpFile
      $result.Valid | Should -BeFalse
    }

    It 'erkennt NES 2.0 Format' {
      $tmpFile = Join-Path $TestDrive 'test2.nes'
      $header = [byte[]]@(0x4E, 0x45, 0x53, 0x1A, 2, 1, 0x01, 0x08, 0,0,0,0,0,0,0,0)
      $padding = [byte[]]::new(1024)
      [System.IO.File]::WriteAllBytes($tmpFile, ($header + $padding))

      $result = Read-RomHeader -FilePath $tmpFile
      $result.Valid | Should -BeTrue
      $result.HeaderType | Should -Be 'NES 2.0'
    }
  }

  Describe 'Read-RomHeader mit N64' {
    It 'erkennt Big-Endian N64 ROM' {
      $tmpFile = Join-Path $TestDrive 'test.z64'
      $data = [byte[]]::new(1024)
      $data[0] = 0x80; $data[1] = 0x37; $data[2] = 0x12; $data[3] = 0x40
      [System.IO.File]::WriteAllBytes($tmpFile, $data)

      $result = Read-RomHeader -FilePath $tmpFile
      $result.Valid | Should -BeTrue
      $result.Platform | Should -Be 'N64'
      $result.HeaderType | Should -BeLike '*Big-Endian*'
    }
  }

  Describe 'Read-RomHeader mit GBA' {
    It 'liest Title und GameCode aus GBA-Header' {
      $tmpFile = Join-Path $TestDrive 'test.gba'
      $data = [byte[]]::new(0xC0)
      # GBA magic byte at 0xB2
      $data[0xB2] = 0x96
      # Title at 0xA0 (12 bytes)
      $title = [System.Text.Encoding]::ASCII.GetBytes('TESTGAME    ')
      [Array]::Copy($title, 0, $data, 0xA0, 12)
      # Game Code at 0xAC (4 bytes)
      $code = [System.Text.Encoding]::ASCII.GetBytes('ATST')
      [Array]::Copy($code, 0, $data, 0xAC, 4)
      [System.IO.File]::WriteAllBytes($tmpFile, $data)

      $result = Read-RomHeader -FilePath $tmpFile
      $result.Valid | Should -BeTrue
      $result.Platform | Should -Be 'GBA'
      $result.Title | Should -BeLike 'TESTGAME*'
    }
  }

  Describe 'Test-RomHeaderIntegrity' {
    It 'meldet OK bei gueltigem Header' {
      $headerResult = @{ Valid = $true; Platform = 'NES'; Title = 'Test'; Warnings = [System.Collections.Generic.List[string]]::new() }
      $result = Test-RomHeaderIntegrity -HeaderResult $headerResult
      $result.Status | Should -Be 'OK'
    }

    It 'meldet Unknown bei ungueltigem Header' {
      $headerResult = @{ Valid = $false; Platform = 'Unknown'; Title = ''; Warnings = [System.Collections.Generic.List[string]]::new() }
      $result = Test-RomHeaderIntegrity -HeaderResult $headerResult
      $result.Status | Should -Be 'Unknown'
    }

    It 'meldet Anomalie bei Warnings' {
      $warnings = [System.Collections.Generic.List[string]]::new()
      $warnings.Add('PRG-Size ist 0')
      $headerResult = @{ Valid = $true; Platform = 'NES'; Title = 'Test'; Warnings = $warnings }
      $result = Test-RomHeaderIntegrity -HeaderResult $headerResult
      $result.Status | Should -Be 'Anomaly'
    }
  }

  Describe 'Read-RomHeader Fehlerbehandlung' {
    It 'gibt Warning bei fehlender Datei' {
      $result = Read-RomHeader -FilePath 'C:\nicht\vorhanden\rom.nes'
      $result.Valid | Should -BeFalse
      $result.Warnings.Count | Should -BeGreaterThan 0
    }
  }
}
