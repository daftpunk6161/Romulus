BeforeAll {
  . "$PSScriptRoot/../../modules/HeaderRepair.ps1"
}

Describe 'LF-06: HeaderRepair' {
  Describe 'Test-NesHeaderValid' {
    It 'erkennt gueltigen iNES-Header' {
      $header = [byte[]](0x4E, 0x45, 0x53, 0x1A, 0x02, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00)
      $result = Test-NesHeaderValid -HeaderBytes $header
      $result.Valid | Should -BeTrue
    }

    It 'erkennt Dirty-Bytes in Header' {
      $header = [byte[]](0x4E, 0x45, 0x53, 0x1A, 0x02, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00)
      $result = Test-NesHeaderValid -HeaderBytes $header
      $result.Valid | Should -BeFalse
      $result.Issues.Count | Should -BeGreaterThan 0
    }
  }

  Describe 'Repair-NesHeader' {
    It 'bereinigt Dirty-Bytes 12-15' {
      $rom = [byte[]]::new(32)
      $rom[0] = 0x4E; $rom[1] = 0x45; $rom[2] = 0x53; $rom[3] = 0x1A
      $rom[12] = 0xFF; $rom[13] = 0xAA
      $result = Repair-NesHeader -RomData $rom
      $result.Success | Should -BeTrue
      $result.Repaired | Should -BeTrue
      $result.Changes | Should -Be 2
      $result.Data[0][12] | Should -Be 0
    }
  }

  Describe 'Test-SnesCopierHeader' {
    It 'erkennt ROM ohne Copier-Header' {
      $rom = [byte[]]::new(0x8000)  # Genau 32KB = Vielfaches von 0x8000
      $result = Test-SnesCopierHeader -RomData $rom
      $result.HasCopierHeader | Should -BeFalse
    }

    It 'erkennt ROM mit Copier-Header' {
      $rom = [byte[]]::new(0x8000 + 512)  # 32KB + 512 Bytes
      $result = Test-SnesCopierHeader -RomData $rom
      $result.HasCopierHeader | Should -BeTrue
    }
  }

  Describe 'Remove-SnesCopierHeader' {
    It 'entfernt 512 Byte Header' {
      $rom = [byte[]]::new(0x8000 + 512)
      $rom[512] = 0xAA  # Erstes echtes ROM-Byte
      $result = Remove-SnesCopierHeader -RomData $rom
      $result.Removed | Should -BeTrue
      $result.Data[0].Count | Should -Be 0x8000
      $result.Data[0][0] | Should -Be 0xAA
    }
  }

  Describe 'Get-HeaderRepairPlan' {
    It 'erstellt Plan fuer NES-ROMs' {
      $header = [byte[]](0x4E, 0x45, 0x53, 0x1A, 0x02, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00)
      $files = @(
        @{ Path = 'test.nes'; Console = 'nes'; HeaderBytes = $header }
      )
      $plan = Get-HeaderRepairPlan -RomFiles $files
      $plan.Count | Should -Be 1
      $plan[0].Action | Should -Be 'RepairNesHeader'
    }
  }
}
