BeforeAll {
  . "$PSScriptRoot/../../modules/ConvertQueue.ps1"
}

Describe 'MF-08: ConvertQueue' {
  Describe 'New-ConvertQueue' {
    It 'erstellt Queue mit Items' {
      $items = @(
        @{ SourcePath = 'a.iso'; TargetPath = 'a.chd'; Format = 'CHD' }
        @{ SourcePath = 'b.iso'; TargetPath = 'b.chd'; Format = 'CHD' }
      )
      $queue = New-ConvertQueue -Items $items
      $queue.TotalItems | Should -Be 2
      $queue.Status | Should -Be 'Ready'
      $queue.CurrentIndex | Should -Be 0
    }

    It 'erstellt leere Queue' {
      $queue = New-ConvertQueue -Items @()
      $queue.TotalItems | Should -Be 0
    }
  }

  Describe 'Suspend/Resume' {
    It 'pausiert und setzt Queue fort' {
      $queue = New-ConvertQueue -Items @( @{ SourcePath = 'a'; TargetPath = 'b'; Format = 'CHD' } )
      $queue.Status = 'Running'
      Suspend-ConvertQueue -Queue $queue
      $queue.Status | Should -Be 'Paused'

      Resume-ConvertQueue -Queue $queue
      $queue.Status | Should -Be 'Running'
    }

    It 'Resume auf nicht-pausierter Queue gibt Fehler' {
      $queue = New-ConvertQueue -Items @( @{ SourcePath = 'a'; TargetPath = 'b'; Format = 'CHD' } )
      $result = Resume-ConvertQueue -Queue $queue
      $result.Status | Should -Be 'Error'
    }
  }

  Describe 'Step-ConvertQueue' {
    It 'verarbeitet naechstes Item' {
      $queue = New-ConvertQueue -Items @(
        @{ SourcePath = 'a'; TargetPath = 'b'; Format = 'CHD' }
        @{ SourcePath = 'c'; TargetPath = 'd'; Format = 'CHD' }
      )
      $result = Step-ConvertQueue -Queue $queue -Mode DryRun
      $result.Status | Should -Be 'DryRun'
      $queue.CurrentIndex | Should -Be 1
    }

    It 'markiert Queue als Completed am Ende' {
      $queue = New-ConvertQueue -Items @( @{ SourcePath = 'a'; TargetPath = 'b'; Format = 'CHD' } )
      Step-ConvertQueue -Queue $queue -Mode DryRun
      $queue.Status | Should -Be 'Completed'
    }
  }

  Describe 'Get-ConvertQueueProgress' {
    It 'berechnet Fortschritt korrekt' {
      $queue = New-ConvertQueue -Items @(
        @{ SourcePath = 'a'; TargetPath = 'b'; Format = 'CHD' }
        @{ SourcePath = 'c'; TargetPath = 'd'; Format = 'CHD' }
      )
      Step-ConvertQueue -Queue $queue -Mode DryRun
      $progress = Get-ConvertQueueProgress -Queue $queue
      $progress.Percent | Should -Be 50.0
      $progress.Completed | Should -Be 1
      $progress.Total | Should -Be 2
    }
  }

  Describe 'Save/Import-ConvertQueue' {
    It 'round-trip persistiert Queue korrekt' {
      $queue = New-ConvertQueue -Items @( @{ SourcePath = 'a'; TargetPath = 'b'; Format = 'CHD' } )
      $path = Join-Path $TestDrive 'queue.json'
      Save-ConvertQueue -Queue $queue -Path $path
      Test-Path $path | Should -BeTrue

      $loaded = Import-ConvertQueue -Path $path
      $loaded.QueueId | Should -Be $queue.QueueId
      $loaded.TotalItems | Should -Be 1
    }
  }
}
