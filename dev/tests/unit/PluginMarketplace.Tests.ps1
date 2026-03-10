BeforeAll {
  . "$PSScriptRoot/../../modules/PluginMarketplace.ps1"
}

Describe 'LF-18: PluginMarketplace' {
  Describe 'New-PluginMarketplaceConfig' {
    It 'erstellt Config' {
      $cfg = New-PluginMarketplaceConfig -CatalogUrl 'https://example.com/catalog.json'
      $cfg.CatalogUrl | Should -Be 'https://example.com/catalog.json'
      $cfg.AllowUntrusted | Should -Be $false
    }
  }

  Describe 'New-PluginListing' {
    It 'erstellt Plugin-Listing' {
      $listing = New-PluginListing -Id 'my-plugin' -Name 'MyPlugin' -Version '1.0.0' -Author 'Test' -Description 'Desc'
      $listing.Id | Should -Be 'my-plugin'
      $listing.Name | Should -Be 'MyPlugin'
      $listing.Version | Should -Be '1.0.0'
      $listing.Author | Should -Be 'Test'
    }
  }

  Describe 'Search-PluginCatalog' {
    BeforeAll {
      $script:catalog = @(
        (New-PluginListing -Id 'retro-ach' -Name 'RetroAchievements' -Version '1.0' -Author 'A' -Description 'Achievement tracking')
        (New-PluginListing -Id 'nes-fix' -Name 'NES-Header-Fix' -Version '2.0' -Author 'B' -Description 'Fix NES headers')
        (New-PluginListing -Id 'snes-sort' -Name 'SNES-Sorter' -Version '1.5' -Author 'A' -Description 'Sort SNES roms')
      )
    }

    It 'findet nach Name' {
      $hits = Search-PluginCatalog -Catalog $script:catalog -Query 'NES'
      $hits.Count | Should -Be 2
    }

    It 'findet nach Beschreibung' {
      $hits = Search-PluginCatalog -Catalog $script:catalog -Query 'Achievement'
      @($hits).Count | Should -Be 1
      @($hits)[0].Name | Should -Be 'RetroAchievements'
    }

    It 'gibt leer zurueck bei keinem Match' {
      $hits = Search-PluginCatalog -Catalog $script:catalog -Query 'nonexistent'
      $hits.Count | Should -Be 0
    }
  }

  Describe 'Test-PluginInstalled' {
    It 'erkennt installiertes Plugin' {
      $cfg = New-PluginMarketplaceConfig
      $cfg.InstalledPlugins['pluginA'] = @{ Version = '1.0' }
      $r = Test-PluginInstalled -Config $cfg -PluginId 'pluginA'
      $r | Should -Be $true
    }

    It 'erkennt nicht-installiertes Plugin' {
      $cfg = New-PluginMarketplaceConfig
      $r = Test-PluginInstalled -Config $cfg -PluginId 'pluginC'
      $r | Should -Be $false
    }
  }

  Describe 'Test-PluginUpdateAvailable' {
    It 'erkennt Update' {
      $cfg = New-PluginMarketplaceConfig
      $cfg.InstalledPlugins['p1'] = @{ Version = '1.0.0' }
      $listing = New-PluginListing -Id 'p1' -Name 'P1' -Version '2.0.0'
      $r = Test-PluginUpdateAvailable -Config $cfg -Listing $listing
      $r.Available | Should -Be $true
    }

    It 'kein Update bei gleicher Version' {
      $cfg = New-PluginMarketplaceConfig
      $cfg.InstalledPlugins['p1'] = @{ Version = '2.0.0' }
      $listing = New-PluginListing -Id 'p1' -Name 'P1' -Version '2.0.0'
      $r = Test-PluginUpdateAvailable -Config $cfg -Listing $listing
      $r.Available | Should -Be $false
    }
  }

  Describe 'Get-PluginCatalogStatistics' {
    It 'berechnet Statistiken' {
      $catalog = @(
        (New-PluginListing -Id 'a' -Name 'A' -Version '1.0' -Type 'operation')
        (New-PluginListing -Id 'b' -Name 'B' -Version '2.0' -Type 'report')
        (New-PluginListing -Id 'c' -Name 'C' -Version '1.5' -Type 'operation')
      )
      $stats = Get-PluginCatalogStatistics -Catalog $catalog
      $stats.Total | Should -Be 3
      $stats.ByType.Count | Should -Be 2
    }
  }
}
