BeforeAll {
  $root = $PSScriptRoot
  while ($root -and -not (Test-Path (Join-Path $root 'simple_sort.ps1'))) {
    $root = Split-Path -Parent $root
  }
  . (Join-Path $root 'dev\modules\RunHistory.ps1')
}

Describe 'RunHistory (QW-14)' {

  Context 'Get-RunHistory' {

    BeforeAll {
      $script:tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) "runhist_$(Get-Random)"
      New-Item -ItemType Directory -Path $script:tmpDir -Force | Out-Null

      # Test Move-Plans erstellen
      @{ Mode = 'DryRun'; Status = 'ok'; Roots = @('D:\Roms'); Moves = @(1,2,3) } |
        ConvertTo-Json | Out-File (Join-Path $script:tmpDir 'move-plan-20260301-100000.json') -Encoding utf8

      @{ Mode = 'Move'; Status = 'ok'; Roots = @('E:\Games'); TotalFiles = 5 } |
        ConvertTo-Json | Out-File (Join-Path $script:tmpDir 'move-plan-20260302-120000.json') -Encoding utf8
    }

    AfterAll {
      Remove-Item -LiteralPath $script:tmpDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'gibt Run-History zurueck' {
      $r = Get-RunHistory -ReportsDir $script:tmpDir
      $r.Total | Should -Be 2
      $r.Entries.Count | Should -Be 2
    }

    It 'Eintraege haben alle Pflichtfelder' {
      $r = Get-RunHistory -ReportsDir $script:tmpDir
      $entry = $r.Entries[0]
      $entry.Id | Should -Not -BeNullOrEmpty
      $entry.FileName | Should -Not -BeNullOrEmpty
      $entry.DateFormatted | Should -Not -BeNullOrEmpty
    }

    It 'liest Metadaten aus JSON' {
      $r = Get-RunHistory -ReportsDir $script:tmpDir
      $move = $r.Entries | Where-Object { $_.Mode -eq 'Move' }
      $move | Should -Not -BeNullOrEmpty
    }

    It 'behandelt nicht-existentes Verzeichnis' {
      $r = Get-RunHistory -ReportsDir 'C:\nonexistent_dir_12345'
      $r.Total | Should -Be 0
      $r.Entries.Count | Should -Be 0
    }
  }

  Context 'Get-RunDetail' {

    It 'liest Detail eines Move-Plans' {
      $tmpFile = Join-Path ([System.IO.Path]::GetTempPath()) "detail_$(Get-Random).json"
      try {
        @{ Mode = 'DryRun'; Status = 'ok' } | ConvertTo-Json | Out-File $tmpFile -Encoding utf8
        $r = Get-RunDetail -PlanFilePath $tmpFile
        $r.Status | Should -Be 'OK'
        $r.Data.Mode | Should -Be 'DryRun'
      } finally {
        Remove-Item -LiteralPath $tmpFile -Force -ErrorAction SilentlyContinue
      }
    }

    It 'gibt FileNotFound fuer nicht-existente Datei' {
      $r = Get-RunDetail -PlanFilePath 'C:\nonexistent.json'
      $r.Status | Should -Be 'FileNotFound'
    }
  }
}
