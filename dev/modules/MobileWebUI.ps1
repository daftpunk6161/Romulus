# ================================================================
#  MOBILE WEB UI – Responsive Web-Frontend fuer REST-API (XL-02)
#  Read-Only-Monitoring vom Handy (React/Vue)
# ================================================================

function New-WebUIConfig {
  <#
  .SYNOPSIS
    Erstellt eine WebUI-Konfiguration fuer das Mobile-Frontend.
  .PARAMETER ApiBaseUrl
    Basis-URL der REST-API.
  .PARAMETER Theme
    UI-Theme (dark/light).
  .PARAMETER RefreshIntervalMs
    Auto-Refresh-Intervall in Millisekunden.
  #>
  param(
    [string]$ApiBaseUrl = 'http://127.0.0.1:8080',
    [ValidateSet('dark','light')][string]$Theme = 'dark',
    [int]$RefreshIntervalMs = 5000
  )

  if ($RefreshIntervalMs -lt 1000) {
    $RefreshIntervalMs = 1000
  }

  return @{
    ApiBaseUrl       = $ApiBaseUrl
    Theme            = $Theme
    RefreshIntervalMs = $RefreshIntervalMs
    ReadOnly         = $true
    Features         = @('run-status', 'progress-monitor', 'collection-stats', 'recent-activity')
    Responsive       = $true
    MinBreakpoint    = 320
  }
}

function Get-WebUIEndpointMap {
  <#
  .SYNOPSIS
    Gibt die Zuordnung von UI-Screens zu API-Endpunkten zurueck.
  #>

  return @(
    @{ Screen = 'dashboard';   Endpoint = '/health';           Method = 'GET';  Description = 'System-Status und Uptime' }
    @{ Screen = 'runs';        Endpoint = '/runs';             Method = 'GET';  Description = 'Laufende und vergangene Runs' }
    @{ Screen = 'run-detail';  Endpoint = '/runs/{id}';        Method = 'GET';  Description = 'Einzelner Run-Status' }
    @{ Screen = 'run-result';  Endpoint = '/runs/{id}/result'; Method = 'GET';  Description = 'Run-Ergebnis mit Details' }
    @{ Screen = 'live';        Endpoint = '/runs/{id}/stream'; Method = 'GET';  Description = 'SSE-Fortschrittsstream' }
  )
}

function New-WebUIRoute {
  <#
  .SYNOPSIS
    Erstellt eine Route-Definition fuer das Web-Frontend.
  .PARAMETER Path
    URL-Pfad der Route.
  .PARAMETER Component
    Komponenten-Name.
  .PARAMETER RequiresAuth
    Ob Authentifizierung erforderlich ist.
  #>
  param(
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$Component,
    [bool]$RequiresAuth = $true
  )

  return @{
    Path         = $Path
    Component    = $Component
    RequiresAuth = $RequiresAuth
    IsReadOnly   = $true
  }
}

function Get-WebUIRoutes {
  <#
  .SYNOPSIS
    Gibt alle vordefinierten Routes fuer das Mobile-Web-UI zurueck.
  #>

  return @(
    (New-WebUIRoute -Path '/'               -Component 'Dashboard'   -RequiresAuth $false)
    (New-WebUIRoute -Path '/runs'           -Component 'RunList'     -RequiresAuth $true)
    (New-WebUIRoute -Path '/runs/:id'       -Component 'RunDetail'   -RequiresAuth $true)
    (New-WebUIRoute -Path '/runs/:id/live'  -Component 'LiveStream'  -RequiresAuth $true)
    (New-WebUIRoute -Path '/stats'          -Component 'Statistics'  -RequiresAuth $true)
    (New-WebUIRoute -Path '/settings'       -Component 'Settings'    -RequiresAuth $true)
  )
}

function Get-ResponsiveBreakpoints {
  <#
  .SYNOPSIS
    Gibt die responsiven Breakpoints fuer das Mobile-Web-UI zurueck.
  #>

  return @{
    Mobile  = @{ Min = 320;  Max = 767;  Columns = 1; Sidebar = $false }
    Tablet  = @{ Min = 768;  Max = 1023; Columns = 2; Sidebar = $true  }
    Desktop = @{ Min = 1024; Max = 9999; Columns = 3; Sidebar = $true  }
  }
}

function Get-WebUIStatistics {
  <#
  .SYNOPSIS
    Gibt Statistiken ueber die WebUI-Konfiguration zurueck.
  .PARAMETER Config
    WebUI-Konfiguration.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Config
  )

  $routes = Get-WebUIRoutes
  $endpoints = Get-WebUIEndpointMap
  $breakpoints = Get-ResponsiveBreakpoints

  return @{
    RouteCount       = @($routes).Count
    EndpointCount    = @($endpoints).Count
    BreakpointCount  = $breakpoints.Count
    Theme            = $Config.Theme
    ReadOnly         = $Config.ReadOnly
    FeatureCount     = @($Config.Features).Count
  }
}
