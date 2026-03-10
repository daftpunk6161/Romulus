# ================================================================
#  DOCKER CONTAINER – CLI + REST-API als Docker-Image (XL-01)
#  Headless-Server: TrueNAS, Unraid, Synology
# ================================================================

function New-DockerfileConfig {
  <#
  .SYNOPSIS
    Erstellt eine Dockerfile-Konfiguration fuer RomCleanup.
  .PARAMETER BaseImage
    Docker-Base-Image (z.B. mcr.microsoft.com/powershell).
  .PARAMETER ExposedPort
    Port fuer die REST-API.
  .PARAMETER Volumes
    Mount-Punkte fuer ROM-Ordner.
  #>
  param(
    [string]$BaseImage = 'mcr.microsoft.com/powershell:lts-alpine-3.17',
    [int]$ExposedPort = 8080,
    [string[]]$Volumes = @('/roms', '/config', '/output')
  )

  if ($ExposedPort -lt 1 -or $ExposedPort -gt 65535) {
    return @{ Valid = $false; Error = 'Port muss zwischen 1 und 65535 liegen' }
  }

  return @{
    BaseImage   = $BaseImage
    ExposedPort = $ExposedPort
    Volumes     = $Volumes
    EntryPoint  = 'pwsh'
    Cmd         = @('-NoProfile', '-File', '/app/Invoke-RomCleanup.ps1')
    WorkDir     = '/app'
    Labels      = @{
      'maintainer'  = 'RomCleanup'
      'description' = 'ROM Collection Management Tool'
      'version'     = '2.0'
    }
    Valid       = $true
  }
}

function ConvertTo-Dockerfile {
  <#
  .SYNOPSIS
    Konvertiert eine DockerfileConfig in Dockerfile-Text.
  .PARAMETER Config
    DockerfileConfig-Hashtable.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Config
  )

  if (-not $Config.Valid) {
    return @{ Content = ''; Valid = $false; Error = $Config.Error }
  }

  $lines = @()
  $lines += "FROM $($Config.BaseImage)"
  $lines += ''

  foreach ($label in $Config.Labels.GetEnumerator()) {
    $safeKey = $label.Key -replace '[^a-zA-Z0-9._-]', ''
    $safeVal = $label.Value -replace '"', '\"'
    $lines += "LABEL `"$safeKey`"=`"$safeVal`""
  }

  $lines += ''
  $lines += "WORKDIR $($Config.WorkDir)"
  $lines += ''
  $lines += 'COPY . /app/'
  $lines += ''

  foreach ($vol in $Config.Volumes) {
    $lines += "VOLUME $vol"
  }

  $lines += ''
  $lines += "EXPOSE $($Config.ExposedPort)"
  $lines += ''

  $cmdJson = ($Config.Cmd | ForEach-Object { "`"$_`"" }) -join ', '
  $lines += "ENTRYPOINT [`"$($Config.EntryPoint)`"]"
  $lines += "CMD [$cmdJson]"

  return @{
    Content = $lines -join "`n"
    Valid   = $true
  }
}

function New-DockerComposeConfig {
  <#
  .SYNOPSIS
    Erstellt eine Docker-Compose-Konfiguration.
  .PARAMETER ServiceName
    Name des Docker-Service.
  .PARAMETER DockerConfig
    DockerfileConfig-Hashtable.
  .PARAMETER RomPath
    Host-Pfad zu den ROMs.
  .PARAMETER ConfigPath
    Host-Pfad zur Konfiguration.
  #>
  param(
    [string]$ServiceName = 'romcleanup',
    [Parameter(Mandatory)][hashtable]$DockerConfig,
    [string]$RomPath = './roms',
    [string]$ConfigPath = './config'
  )

  return @{
    Version  = '3.8'
    Services = @{
      $ServiceName = @{
        Build       = '.'
        Ports       = @("$($DockerConfig.ExposedPort):$($DockerConfig.ExposedPort)")
        Volumes     = @(
          "${RomPath}:/roms:rw",
          "${ConfigPath}:/config:ro"
        )
        Restart     = 'unless-stopped'
        Environment = @{
          'ROM_CLEANUP_API_KEY' = '${ROM_CLEANUP_API_KEY}'
          'ROM_CLEANUP_MODE'    = 'DryRun'
        }
      }
    }
  }
}

function Get-DockerHealthCheck {
  <#
  .SYNOPSIS
    Erstellt einen Docker-HEALTHCHECK-Befehl.
  .PARAMETER Port
    API-Port.
  .PARAMETER IntervalSeconds
    Intervall in Sekunden.
  #>
  param(
    [int]$Port = 8080,
    [int]$IntervalSeconds = 30
  )

  return @{
    Test     = "curl -f http://localhost:$Port/health || exit 1"
    Interval = "${IntervalSeconds}s"
    Timeout  = '10s'
    Retries  = 3
    Command  = "HEALTHCHECK --interval=${IntervalSeconds}s --timeout=10s --retries=3 CMD curl -f http://localhost:$Port/health || exit 1"
  }
}

function Get-DockerContainerStatistics {
  <#
  .SYNOPSIS
    Gibt Statistiken ueber die Docker-Konfiguration zurueck.
  .PARAMETER Config
    DockerfileConfig-Hashtable.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Config
  )

  return @{
    BaseImage    = $Config.BaseImage
    ExposedPort  = $Config.ExposedPort
    VolumeCount  = $Config.Volumes.Count
    LabelCount   = $Config.Labels.Count
    HasHealthCheck = $true
    Platform     = 'linux/amd64'
  }
}
