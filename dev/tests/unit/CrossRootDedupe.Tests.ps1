BeforeAll {
  . "$PSScriptRoot/../../modules/CrossRootDedupe.ps1"
}

Describe 'MF-02: CrossRootDedupe' {
  Describe 'Find-CrossRootDuplicates' {
    It 'findet Duplikate ueber Roots hinweg' {
      $items = @(
        @{ Path = 'C:\Root1\game.zip'; Root = 'C:\Root1'; Hash = 'AAAA'; Size = 1024; Format = 'ZIP' }
        @{ Path = 'D:\Root2\game.zip'; Root = 'D:\Root2'; Hash = 'AAAA'; Size = 1024; Format = 'ZIP' }
        @{ Path = 'C:\Root1\other.zip'; Root = 'C:\Root1'; Hash = 'BBBB'; Size = 2048; Format = 'ZIP' }
      )
      $result = Find-CrossRootDuplicates -FileIndex $items
      $result.Count | Should -Be 1
      $result[0].Hash | Should -Be 'AAAA'
      $result[0].Files.Count | Should -Be 2
    }

    It 'ignoriert gleichen Hash innerhalb desselben Roots' {
      $items = @(
        @{ Path = 'C:\Root1\game1.zip'; Root = 'C:\Root1'; Hash = 'AAAA'; Size = 1024; Format = 'ZIP' }
        @{ Path = 'C:\Root1\game2.zip'; Root = 'C:\Root1'; Hash = 'AAAA'; Size = 1024; Format = 'ZIP' }
      )
      $result = Find-CrossRootDuplicates -FileIndex $items
      $result | Should -HaveCount 0
    }

    It 'gibt leeres Ergebnis bei leerer Liste' {
      $result = Find-CrossRootDuplicates -FileIndex @()
      $result | Should -HaveCount 0
    }
  }

  Describe 'Get-CrossRootMergeAdvice' {
    It 'empfiehlt hoheres Format als Keeper' {
      $dupeGroup = @{
        Hash  = 'AAAA'
        Files = @(
          @{ Path = 'C:\Root1\game.zip'; Format = 'ZIP'; FormatScore = 500 }
          @{ Path = 'D:\Root2\game.chd'; Format = 'CHD'; FormatScore = 850 }
        )
      }
      $result = Get-CrossRootMergeAdvice -DuplicateGroup $dupeGroup
      $result.Keep.Path | Should -Be 'D:\Root2\game.chd'
      $result.Keep.Format | Should -Be 'CHD'
    }
  }
}
