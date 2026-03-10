BeforeAll {
  . "$PSScriptRoot/../../modules/ConversionPipeline.ps1"
}

Describe 'MF-06: ConversionPipeline' {
  Describe 'Test-DiskSpaceForConversion' {
    It 'gibt OK bei genuegend Platz' {
      $tempFile = Join-Path $TestDrive 'test.cso'
      Set-Content -Path $tempFile -Value ('X' * 1000)
      $result = Test-DiskSpaceForConversion -SourcePath $tempFile -TargetDir $TestDrive -MultiplierEstimate 2.0
      $result.Ok | Should -BeTrue
    }

    It 'meldet Fehler bei fehlender Quelldatei' {
      $result = Test-DiskSpaceForConversion -SourcePath 'C:\nonexistent.cso' -TargetDir $TestDrive
      $result.Ok | Should -BeFalse
      $result.Reason | Should -Be 'SourceNotFound'
    }
  }

  Describe 'New-ConversionPipeline' {
    It 'erstellt Pipeline mit Steps' {
      $steps = @( @{ Tool = 'ciso'; Action = 'decompress' }, @{ Tool = 'chdman'; Action = 'createcd' } )
      $result = New-ConversionPipeline -SourcePath 'C:\test.cso' -Steps $steps
      $result.Steps.Count | Should -Be 2
      $result.Status | Should -Be 'Pending'
    }
  }

  Describe 'Get-CsoToChdPipeline' {
    It 'erstellt korrekte CSO-CHD Pipeline' {
      $result = Get-CsoToChdPipeline -SourcePath 'C:\roms\game.cso' -OutputDir 'C:\output'
      $result.Steps.Count | Should -Be 2
      $result.Steps[0].Tool | Should -Be 'ciso'
      $result.Steps[1].Tool | Should -Be 'chdman'
    }
  }

  Describe 'Invoke-ConversionPipelineStep' {
    It 'DryRun gibt Skipped zurueck' {
      $step = @{ Tool = 'ciso'; Action = 'decompress'; Input = 'a.cso'; Output = 'a.iso' }
      $result = Invoke-ConversionPipelineStep -Step $step -Mode DryRun
      $result.Status | Should -Be 'DryRun'
      $result.Skipped | Should -BeTrue
    }
  }
}
