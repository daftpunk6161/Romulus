BeforeAll {
  . "$PSScriptRoot/../../modules/BackupManager.ps1"
}

Describe 'MF-25: BackupManager' {
  Describe 'New-BackupConfig' {
    It 'erstellt Standard-Konfiguration' {
      $cfg = New-BackupConfig -BackupRoot 'D:\Backups'
      $cfg.BackupRoot | Should -Be 'D:\Backups'
      $cfg.RetentionDays | Should -Be 30
      $cfg.MaxSizeGB | Should -Be 50
      $cfg.Enabled | Should -BeTrue
    }

    It 'akzeptiert benutzerdefinierte Werte' {
      $cfg = New-BackupConfig -BackupRoot 'E:\BK' -RetentionDays 7 -MaxSizeGB 10
      $cfg.RetentionDays | Should -Be 7
      $cfg.MaxSizeGB | Should -Be 10
    }
  }

  Describe 'New-BackupSession' {
    It 'erstellt Session mit Timestamp' {
      $cfg = New-BackupConfig -BackupRoot (Join-Path $TestDrive 'backups')
      $session = New-BackupSession -Config $cfg
      $session.SessionId | Should -Not -BeNullOrEmpty
      $session.Status | Should -Be 'Open'
      $session.Files.Count | Should -Be 0
      $session.TotalSize | Should -Be 0
    }

    It 'akzeptiert Label' {
      $cfg = New-BackupConfig -BackupRoot (Join-Path $TestDrive 'backups')
      $session = New-BackupSession -Config $cfg -Label 'pre-dedupe'
      $session.SessionId | Should -BeLike '*pre-dedupe'
    }
  }

  Describe 'Add-FileToBackup' {
    It 'merkt Datei im DryRun vor' {
      $tmpDir = Join-Path $TestDrive 'backup-src'
      New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
      $src = Join-Path $tmpDir 'game.zip'
      Set-Content -Path $src -Value 'testdata'

      $cfg = New-BackupConfig -BackupRoot (Join-Path $TestDrive 'backups')
      $session = New-BackupSession -Config $cfg

      $result = Add-FileToBackup -Session $session -SourcePath $src -Mode 'DryRun'
      $result.Status | Should -Be 'DryRun'
      $session.Files.Count | Should -Be 1
    }

    It 'gibt Fehler bei fehlender Quelldatei' {
      $cfg = New-BackupConfig -BackupRoot (Join-Path $TestDrive 'backups')
      $session = New-BackupSession -Config $cfg
      $result = Add-FileToBackup -Session $session -SourcePath 'C:\nicht\vorhanden.zip' -Mode 'DryRun'
      $result.Status | Should -Be 'Error'
    }
  }

  Describe 'Invoke-BackupRetention' {
    It 'gibt NoBackupDir bei fehlendem Verzeichnis' {
      $cfg = New-BackupConfig -BackupRoot (Join-Path $TestDrive 'nonexistent')
      $result = Invoke-BackupRetention -Config $cfg
      $result.Status | Should -Be 'NoBackupDir'
    }
  }

  Describe 'Get-BackupSizeTotal' {
    It 'gibt 0 bei fehlendem Verzeichnis' {
      $result = Get-BackupSizeTotal -BackupRoot (Join-Path $TestDrive 'nonexistent')
      $result.TotalBytes | Should -Be 0
      $result.SessionCount | Should -Be 0
    }

    It 'berechnet Groesse korrekt' {
      $bkDir = Join-Path $TestDrive 'bk-size'
      $sessDir = Join-Path $bkDir 'session1'
      New-Item -ItemType Directory -Path $sessDir -Force | Out-Null
      Set-Content -Path (Join-Path $sessDir 'a.txt') -Value 'testcontent'

      $result = Get-BackupSizeTotal -BackupRoot $bkDir
      $result.TotalBytes | Should -BeGreaterThan 0
      $result.SessionCount | Should -Be 1
    }
  }
}
