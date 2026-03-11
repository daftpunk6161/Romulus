BeforeAll {
  . "$PSScriptRoot/../../modules/CloudSettingsSync.ps1"
}

Describe 'LF-17: CloudSettingsSync' {
  Describe 'New-CloudSyncConfig' {
    It 'erstellt Config mit Provider' {
      $cfg = New-CloudSyncConfig -Provider 'OneDrive' -SyncPath 'C:\OneDrive\RomCleanup'
      $cfg.Provider | Should -Be 'OneDrive'
      $cfg.SyncPath | Should -Be 'C:\OneDrive\RomCleanup'
      $cfg.AutoSync | Should -Be $false
    }
  }

  Describe 'New-SyncManifest' {
    It 'erstellt Manifest aus Dateien' {
      $files = @(
        @{ Name = 'settings.json'; Hash = 'AABB'; Modified = '2025-01-01T00:00:00' }
        @{ Name = 'rules.json'; Hash = 'CCDD'; Modified = '2025-01-02T00:00:00' }
      )
      $manifest = New-SyncManifest -MachineName 'PC1' -Files $files
      $manifest.MachineName | Should -Be 'PC1'
      @($manifest.Files).Count | Should -BeGreaterOrEqual 1
    }
  }

  Describe 'Compare-SyncManifests' {
    It 'erkennt geaenderte und neue Dateien' {
      $local = New-SyncManifest -MachineName 'PC1' -Files @(
        @{ Name = 'settings.json'; Hash = 'AABB' }
        @{ Name = 'rules.json'; Hash = 'OLD1' }
      )
      $remote = New-SyncManifest -MachineName 'PC2' -Files @(
        @{ Name = 'settings.json'; Hash = 'AABB' }
        @{ Name = 'rules.json'; Hash = 'NEW1' }
        @{ Name = 'extra.json'; Hash = 'EEFF' }
      )
      $diff = Compare-SyncManifests -Local $local -Remote $remote
      $diff.Conflicts.Count | Should -Be 1
      $diff.ToDownload.Count | Should -Be 1
      $diff.InSync | Should -Be $false
    }
  }

  Describe 'Get-SyncStatus' {
    It 'zeigt Status an' {
      $cfg = New-CloudSyncConfig -Provider 'OneDrive' -SyncPath 'C:\NonExistent\Path'
      $status = Get-SyncStatus -Config $cfg
      $status.Provider | Should -Be 'OneDrive'
      $status.Enabled | Should -Be $true
    }
  }
}
