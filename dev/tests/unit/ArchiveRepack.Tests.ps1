BeforeAll {
  $root = $PSScriptRoot
  while ($root -and -not (Test-Path (Join-Path $root 'simple_sort.ps1'))) {
    $root = Split-Path -Parent $root
  }
  . (Join-Path $root 'dev\modules\ArchiveRepack.ps1')
}

Describe 'ArchiveRepack (QW-03)' {

  Context 'Invoke-ArchiveRepack - Validierung' {

    It 'lehnt nicht-existente Datei ab' {
      $r = Invoke-ArchiveRepack -ArchivePath 'C:\nonexistent\archive.zip' -TargetFormat '7z' -Mode DryRun
      $r.Status | Should -Be 'FileNotFound'
    }

    It 'lehnt ungueltige Quelldatei ab (nicht Archiv)' {
      $tmpFile = Join-Path ([System.IO.Path]::GetTempPath()) "test_$(Get-Random).txt"
      [System.IO.File]::WriteAllText($tmpFile, 'not an archive')
      try {
        $r = Invoke-ArchiveRepack -ArchivePath $tmpFile -TargetFormat 'zip' -Mode DryRun
        $r.Status | Should -Be 'UnsupportedFormat'
      } finally {
        Remove-Item -LiteralPath $tmpFile -Force -ErrorAction SilentlyContinue
      }
    }

    It 'lehnt Konvertierung in gleiches Format ab' {
      $tmpFile = Join-Path ([System.IO.Path]::GetTempPath()) "test_$(Get-Random).zip"
      [System.IO.File]::WriteAllText($tmpFile, 'fake zip')
      try {
        $r = Invoke-ArchiveRepack -ArchivePath $tmpFile -TargetFormat 'zip' -Mode DryRun
        $r.Status | Should -Be 'AlreadyTargetFormat'
      } finally {
        Remove-Item -LiteralPath $tmpFile -Force -ErrorAction SilentlyContinue
      }
    }

    It 'gibt ResultHashtable mit korrekten Feldern zurueck' {
      $tmpFile = Join-Path ([System.IO.Path]::GetTempPath()) "test_$(Get-Random).zip"
      [System.IO.File]::WriteAllText($tmpFile, 'fake zip')
      try {
        $r = Invoke-ArchiveRepack -ArchivePath $tmpFile -TargetFormat '7z' -Mode DryRun
        $r | Should -BeOfType [hashtable]
        $r.Keys | Should -Contain 'Status'
        $r.Keys | Should -Contain 'SourcePath'
      } finally {
        Remove-Item -LiteralPath $tmpFile -Force -ErrorAction SilentlyContinue
      }
    }
  }
}
