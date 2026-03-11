BeforeAll {
  . "$PSScriptRoot/../../modules/MobileWebUI.ps1"
}

Describe 'MobileWebUI (XL-02)' {

  Describe 'New-WebUIConfig' {
    It 'erstellt Standard-Konfiguration' {
      $cfg = New-WebUIConfig
      $cfg.ApiBaseUrl | Should -Be 'http://127.0.0.1:8080'
      $cfg.Theme | Should -Be 'dark'
      $cfg.ReadOnly | Should -Be $true
      $cfg.Responsive | Should -Be $true
      $cfg.RefreshIntervalMs | Should -Be 5000
    }

    It 'erzwingt Minimum-Refresh-Intervall' {
      $cfg = New-WebUIConfig -RefreshIntervalMs 500
      $cfg.RefreshIntervalMs | Should -Be 1000
    }

    It 'akzeptiert Light-Theme' {
      $cfg = New-WebUIConfig -Theme 'light'
      $cfg.Theme | Should -Be 'light'
    }

    It 'enthaelt Features-Liste' {
      $cfg = New-WebUIConfig
      @($cfg.Features).Count | Should -BeGreaterThan 0
      $cfg.Features | Should -Contain 'run-status'
    }
  }

  Describe 'Get-WebUIEndpointMap' {
    It 'gibt Endpunkt-Zuordnungen zurueck' {
      $map = Get-WebUIEndpointMap
      @($map).Count | Should -BeGreaterThan 0
      $map[0].Screen | Should -Not -BeNullOrEmpty
      $map[0].Endpoint | Should -Not -BeNullOrEmpty
      $map[0].Method | Should -Be 'GET'
    }

    It 'enthaelt Dashboard-Endpunkt' {
      $map = Get-WebUIEndpointMap
      $dashboard = @($map | Where-Object { $_.Screen -eq 'dashboard' })
      $dashboard.Count | Should -Be 1
      $dashboard[0].Endpoint | Should -Be '/health'
    }
  }

  Describe 'New-WebUIRoute' {
    It 'erstellt Route mit Auth' {
      $route = New-WebUIRoute -Path '/test' -Component 'TestComp'
      $route.Path | Should -Be '/test'
      $route.Component | Should -Be 'TestComp'
      $route.RequiresAuth | Should -Be $true
      $route.IsReadOnly | Should -Be $true
    }

    It 'erstellt Route ohne Auth' {
      $route = New-WebUIRoute -Path '/' -Component 'Home' -RequiresAuth $false
      $route.RequiresAuth | Should -Be $false
    }
  }

  Describe 'Get-WebUIRoutes' {
    It 'gibt alle vordefinierten Routes zurueck' {
      $routes = Get-WebUIRoutes
      @($routes).Count | Should -BeGreaterOrEqual 5
    }

    It 'enthaelt Dashboard ohne Auth' {
      $routes = Get-WebUIRoutes
      $dashRoute = @($routes | Where-Object { $_.Path -eq '/' })
      $dashRoute.Count | Should -Be 1
      $dashRoute[0].RequiresAuth | Should -Be $false
    }
  }

  Describe 'Get-ResponsiveBreakpoints' {
    It 'gibt drei Breakpoints zurueck' {
      $bp = Get-ResponsiveBreakpoints
      $bp.ContainsKey('Mobile') | Should -Be $true
      $bp.ContainsKey('Tablet') | Should -Be $true
      $bp.ContainsKey('Desktop') | Should -Be $true
    }

    It 'Mobile hat keine Sidebar' {
      $bp = Get-ResponsiveBreakpoints
      $bp.Mobile.Sidebar | Should -Be $false
      $bp.Mobile.Columns | Should -Be 1
    }
  }

  Describe 'Get-WebUIStatistics' {
    It 'gibt korrekte Statistiken' {
      $cfg = New-WebUIConfig
      $stats = Get-WebUIStatistics -Config $cfg
      $stats.RouteCount | Should -BeGreaterThan 0
      $stats.EndpointCount | Should -BeGreaterThan 0
      $stats.Theme | Should -Be 'dark'
      $stats.ReadOnly | Should -Be $true
    }
  }
}
