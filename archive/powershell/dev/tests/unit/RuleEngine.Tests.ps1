BeforeAll {
  . "$PSScriptRoot/../../modules/RuleEngine.ps1"
}

Describe 'MF-19: RuleEngine' {
  Describe 'New-ClassificationRule' {
    It 'erstellt eine Regel mit korrekten Feldern' {
      $rule = New-ClassificationRule -Name 'Demo entfernen' -Conditions @(@{ Field = 'Category'; Op = 'eq'; Value = 'DEMO' }) -Action 'junk' -Reason 'Demos nicht gewuenscht'
      $rule.Name | Should -Be 'Demo entfernen'
      $rule.Action | Should -Be 'junk'
      $rule.Enabled | Should -BeTrue
      $rule.Priority | Should -Be 10
    }
  }

  Describe 'Test-ClassificationRule' {
    It 'matcht korrekt bei erfuellter Bedingung' {
      $rule = @{ Name = 'R1'; Conditions = @(@{ Field = 'Console'; Op = 'eq'; Value = 'SNES' }); Action = 'keep'; Enabled = $true }
      $item = @{ Console = 'SNES'; Name = 'Test' }
      Test-ClassificationRule -Rule $rule -Item $item | Should -BeTrue
    }

    It 'matcht nicht bei nicht-erfuellter Bedingung' {
      $rule = @{ Name = 'R1'; Conditions = @(@{ Field = 'Console'; Op = 'eq'; Value = 'SNES' }); Action = 'keep'; Enabled = $true }
      $item = @{ Console = 'NES'; Name = 'Test' }
      Test-ClassificationRule -Rule $rule -Item $item | Should -BeFalse
    }

    It 'respektiert Enabled-Flag' {
      $rule = @{ Name = 'R1'; Conditions = @(@{ Field = 'Console'; Op = 'eq'; Value = 'SNES' }); Action = 'keep'; Enabled = $false }
      $item = @{ Console = 'SNES' }
      Test-ClassificationRule -Rule $rule -Item $item | Should -BeFalse
    }

    It 'unterstuetzt contains-Operator' {
      $rule = @{ Name = 'R1'; Conditions = @(@{ Field = 'FileName'; Op = 'contains'; Value = 'Beta' }); Action = 'junk'; Enabled = $true }
      $item = @{ FileName = 'Game (Beta 1).zip' }
      Test-ClassificationRule -Rule $rule -Item $item | Should -BeTrue
    }
  }

  Describe 'Invoke-RuleEngine' {
    It 'gibt hoechste Prioritaet zurueck' {
      $r1 = @{ Name = 'Low'; Priority = 1; Conditions = @(@{ Field = 'Console'; Op = 'eq'; Value = 'SNES' }); Action = 'keep'; Reason = 'Low'; Enabled = $true }
      $r2 = @{ Name = 'High'; Priority = 100; Conditions = @(@{ Field = 'Console'; Op = 'eq'; Value = 'SNES' }); Action = 'junk'; Reason = 'High'; Enabled = $true }
      $item = @{ Console = 'SNES' }

      $result = Invoke-RuleEngine -Rules @($r1, $r2) -Item $item
      $result.Matched | Should -BeTrue
      $result.RuleName | Should -Be 'High'
      $result.Action | Should -Be 'junk'
    }

    It 'gibt Matched=$false bei leeren Regeln' {
      $result = Invoke-RuleEngine -Rules @() -Item @{ Console = 'SNES' }
      $result.Matched | Should -BeFalse
    }
  }

  Describe 'Test-RuleSyntax' {
    It 'validiert korrekte Regel' {
      $rule = @{ Name = 'Test'; Conditions = @(@{ Field = 'Console'; Op = 'eq'; Value = 'SNES' }); Action = 'keep' }
      $result = Test-RuleSyntax -Rule $rule
      $result.Valid | Should -BeTrue
      $result.Errors.Count | Should -Be 0
    }

    It 'erkennt fehlenden Namen' {
      $rule = @{ Conditions = @(@{ Field = 'X'; Op = 'eq'; Value = 'Y' }); Action = 'keep' }
      $result = Test-RuleSyntax -Rule $rule
      $result.Valid | Should -BeFalse
      $result.Errors | Should -Contain 'Name ist erforderlich'
    }

    It 'erkennt ungueltigen Operator' {
      $rule = @{ Name = 'T'; Conditions = @(@{ Field = 'X'; Op = 'invalid'; Value = 'Y' }); Action = 'keep' }
      $result = Test-RuleSyntax -Rule $rule
      $result.Valid | Should -BeFalse
    }
  }

  Describe 'Invoke-BatchRuleEngine' {
    It 'verarbeitet Batch korrekt' {
      $rule = @{ Name = 'R1'; Priority = 10; Conditions = @(@{ Field = 'Console'; Op = 'eq'; Value = 'SNES' }); Action = 'keep'; Reason = 'SNES behalten'; Enabled = $true }
      $items = @(
        @{ Console = 'SNES'; Name = 'A' }
        @{ Console = 'NES'; Name = 'B' }
        @{ Console = 'SNES'; Name = 'C' }
      )
      $result = Invoke-BatchRuleEngine -Rules @($rule) -Items $items
      $result.Stats.Total | Should -Be 3
      $result.Stats.Matched | Should -Be 2
      $result.Stats.Unmatched | Should -Be 1
    }

    It 'behandelt leere Items' {
      $result = Invoke-BatchRuleEngine -Rules @() -Items @()
      $result.Stats.Total | Should -Be 0
    }
  }
}
