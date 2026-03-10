BeforeAll {
  . "$PSScriptRoot/../../modules/CoverScraper.ps1"
}

Describe 'LF-01: CoverScraper' {
  Describe 'New-CoverScraperConfig' {
    It 'erstellt Config mit Provider' {
      $cfg = New-CoverScraperConfig -Provider 'ScreenScraper'
      $cfg.Provider | Should -Be 'ScreenScraper'
      $cfg.ImageType | Should -Be 'box-2d'
      $cfg.MaxWidth | Should -Be 400
    }
  }

  Describe 'Get-CoverCachePath' {
    It 'gibt korrekten Cache-Pfad zurueck' {
      $path = Get-CoverCachePath -CacheDir 'C:\cache' -ConsoleKey 'snes' -GameName 'Super Mario World'
      $path | Should -BeLike '*snes*Super Mario World*'
    }
  }

  Describe 'New-CoverScrapeRequest' {
    It 'erstellt Request mit korrekten Feldern' {
      $req = New-CoverScrapeRequest -GameName 'Zelda' -ConsoleKey 'nes' -Hash 'AABB' -Region 'EU'
      $req.GameName | Should -Be 'Zelda'
      $req.ConsoleKey | Should -Be 'nes'
      $req.Status | Should -Be 'Pending'
    }
  }

  Describe 'Invoke-CoverScrape' {
    It 'markiert nicht-gecachte als NotFound' {
      $cfg = New-CoverScraperConfig -Provider 'Test' -CacheDir $TestDrive
      $reqs = @(
        (New-CoverScrapeRequest -GameName 'Game1' -ConsoleKey 'nes')
      )
      $results = Invoke-CoverScrape -Config $cfg -Requests $reqs
      $results.Count | Should -Be 1
      $results[0].Status | Should -Be 'NotFound'
    }
  }

  Describe 'Get-CoverScrapeReport' {
    It 'zaehlt Statuswerte korrekt' {
      $results = @(
        @{ Status = 'Cached' }, @{ Status = 'NotFound' }, @{ Status = 'NotFound' }
      )
      $report = Get-CoverScrapeReport -Results $results
      $report.Total | Should -Be 3
      $report.Cached | Should -Be 1
      $report.NotFound | Should -Be 2
    }
  }
}
