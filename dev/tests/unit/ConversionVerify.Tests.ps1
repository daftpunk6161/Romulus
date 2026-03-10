BeforeAll {
  . "$PSScriptRoot/../../modules/ConversionVerify.ps1"
}

Describe 'MF-09: ConversionVerify' {
  Describe 'New-ConversionVerifyRecord' {
    It 'erstellt korrekten Record' {
      $record = New-ConversionVerifyRecord -SourcePath 'a.iso' -SourceHash 'AAAA' -TargetPath 'a.chd'
      $record.SourcePath | Should -Be 'a.iso'
      $record.SourceHash | Should -Be 'AAAA'
      $record.Status | Should -Be 'Pending'
    }
  }

  Describe 'Test-ConversionIntegrity' {
    It 'gibt Valid bei existierender Datei' {
      $tempFile = Join-Path $TestDrive 'test.chd'
      Set-Content -Path $tempFile -Value 'data'
      $result = Test-ConversionIntegrity -TargetPath $tempFile
      $result.Valid | Should -BeTrue
    }

    It 'gibt Fehler bei fehlender Datei' {
      $result = Test-ConversionIntegrity -TargetPath 'C:\nonexistent.chd'
      $result.Valid | Should -BeFalse
      $result.Reason | Should -Be 'FileNotFound'
    }

    It 'gibt Fehler bei zu kleiner Datei' {
      $tempFile = Join-Path $TestDrive 'tiny.chd'
      Set-Content -Path $tempFile -Value ''
      $result = Test-ConversionIntegrity -TargetPath $tempFile -ExpectedMinSize 1000
      $result.Valid | Should -BeFalse
      $result.Reason | Should -Be 'FileTooSmall'
    }
  }

  Describe 'Invoke-BatchVerify' {
    It 'verifiziert mehrere Dateien' {
      $f1 = Join-Path $TestDrive 'a.chd'; Set-Content -Path $f1 -Value 'data1'
      $f2 = Join-Path $TestDrive 'b.chd'; Set-Content -Path $f2 -Value 'data2'
      $records = @(
        (New-ConversionVerifyRecord -SourcePath 'x' -SourceHash 'H1' -TargetPath $f1)
        (New-ConversionVerifyRecord -SourcePath 'y' -SourceHash 'H2' -TargetPath $f2)
      )
      $result = Invoke-BatchVerify -Records $records
      $result.TotalChecked | Should -Be 2
      $result.Passed | Should -Be 2
      $result.Failed | Should -Be 0
    }

    It 'erkennt fehlende Dateien' {
      $records = @(
        (New-ConversionVerifyRecord -SourcePath 'x' -SourceHash 'H1' -TargetPath 'C:\missing.chd')
      )
      $result = Invoke-BatchVerify -Records $records
      $result.Missing | Should -Be 1
    }

    It 'behandelt leeres Array' {
      $result = Invoke-BatchVerify -Records @()
      $result.TotalChecked | Should -Be 0
    }
  }

  Describe 'Get-VerifyReport' {
    It 'berechnet Rate korrekt' {
      $batch = @{ TotalChecked = 10; Passed = 8; Failed = 1; Missing = 1; Results = @() }
      $report = Get-VerifyReport -BatchResult $batch
      $report.Summary.Rate | Should -Be 80.0
    }
  }
}
