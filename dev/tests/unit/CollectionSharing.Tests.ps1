BeforeAll {
  . "$PSScriptRoot/../../modules/CollectionSharing.ps1"
}

Describe 'CollectionSharing (XL-08)' {

  Describe 'New-CollectionExportConfig' {
    It 'erstellt Standard-Konfiguration' {
      $cfg = New-CollectionExportConfig
      $cfg.Title | Should -Be 'Meine ROM-Sammlung'
      $cfg.Format | Should -Be 'HTML'
      $cfg.IncludeStats | Should -Be $true
      $cfg.Privacy.IncludePaths | Should -Be $false
      $cfg.Privacy.IncludeHashes | Should -Be $false
    }

    It 'akzeptiert JSON-Format' {
      $cfg = New-CollectionExportConfig -Format 'JSON'
      $cfg.Format | Should -Be 'JSON'
    }
  }

  Describe 'New-CollectionEntry' {
    It 'erstellt Entry ohne Pfade' {
      $entry = New-CollectionEntry -GameName 'Super Mario' -ConsoleKey 'NES'
      $entry.GameName | Should -Be 'Super Mario'
      $entry.ConsoleKey | Should -Be 'NES'
      $entry.Keys | Should -Not -Contain 'FullPath'
    }
  }

  Describe 'ConvertTo-CollectionHtml' {
    BeforeAll {
      $script:cfg = New-CollectionExportConfig -Title 'Test'
      $script:entries = @(
        (New-CollectionEntry -GameName 'Mario' -ConsoleKey 'NES' -Region 'EU'),
        (New-CollectionEntry -GameName 'Zelda' -ConsoleKey 'SNES' -Region 'US')
      )
    }

    It 'generiert HTML mit CSP-Header' {
      $result = ConvertTo-CollectionHtml -Config $cfg -Entries $entries
      $result.Content | Should -BeLike '*Content-Security-Policy*'
      $result.Format | Should -Be 'HTML'
      $result.EntryCount | Should -Be 2
    }

    It 'escaped HTML-Sonderzeichen' {
      $xssEntries = @((New-CollectionEntry -GameName '<script>alert(1)</script>' -ConsoleKey 'NES'))
      $result = ConvertTo-CollectionHtml -Config $cfg -Entries $xssEntries
      $result.Content | Should -Not -BeLike '*<script>*'
    }

    It 'enthaelt Tabelle mit Eintraegen' {
      $result = ConvertTo-CollectionHtml -Config $cfg -Entries $entries
      $result.Content | Should -BeLike '*<table>*'
      $result.Content | Should -BeLike '*Mario*'
    }
  }

  Describe 'ConvertTo-CollectionJson' {
    It 'generiert gueltiges JSON' {
      $cfg = New-CollectionExportConfig
      $entries = @((New-CollectionEntry -GameName 'Test' -ConsoleKey 'GB'))
      $result = ConvertTo-CollectionJson -Config $cfg -Entries $entries
      $result.Format | Should -Be 'JSON'
      $result.EntryCount | Should -Be 1
      $result.Content | Should -Not -BeNullOrEmpty
      # Pruefe ob es gueltiges JSON ist
      $parsed = $result.Content | ConvertFrom-Json
      $parsed.title | Should -Be 'Meine ROM-Sammlung'
    }
  }

  Describe 'Get-CollectionSharingStatistics' {
    It 'berechnet Statistiken korrekt' {
      $entries = @(
        (New-CollectionEntry -GameName 'G1' -ConsoleKey 'NES' -Region 'EU' -SizeBytes 1024),
        (New-CollectionEntry -GameName 'G2' -ConsoleKey 'NES' -Region 'US' -SizeBytes 2048),
        (New-CollectionEntry -GameName 'G3' -ConsoleKey 'SNES' -Region 'EU' -SizeBytes 4096)
      )
      $stats = Get-CollectionSharingStatistics -Entries $entries
      $stats.TotalGames | Should -Be 3
      $stats.ConsoleCount | Should -Be 2
      $stats.RegionCount | Should -Be 2
      $stats.TotalSizeBytes | Should -Be 7168
    }
  }
}
