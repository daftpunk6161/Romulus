BeforeAll {
  . "$PSScriptRoot/../../modules/Quarantine.ps1"
}

Describe 'MF-26: Quarantine' {
  Describe 'Test-QuarantineCandidate' {
    It 'erkennt unbekannte Konsole und Format' {
      $item = @{ Console = 'Unknown'; Format = 'Unknown'; DatStatus = ''; Category = '' }
      $result = Test-QuarantineCandidate -Item $item
      $result.IsCandidate | Should -BeTrue
      $result.Reasons | Should -Contain 'UnbekannteKonsoleUndFormat'
    }

    It 'erkennt Header-Anomalie' {
      $item = @{ Console = 'SNES'; Format = 'ZIP'; HeaderStatus = 'Corrupted'; Category = 'GAME' }
      $result = Test-QuarantineCandidate -Item $item
      $result.IsCandidate | Should -BeTrue
      $result.Reasons | Should -Contain 'HeaderAnomalie'
    }

    It 'erkennt kein Quarantaene-Kandidat bei normalem Item' {
      $item = @{ Console = 'SNES'; Format = 'ZIP'; DatStatus = 'Verified'; Category = 'GAME'; HeaderStatus = 'OK' }
      $result = Test-QuarantineCandidate -Item $item
      $result.IsCandidate | Should -BeFalse
    }

    It 'evaluiert Custom Rules' {
      $item = @{ Console = 'SNES'; Format = 'ZIP'; Category = 'HOMEBREW' }
      $rules = @(@{ Field = 'Category'; Value = 'HOMEBREW' })
      $result = Test-QuarantineCandidate -Item $item -Rules $rules
      $result.IsCandidate | Should -BeTrue
    }
  }

  Describe 'New-QuarantineAction' {
    It 'erstellt Quarantaene-Aktion' {
      $action = New-QuarantineAction -SourcePath 'C:\Roms\bad.zip' -QuarantineRoot 'D:\Quarantine' -Reasons @('HeaderAnomalie') -Mode 'DryRun'
      $action.SourcePath | Should -Be 'C:\Roms\bad.zip'
      $action.Status | Should -Be 'Pending'
      $action.Reasons | Should -Contain 'HeaderAnomalie'
      $action.TargetPath | Should -BeLike 'D:\Quarantine*bad.zip'
    }
  }

  Describe 'Invoke-Quarantine' {
    It 'behandelt leere Aktionen' {
      $result = Invoke-Quarantine -Actions @() -Mode 'DryRun'
      $result.Processed | Should -Be 0
      $result.Moved | Should -Be 0
    }

    It 'markiert Aktionen im DryRun' {
      $action = @{
        SourcePath = 'C:\test.zip'; TargetPath = 'D:\Q\test.zip'
        QuarantineDir = 'D:\Q'; Reasons = @('Test'); Mode = 'DryRun'; Status = 'Pending'
      }
      $result = Invoke-Quarantine -Actions @($action) -Mode 'DryRun'
      $result.Processed | Should -Be 1
      $result.Results[0].Status | Should -Be 'DryRun'
    }
  }

  Describe 'Get-QuarantineContents' {
    It 'gibt leere Liste bei fehlendem Verzeichnis' {
      $result = Get-QuarantineContents -QuarantineRoot (Join-Path $TestDrive 'nonexistent')
      $result.Files.Count | Should -Be 0
      $result.TotalSize | Should -Be 0
    }

    It 'listet Dateien korrekt' {
      $qDir = Join-Path $TestDrive 'quarantine'
      $dateDir = Join-Path $qDir '20260309'
      New-Item -ItemType Directory -Path $dateDir -Force | Out-Null
      Set-Content -Path (Join-Path $dateDir 'bad.zip') -Value 'testdata'

      $result = Get-QuarantineContents -QuarantineRoot $qDir
      $result.Files.Count | Should -Be 1
      $result.TotalSize | Should -BeGreaterThan 0
      $result.DateGroups['20260309'].Count | Should -Be 1
    }
  }

  Describe 'Restore-FromQuarantine' {
    It 'simuliert Restore im DryRun' {
      $qDir = Join-Path $TestDrive 'q-restore'
      New-Item -ItemType Directory -Path $qDir -Force | Out-Null
      $qFile = Join-Path $qDir 'restored.zip'
      Set-Content -Path $qFile -Value 'quarantined'

      $result = Restore-FromQuarantine -QuarantinePath $qFile -OriginalPath 'C:\Roms\restored.zip' -Mode 'DryRun'
      $result.Status | Should -Be 'DryRun'
    }

    It 'gibt Fehler bei fehlender Quarantaene-Datei' {
      $result = Restore-FromQuarantine -QuarantinePath 'C:\nope\no.zip' -OriginalPath 'C:\out.zip' -Mode 'DryRun'
      $result.Status | Should -Be 'Error'
    }
  }
}
