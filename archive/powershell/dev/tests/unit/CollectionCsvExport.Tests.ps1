BeforeAll {
  $root = $PSScriptRoot
  while ($root -and -not (Test-Path (Join-Path $root 'simple_sort.ps1'))) {
    $root = Split-Path -Parent $root
  }
  . (Join-Path $root 'dev\modules\CollectionCsvExport.ps1')
}

Describe 'CollectionCsvExport (QW-13)' {

  Context 'Protect-CsvField - CSV-Injection-Schutz' {

    It 'schuetzt gegen = Prefix' {
      $r = Protect-CsvField -Value '=HYPERLINK("evil")'
      $r | Should -BeLike "'*"
    }

    It 'schuetzt gegen + Prefix' {
      $r = Protect-CsvField -Value '+cmd|/c calc'
      $r | Should -BeLike "'*"
    }

    It 'schuetzt gegen - Prefix' {
      $r = Protect-CsvField -Value '-cmd|/c calc'
      $r | Should -BeLike "'*"
    }

    It 'schuetzt gegen @ Prefix' {
      $r = Protect-CsvField -Value '@SUM(A1:A10)'
      $r | Should -BeLike "'*"
    }

    It 'laesst normalen Text unveraendert' {
      $r = Protect-CsvField -Value 'Normal Game Name'
      $r | Should -Be 'Normal Game Name'
    }

    It 'behandelt leeren String' {
      $r = Protect-CsvField -Value ''
      $r | Should -Be ''
    }
  }

  Context 'Export-CollectionCsv' {

    It 'exportiert Daten als CSV-Datei' {
      $tmpFile = Join-Path ([System.IO.Path]::GetTempPath()) "csv_test_$(Get-Random).csv"
      try {
        $items = @(
          @{ Name = 'Mario'; Console = 'SNES'; Region = 'USA'; Format = 'zip'; Size = 1024; Category = 'GAME'; DatStatus = ''; Path = 'C:\roms\mario.zip' }
        )

        $r = Export-CollectionCsv -Items $items -OutputPath $tmpFile
        $r.Status | Should -Be 'Success'
        $r.RowCount | Should -Be 1
        (Test-Path -LiteralPath $tmpFile) | Should -BeTrue

        $content = Get-Content -LiteralPath $tmpFile -Raw
        $content | Should -BeLike '*Dateiname*'
        $content | Should -BeLike '*Mario*'
      } finally {
        Remove-Item -LiteralPath $tmpFile -Force -ErrorAction SilentlyContinue
      }
    }

    It 'behandelt leere Items-Liste' {
      $r = Export-CollectionCsv -Items @() -OutputPath 'C:\dummy.csv'
      $r.Status | Should -Be 'Empty'
      $r.RowCount | Should -Be 0
    }

    It 'schuetzt gegen CSV-Injection in Ausgabe' {
      $tmpFile = Join-Path ([System.IO.Path]::GetTempPath()) "csv_inj_$(Get-Random).csv"
      try {
        $items = @(@{ Name = '=cmd|calc'; Console = 'SNES'; Region = 'US'; Path = 'test' })
        $r = Export-CollectionCsv -Items $items -OutputPath $tmpFile
        $content = Get-Content -LiteralPath $tmpFile -Raw
        $content | Should -BeLike "*'=cmd*"
      } finally {
        Remove-Item -LiteralPath $tmpFile -Force -ErrorAction SilentlyContinue
      }
    }

    It 'verwendet Semikolon als Standard-Delimiter' {
      $tmpFile = Join-Path ([System.IO.Path]::GetTempPath()) "csv_dlm_$(Get-Random).csv"
      try {
        $items = @(@{ Name = 'Game'; Console = 'NES'; Path = 'test' })
        Export-CollectionCsv -Items $items -OutputPath $tmpFile | Out-Null
        $content = Get-Content -LiteralPath $tmpFile -Raw
        $content | Should -BeLike '*;*'
      } finally {
        Remove-Item -LiteralPath $tmpFile -Force -ErrorAction SilentlyContinue
      }
    }
  }
}
