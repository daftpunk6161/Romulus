BeforeAll {
  $root = $PSScriptRoot
  while ($root -and -not (Test-Path (Join-Path $root 'simple_sort.ps1'))) {
    $root = Split-Path -Parent $root
  }
  . (Join-Path $root 'dev\modules\PortableMode.ps1')
}

Describe 'PortableMode (QW-12)' {

  Context 'Test-PortableMode' {

    It 'gibt true bei explizit gesetztem Portable-Flag' {
      $r = Test-PortableMode -Portable $true -ProgramRoot 'C:\nonexistent'
      $r | Should -BeTrue
    }

    It 'gibt false bei Portable=false und keiner Marker-Datei' {
      $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) "port_test_$(Get-Random)"
      New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
      try {
        $r = Test-PortableMode -ProgramRoot $tmpDir -Portable $false
        $r | Should -BeFalse
      } finally {
        Remove-Item -LiteralPath $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
      }
    }

    It 'gibt true bei vorhandener .portable Datei' {
      $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) "port_test_$(Get-Random)"
      New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
      try {
        [System.IO.File]::WriteAllText((Join-Path $tmpDir '.portable'), '')
        $r = Test-PortableMode -ProgramRoot $tmpDir -Portable $false
        $r | Should -BeTrue
      } finally {
        Remove-Item -LiteralPath $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
      }
    }
  }

  Context 'Get-PortableSettingsRoot' {

    It 'gibt Portable-Root im Programmordner zurueck' {
      $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) "port_test_$(Get-Random)"
      New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
      try {
        $r = Get-PortableSettingsRoot -ProgramRoot $tmpDir -Portable $true
        $r | Should -BeLike "*$([System.IO.Path]::GetFileName($tmpDir))*"
        $r | Should -BeLike '*.romcleanup*'
      } finally {
        Remove-Item -LiteralPath $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
      }
    }

    It 'gibt APPDATA-Pfad bei nicht-Portable zurueck' {
      $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) "port_test_$(Get-Random)"
      New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
      try {
        $r = Get-PortableSettingsRoot -ProgramRoot $tmpDir -Portable $false
        $r | Should -Not -BeNullOrEmpty
      } finally {
        Remove-Item -LiteralPath $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
      }
    }
  }

  Context 'Get-PortablePath' {

    It 'gibt korrekten Unterpfad zurueck' {
      $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) "port_test_$(Get-Random)"
      New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
      try {
        $r = Get-PortablePath -ProgramRoot $tmpDir -SubPath 'settings.json' -Portable $true
        $r | Should -BeLike '*settings.json*'
      } finally {
        Remove-Item -LiteralPath $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
      }
    }
  }
}
