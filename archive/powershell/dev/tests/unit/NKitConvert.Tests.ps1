BeforeAll {
  . "$PSScriptRoot/../../modules/NKitConvert.ps1"
}

Describe 'MF-07: NKitConvert' {
  Describe 'Test-NKitImage' {
    It 'erkennt .nkit.iso Dateien' {
      $tempFile = Join-Path $TestDrive 'game.nkit.iso'
      Set-Content -Path $tempFile -Value 'dummy'
      Test-NKitImage -Path $tempFile | Should -BeTrue
    }

    It 'lehnt normale ISO ab' {
      $tempFile = Join-Path $TestDrive 'game.iso'
      Set-Content -Path $tempFile -Value 'dummy'
      Test-NKitImage -Path $tempFile | Should -BeFalse
    }

    It 'gibt false bei nicht-existierender Datei' {
      Test-NKitImage -Path 'C:\nonexistent.nkit.iso' | Should -BeFalse
    }
  }

  Describe 'Get-NKitConversionParams' {
    It 'erstellt korrekte Parameter fuer ISO-Ziel' {
      $result = Get-NKitConversionParams -SourcePath 'C:\game.nkit.iso' -OutputDir 'C:\output' -TargetFormat 'ISO'
      $result.TargetFormat | Should -Be 'ISO'
      $result.OutputPath | Should -BeLike '*.iso'
      $result.Tool | Should -Be 'nkit'
    }

    It 'erstellt korrekte Parameter fuer RVZ-Ziel' {
      $result = Get-NKitConversionParams -SourcePath 'C:\game.nkit.iso' -OutputDir 'C:\output' -TargetFormat 'RVZ'
      $result.OutputPath | Should -BeLike '*.rvz'
    }
  }

  Describe 'Invoke-NKitConversion' {
    It 'lehnt nicht-NKit Dateien ab' {
      $tempFile = Join-Path $TestDrive 'game.iso'
      Set-Content -Path $tempFile -Value 'dummy'
      $result = Invoke-NKitConversion -SourcePath $tempFile -OutputDir $TestDrive
      $result.Status | Should -Be 'Error'
      $result.Reason | Should -Be 'NotNKitImage'
    }

    It 'DryRun gibt simuliertes Ergebnis' {
      $tempFile = Join-Path $TestDrive 'game.nkit.iso'
      Set-Content -Path $tempFile -Value 'dummy'
      $result = Invoke-NKitConversion -SourcePath $tempFile -OutputDir $TestDrive -Mode DryRun
      $result.Status | Should -Be 'DryRun'
    }
  }
}
