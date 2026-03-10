BeforeAll {
  . "$PSScriptRoot/../../modules/Telemetry.ps1"
}

Describe 'Telemetry (XL-14)' {

  Describe 'New-TelemetryConfig' {
    It 'erstellt Config mit Opt-out als Standard' {
      $cfg = New-TelemetryConfig
      $cfg.Enabled | Should -Be $false
      $cfg.CollectionLevel | Should -Be 'Minimal'
      $cfg.AnonymousId | Should -Not -BeNullOrEmpty
      $cfg.ConsentDate | Should -BeNullOrEmpty
    }

    It 'setzt ConsentDate bei Aktivierung' {
      $cfg = New-TelemetryConfig -Enabled $true
      $cfg.Enabled | Should -Be $true
      $cfg.ConsentDate | Should -Not -BeNullOrEmpty
    }

    It 'akzeptiert benutzerdefinierte ID' {
      $cfg = New-TelemetryConfig -AnonymousId 'custom123'
      $cfg.AnonymousId | Should -Be 'custom123'
    }
  }

  Describe 'New-TelemetryEvent' {
    It 'erstellt Event wenn aktiviert' {
      $cfg = New-TelemetryConfig -Enabled $true
      $event = New-TelemetryEvent -EventName 'RunStarted' -Category 'Feature' -Config $cfg
      $event | Should -Not -BeNullOrEmpty
      $event.EventName | Should -Be 'RunStarted'
      $event.Category | Should -Be 'Feature'
    }

    It 'gibt null bei deaktivierter Telemetrie' {
      $cfg = New-TelemetryConfig -Enabled $false
      $event = New-TelemetryEvent -EventName 'Test' -Config $cfg
      $event | Should -BeNullOrEmpty
    }

    It 'filtert sensitive Daten (Pfade)' {
      $cfg = New-TelemetryConfig -Enabled $true
      $props = @{ SafeKey = 'safeValue'; UnsafePath = 'C:\Users\test\roms' }
      $event = New-TelemetryEvent -EventName 'Test' -Config $cfg -Properties $props
      $event.Properties.ContainsKey('SafeKey') | Should -Be $true
      $event.Properties.ContainsKey('UnsafePath') | Should -Be $false
    }

    It 'filtert IPs' {
      $cfg = New-TelemetryConfig -Enabled $true
      $props = @{ Server = '192.168.1.1'; Count = 42 }
      $event = New-TelemetryEvent -EventName 'Test' -Config $cfg -Properties $props
      $event.Properties.ContainsKey('Server') | Should -Be $false
      $event.Properties.ContainsKey('Count') | Should -Be $true
    }
  }

  Describe 'Get-TelemetryConsentText' {
    It 'gibt deutschen Text zurueck' {
      $text = Get-TelemetryConsentText -Language 'de'
      $text.Title | Should -Not -BeNullOrEmpty
      $text.Body | Should -BeLike '*anonym*'
      $text.Consent | Should -Not -BeNullOrEmpty
      $text.Decline | Should -Not -BeNullOrEmpty
    }

    It 'gibt englischen Text zurueck' {
      $text = Get-TelemetryConsentText -Language 'en'
      $text.Title | Should -BeLike '*Anonymous*'
    }
  }

  Describe 'Add-TelemetryEvent' {
    It 'fuegt Event zur Queue hinzu' {
      $event = @{ EventName = 'Test'; Timestamp = 'now' }
      $queue = Add-TelemetryEvent -Queue @() -Event $event
      @($queue).Count | Should -Be 1
    }

    It 'beschraenkt Queue-Groesse' {
      $queue = @()
      for ($i = 0; $i -lt 10; $i++) {
        $event = @{ EventName = "Event$i" }
        $queue = Add-TelemetryEvent -Queue $queue -Event $event -MaxQueueSize 5
      }
      @($queue).Count | Should -Be 5
    }
  }

  Describe 'Get-TelemetryAggregation' {
    It 'aggregiert Events korrekt' {
      $events = @(
        @{ EventName = 'RunStarted'; Category = 'Feature'; Timestamp = '2025-01-01' },
        @{ EventName = 'RunStarted'; Category = 'Feature'; Timestamp = '2025-01-02' },
        @{ EventName = 'ErrorOccurred'; Category = 'Error'; Timestamp = '2025-01-03' }
      )
      $agg = Get-TelemetryAggregation -Events $events
      $agg.TotalEvents | Should -Be 3
      $agg.Categories['Feature'] | Should -Be 2
      $agg.Categories['Error'] | Should -Be 1
      $agg.TopFeatures['RunStarted'] | Should -Be 2
    }
  }

  Describe 'Get-TelemetryStatistics' {
    It 'gibt maskierte ID zurueck' {
      $cfg = New-TelemetryConfig -Enabled $true
      $stats = Get-TelemetryStatistics -Config $cfg -EventCount 42
      $stats.Enabled | Should -Be $true
      $stats.EventCount | Should -Be 42
      $stats.AnonymousId | Should -BeLike '*...'
    }
  }
}
