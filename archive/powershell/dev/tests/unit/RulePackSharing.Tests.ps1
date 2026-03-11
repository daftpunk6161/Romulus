BeforeAll {
  . "$PSScriptRoot/../../modules/RulePackSharing.ps1"
}

Describe 'LF-19: RulePackSharing' {
  Describe 'New-RulePack' {
    It 'erstellt leeres RulePack' {
      $rp = New-RulePack -Name 'MyPack' -Author 'Tester' -Description 'Test rules'
      $rp.Name | Should -Be 'MyPack'
      $rp.Author | Should -Be 'Tester'
      $rp.Rules.Count | Should -Be 0
    }
  }

  Describe 'Add-RuleToRulePack' {
    It 'fuegt Regel hinzu' {
      $rp = New-RulePack -Name 'Pack' -Author 'A'
      $rp = Add-RuleToRulePack -RulePack $rp -Pattern '^\(Beta\)' -Category 'junk'
      $rp.Rules.Count | Should -Be 1
      $rp.Rules[0].Pattern | Should -Be '^\(Beta\)'
    }
  }

  Describe 'Test-RulePackValid' {
    It 'akzeptiert gueltiges Pack' {
      $rp = New-RulePack -Name 'Valid' -Author 'A'
      $rp = Add-RuleToRulePack -RulePack $rp -Pattern '^\(Beta\)' -Category 'junk'
      $result = Test-RulePackValid -RulePack $rp
      $result.Valid | Should -Be $true
    }

    It 'lehnt Pack ohne Name ab' {
      $rp = @{ Name = ''; Author = 'A'; Version = '1.0'; Rules = @() }
      $result = Test-RulePackValid -RulePack $rp
      $result.Valid | Should -Be $false
    }
  }

  Describe 'Merge-RulePacks' {
    It 'merged zwei Packs ohne Duplikate' {
      $rp1 = New-RulePack -Name 'Pack1' -Author 'A'
      $rp1 = Add-RuleToRulePack -RulePack $rp1 -Pattern 'a' -Category 'junk'
      $rp2 = New-RulePack -Name 'Pack2' -Author 'B'
      $rp2 = Add-RuleToRulePack -RulePack $rp2 -Pattern 'b' -Category 'region'
      $rp2 = Add-RuleToRulePack -RulePack $rp2 -Pattern 'a' -Category 'junk'
      $merged = Merge-RulePacks -RulePacks @($rp1, $rp2) -MergedName 'Merged'
      $merged.Rules.Count | Should -Be 2
    }
  }

  Describe 'Export/Import-RulePackJson' {
    It 'roundtripped korrekt' {
      $rp = New-RulePack -Name 'RT' -Author 'Z' -Description 'Roundtrip test'
      $rp = Add-RuleToRulePack -RulePack $rp -Pattern '^Demo' -Category 'junk'
      $json = Export-RulePackJson -RulePack $rp
      $imported = Import-RulePackJson -JsonString $json
      $imported.Name | Should -Be 'RT'
      $imported.Rules.Count | Should -Be 1
    }
  }

  Describe 'Get-RulePackStatistics' {
    It 'berechnet Statistiken' {
      $rp = New-RulePack -Name 'Stats' -Author 'A'
      $rp = Add-RuleToRulePack -RulePack $rp -Pattern 'a' -Category 'junk'
      $rp = Add-RuleToRulePack -RulePack $rp -Pattern 'b' -Category 'region'
      $rp = Add-RuleToRulePack -RulePack $rp -Pattern 'c' -Category 'junk'
      $stats = Get-RulePackStatistics -RulePack $rp
      $stats.TotalRules | Should -Be 3
      $stats.ByCategory.junk | Should -Be 2
      $stats.ByCategory.region | Should -Be 1
    }
  }
}
