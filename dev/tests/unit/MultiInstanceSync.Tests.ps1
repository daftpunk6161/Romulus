BeforeAll {
  . "$PSScriptRoot/../../modules/MultiInstanceSync.ps1"
}

Describe 'MultiInstanceSync (XL-13)' {

  Describe 'New-InstanceIdentity' {
    It 'erstellt eindeutige Identitaet' {
      $id = New-InstanceIdentity
      $id.InstanceId | Should -Not -BeNullOrEmpty
      $id.InstanceId.Length | Should -Be 12
      $id.MachineName | Should -Not -BeNullOrEmpty
      $id.Status | Should -Be 'Active'
    }

    It 'akzeptiert benutzerdefinierte Werte' {
      $id = New-InstanceIdentity -MachineName 'TestPC' -InstanceName 'worker1'
      $id.MachineName | Should -Be 'TestPC'
      $id.InstanceName | Should -Be 'worker1'
    }

    It 'generiert unterschiedliche IDs' {
      $id1 = New-InstanceIdentity
      $id2 = New-InstanceIdentity
      $id1.InstanceId | Should -Not -Be $id2.InstanceId
    }
  }

  Describe 'New-SyncManifest' {
    It 'erstellt Manifest' {
      $identity = New-InstanceIdentity -MachineName 'PC1'
      $manifest = New-SyncManifest -Identity $identity -SyncRoot '\\server\sync'
      $manifest.InstanceId | Should -Be $identity.InstanceId
      $manifest.SyncRoot | Should -Be '\\server\sync'
      $manifest.Version | Should -Be 1
      @($manifest.Operations).Count | Should -Be 0
    }
  }

  Describe 'Add-SyncOperation' {
    It 'fuegt Operation hinzu' {
      $identity = New-InstanceIdentity
      $manifest = New-SyncManifest -Identity $identity -SyncRoot 'C:\sync'
      $op = Add-SyncOperation -Manifest $manifest -OperationType 'Move' -SourcePath 'roms/a.rom' -TargetPath 'sorted/NES/a.rom'
      $op.OperationType | Should -Be 'Move'
      $op.Status | Should -Be 'Pending'
      @($manifest.Operations).Count | Should -Be 1
      $manifest.Version | Should -Be 2
    }
  }

  Describe 'Test-SyncConflict' {
    It 'erkennt Konflikte' {
      $id1 = New-InstanceIdentity -MachineName 'PC1'
      $id2 = New-InstanceIdentity -MachineName 'PC2'
      $m1 = New-SyncManifest -Identity $id1 -SyncRoot 'sync'
      $m2 = New-SyncManifest -Identity $id2 -SyncRoot 'sync'

      Add-SyncOperation -Manifest $m1 -OperationType 'Move' -SourcePath 'game.rom' -TargetPath 'NES/game.rom' | Out-Null
      Add-SyncOperation -Manifest $m2 -OperationType 'Delete' -SourcePath 'game.rom' | Out-Null

      $result = Test-SyncConflict -LocalManifest $m1 -RemoteManifest $m2
      $result.HasConflicts | Should -Be $true
      $result.ConflictCount | Should -Be 1
    }

    It 'keine Konflikte bei unterschiedlichen Dateien' {
      $id1 = New-InstanceIdentity
      $id2 = New-InstanceIdentity
      $m1 = New-SyncManifest -Identity $id1 -SyncRoot 'sync'
      $m2 = New-SyncManifest -Identity $id2 -SyncRoot 'sync'

      Add-SyncOperation -Manifest $m1 -OperationType 'Move' -SourcePath 'a.rom' -TargetPath 'x' | Out-Null
      Add-SyncOperation -Manifest $m2 -OperationType 'Move' -SourcePath 'b.rom' -TargetPath 'y' | Out-Null

      $result = Test-SyncConflict -LocalManifest $m1 -RemoteManifest $m2
      $result.HasConflicts | Should -Be $false
    }
  }

  Describe 'Merge-SyncManifests' {
    It 'merged konfliktfrei' {
      $id1 = New-InstanceIdentity
      $id2 = New-InstanceIdentity
      $m1 = New-SyncManifest -Identity $id1 -SyncRoot 'sync'
      $m2 = New-SyncManifest -Identity $id2 -SyncRoot 'sync'

      Add-SyncOperation -Manifest $m1 -OperationType 'Move' -SourcePath 'a.rom' -TargetPath 'x' | Out-Null
      Add-SyncOperation -Manifest $m2 -OperationType 'Move' -SourcePath 'b.rom' -TargetPath 'y' | Out-Null

      $result = Merge-SyncManifests -LocalManifest $m1 -RemoteManifest $m2
      $result.Merged | Should -Be $true
      $result.MergedCount | Should -Be 1
    }

    It 'verweigert Merge bei Konflikten' {
      $id1 = New-InstanceIdentity
      $id2 = New-InstanceIdentity
      $m1 = New-SyncManifest -Identity $id1 -SyncRoot 'sync'
      $m2 = New-SyncManifest -Identity $id2 -SyncRoot 'sync'

      Add-SyncOperation -Manifest $m1 -OperationType 'Move' -SourcePath 'same.rom' -TargetPath 'a' | Out-Null
      Add-SyncOperation -Manifest $m2 -OperationType 'Delete' -SourcePath 'same.rom' | Out-Null

      $result = Merge-SyncManifests -LocalManifest $m1 -RemoteManifest $m2
      $result.Merged | Should -Be $false
    }
  }

  Describe 'Get-MultiInstanceStatistics' {
    It 'berechnet Statistiken' {
      $id1 = New-InstanceIdentity -MachineName 'PC1'
      $id2 = New-InstanceIdentity -MachineName 'PC2'
      $m1 = New-SyncManifest -Identity $id1 -SyncRoot 'sync'
      $m2 = New-SyncManifest -Identity $id2 -SyncRoot 'sync'
      Add-SyncOperation -Manifest $m1 -OperationType 'Move' -SourcePath 'a' -TargetPath 'b' | Out-Null

      $stats = Get-MultiInstanceStatistics -Manifests @($m1, $m2)
      $stats.ManifestCount | Should -Be 2
      $stats.TotalOperations | Should -Be 1
      $stats.UniqueInstances | Should -Be 2
    }
  }
}
