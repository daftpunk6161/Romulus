BeforeAll {
  . "$PSScriptRoot/../../modules/UsnJournalScan.ps1"
}

Describe 'UsnJournalScan (XL-10)' {

  Describe 'Test-UsnJournalAvailable' {
    It 'prueft Laufwerk C' {
      $result = Test-UsnJournalAvailable -DriveLetter 'C'
      $result.DriveLetter | Should -Be 'C'
      # On Windows with NTFS, should be available
      $result.ContainsKey('IsNTFS') | Should -Be $true
      $result.ContainsKey('IsAvailable') | Should -Be $true
    }

    It 'behandelt ungueltige Laufwerksbuchstaben' {
      $result = Test-UsnJournalAvailable -DriveLetter 'Z'
      # Z drive likely doesn't exist
      $result.DriveLetter | Should -Be 'Z'
    }

    It 'trimmt Doppelpunkt' {
      $result = Test-UsnJournalAvailable -DriveLetter 'C:'
      $result.DriveLetter | Should -Be 'C'
    }
  }

  Describe 'New-UsnScanState' {
    It 'erstellt neuen State' {
      $state = New-UsnScanState -DriveLetter 'C'
      $state.DriveLetter | Should -Be 'C'
      $state.LastUsn | Should -Be 0
      $state.Version | Should -Be 1
      $state.Timestamp | Should -Not -BeNullOrEmpty
    }

    It 'akzeptiert ScanRoot' {
      $state = New-UsnScanState -DriveLetter 'D' -ScanRoot 'D:\Roms'
      $state.ScanRoot | Should -Be 'D:\Roms'
    }
  }

  Describe 'New-UsnChangeRecord' {
    It 'erstellt Change-Record' {
      $rec = New-UsnChangeRecord -FileName 'test.rom' -Reason 'Create' -Usn 12345
      $rec.FileName | Should -Be 'test.rom'
      $rec.Reason | Should -Be 'Create'
      $rec.Usn | Should -Be 12345
    }
  }

  Describe 'Group-UsnChangesByType' {
    BeforeAll {
      $script:changes = @(
        (New-UsnChangeRecord -FileName 'a.rom' -Reason 'Create'),
        (New-UsnChangeRecord -FileName 'b.rom' -Reason 'Delete'),
        (New-UsnChangeRecord -FileName 'c.rom' -Reason 'Modify'),
        (New-UsnChangeRecord -FileName 'd.rom' -Reason 'Create'),
        (New-UsnChangeRecord -FileName 'e.rom' -Reason 'Rename')
      )
    }

    It 'gruppiert nach Typ' {
      $grouped = Group-UsnChangesByType -Changes $changes
      @($grouped['Create']).Count | Should -Be 2
      @($grouped['Delete']).Count | Should -Be 1
      @($grouped['Modify']).Count | Should -Be 1
      @($grouped['Rename']).Count | Should -Be 1
    }
  }

  Describe 'Get-UsnDifferentialScanPlan' {
    It 'erstellt Scan-Plan' {
      $state = New-UsnScanState -DriveLetter 'C'
      $changes = @(
        (New-UsnChangeRecord -FileName 'new.rom' -Reason 'Create'),
        (New-UsnChangeRecord -FileName 'old.rom' -Reason 'Delete'),
        (New-UsnChangeRecord -FileName 'mod.rom' -Reason 'Modify')
      )
      $plan = Get-UsnDifferentialScanPlan -State $state -Changes $changes
      $plan.TotalChanges | Should -Be 3
      $plan.RescanCount | Should -Be 2
      @($plan.DeletedFiles).Count | Should -Be 1
    }
  }

  Describe 'Get-UsnScanStatistics' {
    It 'berechnet Statistiken' {
      $state = New-UsnScanState -DriveLetter 'D' -ScanRoot 'D:\Roms'
      $changes = @(
        (New-UsnChangeRecord -FileName 'a.rom' -Reason 'Create'),
        (New-UsnChangeRecord -FileName 'b.rom' -Reason 'Modify')
      )
      $stats = Get-UsnScanStatistics -State $state -Changes $changes
      $stats.TotalChanges | Should -Be 2
      $stats.Creates | Should -Be 1
      $stats.Modifies | Should -Be 1
      $stats.ScanRoot | Should -Be 'D:\Roms'
    }
  }
}
