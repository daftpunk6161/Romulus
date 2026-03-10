# ================================================================
#  KEYBOARD SHORTCUTS – Shortcut-Definition und Registry (QW-06)
#  Dependencies: (standalone, WPF-Integration separat)
# ================================================================

function Get-RomCleanupShortcuts {
  <#
  .SYNOPSIS
    Gibt die Standard-Tastenkuerzel fuer RomCleanup zurueck.
    Kann fuer Shortcut-Overlay, XAML InputBindings und Dokumentation genutzt werden.
  #>

  return @(
    @{ Key = 'R'; Modifiers = 'Ctrl';       CommandName = 'RunCommand';          Label = 'Run starten';           Category = 'Run' }
    @{ Key = 'D'; Modifiers = 'Ctrl+Shift'; CommandName = 'DryRunCommand';       Label = 'DryRun starten';        Category = 'Run' }
    @{ Key = 'Z'; Modifiers = 'Ctrl';       CommandName = 'UndoCommand';         Label = 'Letzte Aktion rueckgaengig'; Category = 'Edit' }
    @{ Key = 'F5'; Modifiers = '';          CommandName = 'RefreshCommand';      Label = 'Ansicht aktualisieren'; Category = 'Navigation' }
    @{ Key = 'Escape'; Modifiers = '';      CommandName = 'CancelCommand';       Label = 'Abbrechen';             Category = 'Run' }
    @{ Key = 'F1'; Modifiers = '';          CommandName = 'ShowShortcutsCommand'; Label = 'Tastenkuerzel anzeigen'; Category = 'Help' }
    @{ Key = 'P'; Modifiers = 'Ctrl+Shift'; CommandName = 'CommandPaletteCommand'; Label = 'Befehlspalette';       Category = 'Navigation' }
    @{ Key = 'S'; Modifiers = 'Ctrl';       CommandName = 'SaveSettingsCommand'; Label = 'Settings speichern';    Category = 'Settings' }
    @{ Key = 'E'; Modifiers = 'Ctrl';       CommandName = 'ExportCommand';       Label = 'Report exportieren';    Category = 'Export' }
  )
}

function Format-ShortcutLabel {
  <#
  .SYNOPSIS
    Formatiert einen Shortcut als lesbaren String (z.B. "Ctrl+Shift+D").
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Shortcut
  )

  $parts = @()
  if ($Shortcut.Modifiers) {
    $parts += $Shortcut.Modifiers.Split('+')
  }
  $parts += $Shortcut.Key

  return ($parts -join '+')
}

function Get-ShortcutOverlayData {
  <#
  .SYNOPSIS
    Gibt Shortcut-Daten gruppiert nach Kategorie fuer die Overlay-Anzeige zurueck.
  #>

  $shortcuts = Get-RomCleanupShortcuts
  $grouped = @{}

  foreach ($sc in $shortcuts) {
    $cat = $sc.Category
    if (-not $grouped.ContainsKey($cat)) {
      $grouped[$cat] = [System.Collections.Generic.List[hashtable]]::new()
    }
    [void]$grouped[$cat].Add(@{
      Shortcut = Format-ShortcutLabel -Shortcut $sc
      Label    = $sc.Label
      Command  = $sc.CommandName
    })
  }

  return $grouped
}
