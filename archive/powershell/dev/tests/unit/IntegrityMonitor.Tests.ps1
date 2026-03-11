BeforeAll {
  . "$PSScriptRoot/../../modules/IntegrityMonitor.ps1"
}

Describe 'MF-24: IntegrityMonitor' {
  Describe 'New-IntegrityBaseline' {
    It 'erstellt Baseline aus echten Dateien' {
      $tmpDir = Join-Path $TestDrive 'integrity'
      New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
      $f1 = Join-Path $tmpDir 'test1.txt'
      $f2 = Join-Path $tmpDir 'test2.txt'
      Set-Content -Path $f1 -Value 'Inhalt1'
      Set-Content -Path $f2 -Value 'Inhalt2'

      $baseline = New-IntegrityBaseline -Files @($f1, $f2) -Algorithm 'SHA256'
      $baseline.Count | Should -Be 2
      $baseline.Algorithm | Should -Be 'SHA256'
      $baseline.Entries[$f1].Hash | Should -Not -BeNullOrEmpty
    }

    It 'behandelt leere Dateiliste' {
      $baseline = New-IntegrityBaseline -Files @()
      $baseline.Count | Should -Be 0
    }

    It 'ignoriert nicht existierende Dateien' {
      $baseline = New-IntegrityBaseline -Files @('C:\nicht\vorhanden\datei.txt')
      $baseline.Count | Should -Be 0
    }
  }

  Describe 'Test-IntegrityAgainstBaseline' {
    It 'erkennt intakte Dateien' {
      $tmpDir = Join-Path $TestDrive 'integ-check'
      New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
      $f1 = Join-Path $tmpDir 'ok.txt'
      Set-Content -Path $f1 -Value 'Original'

      $baseline = New-IntegrityBaseline -Files @($f1)
      $result = Test-IntegrityAgainstBaseline -Baseline $baseline
      $result.Summary.Intact | Should -Be 1
      $result.Summary.Changed | Should -Be 0
      $result.Summary.BitRotRisk | Should -BeFalse
    }

    It 'erkennt geaenderte Dateien (Bit-Rot)' {
      $tmpDir = Join-Path $TestDrive 'integ-change'
      New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
      $f1 = Join-Path $tmpDir 'changed.txt'
      Set-Content -Path $f1 -Value 'VorherInhalt'

      $baseline = New-IntegrityBaseline -Files @($f1)
      # Datei aendern
      Set-Content -Path $f1 -Value 'NachherInhalt'

      $result = Test-IntegrityAgainstBaseline -Baseline $baseline
      $result.Summary.Changed | Should -Be 1
      $result.Summary.BitRotRisk | Should -BeTrue
    }

    It 'erkennt fehlende Dateien' {
      $tmpDir = Join-Path $TestDrive 'integ-missing'
      New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
      $f1 = Join-Path $tmpDir 'will-delete.txt'
      Set-Content -Path $f1 -Value 'Temp'

      $baseline = New-IntegrityBaseline -Files @($f1)
      Remove-Item $f1

      $result = Test-IntegrityAgainstBaseline -Baseline $baseline
      $result.Summary.Missing | Should -Be 1
    }
  }

  Describe 'Get-IntegrityReport' {
    It 'gibt OK bei intakten Dateien' {
      $checkResult = @{
        Changed = @(); Missing = @(); Intact = @('a.zip'); Errors = @()
        Summary = @{ Total = 1; Intact = 1; Changed = 0; Missing = 0; Errors = 0; BitRotRisk = $false }
      }
      $report = Get-IntegrityReport -CheckResult $checkResult
      $report.Status | Should -Be 'OK'
      $report.Message | Should -BeLike '*intakt*'
    }

    It 'gibt Warning bei Bit-Rot' {
      $checkResult = @{
        Changed = @(@{ Path = 'a.zip' }); Missing = @(); Intact = @(); Errors = @()
        Summary = @{ Total = 1; Intact = 0; Changed = 1; Missing = 0; Errors = 0; BitRotRisk = $true }
      }
      $report = Get-IntegrityReport -CheckResult $checkResult
      $report.Status | Should -Be 'Warning'
    }
  }
}
