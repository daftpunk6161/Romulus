BeforeAll {
  $root = $PSScriptRoot
  while ($root -and -not (Test-Path (Join-Path $root 'simple_sort.ps1'))) {
    $root = Split-Path -Parent $root
  }
  . (Join-Path $root 'dev\modules\KeyboardShortcuts.ps1')
}

Describe 'KeyboardShortcuts (QW-06)' {

  Context 'Get-RomCleanupShortcuts' {

    It 'gibt nicht-leeres Array zurueck' {
      $shortcuts = Get-RomCleanupShortcuts
      $shortcuts | Should -Not -BeNullOrEmpty
      $shortcuts.Count | Should -BeGreaterThan 5
    }

    It 'jeder Shortcut hat Key und CommandName' {
      $shortcuts = Get-RomCleanupShortcuts
      foreach ($sc in $shortcuts) {
        $sc.Key | Should -Not -BeNullOrEmpty
        $sc.CommandName | Should -Not -BeNullOrEmpty
        $sc.Label | Should -Not -BeNullOrEmpty
        $sc.Category | Should -Not -BeNullOrEmpty
      }
    }

    It 'enthaelt Ctrl+R fuer Run' {
      $shortcuts = Get-RomCleanupShortcuts
      $runSc = $shortcuts | Where-Object { $_.Key -eq 'R' -and $_.Modifiers -eq 'Ctrl' }
      $runSc | Should -Not -BeNullOrEmpty
    }

    It 'enthaelt Escape fuer Cancel' {
      $shortcuts = Get-RomCleanupShortcuts
      $cancelSc = $shortcuts | Where-Object { $_.Key -eq 'Escape' }
      $cancelSc | Should -Not -BeNullOrEmpty
    }
  }

  Context 'Format-ShortcutLabel' {

    It 'formatiert Ctrl+R korrekt' {
      $lbl = Format-ShortcutLabel -Shortcut @{ Key = 'R'; Modifiers = 'Ctrl' }
      $lbl | Should -Be 'Ctrl+R'
    }

    It 'formatiert Ctrl+Shift+D korrekt' {
      $lbl = Format-ShortcutLabel -Shortcut @{ Key = 'D'; Modifiers = 'Ctrl+Shift' }
      $lbl | Should -Be 'Ctrl+Shift+D'
    }

    It 'formatiert Taste ohne Modifiers korrekt' {
      $lbl = Format-ShortcutLabel -Shortcut @{ Key = 'F5'; Modifiers = '' }
      $lbl | Should -Be 'F5'
    }
  }

  Context 'Get-ShortcutOverlayData' {

    It 'gruppiert nach Kategorie' {
      $overlay = Get-ShortcutOverlayData
      $overlay | Should -BeOfType [hashtable]
      $overlay.Keys.Count | Should -BeGreaterThan 0
    }

    It 'jede Kategorie enthaelt Shortcut-Objekte' {
      $overlay = Get-ShortcutOverlayData
      foreach ($cat in $overlay.Keys) {
        $overlay[$cat].Count | Should -BeGreaterThan 0
        $overlay[$cat][0].Keys | Should -Contain 'Shortcut'
        $overlay[$cat][0].Keys | Should -Contain 'Label'
      }
    }
  }
}
