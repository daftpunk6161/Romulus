#  PDF REPORT EXPORT (LF-14)
#  Sammlungs-Report als PDF-Daten mit Statistiken und Diagramm-Metadaten.

function New-PdfReportConfig {
  <#
  .SYNOPSIS
    Erstellt eine PDF-Report-Konfiguration.
  #>
  param(
    [string]$Title = 'ROM Collection Report',
    [string]$Author = 'RomCleanup',
    [ValidateSet('A4','Letter','A3')]
    [string]$PageSize = 'A4',
    [ValidateSet('Portrait','Landscape')]
    [string]$Orientation = 'Portrait',
    [switch]$IncludeCharts,
    [switch]$IncludeCovers
  )

  return @{
    Title         = $Title
    Author        = $Author
    PageSize      = $PageSize
    Orientation   = $Orientation
    IncludeCharts = [bool]$IncludeCharts
    IncludeCovers = [bool]$IncludeCovers
    Created       = (Get-Date).ToString('o')
    Margins       = @{ Top = 20; Bottom = 20; Left = 15; Right = 15 }
  }
}

function Build-PdfReportData {
  <#
  .SYNOPSIS
    Baut die Report-Datenstruktur fuer PDF-Generierung.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Config,
    [Parameter(Mandatory)][hashtable]$Summary,
    [array]$Details = @()
  )

  $sections = [System.Collections.Generic.List[hashtable]]::new()

  # Header-Sektion
  $sections.Add(@{
    Type    = 'Header'
    Title   = $Config.Title
    Date    = $Config.Created
    Author  = $Config.Author
  })

  # Summary-Sektion
  $sections.Add(@{
    Type  = 'Summary'
    Data  = $Summary
  })

  # Details-Tabelle
  if ($Details.Count -gt 0) {
    $sections.Add(@{
      Type    = 'Table'
      Caption = 'ROM Details'
      Rows    = $Details
      Columns = @('Name','Console','Region','Format','Size','Status')
    })
  }

  return @{
    Config   = $Config
    Sections = ,$sections.ToArray()
    PageCount = [math]::Max(1, [math]::Ceiling($Details.Count / 40))
  }
}

function ConvertTo-PdfTableRow {
  <#
  .SYNOPSIS
    Konvertiert ein ROM-Datenobjekt in eine Tabellenzeile.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$RomData
  )

  return @{
    Name    = if ($RomData.ContainsKey('Name')) { $RomData.Name } else { '' }
    Console = if ($RomData.ContainsKey('Console')) { $RomData.Console } else { '' }
    Region  = if ($RomData.ContainsKey('Region')) { $RomData.Region } else { '' }
    Format  = if ($RomData.ContainsKey('Format')) { $RomData.Format } else { '' }
    Size    = if ($RomData.ContainsKey('Size')) { $RomData.Size } else { 0 }
    Status  = if ($RomData.ContainsKey('Status')) { $RomData.Status } else { 'Unknown' }
  }
}

function Build-ChartMetadata {
  <#
  .SYNOPSIS
    Erstellt Chart-Metadaten fuer den PDF-Report (Pie/Bar).
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Data,
    [ValidateSet('Pie','Bar','HorizontalBar')]
    [string]$ChartType = 'Pie',
    [string]$Title = 'Distribution'
  )

  $labels = @($Data.Keys | Sort-Object)
  $values = @($labels | ForEach-Object { $Data[$_] })

  return @{
    ChartType = $ChartType
    Title     = $Title
    Labels    = $labels
    Values    = $values
    Total     = ($values | Measure-Object -Sum).Sum
  }
}

function Get-PdfReportSummary {
  <#
  .SYNOPSIS
    Erstellt eine Zusammenfassung fuer den PDF-Report.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$ReportData
  )

  return @{
    Title     = $ReportData.Config.Title
    Pages     = $ReportData.PageCount
    Sections  = $ReportData.Sections.Count
    Generated = $ReportData.Config.Created
  }
}
