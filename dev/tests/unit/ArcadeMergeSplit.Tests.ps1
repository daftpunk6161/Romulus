BeforeAll {
  . "$PSScriptRoot/../../modules/ArcadeMergeSplit.ps1"
}

Describe 'LF-07: ArcadeMergeSplit' {
  Describe 'Get-ArcadeSetTypes' {
    It 'gibt drei Set-Typen zurueck' {
      $types = Get-ArcadeSetTypes
      $types.Count | Should -Be 3
      ($types | Where-Object { $_.Key -eq 'non-merged' }) | Should -Not -BeNullOrEmpty
    }
  }

  Describe 'Read-ArcadeDatParentClone' {
    It 'erkennt Parent und Clone' {
      $dat = @{
        'sf2'  = @{ Name = 'Street Fighter II'; CloneOf = '' }
        'sf2ce' = @{ Name = 'Street Fighter II CE'; CloneOf = 'sf2' }
        'sf2hf' = @{ Name = 'Street Fighter II HF'; CloneOf = 'sf2' }
      }
      $result = Read-ArcadeDatParentClone -DatIndex $dat
      $result.ParentCount | Should -Be 1
      $result.CloneCount | Should -Be 2
    }
  }

  Describe 'Get-ArcadeSetInfo' {
    It 'gibt Fehler bei fehlender Datei' {
      $result = Get-ArcadeSetInfo -ZipPath "$TestDrive\notexist.zip"
      $result.Valid | Should -BeFalse
    }
  }

  Describe 'New-MergeOperation' {
    It 'erstellt Operation mit korrekten Typen' {
      $op = New-MergeOperation -SourceType 'non-merged' -TargetType 'merged' -Sets @(@{SetName='sf2'})
      $op.SourceType | Should -Be 'non-merged'
      $op.TargetType | Should -Be 'merged'
      $op.Status | Should -Be 'Pending'
    }
  }

  Describe 'Get-MergePlan' {
    It 'erstellt Plan mit Parent/Clone-Info' {
      $dat = @{
        'sf2'  = @{ Name = 'SF2'; CloneOf = '' }
        'sf2ce' = @{ Name = 'SF2CE'; CloneOf = 'sf2' }
      }
      $sets = @(
        @{ SetName = 'sf2' }
        @{ SetName = 'sf2ce' }
      )
      $plan = Get-MergePlan -SourceType 'split' -TargetType 'merged' -Sets $sets -DatIndex $dat
      $plan.TotalSets | Should -Be 2
      $plan.ParentSets | Should -Be 1
      $plan.CloneSets | Should -Be 1
    }
  }
}
