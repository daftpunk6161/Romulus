BeforeAll {
  . "$PSScriptRoot/../../modules/PdfReportExport.ps1"
}

Describe 'LF-14: PdfReportExport' {
  Describe 'New-PdfReportConfig' {
    It 'erstellt Standard-Config' {
      $cfg = New-PdfReportConfig -Title 'ROM Report'
      $cfg.Title | Should -Be 'ROM Report'
      $cfg.PageSize | Should -Be 'A4'
      $cfg.Orientation | Should -Be 'Portrait'
    }
  }

  Describe 'Build-PdfReportData' {
    It 'baut Report-Daten' {
      $cfg = New-PdfReportConfig -Title 'Test'
      $summary = @{ TotalRoms = 100; KeepCount = 80; JunkCount = 20 }
      $details = @(
        @{ Name = 'Mario'; Console = 'nes'; Status = 'keep' }
        @{ Name = 'Sonic'; Console = 'genesis'; Status = 'move' }
      )
      $data = Build-PdfReportData -Config $cfg -Summary $summary -Details $details
      $data.Sections.Count | Should -BeGreaterThan 0
      $data.PageCount | Should -BeGreaterOrEqual 1
    }
  }

  Describe 'ConvertTo-PdfTableRow' {
    It 'konvertiert ROM-Daten in Tabellenzeile' {
      $rom = @{ Name = 'Zelda'; Console = 'snes'; Region = 'EU'; Size = 2048 }
      $row = ConvertTo-PdfTableRow -RomData $rom
      $row.Name | Should -Be 'Zelda'
      $row.Console | Should -Be 'snes'
    }
  }

  Describe 'Build-ChartMetadata' {
    It 'erstellt Chart-Metadaten' {
      $data = @{ 'nes' = 50; 'snes' = 30; 'genesis' = 20 }
      $meta = Build-ChartMetadata -Data $data
      $meta.Labels.Count | Should -Be 3
      $meta.Total | Should -Be 100
    }
  }

  Describe 'Get-PdfReportSummary' {
    It 'berechnet Summary-Statistik' {
      $cfg = New-PdfReportConfig -Title 'Summary Test'
      $summary = @{ Total = 50 }
      $reportData = Build-PdfReportData -Config $cfg -Summary $summary
      $result = Get-PdfReportSummary -ReportData $reportData
      $result.Title | Should -Be 'Summary Test'
      $result.Pages | Should -BeGreaterOrEqual 1
    }
  }
}
