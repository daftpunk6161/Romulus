BeforeAll {
  . "$PSScriptRoot/../../modules/FilterBuilder.ps1"
}

Describe 'MF-17: FilterBuilder' {
  Describe 'New-FilterCondition' {
    It 'erstellt eine Filter-Bedingung' {
      $cond = New-FilterCondition -Field 'Console' -Operator 'eq' -Value 'SNES'
      $cond.Field | Should -Be 'Console'
      $cond.Operator | Should -Be 'eq'
      $cond.Value | Should -Be 'SNES'
    }

    It 'akzeptiert leeren Value' {
      $cond = New-FilterCondition -Field 'Region' -Operator 'eq' -Value ''
      $cond.Value | Should -Be ''
    }
  }

  Describe 'Test-FilterCondition' {
    It 'prueft eq-Operator korrekt' {
      $cond = @{ Field = 'Console'; Operator = 'eq'; Value = 'SNES' }
      Test-FilterCondition -Condition $cond -ItemValue 'SNES' | Should -BeTrue
      Test-FilterCondition -Condition $cond -ItemValue 'NES' | Should -BeFalse
    }

    It 'prueft gt-Operator korrekt' {
      $cond = @{ Field = 'SizeMB'; Operator = 'gt'; Value = '100' }
      Test-FilterCondition -Condition $cond -ItemValue 200 | Should -BeTrue
      Test-FilterCondition -Condition $cond -ItemValue 50 | Should -BeFalse
    }

    It 'prueft contains-Operator korrekt' {
      $cond = @{ Field = 'FileName'; Operator = 'contains'; Value = 'Mario' }
      Test-FilterCondition -Condition $cond -ItemValue 'Super Mario World.zip' | Should -BeTrue
      Test-FilterCondition -Condition $cond -ItemValue 'Zelda.zip' | Should -BeFalse
    }

    It 'prueft regex-Operator korrekt' {
      $cond = @{ Field = 'FileName'; Operator = 'regex'; Value = '^\d+' }
      Test-FilterCondition -Condition $cond -ItemValue '007 - GoldenEye.zip' | Should -BeTrue
      Test-FilterCondition -Condition $cond -ItemValue 'Zelda.zip' | Should -BeFalse
    }
  }

  Describe 'New-FilterQuery und Invoke-FilterQuery' {
    It 'filtert Items mit AND-Logik' {
      $c1 = New-FilterCondition -Field 'Console' -Operator 'eq' -Value 'SNES'
      $c2 = New-FilterCondition -Field 'Region' -Operator 'eq' -Value 'EU'
      $query = New-FilterQuery -Conditions @($c1, $c2) -Logic 'AND'

      $items = @(
        @{ Console = 'SNES'; Region = 'EU'; Name = 'A' }
        @{ Console = 'SNES'; Region = 'US'; Name = 'B' }
        @{ Console = 'NES'; Region = 'EU'; Name = 'C' }
      )
      $result = Invoke-FilterQuery -Items $items -Query $query
      $result.Count | Should -Be 1
      $result[0].Name | Should -Be 'A'
    }

    It 'filtert Items mit OR-Logik' {
      $c1 = New-FilterCondition -Field 'Console' -Operator 'eq' -Value 'SNES'
      $c2 = New-FilterCondition -Field 'Console' -Operator 'eq' -Value 'NES'
      $query = New-FilterQuery -Conditions @($c1, $c2) -Logic 'OR'

      $items = @(
        @{ Console = 'SNES'; Name = 'A' }
        @{ Console = 'NES'; Name = 'B' }
        @{ Console = 'GBA'; Name = 'C' }
      )
      $result = Invoke-FilterQuery -Items $items -Query $query
      $result.Count | Should -Be 2
    }

    It 'behandelt leere Items' {
      $c1 = New-FilterCondition -Field 'Console' -Operator 'eq' -Value 'SNES'
      $query = New-FilterQuery -Conditions @($c1)
      $result = Invoke-FilterQuery -Items @() -Query $query
      $result.Count | Should -Be 0
    }
  }

  Describe 'ConvertTo-FilterQueryString' {
    It 'erzeugt lesbaren Query-String' {
      $c1 = New-FilterCondition -Field 'Console' -Operator 'eq' -Value 'SNES'
      $c2 = New-FilterCondition -Field 'Region' -Operator 'neq' -Value 'JP'
      $query = New-FilterQuery -Conditions @($c1, $c2) -Logic 'AND'
      $str = ConvertTo-FilterQueryString -Query $query
      $str | Should -BeLike "*Console*=*'SNES'*"
      $str | Should -BeLike "*Region*!=*'JP'*"
      $str | Should -BeLike '*AND*'
    }
  }

  Describe 'Get-FilterableFields' {
    It 'gibt Standardfelder zurueck' {
      $fields = Get-FilterableFields
      $fields.Count | Should -BeGreaterOrEqual 5
      $fields.Name | Should -Contain 'Console'
      $fields.Name | Should -Contain 'Region'
    }
  }
}
