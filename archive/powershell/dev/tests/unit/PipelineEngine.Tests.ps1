BeforeAll {
  . "$PSScriptRoot/../../modules/PipelineEngine.ps1"
}

Describe 'MF-20: PipelineEngine' {
  Describe 'New-PipelineStep' {
    It 'erstellt einen Schritt mit korrekten Defaults' {
      $step = New-PipelineStep -Action 'sort'
      $step.Action | Should -Be 'sort'
      $step.Status | Should -Be 'Pending'
      $step.Params.Count | Should -Be 0
    }

    It 'akzeptiert Params und Condition' {
      $step = New-PipelineStep -Action 'dedupe' -Params @{ Root = 'C:\Roms' } -Condition 'PreviousSuccess'
      $step.Action | Should -Be 'dedupe'
      $step.Params.Root | Should -Be 'C:\Roms'
      $step.Condition | Should -Be 'PreviousSuccess'
    }
  }

  Describe 'New-Pipeline' {
    It 'erstellt Pipeline mit Id und Steps' {
      $s1 = New-PipelineStep -Action 'sort'
      $s2 = New-PipelineStep -Action 'dedupe'
      $pipeline = New-Pipeline -Name 'TestPipeline' -Steps @($s1, $s2)
      $pipeline.Name | Should -Be 'TestPipeline'
      $pipeline.Steps.Count | Should -Be 2
      $pipeline.Status | Should -Be 'Ready'
      $pipeline.Id | Should -Not -BeNullOrEmpty
    }
  }

  Describe 'Invoke-PipelineStep' {
    It 'simuliert im DryRun-Modus' {
      $step = New-PipelineStep -Action 'sort'
      $result = Invoke-PipelineStep -Step $step -Mode 'DryRun'
      $result.Status | Should -Be 'DryRun'
      $step.Result.Simulated | Should -BeTrue
    }

    It 'fuehrt im Move-Modus aus' {
      $step = New-PipelineStep -Action 'sort'
      $result = Invoke-PipelineStep -Step $step -Mode 'Move'
      $result.Status | Should -Be 'Completed'
      $step.Result.Simulated | Should -BeFalse
    }

    It 'ueberspringt bei Condition-Pruefung' {
      $step = New-PipelineStep -Action 'dedupe' -Condition 'PreviousSuccess'
      $context = @{ PreviousStatus = 'Failed' }
      $result = Invoke-PipelineStep -Step $step -Mode 'Move' -Context $context
      $result.Status | Should -Be 'Skipped'
    }
  }

  Describe 'Invoke-Pipeline' {
    It 'fuehrt alle Steps im DryRun aus' {
      $s1 = New-PipelineStep -Action 'sort'
      $s2 = New-PipelineStep -Action 'dedupe'
      $pipeline = New-Pipeline -Name 'Test' -Steps @($s1, $s2)
      $result = Invoke-Pipeline -Pipeline $pipeline -Mode 'DryRun'
      $result.Completed | Should -Be 2
      $result.Failed | Should -Be 0
    }
  }

  Describe 'ConvertTo-PipelineDefinition' {
    It 'parst JSON-Definition korrekt' {
      $json = '{"name":"FromJSON","steps":[{"action":"sort","params":{"root":"C:\\Roms"}},{"action":"verify","params":{}}],"onError":"continue"}'
      $pipeline = ConvertTo-PipelineDefinition -Json $json
      $pipeline.Name | Should -Be 'FromJSON'
      $pipeline.Steps.Count | Should -Be 2
      $pipeline.OnError | Should -Be 'continue'
    }
  }
}
