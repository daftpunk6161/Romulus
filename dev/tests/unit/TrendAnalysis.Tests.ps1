BeforeAll {
  . "$PSScriptRoot/../../modules/TrendAnalysis.ps1"
}

Describe 'TrendAnalysis (XL-06)' {

  Describe 'New-TrendSnapshot' {
    It 'erstellt Snapshot mit Pflichtfeldern' {
      $snap = New-TrendSnapshot -TotalFiles 100 -TotalSizeBytes 1048576
      $snap.TotalFiles | Should -Be 100
      $snap.TotalSizeBytes | Should -Be 1048576
      $snap.Timestamp | Should -Not -BeNullOrEmpty
      $snap.QualityScore | Should -Be 0
    }

    It 'berechnet QualityScore korrekt' {
      $snap = New-TrendSnapshot -TotalFiles 200 -TotalSizeBytes 0 -VerifiedCount 150
      $snap.QualityScore | Should -Be 75.0
    }

    It 'behandelt 0 Dateien ohne Division-by-Zero' {
      $snap = New-TrendSnapshot -TotalFiles 0 -TotalSizeBytes 0
      $snap.QualityScore | Should -Be 0
    }
  }

  Describe 'Add-TrendSnapshot' {
    It 'fuegt Snapshot zur leeren Historie hinzu' {
      $snap = New-TrendSnapshot -TotalFiles 10 -TotalSizeBytes 100
      $history = Add-TrendSnapshot -History @() -Snapshot $snap
      @($history).Count | Should -Be 1
    }

    It 'beschraenkt auf MaxEntries' {
      $history = @()
      for ($i = 0; $i -lt 5; $i++) {
        $snap = New-TrendSnapshot -TotalFiles $i -TotalSizeBytes 0
        $history = Add-TrendSnapshot -History $history -Snapshot $snap -MaxEntries 3
      }
      @($history).Count | Should -Be 3
      $history[2].TotalFiles | Should -Be 4
    }
  }

  Describe 'Get-TrendDelta' {
    It 'berechnet positive Deltas' {
      $old = New-TrendSnapshot -TotalFiles 100 -TotalSizeBytes 1000 -VerifiedCount 50
      $new = New-TrendSnapshot -TotalFiles 150 -TotalSizeBytes 2000 -VerifiedCount 120
      $delta = Get-TrendDelta -OldSnapshot $old -NewSnapshot $new
      $delta.FileDelta | Should -Be 50
      $delta.SizeDelta | Should -Be 1000
      $delta.FilesTrend | Should -Be 'up'
      $delta.SizeTrend | Should -Be 'up'
    }

    It 'erkennt stabile Werte' {
      $snap = New-TrendSnapshot -TotalFiles 100 -TotalSizeBytes 1000
      $delta = Get-TrendDelta -OldSnapshot $snap -NewSnapshot $snap
      $delta.FileDelta | Should -Be 0
      $delta.FilesTrend | Should -Be 'stable'
    }

    It 'erkennt negative Trends' {
      $old = New-TrendSnapshot -TotalFiles 200 -TotalSizeBytes 5000
      $new = New-TrendSnapshot -TotalFiles 100 -TotalSizeBytes 2000
      $delta = Get-TrendDelta -OldSnapshot $old -NewSnapshot $new
      $delta.FilesTrend | Should -Be 'down'
      $delta.SizeTrend | Should -Be 'down'
    }
  }

  Describe 'Get-TrendChartData' {
    It 'erstellt Chart-Daten aus Historie' {
      $history = @(
        (New-TrendSnapshot -TotalFiles 10 -TotalSizeBytes 100),
        (New-TrendSnapshot -TotalFiles 20 -TotalSizeBytes 200)
      )
      $chart = Get-TrendChartData -History $history -Metric 'TotalFiles'
      $chart.Metric | Should -Be 'TotalFiles'
      $chart.PointCount | Should -Be 2
      $chart.Min | Should -Be 10
      $chart.Max | Should -Be 20
    }

    It 'behandelt leere Historie' {
      $chart = Get-TrendChartData -History @() -Metric 'TotalFiles'
      $chart.PointCount | Should -Be 0
      $chart.Min | Should -Be 0
      $chart.Max | Should -Be 0
    }
  }

  Describe 'Get-TrendStatistics' {
    It 'gibt Statistiken fuer gefuellte Historie' {
      $history = @(
        (New-TrendSnapshot -TotalFiles 50 -TotalSizeBytes 500),
        (New-TrendSnapshot -TotalFiles 100 -TotalSizeBytes 1000)
      )
      $stats = Get-TrendStatistics -History $history
      $stats.SnapshotCount | Should -Be 2
      $stats.HasData | Should -Be $true
      $stats.OverallFileDelta | Should -Be 50
    }

    It 'behandelt leere Historie' {
      $stats = Get-TrendStatistics -History @()
      $stats.SnapshotCount | Should -Be 0
      $stats.HasData | Should -Be $false
    }
  }
}
