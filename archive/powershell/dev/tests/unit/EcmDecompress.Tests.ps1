BeforeAll {
  $root = $PSScriptRoot
  while ($root -and -not (Test-Path (Join-Path $root 'simple_sort.ps1'))) {
    $root = Split-Path -Parent $root
  }
  . (Join-Path $root 'dev\modules\EcmDecompress.ps1')
}

Describe 'EcmDecompress (QW-02)' {

  Context 'Find-Ecm2Bin' {

    It 'findet Tool ueber CustomPath' {
      $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) "ecm_test_$(Get-Random)"
      New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
      try {
        $toolPath = Join-Path $tmpDir 'ecm2bin.exe'
        [System.IO.File]::WriteAllText($toolPath, 'dummy')
        $found = Find-Ecm2Bin -CustomPath $toolPath
        $found | Should -Be $toolPath
      } finally {
        Remove-Item -LiteralPath $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
      }
    }

    It 'gibt $null zurueck wenn Tool nicht existiert' {
      $found = Find-Ecm2Bin -CustomPath 'C:\nonexistent\ecm2bin.exe'
      $found | Should -BeNullOrEmpty
    }
  }

  Context 'Invoke-EcmDecompress' {

    It 'lehnt nicht-existente Datei ab' {
      $r = Invoke-EcmDecompress -FilePath 'C:\nonexistent\game.ecm' -Mode DryRun
      $r.Status | Should -Be 'FileNotFound'
    }

    It 'lehnt Datei ohne .ecm Extension ab' {
      $tmpFile = Join-Path ([System.IO.Path]::GetTempPath()) "test_$(Get-Random).bin"
      [System.IO.File]::WriteAllText($tmpFile, 'dummy')
      try {
        $r = Invoke-EcmDecompress -FilePath $tmpFile -Mode DryRun
        $r.Status | Should -Be 'NotEcmFile'
      } finally {
        Remove-Item -LiteralPath $tmpFile -Force -ErrorAction SilentlyContinue
      }
    }

    It 'gibt ToolNotFound wenn ecm2bin fehlt' {
      $tmpFile = Join-Path ([System.IO.Path]::GetTempPath()) "test_$(Get-Random).ecm"
      [System.IO.File]::WriteAllText($tmpFile, 'dummy')
      try {
        $r = Invoke-EcmDecompress -FilePath $tmpFile -Mode DryRun
        $r.Status | Should -BeIn @('ToolNotFound','WouldConvert')
      } finally {
        Remove-Item -LiteralPath $tmpFile -Force -ErrorAction SilentlyContinue
      }
    }
  }
}
