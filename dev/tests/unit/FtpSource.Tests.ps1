BeforeAll {
  . "$PSScriptRoot/../../modules/FtpSource.ps1"
}

Describe 'LF-16: FtpSource' {
  Describe 'New-FtpSourceConfig' {
    It 'erstellt Config mit Defaults' {
      $cfg = New-FtpSourceConfig -HostName 'ftp.example.com' -Port 21 -Username 'user'
      $cfg.Host | Should -Be 'ftp.example.com'
      $cfg.Port | Should -Be 21
      $cfg.Protocol | Should -Be 'FTP'
    }

    It 'unterstuetzt SFTP-Konfiguration' {
      $cfg = New-FtpSourceConfig -HostName 'sftp.example.com' -Port 22 -Protocol 'SFTP' -Username 'user'
      $cfg.Protocol | Should -Be 'SFTP'
      $cfg.Port | Should -Be 22
    }
  }

  Describe 'Test-FtpUri' {
    It 'validiert korrekte FTP-URI' {
      $r = Test-FtpUri -Uri 'ftp://ftp.example.com/roms'
      $r.Valid | Should -Be $true
    }

    It 'lehnt ungueltige URI ab' {
      $r = Test-FtpUri -Uri 'not-a-uri'
      $r.Valid | Should -Be $false
    }

    It 'lehnt HTTP ab' {
      $r = Test-FtpUri -Uri 'http://example.com'
      $r.Valid | Should -Be $false
    }
  }

  Describe 'New-FtpSyncPlan' {
    It 'erstellt Sync-Plan' {
      $cfg = New-FtpSourceConfig -HostName 'ftp.example.com' -Username 'user'
      $remote = @(
        @{ Name = 'game1.sfc'; Size = 1024 }
        @{ Name = 'game2.sfc'; Size = 2048 }
      )
      $local = @(
        @{ Name = 'game1.sfc'; Size = 1024 }
      )
      $plan = New-FtpSyncPlan -Config $cfg -RemoteFiles $remote -LocalFiles $local
      $plan.Download.Count | Should -Be 1
      $plan.Download[0].Name | Should -Be 'game2.sfc'
    }
  }

  Describe 'Get-FtpTransferProgress' {
    It 'berechnet Fortschritt' {
      $progress = Get-FtpTransferProgress -BytesTransferred 512 -TotalBytes 1024
      $progress.BytePercent | Should -Be 50
    }
  }
}
