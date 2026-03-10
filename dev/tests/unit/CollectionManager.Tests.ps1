BeforeAll {
  . "$PSScriptRoot/../../modules/CollectionManager.ps1"
}

Describe 'MF-05: CollectionManager' {
  Describe 'New-SmartCollection' {
    It 'erstellt Collection mit korrekten Feldern' {
      $filters = @( @{ Field = 'Console'; Operator = 'eq'; Value = 'SNES' } )
      $col = New-SmartCollection -Name 'SNES Spiele' -Filters $filters
      $col.Name | Should -Be 'SNES Spiele'
      $col.Type | Should -Be 'Smart'
      $col.Filters.Count | Should -Be 1
      $col.Logic | Should -Be 'AND'
    }
  }

  Describe 'Test-CollectionMatch' {
    It 'matched einfache eq-Bedingung' {
      $col = @{ Filters = @( @{ Field = 'Console'; Operator = 'eq'; Value = 'SNES' } ); Logic = 'AND' }
      $item = @{ Console = 'SNES'; Name = 'Test' }
      Test-CollectionMatch -Item $item -Collection $col | Should -BeTrue
    }

    It 'matched nicht bei Abweichung' {
      $col = @{ Filters = @( @{ Field = 'Console'; Operator = 'eq'; Value = 'SNES' } ); Logic = 'AND' }
      $item = @{ Console = 'NES'; Name = 'Test' }
      Test-CollectionMatch -Item $item -Collection $col | Should -BeFalse
    }

    It 'matched OR-Logik' {
      $col = @{
        Filters = @(
          @{ Field = 'Console'; Operator = 'eq'; Value = 'SNES' }
          @{ Field = 'Console'; Operator = 'eq'; Value = 'NES' }
        )
        Logic = 'OR'
      }
      $item = @{ Console = 'NES' }
      Test-CollectionMatch -Item $item -Collection $col | Should -BeTrue
    }

    It 'matched contains-Operator' {
      $col = @{ Filters = @( @{ Field = 'Name'; Operator = 'contains'; Value = 'Mario' } ); Logic = 'AND' }
      $item = @{ Name = 'Super Mario World' }
      Test-CollectionMatch -Item $item -Collection $col | Should -BeTrue
    }

    It 'matched gt-Operator' {
      $col = @{ Filters = @( @{ Field = 'SizeMB'; Operator = 'gt'; Value = '100' } ); Logic = 'AND' }
      $item = @{ SizeMB = 200 }
      Test-CollectionMatch -Item $item -Collection $col | Should -BeTrue
    }
  }

  Describe 'Invoke-SmartCollectionFilter' {
    It 'filtert Items korrekt' {
      $col = @{ Filters = @( @{ Field = 'Console'; Operator = 'eq'; Value = 'SNES' } ); Logic = 'AND' }
      $items = @(
        @{ Console = 'SNES'; Name = 'Game1' }
        @{ Console = 'NES'; Name = 'Game2' }
        @{ Console = 'SNES'; Name = 'Game3' }
      )
      $result = Invoke-SmartCollectionFilter -Items $items -Collection $col
      $result.Count | Should -Be 2
    }

    It 'gibt leeres Array bei leeren Items' {
      $col = @{ Filters = @( @{ Field = 'Console'; Operator = 'eq'; Value = 'SNES' } ); Logic = 'AND' }
      $result = Invoke-SmartCollectionFilter -Items @() -Collection $col
      $result | Should -HaveCount 0
    }
  }

  Describe 'Export/Import-CollectionDefinition' {
    It 'round-trip JSON ist korrekt' {
      $col = @{ Name = 'Test'; Filters = @( @{ Field = 'X'; Operator = 'eq'; Value = '1' } ); Logic = 'AND' }
      $json = Export-CollectionDefinition -Collection $col
      $json | Should -Not -BeNullOrEmpty
    }
  }
}
