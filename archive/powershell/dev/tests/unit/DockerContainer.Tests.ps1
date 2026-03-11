BeforeAll {
  . "$PSScriptRoot/../../modules/DockerContainer.ps1"
}

Describe 'DockerContainer (XL-01)' {

  Describe 'New-DockerfileConfig' {
    It 'erstellt gueltige Standard-Konfiguration' {
      $cfg = New-DockerfileConfig
      $cfg.Valid | Should -Be $true
      $cfg.BaseImage | Should -BeLike '*powershell*'
      $cfg.ExposedPort | Should -Be 8080
      $cfg.Volumes.Count | Should -Be 3
      $cfg.EntryPoint | Should -Be 'pwsh'
      $cfg.WorkDir | Should -Be '/app'
    }

    It 'akzeptiert benutzerdefinierten Port' {
      $cfg = New-DockerfileConfig -ExposedPort 9090
      $cfg.ExposedPort | Should -Be 9090
      $cfg.Valid | Should -Be $true
    }

    It 'gibt Fehler bei ungueltigem Port' {
      $cfg = New-DockerfileConfig -ExposedPort 0
      $cfg.Valid | Should -Be $false
      $cfg.Error | Should -BeLike '*Port*'
    }

    It 'gibt Fehler bei Port ueber 65535' {
      $cfg = New-DockerfileConfig -ExposedPort 70000
      $cfg.Valid | Should -Be $false
    }

    It 'setzt Labels korrekt' {
      $cfg = New-DockerfileConfig
      $cfg.Labels.Count | Should -BeGreaterThan 0
      $cfg.Labels['version'] | Should -Be '2.0'
    }
  }

  Describe 'ConvertTo-Dockerfile' {
    It 'generiert gueltigen Dockerfile-Text' {
      $cfg = New-DockerfileConfig -ExposedPort 8080
      $result = ConvertTo-Dockerfile -Config $cfg
      $result.Valid | Should -Be $true
      $result.Content | Should -BeLike 'FROM*'
      $result.Content | Should -BeLike '*EXPOSE 8080*'
      $result.Content | Should -BeLike '*WORKDIR /app*'
      $result.Content | Should -BeLike '*ENTRYPOINT*'
    }

    It 'gibt Fehler bei ungueltiger Config' {
      $cfg = New-DockerfileConfig -ExposedPort 0
      $result = ConvertTo-Dockerfile -Config $cfg
      $result.Valid | Should -Be $false
    }

    It 'enthaelt VOLUME-Eintraege' {
      $cfg = New-DockerfileConfig
      $result = ConvertTo-Dockerfile -Config $cfg
      $result.Content | Should -BeLike '*VOLUME*'
    }
  }

  Describe 'New-DockerComposeConfig' {
    It 'erstellt Docker-Compose-Konfiguration' {
      $cfg = New-DockerfileConfig
      $compose = New-DockerComposeConfig -DockerConfig $cfg
      $compose.Version | Should -Be '3.8'
      $compose.Services.ContainsKey('romcleanup') | Should -Be $true
      $compose.Services['romcleanup'].Restart | Should -Be 'unless-stopped'
    }

    It 'akzeptiert benutzerdefinierten ServiceName' {
      $cfg = New-DockerfileConfig
      $compose = New-DockerComposeConfig -DockerConfig $cfg -ServiceName 'myservice'
      $compose.Services.ContainsKey('myservice') | Should -Be $true
    }
  }

  Describe 'Get-DockerHealthCheck' {
    It 'erstellt Health-Check mit Standard-Port' {
      $hc = Get-DockerHealthCheck
      $hc.Test | Should -BeLike '*8080*'
      $hc.Retries | Should -Be 3
      $hc.Command | Should -BeLike 'HEALTHCHECK*'
    }

    It 'verwendet benutzerdefinierten Port' {
      $hc = Get-DockerHealthCheck -Port 9090
      $hc.Test | Should -BeLike '*9090*'
    }
  }

  Describe 'Get-DockerContainerStatistics' {
    It 'gibt korrekte Statistiken' {
      $cfg = New-DockerfileConfig
      $stats = Get-DockerContainerStatistics -Config $cfg
      $stats.ExposedPort | Should -Be 8080
      $stats.VolumeCount | Should -Be 3
      $stats.LabelCount | Should -BeGreaterThan 0
      $stats.HasHealthCheck | Should -Be $true
    }
  }
}
