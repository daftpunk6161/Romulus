# ================================================================
#  WpfSlice.AdvancedFeatures.ps1  –  Slice 6: Plugin, rollback, watch-mode & advanced handlers
#  Extracted from WpfEventHandlers.ps1 (TD-001 Slice 6/6)
# ================================================================

# ── Watch/Rollback variable initializations (moved from WpfEventHandlers.ps1) ──
if (-not (Get-Variable -Name WpfWatchRegistry -Scope Script -ErrorAction SilentlyContinue)) {
  $script:WpfWatchRegistry = [hashtable]::new([StringComparer]::OrdinalIgnoreCase)
}
if (-not (Get-Variable -Name WpfWatchPendingRun -Scope Script -ErrorAction SilentlyContinue)) {
  $script:WpfWatchPendingRun = $false
}
if (-not (Get-Variable -Name WpfWatchLastEventUtc -Scope Script -ErrorAction SilentlyContinue)) {
  $script:WpfWatchLastEventUtc = [datetime]::MinValue
}
if (-not (Get-Variable -Name WpfRollbackUndoStack -Scope Script -ErrorAction SilentlyContinue)) {
  $script:WpfRollbackUndoStack = New-Object System.Collections.Generic.List[object]
}
if (-not (Get-Variable -Name WpfRollbackRedoStack -Scope Script -ErrorAction SilentlyContinue)) {
  $script:WpfRollbackRedoStack = New-Object System.Collections.Generic.List[object]
}

function Show-WpfTextInputDialog {
  param(
    [Parameter(Mandatory)][System.Windows.Window]$Owner,
    [Parameter(Mandatory)][string]$Title,
    [Parameter(Mandatory)][string]$Message,
    [string]$DefaultValue = ''
  )

  if (Get-Command Read-WpfPromptText -ErrorAction SilentlyContinue) {
    return (Read-WpfPromptText -Owner $Owner -Title $Title -Message $Message -DefaultValue $DefaultValue)
  }

  $dialog = New-WpfStyledDialog -Owner $Owner -Title $Title -ResizeMode ([System.Windows.ResizeMode]::NoResize) -SizeToContent ([System.Windows.SizeToContent]::WidthAndHeight)
  $dialog.MinWidth = 460
  $surfaceBrush = $Owner.TryFindResource('BrushSurface')
  if (-not ($surfaceBrush -is [System.Windows.Media.SolidColorBrush])) { $surfaceBrush = [System.Windows.Media.SolidColorBrush]([System.Windows.Media.ColorConverter]::ConvertFromString('#1A1A3A')) }
  $dialog.Background = $surfaceBrush

  $root = New-Object System.Windows.Controls.Grid
  $root.Margin = [System.Windows.Thickness]::new(16,16,16,16)
  [void]$root.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition -Property @{ Height = [System.Windows.GridLength]::Auto }))
  [void]$root.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition -Property @{ Height = [System.Windows.GridLength]::Auto }))
  [void]$root.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition -Property @{ Height = [System.Windows.GridLength]::Auto }))

  $label = New-Object System.Windows.Controls.TextBlock
  $label.Text = $Message
  $label.TextWrapping = [System.Windows.TextWrapping]::Wrap
  $label.Margin = [System.Windows.Thickness]::new(0,0,0,10)
  [System.Windows.Controls.Grid]::SetRow($label, 0)
  [void]$root.Children.Add($label)

  $tb = New-Object System.Windows.Controls.TextBox
  $tb.Text = [string]$DefaultValue
  $tb.MinWidth = 360
  $tb.Margin = [System.Windows.Thickness]::new(0,0,0,14)
  [System.Windows.Controls.Grid]::SetRow($tb, 1)
  [void]$root.Children.Add($tb)

  $buttons = New-Object System.Windows.Controls.StackPanel
  $buttons.Orientation = [System.Windows.Controls.Orientation]::Horizontal
  $buttons.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Right

  $btnCancel = New-Object System.Windows.Controls.Button
  $btnCancel.Content = 'Abbrechen'
  $btnCancel.Width = 110
  $btnCancel.Margin = [System.Windows.Thickness]::new(0,0,8,0)

  $btnOk = New-Object System.Windows.Controls.Button
  $btnOk.Content = 'OK'
  $btnOk.Width = 110

  $btnCancel.add_Click({
    $dialog.DialogResult = $false
    $dialog.Close()
  })
  $btnOk.add_Click({
    $dialog.DialogResult = $true
    $dialog.Close()
  })

  [void]$buttons.Children.Add($btnCancel)
  [void]$buttons.Children.Add($btnOk)
  [System.Windows.Controls.Grid]::SetRow($buttons, 2)
  [void]$root.Children.Add($buttons)

  $dialog.Content = $root
  $tb.Focus() | Out-Null
  $tb.SelectAll()
  $shown = $dialog.ShowDialog()

  if ($shown -eq $true) {
    return [string]$tb.Text
  }
  return $null
}

function Get-WpfRollbackRoots {
  <# Collects restore-roots and current-roots from the UI context (DUP-106). #>
  param([Parameter(Mandatory)][hashtable]$Ctx)

  $restoreRoots = New-Object System.Collections.Generic.List[string]
  foreach ($item in @($Ctx['listRoots'].Items)) {
    $s = if ($item -is [string]) { [string]$item } else { [string]$item.Content }
    if (-not [string]::IsNullOrWhiteSpace($s)) {
      [void]$restoreRoots.Add($s.Trim())
    }
  }

  $currentRoots = New-Object System.Collections.Generic.List[string]
  foreach ($rootPath in @($restoreRoots)) { [void]$currentRoots.Add([string]$rootPath) }
  if ($Ctx.ContainsKey('txtTrash') -and -not [string]::IsNullOrWhiteSpace([string]$Ctx['txtTrash'].Text)) {
    [void]$currentRoots.Add(([string]$Ctx['txtTrash'].Text).Trim())
  }

  return [pscustomobject]@{
    RestoreRoots = $restoreRoots
    CurrentRoots = $currentRoots
  }
}

function Invoke-WpfRollbackCore {
  <# Shared rollback execution + undo/redo stack update (DUP-106).
     Called by both Invoke-WpfRollbackWizard and Invoke-WpfQuickRollback. #>
  param(
    [Parameter(Mandatory)][string]$AuditCsvPath,
    [Parameter(Mandatory)][System.Collections.Generic.List[string]]$AllowedRestoreRoots,
    [Parameter(Mandatory)][System.Collections.Generic.List[string]]$AllowedCurrentRoots,
    [Parameter(Mandatory)][hashtable]$Ctx
  )

  if (-not (Get-Command Invoke-RunRollbackService -ErrorAction SilentlyContinue)) {
    throw 'Rollback-Service nicht verfügbar (Invoke-RunRollbackService fehlt).'
  }

  $rollback = Invoke-RunRollbackService -Parameters @{
    AuditCsvPath = $AuditCsvPath
    AllowedRestoreRoots = @($AllowedRestoreRoots)
    AllowedCurrentRoots = @($AllowedCurrentRoots)
    Log = { param([string]$m) Add-WpfLogLine -Ctx $Ctx -Line $m -Level 'INFO' }
  }

  try {
    $inverseCsv = New-WpfInverseRollbackCsv -AuditCsvPath $AuditCsvPath
    [void]$script:WpfRollbackUndoStack.Add([pscustomobject]@{
      UndoCsvPath = $inverseCsv
      RedoCsvPath = $AuditCsvPath
      AllowedRestoreRoots = @($AllowedRestoreRoots)
      AllowedCurrentRoots = @($AllowedCurrentRoots)
    })
    $script:WpfRollbackRedoStack.Clear()
    if ($Ctx.ContainsKey('btnRollbackUndo') -and $Ctx['btnRollbackUndo']) { $Ctx['btnRollbackUndo'].IsEnabled = $true }
    if ($Ctx.ContainsKey('btnRollbackRedo') -and $Ctx['btnRollbackRedo']) { $Ctx['btnRollbackRedo'].IsEnabled = $false }
  } catch {
    Add-WpfLogLine -Ctx $Ctx -Line ("Undo/Redo-Stack konnte nicht aktualisiert werden: {0}" -f $_.Exception.Message) -Level 'WARN'
  }

  return $rollback
}

function Invoke-WpfRollbackWizard {
  param(
    [Parameter(Mandatory)][System.Windows.Window]$Window,
    [Parameter(Mandatory)][hashtable]$Ctx
  )

  $initialAuditDir = if ($Ctx.ContainsKey('txtAuditRoot') -and -not [string]::IsNullOrWhiteSpace([string]$Ctx['txtAuditRoot'].Text) -and (Test-Path -LiteralPath $Ctx['txtAuditRoot'].Text -PathType Container)) {
    [string]$Ctx['txtAuditRoot'].Text
  } else {
    Join-Path (Get-Location).Path 'audit-logs'
  }

  $dlg = New-Object Microsoft.Win32.OpenFileDialog
  $dlg.Title = 'Audit-CSV für Rollback auswählen'
  $dlg.Filter = 'Audit CSV (rom-move-audit-*.csv)|rom-move-audit-*.csv|CSV (*.csv)|*.csv|All files (*.*)|*.*'
  if (Test-Path -LiteralPath $initialAuditDir -PathType Container) {
    $dlg.InitialDirectory = $initialAuditDir
  }
  $selectedAudit = $null
  if ($dlg.ShowDialog($Window) -eq $true) {
    $selectedAudit = [string]$dlg.FileName
  }
  if ([string]::IsNullOrWhiteSpace($selectedAudit)) { return }

  $roots = Get-WpfRollbackRoots -Ctx $Ctx
  $rollbackAllowedRestoreRoots = $roots.RestoreRoots
  $rollbackAllowedCurrentRoots = $roots.CurrentRoots

  if ($rollbackAllowedRestoreRoots.Count -eq 0) {
    [System.Windows.MessageBox]::Show(
      'Rollback benötigt mindestens einen konfigurierten ROM-Root als Sicherheitsgrenze.',
      'Rollback',
      [System.Windows.MessageBoxButton]::OK,
      [System.Windows.MessageBoxImage]::Warning) | Out-Null
    return
  }

  $step1 = [System.Windows.MessageBox]::Show(
    ("Rollback Wizard (Schritt 1/3)`n`nAudit: {0}`nRestore-Roots: {1}`nCurrent-Roots: {2}`n`nWeiter zur Vorschau?" -f $selectedAudit, $rollbackAllowedRestoreRoots.Count, @($rollbackAllowedCurrentRoots).Count),
    'Rollback Wizard',
    [System.Windows.MessageBoxButton]::YesNo,
    [System.Windows.MessageBoxImage]::Question)
  if ($step1 -ne [System.Windows.MessageBoxResult]::Yes) { return }

  $previewRows = @()
  $previewEligible = 0
  $previewUnsafe = 0
  $previewParseErrors = 0
  try {
    $previewRows = @(Import-Csv -LiteralPath $selectedAudit -Encoding UTF8)
    foreach ($row in $previewRows) {
      try {
        $action = [string]$row.Action
        $source = [string]$row.Source
        $dest = [string]$row.Dest
        if ([string]::IsNullOrWhiteSpace($action) -or [string]::IsNullOrWhiteSpace($source) -or [string]::IsNullOrWhiteSpace($dest)) { continue }
        $actionNorm = $action.Trim().ToUpperInvariant()
        if ($actionNorm -notin @('MOVE','JUNK','BIOS-MOVE')) { continue }

        $restoreSafe = $false
        foreach ($rootCandidate in @($rollbackAllowedRestoreRoots)) {
          if (Test-PathWithinRoot -Path $source -Root $rootCandidate -DisallowReparsePoints) { $restoreSafe = $true; break }
        }
        $currentSafe = $false
        foreach ($rootCandidate in @($rollbackAllowedCurrentRoots)) {
          if (Test-PathWithinRoot -Path $dest -Root $rootCandidate -DisallowReparsePoints) { $currentSafe = $true; break }
        }

        if ($restoreSafe -and $currentSafe) {
          $previewEligible++
        } else {
          $previewUnsafe++
        }
      } catch {
        $previewParseErrors++
      }
    }
  } catch {
    [System.Windows.MessageBox]::Show(
      ("Rollback-Vorschau konnte Audit nicht lesen:`n{0}" -f $_.Exception.Message),
      'Rollback Wizard',
      [System.Windows.MessageBoxButton]::OK,
      [System.Windows.MessageBoxImage]::Error) | Out-Null
    return
  }

  $step2 = [System.Windows.MessageBox]::Show(
    ("Rollback Wizard (Schritt 2/3) - Vorschau`n`nAudit-Zeilen: {0}`nVoraussichtlich zulässige Restores: {1}`nPotenziell unsicher (wird geskippt): {2}`nParse-Fehler: {3}`n`nWeiter zur finalen Bestätigung?" -f $previewRows.Count, $previewEligible, $previewUnsafe, $previewParseErrors),
    'Rollback Wizard',
    [System.Windows.MessageBoxButton]::YesNo,
    [System.Windows.MessageBoxImage]::Information)
  if ($step2 -ne [System.Windows.MessageBoxResult]::Yes) { return }

  $confirmText = Read-WpfPromptText -Owner $Window -Title 'Rollback Wizard' -Message 'Rollback starten: bitte ROLLBACK eingeben, um Schritt 3/3 zu bestätigen.' -DefaultValue ''
  if ([string]::IsNullOrWhiteSpace($confirmText) -or $confirmText.Trim().ToUpperInvariant() -ne 'ROLLBACK') {
    [System.Windows.MessageBox]::Show(
      'Rollback abgebrochen: Bestätigungstext nicht korrekt.',
      'Rollback Wizard',
      [System.Windows.MessageBoxButton]::OK,
      [System.Windows.MessageBoxImage]::Warning) | Out-Null
    return
  }

  Add-WpfLogLine -Ctx $Ctx -Line ("=== Rollback aus Audit: {0} ===" -f $selectedAudit) -Level 'INFO'
  try {
    $rollback = Invoke-WpfRollbackCore -AuditCsvPath $selectedAudit -AllowedRestoreRoots $rollbackAllowedRestoreRoots -AllowedCurrentRoots $rollbackAllowedCurrentRoots -Ctx $Ctx
    Add-WpfLogLine -Ctx $Ctx -Line ("Rollback Ergebnis: zurück={0}, skip-unsafe={1}, skip-fehlt={2}, skip-existiert={3}, fehler={4}" -f $rollback.RolledBack, $rollback.SkippedUnsafe, $rollback.SkippedMissingDest, $rollback.SkippedCollision, $rollback.Failed) -Level 'INFO'

    [System.Windows.MessageBox]::Show(
      ("Rollback abgeschlossen.`n`nZurückverschoben: {0}`nSkip (unsicher): {1}`nSkip (Quelle fehlt): {2}`nSkip (Ziel existiert): {3}`nFehler: {4}" -f $rollback.RolledBack, $rollback.SkippedUnsafe, $rollback.SkippedMissingDest, $rollback.SkippedCollision, $rollback.Failed),
      'Rollback',
      [System.Windows.MessageBoxButton]::OK,
      [System.Windows.MessageBoxImage]::Information) | Out-Null

    if ($rollback.Failed -gt 0) {
      Set-QuickTone -Tone 'error' -Phase 'Rollback mit Fehlern'
    } else {
      Set-QuickTone -Tone 'success' -Phase 'Rollback abgeschlossen'
    }
  } catch {
    Add-WpfLogLine -Ctx $Ctx -Line ("Rollback fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
    [System.Windows.MessageBox]::Show(
      ("Rollback fehlgeschlagen:`n{0}" -f $_.Exception.Message),
      'Rollback',
      [System.Windows.MessageBoxButton]::OK,
      [System.Windows.MessageBoxImage]::Error) | Out-Null
  }
}

function Invoke-WpfCollectionDiffReport {
  param([Parameter(Mandatory)][hashtable]$Ctx)

  $reportsDir = Join-Path (Get-Location).Path 'reports'
  if (-not (Test-Path -LiteralPath $reportsDir -PathType Container)) {
    throw 'reports-Verzeichnis nicht gefunden.'
  }

  $newer = $null
  $older = $null

  try {
    $dialog = New-Object Microsoft.Win32.OpenFileDialog
    $dialog.Title = 'Collection-Diff: Zwei Reports auswählen (neu + alt)'
    $dialog.Filter = 'JSON (*.json)|*.json|Alle Dateien (*.*)|*.*'
    $dialog.Multiselect = $true
    $dialog.InitialDirectory = $reportsDir
    $picked = $dialog.ShowDialog()
    if ($picked -eq $true -and $dialog.FileNames.Count -ge 2) {
      $chosen = @($dialog.FileNames | ForEach-Object { Get-Item -LiteralPath $_ -ErrorAction SilentlyContinue } | Where-Object { $_ })
      if ($chosen.Count -ge 2) {
        $ordered = @($chosen | Sort-Object LastWriteTimeUtc -Descending)
        $newer = $ordered[0]
        $older = $ordered[1]
      }
    }
  } catch { }

  if (-not $newer -or -not $older) {
    $candidates = @(Get-ChildItem -LiteralPath $reportsDir -Filter 'test-pipeline-*.json' -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc -Descending)
    if ($candidates.Count -lt 2) {
      throw 'Nicht genug Test-Pipeline-Reports für Diff vorhanden.'
    }
    $newer = $candidates[0]
    $older = $candidates[1]
  }

  $newObj = Get-Content -LiteralPath $newer.FullName -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
  $oldObj = Get-Content -LiteralPath $older.FullName -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop

  $outPath = Join-Path $reportsDir ('collection-diff-{0}.md' -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
  $lines = New-Object System.Collections.Generic.List[string]
  [void]$lines.Add('# Collection Diff Report')
  [void]$lines.Add('')
  [void]$lines.Add(('Neu: {0}' -f $newer.Name))
  [void]$lines.Add(('Alt: {0}' -f $older.Name))
  [void]$lines.Add('')
  [void]$lines.Add(('Neu geändert: {0:o}' -f $newer.LastWriteTimeUtc))
  [void]$lines.Add(('Alt geändert: {0:o}' -f $older.LastWriteTimeUtc))
  [void]$lines.Add('')

  foreach ($key in @('stage','status','summary')) {
    $newValue = if ($newObj.PSObject.Properties.Name -contains $key) { [string]($newObj.$key | ConvertTo-Json -Compress -Depth 6) } else { '<n/a>' }
    $oldValue = if ($oldObj.PSObject.Properties.Name -contains $key) { [string]($oldObj.$key | ConvertTo-Json -Compress -Depth 6) } else { '<n/a>' }
    [void]$lines.Add(('## {0}' -f $key))
    [void]$lines.Add(('New: `{0}`' -f $newValue))
    [void]$lines.Add(('Old: `{0}`' -f $oldValue))
    [void]$lines.Add('')
  }

  $lines | Set-Content -LiteralPath $outPath -Encoding UTF8 -Force
  Add-WpfLogLine -Ctx $Ctx -Line ("Collection-Diff erstellt: {0}" -f $outPath) -Level 'INFO'
  Set-WpfAdvancedStatus -Ctx $Ctx -Text 'Collection-Diff erstellt'
  return $outPath
}

function Invoke-WpfPluginManager {
  param(
    [Parameter(Mandatory)][System.Windows.Window]$Window,
    [Parameter(Mandatory)][hashtable]$Ctx
  )

  $pluginRoots = @(
    Join-Path (Get-Location).Path 'plugins\consoles',
    Join-Path (Get-Location).Path 'plugins\operations',
    Join-Path (Get-Location).Path 'plugins\reports'
  )

  $items = New-Object System.Collections.Generic.List[object]
  foreach ($root in $pluginRoots) {
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
    foreach ($f in @(Get-ChildItem -LiteralPath $root -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -like '*.ps1' -or $_.Name -like '*.ps1.disabled' })) {
      $manifestVersion = '0.0.0'
      $manifestId = [System.IO.Path]::GetFileNameWithoutExtension([string]$f.Name)
      $depCount = 0
      $manifestPath = [System.IO.Path]::ChangeExtension([string]$f.FullName, '.manifest.json')
      if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
        try {
          $manifestRaw = Get-Content -LiteralPath $manifestPath -Raw -ErrorAction Stop
          if (-not [string]::IsNullOrWhiteSpace($manifestRaw)) {
            $manifestData = $manifestRaw | ConvertFrom-Json -ErrorAction Stop
            if ($manifestData.id -and -not [string]::IsNullOrWhiteSpace([string]$manifestData.id)) { $manifestId = [string]$manifestData.id }
            if ($manifestData.version -and -not [string]::IsNullOrWhiteSpace([string]$manifestData.version)) { $manifestVersion = [string]$manifestData.version }
            if ($manifestData.dependencies) { $depCount = @($manifestData.dependencies).Count }
          }
        } catch { }
      }

      [void]$items.Add([pscustomobject]@{
        Root = $root
        Name = $f.Name
        Path = $f.FullName
        PluginId = $manifestId
        Version = $manifestVersion
        DependencyCount = $depCount
      })
    }
  }

  if ($items.Count -eq 0) {
    [System.Windows.MessageBox]::Show('Keine Plugins gefunden.', 'Plugin-Manager', [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Information) | Out-Null
    return
  }

  $dialog = New-WpfStyledDialog -Owner $Window -Title 'Plugin-Manager' -Width 760 -Height 460

  $layout = New-Object System.Windows.Controls.Grid
  [void]$layout.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition -Property @{ Height = [System.Windows.GridLength]::Auto }))
  [void]$layout.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition -Property @{ Height = [System.Windows.GridLength]::new(1, [System.Windows.GridUnitType]::Star) }))
  [void]$layout.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition -Property @{ Height = [System.Windows.GridLength]::Auto }))
  $layout.Margin = [System.Windows.Thickness]::new(12)

  $hint = New-Object System.Windows.Controls.TextBlock
  $hint.Text = 'Plugin auswählen und aktivieren/deaktivieren.'
  $hint.Margin = [System.Windows.Thickness]::new(0,0,0,8)
  [System.Windows.Controls.Grid]::SetRow($hint, 0)
  [void]$layout.Children.Add($hint)

  $list = New-Object System.Windows.Controls.ListBox
  $list.BorderThickness = [System.Windows.Thickness]::new(1)
  $list.BorderBrush = Resolve-WpfThemeBrush -Ctx $Ctx -BrushKey 'BrushBorder' -Fallback '#2D2D5A'
  $list.Background = Resolve-WpfThemeBrush -Ctx $Ctx -BrushKey 'BrushSurfaceAlt' -Fallback '#13133A'
  [System.Windows.Controls.Grid]::SetRow($list, 1)
  [void]$layout.Children.Add($list)

  $refresh = {
    $list.Items.Clear()
    foreach ($plugin in @($items | Sort-Object Name)) {
      $isDisabled = [string]$plugin.Path -like '*.disabled'
      $display = '[{0}] {1}  -  {2}  ({3}@{4}, deps:{5})' -f $(if ($isDisabled) { 'AUS' } else { 'AN' }), [string]$plugin.Name, [string]$plugin.Root, [string]$plugin.PluginId, [string]$plugin.Version, [int]$plugin.DependencyCount
      [void]$list.Items.Add([pscustomobject]@{
        Display = $display
        Name = [string]$plugin.Name
        Path = [string]$plugin.Path
        Root = [string]$plugin.Root
        Disabled = $isDisabled
        PluginId = [string]$plugin.PluginId
        Version = [string]$plugin.Version
        DependencyCount = [int]$plugin.DependencyCount
      })
    }
    $list.DisplayMemberPath = 'Display'
  }.GetNewClosure()

  $buttonRow = New-Object System.Windows.Controls.StackPanel
  $buttonRow.Orientation = [System.Windows.Controls.Orientation]::Horizontal
  $buttonRow.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Right
  $buttonRow.Margin = [System.Windows.Thickness]::new(0,10,0,0)

  $btnEnable = New-Object System.Windows.Controls.Button
  $btnEnable.Content = 'Aktivieren'
  $btnEnable.Width = 110
  $btnEnable.Margin = [System.Windows.Thickness]::new(0,0,8,0)

  $btnDisable = New-Object System.Windows.Controls.Button
  $btnDisable.Content = 'Deaktivieren'
  $btnDisable.Width = 110
  $btnDisable.Margin = [System.Windows.Thickness]::new(0,0,8,0)

  $btnClose = New-Object System.Windows.Controls.Button
  $btnClose.Content = 'Schließen'
  $btnClose.Width = 110

  $btnInstallUrl = New-Object System.Windows.Controls.Button
  $btnInstallUrl.Content = 'Installieren (URL)'
  $btnInstallUrl.Width = 145
  $btnInstallUrl.Margin = [System.Windows.Thickness]::new(0,0,8,0)

  $btnEnable.add_Click({
    $selected = $list.SelectedItem
    if (-not $selected) { return }
    $currentPath = [string]$selected.Path
    if ($currentPath -like '*.disabled') {
      $dest = $currentPath.Substring(0, $currentPath.Length - '.disabled'.Length)
      Move-Item -LiteralPath $currentPath -Destination $dest -Force
      Add-WpfLogLine -Ctx $Ctx -Line ('Plugin aktiviert: {0}' -f [string]$selected.Name) -Level 'INFO'
      foreach ($entry in @($items)) {
        if ([string]$entry.Path -eq $currentPath) { $entry.Path = $dest; break }
      }
      & $refresh
    }
  }.GetNewClosure())

  $btnDisable.add_Click({
    $selected = $list.SelectedItem
    if (-not $selected) { return }
    $currentPath = [string]$selected.Path
    if ($currentPath -notlike '*.disabled') {
      $dest = ($currentPath + '.disabled')
      Move-Item -LiteralPath $currentPath -Destination $dest -Force
      Add-WpfLogLine -Ctx $Ctx -Line ('Plugin deaktiviert: {0}' -f [string]$selected.Name) -Level 'INFO'
      foreach ($entry in @($items)) {
        if ([string]$entry.Path -eq $currentPath) { $entry.Path = $dest; break }
      }
      & $refresh
    }
  }.GetNewClosure())

  $btnClose.add_Click({ $dialog.Close() })

  # ── Per-Plugin Konfiguration (5.4.1) ──
  $btnConfig = New-Object System.Windows.Controls.Button
  $btnConfig.Content = 'Konfigurieren'
  $btnConfig.Width = 120
  $btnConfig.Margin = [System.Windows.Thickness]::new(0,0,8,0)

  $btnConfig.add_Click({
    $selected = $list.SelectedItem
    if (-not $selected) { return }
    try {
      $manifestPath = [System.IO.Path]::ChangeExtension([string]$selected.Path -replace '\.disabled$','', '.manifest.json')
      if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        # Kein Manifest vorhanden -- Default erzeugen
        $defaultManifest = [ordered]@{
          id = [string]$selected.PluginId
          version = [string]$selected.Version
          dependencies = @()
          config = [ordered]@{}
        }
        Write-JsonFile -Path $manifestPath -Data $defaultManifest -Depth 5
      }

      $rawJson = Get-Content -LiteralPath $manifestPath -Raw -ErrorAction Stop
      $manifest = $rawJson | ConvertFrom-Json -ErrorAction Stop

      # Config-Sektion bauen (Key-Value-Paare)
      $configSection = [ordered]@{}
      if ($manifest.PSObject.Properties.Name -contains 'config' -and $manifest.config) {
        foreach ($prop in $manifest.config.PSObject.Properties) {
          $configSection[$prop.Name] = [string]$prop.Value
        }
      }

      # Dialog mit TextBox fuer JSON-Bearbeitung
      $cfgDialog = New-WpfStyledDialog -Owner $dialog -Title ('Plugin-Konfiguration: {0}' -f [string]$selected.PluginId) -Width 550 -Height 420

      $cfgGrid = New-Object System.Windows.Controls.Grid
      $cfgGrid.Margin = [System.Windows.Thickness]::new(12)
      $rd1 = New-Object System.Windows.Controls.RowDefinition; $rd1.Height = [System.Windows.GridLength]::Auto
      $rd2 = New-Object System.Windows.Controls.RowDefinition; $rd2.Height = [System.Windows.GridLength]::new(1, [System.Windows.GridUnitType]::Star)
      $rd3 = New-Object System.Windows.Controls.RowDefinition; $rd3.Height = [System.Windows.GridLength]::Auto
      [void]$cfgGrid.RowDefinitions.Add($rd1)
      [void]$cfgGrid.RowDefinitions.Add($rd2)
      [void]$cfgGrid.RowDefinitions.Add($rd3)

      $cfgLabel = New-Object System.Windows.Controls.TextBlock
      $cfgLabel.Text = 'Manifest-JSON bearbeiten (config-Sektion fuer Plugin-Einstellungen):'
      $cfgLabel.Foreground = Resolve-WpfThemeBrush -Ctx $Ctx -BrushKey 'BrushTextMuted' -Fallback '#9999CC'
      $cfgLabel.Margin = [System.Windows.Thickness]::new(0,0,0,8)
      [System.Windows.Controls.Grid]::SetRow($cfgLabel, 0)

      $cfgText = New-Object System.Windows.Controls.TextBox
      $cfgText.FontFamily = New-Object System.Windows.Media.FontFamily('Consolas')
      $cfgText.FontSize = 12
      $cfgText.AcceptsReturn = $true
      $cfgText.AcceptsTab = $true
      $cfgText.VerticalScrollBarVisibility = [System.Windows.Controls.ScrollBarVisibility]::Auto
      $cfgText.Background = Resolve-WpfThemeBrush -Ctx $Ctx -BrushKey 'BrushSurfaceAlt' -Fallback '#13133A'
      $cfgText.Foreground = Resolve-WpfThemeBrush -Ctx $Ctx -BrushKey 'BrushTextPrimary' -Fallback '#E8E8F8'
      $cfgText.BorderBrush = Resolve-WpfThemeBrush -Ctx $Ctx -BrushKey 'BrushBorder' -Fallback '#2D2D5A'
      $cfgText.Text = ($rawJson)
      [System.Windows.Controls.Grid]::SetRow($cfgText, 1)

      $cfgBtnRow = New-Object System.Windows.Controls.StackPanel
      $cfgBtnRow.Orientation = [System.Windows.Controls.Orientation]::Horizontal
      $cfgBtnRow.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Right
      $cfgBtnRow.Margin = [System.Windows.Thickness]::new(0,10,0,0)

      $cfgBtnSave = New-Object System.Windows.Controls.Button
      $cfgBtnSave.Content = 'Speichern'
      $cfgBtnSave.Width = 100
      $cfgBtnSave.Margin = [System.Windows.Thickness]::new(0,0,8,0)

      $cfgBtnCancel = New-Object System.Windows.Controls.Button
      $cfgBtnCancel.Content = 'Abbrechen'
      $cfgBtnCancel.Width = 100

      $cfgBtnSave.add_Click({
        try {
          $newJson = $cfgText.Text
          # Validate JSON
          $null = $newJson | ConvertFrom-Json -ErrorAction Stop
          $newJson | Out-File -LiteralPath $manifestPath -Encoding utf8 -Force
          Add-WpfLogLine -Ctx $Ctx -Line ('Plugin-Config gespeichert: {0}' -f [string]$selected.PluginId) -Level 'INFO'
          $cfgDialog.Close()
        } catch {
          [System.Windows.MessageBox]::Show(
            ('Ungültiges JSON: {0}' -f $_.Exception.Message),
            'Fehler',
            [System.Windows.MessageBoxButton]::OK,
            [System.Windows.MessageBoxImage]::Warning
          ) | Out-Null
        }
      }.GetNewClosure())

      $cfgBtnCancel.add_Click({ $cfgDialog.Close() }.GetNewClosure())

      [void]$cfgBtnRow.Children.Add($cfgBtnSave)
      [void]$cfgBtnRow.Children.Add($cfgBtnCancel)
      [System.Windows.Controls.Grid]::SetRow($cfgBtnRow, 2)

      [void]$cfgGrid.Children.Add($cfgLabel)
      [void]$cfgGrid.Children.Add($cfgText)
      [void]$cfgGrid.Children.Add($cfgBtnRow)
      $cfgDialog.Content = $cfgGrid
      [void]$cfgDialog.ShowDialog()
    } catch {
      Add-WpfLogLine -Ctx $Ctx -Line ('Plugin-Config fehlgeschlagen: {0}' -f $_.Exception.Message) -Level 'ERROR'
    }
  }.GetNewClosure())

  $btnInstallUrl.add_Click({
    try {
      # BUG-047 FIX: Check PluginTrustMode before allowing URL install
      $trustMode = 'trusted-only'
      $envTrust = [string]$env:ROMCLEANUP_PLUGIN_TRUST_MODE
      if (-not [string]::IsNullOrWhiteSpace($envTrust)) {
        $trustMode = $envTrust
      } elseif (Get-Command Get-AppStateValue -ErrorAction SilentlyContinue) {
        try { $trustMode = [string](Get-AppStateValue -Key 'PluginTrustMode' -Default 'trusted-only') } catch {}
      }
      if ($trustMode -eq 'signed-only') {
        [System.Windows.MessageBox]::Show(
          'Plugin-Installation von URLs ist im Modus "signed-only" nicht erlaubt. Nur signierte Plugins können installiert werden.',
          'TrustMode: signed-only',
          [System.Windows.MessageBoxButton]::OK,
          [System.Windows.MessageBoxImage]::Warning
        ) | Out-Null
        return
      }
      if ($trustMode -eq 'trusted-only') {
        $confirm = [System.Windows.MessageBox]::Show(
          ('TrustMode ist "trusted-only". Plugins von externen URLs sind nicht als vertrauenswürdig markiert.{0}{0}Trotzdem installieren?' -f [Environment]::NewLine),
          'TrustMode-Warnung',
          [System.Windows.MessageBoxButton]::YesNo,
          [System.Windows.MessageBoxImage]::Warning
        )
        if ($confirm -ne [System.Windows.MessageBoxResult]::Yes) { return }
      }

      $url = Show-WpfTextInputDialog -Owner $dialog -Title 'Plugin installieren' -Message 'Plugin-URL (.ps1) eingeben:' -DefaultValue ''
      if ([string]::IsNullOrWhiteSpace([string]$url)) { return }

      $operationsRoot = Join-Path (Get-Location).Path 'plugins\operations'
      Assert-DirectoryExists -Path $operationsRoot

      $uriObj = [System.Uri]::new([string]$url)
      $leaf = [System.IO.Path]::GetFileName($uriObj.AbsolutePath)
      if ([string]::IsNullOrWhiteSpace($leaf) -or -not $leaf.ToLowerInvariant().EndsWith('.ps1')) {
        throw 'URL muss auf eine .ps1-Datei zeigen.'
      }

      $target = Join-Path $operationsRoot $leaf
      Invoke-WebRequest -Uri $uriObj.AbsoluteUri -OutFile $target -UseBasicParsing -ErrorAction Stop | Out-Null
      Add-WpfLogLine -Ctx $Ctx -Line ('Plugin installiert: {0}' -f $target) -Level 'INFO'

      $manifestPath = [System.IO.Path]::ChangeExtension([string]$target, '.manifest.json')
      if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        $defaultManifest = [ordered]@{
          id = [System.IO.Path]::GetFileNameWithoutExtension($leaf)
          version = '0.1.0'
          dependencies = @()
        }
        Write-JsonFile -Path $manifestPath -Data $defaultManifest -Depth 5
      }

      # Plugin-Schema-Validierung (5.4.4)
      if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
        if (Get-Command Test-JsonPayloadSchema -ErrorAction SilentlyContinue) {
          try {
            $mRaw = Get-Content -LiteralPath $manifestPath -Raw -ErrorAction Stop
            $mPayload = $mRaw | ConvertFrom-Json -ErrorAction Stop
            $schemaObj = Get-JsonSchema -Name 'plugin-manifest-v1' -ErrorAction SilentlyContinue
            if ($schemaObj) {
              $check = Test-JsonPayloadSchema -Payload $mPayload -Schema $schemaObj
              if (-not $check.IsValid) {
                $errMsg = ($check.Errors | Select-Object -First 3) -join '; '
                $result = [System.Windows.MessageBox]::Show(
                  ('Manifest-Schema-Validierung fehlgeschlagen:{0}{1}{0}{0}Plugin trotzdem installieren?' -f [Environment]::NewLine, $errMsg),
                  'Schema-Warnung',
                  [System.Windows.MessageBoxButton]::YesNo,
                  [System.Windows.MessageBoxImage]::Warning
                )
                if ($result -eq [System.Windows.MessageBoxResult]::No) {
                  Remove-Item -LiteralPath $target -Force -ErrorAction SilentlyContinue
                  Remove-Item -LiteralPath $manifestPath -Force -ErrorAction SilentlyContinue
                  Add-WpfLogLine -Ctx $Ctx -Line ('Plugin-Installation abgebrochen (Schema-Fehler): {0}' -f $leaf) -Level 'WARN'
                  return
                }
                Add-WpfLogLine -Ctx $Ctx -Line ('Plugin installiert mit Schema-Warnung: {0}' -f $errMsg) -Level 'WARN'
              }
            }
          } catch {
            Add-WpfLogLine -Ctx $Ctx -Line ('Schema-Pruefung uebersprungen: {0}' -f $_.Exception.Message) -Level 'DEBUG'
          }
        }
      }

      $f = Get-Item -LiteralPath $target -ErrorAction Stop
      [void]$items.Add([pscustomobject]@{
        Root = $operationsRoot
        Name = $f.Name
        Path = $f.FullName
        PluginId = [System.IO.Path]::GetFileNameWithoutExtension([string]$f.Name)
        Version = '0.1.0'
        DependencyCount = 0
      })
      & $refresh
    } catch {
      Add-WpfLogLine -Ctx $Ctx -Line ('Plugin-Installation fehlgeschlagen: {0}' -f $_.Exception.Message) -Level 'ERROR'
    }
  }.GetNewClosure())

  # ── Plugin-Marketplace (5.4.2) ──
  $btnMarketplace = New-Object System.Windows.Controls.Button
  $btnMarketplace.Content = 'Marketplace'
  $btnMarketplace.Width = 110
  $btnMarketplace.Margin = [System.Windows.Thickness]::new(0,0,8,0)

  $btnMarketplace.add_Click({
    try {
      $marketRoot = Join-Path (Get-Location).Path 'plugins\marketplace'
      $indexPath = Join-Path $marketRoot 'index.json'

      # Marketplace-Verzeichnis + index.json anlegen falls nicht vorhanden
      Assert-DirectoryExists -Path $marketRoot
      if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf)) {
        $defaultIndex = [ordered]@{
          schemaVersion = 'marketplace-v1'
          description = 'Lokales Plugin-Repository. Plugins hier ablegen und in index.json registrieren.'
          plugins = @(
            [ordered]@{
              id = 'example-plugin'
              name = 'Beispiel-Plugin'
              version = '1.0.0'
              description = 'Ein Beispiel-Plugin zur Demonstration.'
              category = 'operations'
              file = 'example-plugin.ps1'
            }
          )
        }
        Write-JsonFile -Path $indexPath -Data $defaultIndex -Depth 5
      }

      $raw = Get-Content -LiteralPath $indexPath -Raw -ErrorAction Stop
      $index = $raw | ConvertFrom-Json -ErrorAction Stop
      $available = @()
      if ($index.plugins) { $available = @($index.plugins) }

      # Bereits installierte Plugin-IDs sammeln
      $installedIds = @{}
      foreach ($it in @($items)) { $installedIds[[string]$it.PluginId] = $true }

      # Marketplace-Dialog
      $mkDialog = New-WpfStyledDialog -Owner $dialog -Title 'Plugin-Marketplace (lokal)' -Width 620 -Height 400

      $mkGrid = New-Object System.Windows.Controls.Grid
      $mkGrid.Margin = [System.Windows.Thickness]::new(12)
      $mkRd1 = New-Object System.Windows.Controls.RowDefinition; $mkRd1.Height = [System.Windows.GridLength]::Auto
      $mkRd2 = New-Object System.Windows.Controls.RowDefinition; $mkRd2.Height = [System.Windows.GridLength]::new(1, [System.Windows.GridUnitType]::Star)
      $mkRd3 = New-Object System.Windows.Controls.RowDefinition; $mkRd3.Height = [System.Windows.GridLength]::Auto
      [void]$mkGrid.RowDefinitions.Add($mkRd1)
      [void]$mkGrid.RowDefinitions.Add($mkRd2)
      [void]$mkGrid.RowDefinitions.Add($mkRd3)

      $mkHint = New-Object System.Windows.Controls.TextBlock
      $mkHint.Text = '{0} Plugin(s) im Marketplace verfuegbar. Plugins liegen in plugins\marketplace\.' -f $available.Count
      $mkHint.Foreground = Resolve-WpfThemeBrush -Ctx $Ctx -BrushKey 'BrushTextMuted' -Fallback '#9999CC'
      $mkHint.Margin = [System.Windows.Thickness]::new(0,0,0,8)
      [System.Windows.Controls.Grid]::SetRow($mkHint, 0)

      $mkList = New-Object System.Windows.Controls.ListBox
      $mkList.Background = Resolve-WpfThemeBrush -Ctx $Ctx -BrushKey 'BrushSurfaceAlt' -Fallback '#13133A'
      $mkList.Foreground = Resolve-WpfThemeBrush -Ctx $Ctx -BrushKey 'BrushTextPrimary' -Fallback '#E8E8F8'
      $mkList.BorderBrush = Resolve-WpfThemeBrush -Ctx $Ctx -BrushKey 'BrushBorder' -Fallback '#2D2D5A'
      $mkList.BorderThickness = [System.Windows.Thickness]::new(1)
      foreach ($p in $available) {
        $status = if ($installedIds.ContainsKey([string]$p.id)) { '[INSTALLIERT]' } else { '[VERFUEGBAR]' }
        $displayText = ('{0} {1} v{2} -- {3}' -f $status, [string]$p.name, [string]$p.version, [string]$p.description)
        [void]$mkList.Items.Add([pscustomobject]@{
          Display = $displayText
          PluginDef = $p
          Installed = $installedIds.ContainsKey([string]$p.id)
        })
      }
      $mkList.DisplayMemberPath = 'Display'
      [System.Windows.Controls.Grid]::SetRow($mkList, 1)

      $mkBtnRow = New-Object System.Windows.Controls.StackPanel
      $mkBtnRow.Orientation = [System.Windows.Controls.Orientation]::Horizontal
      $mkBtnRow.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Right
      $mkBtnRow.Margin = [System.Windows.Thickness]::new(0,10,0,0)

      $mkBtnInstall = New-Object System.Windows.Controls.Button
      $mkBtnInstall.Content = 'Installieren'
      $mkBtnInstall.Width = 110
      $mkBtnInstall.Margin = [System.Windows.Thickness]::new(0,0,8,0)
      $mkBtnInstall.add_Click({
        $sel = $mkList.SelectedItem
        if (-not $sel) { return }
        if ($sel.Installed) {
          [System.Windows.MessageBox]::Show('Plugin ist bereits installiert.', 'Info', [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Information) | Out-Null
          return
        }
        try {
          $pDef = $sel.PluginDef
          $category = if ([string]$pDef.category) { [string]$pDef.category } else { 'operations' }
          $destRoot = Join-Path (Get-Location).Path ('plugins\{0}' -f $category)
          Assert-DirectoryExists -Path $destRoot
          $sourceFile = Join-Path $marketRoot ([string]$pDef.file)
          if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf)) {
            [System.Windows.MessageBox]::Show(
              ('Plugin-Datei nicht gefunden: {0}{1}Bitte die Datei in plugins\marketplace\ ablegen.' -f [string]$pDef.file, [Environment]::NewLine),
              'Fehler', [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Warning
            ) | Out-Null
            return
          }
          $destFile = Join-Path $destRoot ([string]$pDef.file)
          Copy-Item -LiteralPath $sourceFile -Destination $destFile -Force

          # Manifest erzeugen
          $mfPath = [System.IO.Path]::ChangeExtension($destFile, '.manifest.json')
          if (-not (Test-Path -LiteralPath $mfPath -PathType Leaf)) {
            $mf = [ordered]@{ id = [string]$pDef.id; version = [string]$pDef.version; dependencies = @() }
            Write-JsonFile -Path $mfPath -Data $mf -Depth 5
          }

          # In items-Liste aufnehmen
          $fInfo = Get-Item -LiteralPath $destFile -ErrorAction Stop
          [void]$items.Add([pscustomobject]@{
            Root = $destRoot; Name = $fInfo.Name; Path = $fInfo.FullName
            PluginId = [string]$pDef.id; Version = [string]$pDef.version; DependencyCount = 0
          })
          & $refresh

          Add-WpfLogLine -Ctx $Ctx -Line ('Plugin aus Marketplace installiert: {0}' -f [string]$pDef.name) -Level 'INFO'
          $mkDialog.Close()
        } catch {
          Add-WpfLogLine -Ctx $Ctx -Line ('Marketplace-Install fehlgeschlagen: {0}' -f $_.Exception.Message) -Level 'ERROR'
        }
      }.GetNewClosure())

      $mkBtnClose = New-Object System.Windows.Controls.Button
      $mkBtnClose.Content = 'Schliessen'
      $mkBtnClose.Width = 110
      $mkBtnClose.add_Click({ $mkDialog.Close() }.GetNewClosure())

      [void]$mkBtnRow.Children.Add($mkBtnInstall)
      [void]$mkBtnRow.Children.Add($mkBtnClose)
      [System.Windows.Controls.Grid]::SetRow($mkBtnRow, 2)

      [void]$mkGrid.Children.Add($mkHint)
      [void]$mkGrid.Children.Add($mkList)
      [void]$mkGrid.Children.Add($mkBtnRow)
      $mkDialog.Content = $mkGrid
      [void]$mkDialog.ShowDialog()
    } catch {
      Add-WpfLogLine -Ctx $Ctx -Line ('Marketplace fehlgeschlagen: {0}' -f $_.Exception.Message) -Level 'ERROR'
    }
  }.GetNewClosure())

  [void]$buttonRow.Children.Add($btnMarketplace)
  [void]$buttonRow.Children.Add($btnInstallUrl)
  [void]$buttonRow.Children.Add($btnConfig)
  [void]$buttonRow.Children.Add($btnEnable)
  [void]$buttonRow.Children.Add($btnDisable)
  [void]$buttonRow.Children.Add($btnClose)
  [System.Windows.Controls.Grid]::SetRow($buttonRow, 2)
  [void]$layout.Children.Add($buttonRow)

  $dialog.Content = $layout
  & $refresh
  [void]$dialog.ShowDialog()
}

function Invoke-WpfQuickRollback {
  param(
    [Parameter(Mandatory)][System.Windows.Window]$Window,
    [Parameter(Mandatory)][hashtable]$Ctx
  )

  if (-not (Get-Command Invoke-RunRollbackService -ErrorAction SilentlyContinue)) {
    throw 'Rollback-Service nicht verfügbar (Invoke-RunRollbackService fehlt).'
  }

  $auditRoot = ''
  if ($Ctx.ContainsKey('txtAuditRoot') -and $Ctx['txtAuditRoot']) {
    $auditRoot = [string]$Ctx['txtAuditRoot'].Text
  }
  if ([string]::IsNullOrWhiteSpace($auditRoot)) {
    $auditRoot = Join-Path (Get-Location).Path 'audit-logs'
  }
  if (-not (Test-Path -LiteralPath $auditRoot -PathType Container)) {
    throw 'Audit-Verzeichnis nicht gefunden.'
  }

  $selectedAudit = @(Get-ChildItem -LiteralPath $auditRoot -Filter '*.csv' -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1)
  if ($selectedAudit.Count -eq 0) {
    throw 'Keine Audit-CSV gefunden.'
  }

  $roots = Get-WpfRollbackRoots -Ctx $Ctx
  $restoreRoots = $roots.RestoreRoots
  $currentRoots = $roots.CurrentRoots

  $confirm = [System.Windows.MessageBox]::Show(
    ("Schnell-Rollback`n`nAudit: {0}`nRestore-Roots: {1}`nCurrent-Roots: {2}`n`nRollback jetzt ausführen?" -f $selectedAudit[0].Name, $restoreRoots.Count, $currentRoots.Count),
    'Schnell-Rollback',
    [System.Windows.MessageBoxButton]::YesNo,
    [System.Windows.MessageBoxImage]::Warning)
  if ($confirm -ne [System.Windows.MessageBoxResult]::Yes) { return }

  Add-WpfLogLine -Ctx $Ctx -Line ("=== Schnell-Rollback aus Audit: {0} ===" -f $selectedAudit[0].FullName) -Level 'INFO'

  $rollback = Invoke-WpfRollbackCore -AuditCsvPath ([string]$selectedAudit[0].FullName) -AllowedRestoreRoots $restoreRoots -AllowedCurrentRoots $currentRoots -Ctx $Ctx

  Add-WpfLogLine -Ctx $Ctx -Line ("Schnell-Rollback Ergebnis: zurück={0}, skip-unsafe={1}, skip-fehlt={2}, skip-existiert={3}, fehler={4}" -f $rollback.RolledBack, $rollback.SkippedUnsafe, $rollback.SkippedMissingDest, $rollback.SkippedCollision, $rollback.Failed) -Level 'INFO'
  [System.Windows.MessageBox]::Show(
    ("Schnell-Rollback abgeschlossen.`n`nZurückverschoben: {0}`nSkip (unsicher): {1}`nSkip (Quelle fehlt): {2}`nSkip (Ziel existiert): {3}`nFehler: {4}" -f $rollback.RolledBack, $rollback.SkippedUnsafe, $rollback.SkippedMissingDest, $rollback.SkippedCollision, $rollback.Failed),
    'Schnell-Rollback',
    [System.Windows.MessageBoxButton]::OK,
    [System.Windows.MessageBoxImage]::Information) | Out-Null
}

function Set-WpfWatchMode {
  param(
    [Parameter(Mandatory)][System.Windows.Window]$Window,
    [Parameter(Mandatory)][hashtable]$Ctx,
    [Parameter(Mandatory)][bool]$Enabled
  )

  foreach ($entry in @($script:WpfWatchRegistry.Values)) {
    try { if ($entry.Subscribers) { foreach ($sub in @($entry.Subscribers)) { Unregister-Event -SourceIdentifier $sub -ErrorAction SilentlyContinue } } } catch { }
    try { if ($entry.Watcher) { $entry.Watcher.EnableRaisingEvents = $false; $entry.Watcher.Dispose() } } catch { }
  }
  $script:WpfWatchRegistry.Clear()
  $script:WpfWatchPendingRun = $false
  $script:WpfWatchLastEventUtc = [datetime]::MinValue

  if (-not $Enabled) {
    Set-WpfAdvancedStatus -Ctx $Ctx -Text 'Watch-Mode deaktiviert'
    return
  }

  $roots = @((Get-WpfRunParameters -Ctx $Ctx).Roots)
  foreach ($root in $roots) {
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
    $watcher = New-Object System.IO.FileSystemWatcher
    $watcher.Path = $root
    $watcher.IncludeSubdirectories = $true
    $watcher.NotifyFilter = [IO.NotifyFilters]'FileName, DirectoryName, LastWrite, Size'
    $watcher.EnableRaisingEvents = $true

    $idBase = ('RomCleanup.WpfWatch.{0}' -f ([guid]::NewGuid().ToString('N')))
    $debounceMs = 5000
    $action = {
      try {
        $w = $Event.MessageData.Window
        $ctxRef = $Event.MessageData.Ctx
        $current = [datetime]::UtcNow
        $delta = ($current - $script:WpfWatchLastEventUtc).TotalMilliseconds
        if ($script:WpfWatchPendingRun -or ($delta -lt $debounceMs)) { return }
        $script:WpfWatchPendingRun = $true
        $script:WpfWatchLastEventUtc = $current
        if (-not $ctxRef['btnRunGlobal'].IsEnabled) { return }
        $w.Dispatcher.BeginInvoke([System.Action]{
          try {
            if (-not (Set-WpfViewModelProperty -Ctx $ctxRef -Name 'DryRun' -Value $true)) {
              $ctxRef['chkReportDryRun'].IsChecked = $true
            }
            Start-WpfOperationAsync -Window $w -Ctx $ctxRef
          } catch { }
          finally { $script:WpfWatchPendingRun = $false }
        }) | Out-Null
      } catch {
        $script:WpfWatchPendingRun = $false
      }
    }

    $sub1 = ($idBase + '.Created')
    $sub2 = ($idBase + '.Changed')
    Register-ObjectEvent -InputObject $watcher -EventName Created -SourceIdentifier $sub1 -Action $action -MessageData @{ Window = $Window; Ctx = $Ctx } | Out-Null
    Register-ObjectEvent -InputObject $watcher -EventName Changed -SourceIdentifier $sub2 -Action $action -MessageData @{ Window = $Window; Ctx = $Ctx } | Out-Null

    $script:WpfWatchRegistry[$root] = [pscustomobject]@{
      Watcher = $watcher
      Subscribers = @($sub1, $sub2)
    }
  }

  Set-WpfAdvancedStatus -Ctx $Ctx -Text ('Watch-Mode aktiv ({0} Roots)' -f $script:WpfWatchRegistry.Count)
}

function Invoke-WpfAutoProfileByConsole {
  param([Parameter(Mandatory)][hashtable]$Ctx)

  if (-not (Get-Command Get-ConfigProfiles -ErrorAction SilentlyContinue)) { return }
  $profiles = Get-ConfigProfiles
  if (-not $profiles -or $profiles.Count -eq 0) { return }

  $roots = @((Get-WpfRunParameters -Ctx $Ctx).Roots)
  $detected = $null
  foreach ($root in $roots) {
    $leaf = [System.IO.Path]::GetFileName([string]$root)
    if ([string]::IsNullOrWhiteSpace($leaf)) { continue }
    $normalizedLeaf = $leaf.Trim().ToLowerInvariant()
    $candidates = @($profiles.Keys | Where-Object { ([string]$_).Trim().ToLowerInvariant() -eq $normalizedLeaf })
    if ($candidates.Count -gt 0) { $detected = [string]$candidates[0]; break }
  }

  if (-not [string]::IsNullOrWhiteSpace($detected) -and $Ctx.ContainsKey('cmbConfigProfile') -and $Ctx['cmbConfigProfile']) {
    $Ctx['cmbConfigProfile'].Text = $detected
    Add-WpfLogLine -Ctx $Ctx -Line ("Auto-Profil gewählt: {0}" -f $detected) -Level 'INFO'
  }
}

function New-WpfInverseRollbackCsv {
  param([Parameter(Mandatory)][string]$AuditCsvPath)

  $rows = @(Import-Csv -LiteralPath $AuditCsvPath -Encoding UTF8)
  $inverseRows = New-Object System.Collections.Generic.List[object]
  foreach ($row in $rows) {
    $action = ([string]$row.Action).Trim().ToUpperInvariant()
    if ($action -notin @('MOVE','JUNK','BIOS-MOVE')) { continue }
    [void]$inverseRows.Add([pscustomobject]@{
      Action = $action
      Source = [string]$row.Dest
      Dest = [string]$row.Source
    })
  }

  $target = Join-Path ([System.IO.Path]::GetDirectoryName($AuditCsvPath)) ('rollback-inverse-{0}.csv' -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
  $inverseRows | Export-Csv -LiteralPath $target -NoTypeInformation -Encoding UTF8
  return $target
}

function Register-WpfAdvancedFeatureHandlers {
  <#
  .SYNOPSIS
    Registers advanced feature event handlers (Slice 6):
    Plugin manager, rollback, watch-mode, auto-profile, collection diff, conflict policy.
  .PARAMETER Window
    The WPF Window object.
  .PARAMETER Ctx
    The named-element hashtable from Get-WpfNamedElements.
  .PARAMETER PersistSettingsNow
    Scriptblock to persist settings immediately.
  #>
  param(
    [Parameter(Mandatory)][System.Windows.Window]$Window,
    [Parameter(Mandatory)][hashtable]$Ctx,
    [Parameter(Mandatory)][scriptblock]$PersistSettingsNow
  )

  # ── Collection Diff ────────────────────────────────────────────────────
  if ($Ctx.ContainsKey('btnCollectionDiff') -and $Ctx['btnCollectionDiff']) {
    $Ctx['btnCollectionDiff'].add_Click({
      try {
        $outPath = Invoke-WpfCollectionDiffReport -Ctx $Ctx
        if ($outPath) { Start-Process $outPath | Out-Null }
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Collection-Diff fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ── Plugin Manager ────────────────────────────────────────────────────
  if ($Ctx.ContainsKey('btnPluginManager') -and $Ctx['btnPluginManager']) {
    $Ctx['btnPluginManager'].add_Click({
      try { Invoke-WpfPluginManager -Window $Window -Ctx $Ctx } catch { Add-WpfLogLine -Ctx $Ctx -Line ("Plugin-Manager fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR' }
    }.GetNewClosure())
  }

  # ── Auto Profile ──────────────────────────────────────────────────────
  if ($Ctx.ContainsKey('btnAutoProfile') -and $Ctx['btnAutoProfile']) {
    $Ctx['btnAutoProfile'].add_Click({
      try { Invoke-WpfAutoProfileByConsole -Ctx $Ctx } catch { Add-WpfLogLine -Ctx $Ctx -Line ("Auto-Profil fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR' }
    }.GetNewClosure())
  }

  # ── Watch Mode ────────────────────────────────────────────────────────
  if ($Ctx.ContainsKey('btnWatchApply') -and $Ctx['btnWatchApply']) {
    $Ctx['btnWatchApply'].add_Click({
      try {
        $enabled = (Get-WpfIsChecked -Ctx $Ctx -Key 'chkWatchMode')
        Set-WpfWatchMode -Window $Window -Ctx $Ctx -Enabled:$enabled
        Add-WpfLogLine -Ctx $Ctx -Line ("Watch-Mode {0}" -f $(if ($enabled) { 'aktiviert' } else { 'deaktiviert' })) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Watch-Mode fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ── Conflict Policy ───────────────────────────────────────────────────
  if ($Ctx.ContainsKey('btnConflictPolicy') -and $Ctx['btnConflictPolicy']) {
    $Ctx['btnConflictPolicy'].add_Click({
      try {
        $current = [string](Get-AppStateValue -Key 'ConflictPolicy' -Default 'KeepExisting')
          $policyInput = Read-WpfPromptText -Owner $Window -Title 'Conflict-Policy' -Message 'Policy eingeben: KeepExisting | Overwrite | CreateDuplicate' -DefaultValue $current
          if (-not [string]::IsNullOrWhiteSpace($policyInput)) {
            $normalized = $policyInput.Trim()
          if ($normalized -in @('KeepExisting','Overwrite','CreateDuplicate')) {
            [void](Set-AppStateValue -Key 'ConflictPolicy' -Value $normalized)
            Add-WpfLogLine -Ctx $Ctx -Line ("Conflict-Policy gesetzt: {0}" -f $normalized) -Level 'INFO'
          }
        }
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Conflict-Policy fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ── Quick Rollback ────────────────────────────────────────────────────
  if ($Ctx.ContainsKey('btnRollbackQuick') -and $Ctx['btnRollbackQuick']) {
    $Ctx['btnRollbackQuick'].add_Click({
      try {
        Invoke-WpfQuickRollback -Window $Window -Ctx $Ctx
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Schnell-Rollback fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ── Rollback Undo/Redo ────────────────────────────────────────────────
  if ($Ctx.ContainsKey('btnRollbackUndo') -and $Ctx['btnRollbackUndo']) {
    $Ctx['btnRollbackUndo'].IsEnabled = ($script:WpfRollbackUndoStack.Count -gt 0)
    $Ctx['btnRollbackUndo'].add_Click({
      try {
        if ($script:WpfRollbackUndoStack.Count -le 0) { return }
        $entry = $script:WpfRollbackUndoStack[$script:WpfRollbackUndoStack.Count - 1]
        $script:WpfRollbackUndoStack.RemoveAt($script:WpfRollbackUndoStack.Count - 1)
        $undoResult = Invoke-RunRollbackService -Parameters @{
          AuditCsvPath = [string]$entry.UndoCsvPath
          AllowedRestoreRoots = @($entry.AllowedRestoreRoots)
          AllowedCurrentRoots = @($entry.AllowedCurrentRoots)
          Log = { param([string]$m) Add-WpfLogLine -Ctx $Ctx -Line $m -Level 'INFO' }
        }
        Add-WpfLogLine -Ctx $Ctx -Line ("Rollback Undo: zurück={0}, fehler={1}" -f $undoResult.RolledBack, $undoResult.Failed) -Level 'INFO'
        [void]$script:WpfRollbackRedoStack.Add($entry)
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Rollback Undo fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      } finally {
        $Ctx['btnRollbackUndo'].IsEnabled = ($script:WpfRollbackUndoStack.Count -gt 0)
        if ($Ctx.ContainsKey('btnRollbackRedo') -and $Ctx['btnRollbackRedo']) { $Ctx['btnRollbackRedo'].IsEnabled = ($script:WpfRollbackRedoStack.Count -gt 0) }
      }
    }.GetNewClosure())
  }

  if ($Ctx.ContainsKey('btnRollbackRedo') -and $Ctx['btnRollbackRedo']) {
    $Ctx['btnRollbackRedo'].IsEnabled = ($script:WpfRollbackRedoStack.Count -gt 0)
    $Ctx['btnRollbackRedo'].add_Click({
      try {
        if ($script:WpfRollbackRedoStack.Count -le 0) { return }
        $entry = $script:WpfRollbackRedoStack[$script:WpfRollbackRedoStack.Count - 1]
        $script:WpfRollbackRedoStack.RemoveAt($script:WpfRollbackRedoStack.Count - 1)
        $redoResult = Invoke-RunRollbackService -Parameters @{
          AuditCsvPath = [string]$entry.RedoCsvPath
          AllowedRestoreRoots = @($entry.AllowedRestoreRoots)
          AllowedCurrentRoots = @($entry.AllowedCurrentRoots)
          Log = { param([string]$m) Add-WpfLogLine -Ctx $Ctx -Line $m -Level 'INFO' }
        }
        Add-WpfLogLine -Ctx $Ctx -Line ("Rollback Redo: zurück={0}, fehler={1}" -f $redoResult.RolledBack, $redoResult.Failed) -Level 'INFO'
        [void]$script:WpfRollbackUndoStack.Add($entry)
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Rollback Redo fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      } finally {
        $Ctx['btnRollbackRedo'].IsEnabled = ($script:WpfRollbackRedoStack.Count -gt 0)
        if ($Ctx.ContainsKey('btnRollbackUndo') -and $Ctx['btnRollbackUndo']) { $Ctx['btnRollbackUndo'].IsEnabled = ($script:WpfRollbackUndoStack.Count -gt 0) }
      }
    }.GetNewClosure())
  }

  # ── Rollback Wizard ───────────────────────────────────────────────────
  if ($Ctx.ContainsKey('btnRollback') -and $Ctx['btnRollback']) {
    $Ctx['btnRollback'].add_Click({
      Invoke-WpfRollbackWizard -Window $Window -Ctx $Ctx
    }.GetNewClosure())
  }

  # ── DAT-Rename (ISS-003) ──────────────────────────────────────────────
  if ($Ctx.ContainsKey('btnDatRename') -and $Ctx['btnDatRename']) {
    $Ctx['btnDatRename'].add_Click({
      try {
        $datRoot = Get-AppStateValue -Key 'DatRoot' -Default ''
        $hashType = Get-AppStateValue -Key 'DatHashType' -Default 'SHA1'
        if ([string]::IsNullOrWhiteSpace($datRoot)) {
          Add-WpfLogLine -Ctx $Ctx -Line 'DAT-Rename: Kein DatRoot konfiguriert.' -Level 'WARNING'
          return
        }
        $roots = Get-WpfRootsList -Ctx $Ctx
        if (-not $roots -or $roots.Count -eq 0) {
          Add-WpfLogLine -Ctx $Ctx -Line 'DAT-Rename: Keine Roots konfiguriert.' -Level 'WARNING'
          return
        }
        $datIdx = Get-DatIndex -DatRoot $datRoot -HashType $hashType
        $files = foreach ($r in $roots) {
          if (Test-Path -LiteralPath $r -PathType Container) {
            Get-ChildItem -LiteralPath $r -Recurse -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
          }
        }
        if (-not $files) { Add-WpfLogLine -Ctx $Ctx -Line 'DAT-Rename: Keine Dateien gefunden.' -Level 'INFO'; return }
        $renameResult = Invoke-RunDatRenameService -Operation 'Preview' -Files @($files) -DatIndex $datIdx -HashType $hashType -Log { param($m) Add-WpfLogLine -Ctx $Ctx -Line $m -Level 'INFO' } -Ports @{}
        Add-WpfLogLine -Ctx $Ctx -Line ("DAT-Rename Preview: {0} würden umbenannt, {1} kein Match, {2} Konflikte" -f $renameResult.Renamed, $renameResult.NoMatch, $renameResult.Conflicts) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("DAT-Rename fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ── M3U-Generierung (ISS-004) ─────────────────────────────────────────
  if ($Ctx.ContainsKey('btnGenerateM3u') -and $Ctx['btnGenerateM3u']) {
    $Ctx['btnGenerateM3u'].add_Click({
      try {
        $roots = Get-WpfRootsList -Ctx $Ctx
        if (-not $roots -or $roots.Count -eq 0) {
          Add-WpfLogLine -Ctx $Ctx -Line 'M3U: Keine Roots konfiguriert.' -Level 'WARNING'
          return
        }
        $discFiles = foreach ($r in $roots) {
          if (Test-Path -LiteralPath $r -PathType Container) {
            Get-ChildItem -LiteralPath $r -Recurse -File -ErrorAction SilentlyContinue |
              Where-Object { $_.Extension -imatch '^\.(chd|cue|ccd|gdi|iso|pbp)$' } |
              Select-Object -ExpandProperty FullName
          }
        }
        if (-not $discFiles) { Add-WpfLogLine -Ctx $Ctx -Line 'M3U: Keine Disc-Dateien gefunden.' -Level 'INFO'; return }
        $m3uResult = Invoke-RunM3uGenerationService -Files @($discFiles) -OutputDir $roots[0] -Mode 'DryRun' -Log { param($m) Add-WpfLogLine -Ctx $Ctx -Line $m -Level 'INFO' } -Ports @{}
        Add-WpfLogLine -Ctx $Ctx -Line ("M3U Preview: {0} würden generiert, {1} übersprungen" -f $m3uResult.Generated, $m3uResult.Skipped) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("M3U-Generierung fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ── CSV-Export (ISS-009) ───────────────────────────────────────────────
  if ($Ctx.ContainsKey('btnExportCsv') -and $Ctx['btnExportCsv']) {
    $Ctx['btnExportCsv'].add_Click({
      try {
        $lastResult = Get-AppStateValue -Key 'LastDedupeResult' -Default $null
        if (-not $lastResult) {
          Add-WpfLogLine -Ctx $Ctx -Line 'CSV-Export: Kein Scan-Ergebnis vorhanden. Zuerst DryRun ausführen.' -Level 'WARNING'
          return
        }
        $csvPath = Join-Path $PSScriptRoot ('reports\collection-export-{0}.csv' -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
        $items = @()
        if ($lastResult.PSObject.Properties.Name -contains 'AllItems') { $items = @($lastResult.AllItems) }
        elseif ($lastResult.PSObject.Properties.Name -contains 'Winners') { $items = @($lastResult.Winners) }
        if ($items.Count -eq 0) { Add-WpfLogLine -Ctx $Ctx -Line 'CSV-Export: Keine Daten zum Exportieren.' -Level 'WARNING'; return }
        Invoke-RunCsvExportService -Items $items -OutputPath $csvPath -Log { param($m) Add-WpfLogLine -Ctx $Ctx -Line $m -Level 'INFO' } -Ports @{}
        Add-WpfLogLine -Ctx $Ctx -Line ("CSV-Export: {0}" -f $csvPath) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("CSV-Export fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ── CLI-Command Export (ISS-008) ───────────────────────────────────────
  if ($Ctx.ContainsKey('btnExportCliCommand') -and $Ctx['btnExportCliCommand']) {
    $Ctx['btnExportCliCommand'].add_Click({
      try {
        $settings = Get-WpfRunParameters -Ctx $Ctx
        $cmd = Invoke-RunCliExportService -Settings $settings -Ports @{}
        if ($cmd) {
          [System.Windows.Clipboard]::SetText($cmd)
          Add-WpfLogLine -Ctx $Ctx -Line ('CLI-Kommando in Zwischenablage kopiert: {0}' -f ($cmd.Substring(0, [Math]::Min(100, $cmd.Length)))) -Level 'INFO'
        }
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("CLI-Export fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ── RetroArch Playlist Export (ISS-015) ────────────────────────────────
  if ($Ctx.ContainsKey('btnExportRetroArch') -and $Ctx['btnExportRetroArch']) {
    $Ctx['btnExportRetroArch'].add_Click({
      try {
        $lastResult = Get-AppStateValue -Key 'LastDedupeResult' -Default $null
        if (-not $lastResult) {
          Add-WpfLogLine -Ctx $Ctx -Line 'RetroArch-Export: Kein Scan-Ergebnis. Zuerst DryRun ausführen.' -Level 'WARNING'
          return
        }
        $roots = Get-WpfRootsList -Ctx $Ctx
        $outputPath = if ($roots -and $roots.Count -gt 0) { Join-Path $roots[0] '_playlists' } else { Join-Path $PSScriptRoot 'reports\_playlists' }
        $items = @()
        if ($lastResult.PSObject.Properties.Name -contains 'Winners') { $items = @($lastResult.Winners) }
        if ($items.Count -eq 0) { Add-WpfLogLine -Ctx $Ctx -Line 'RetroArch-Export: Keine Winner-Daten.' -Level 'WARNING'; return }
        Invoke-RunRetroArchExportService -Items $items -OutputPath $outputPath -Log { param($m) Add-WpfLogLine -Ctx $Ctx -Line $m -Level 'INFO' } -Ports @{}
        Add-WpfLogLine -Ctx $Ctx -Line ("RetroArch-Playlists exportiert nach: {0}" -f $outputPath) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("RetroArch-Export fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ── ECM-Dekompression (ISS-005) ───────────────────────────────────────
  if ($Ctx.ContainsKey('btnEcmDecompress') -and $Ctx['btnEcmDecompress']) {
    $Ctx['btnEcmDecompress'].add_Click({
      try {
        $roots = Get-WpfRootsList -Ctx $Ctx
        if (-not $roots -or $roots.Count -eq 0) {
          Add-WpfLogLine -Ctx $Ctx -Line 'ECM: Keine Roots konfiguriert.' -Level 'WARNING'
          return
        }
        $ecmFiles = foreach ($r in $roots) {
          if (Test-Path -LiteralPath $r -PathType Container) {
            Get-ChildItem -LiteralPath $r -Filter '*.ecm' -Recurse -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
          }
        }
        if (-not $ecmFiles) { Add-WpfLogLine -Ctx $Ctx -Line 'ECM: Keine .ecm-Dateien gefunden.' -Level 'INFO'; return }
        $ecmResult = Invoke-RunEcmDecompressService -Files @($ecmFiles) -Mode 'DryRun' -Log { param($m) Add-WpfLogLine -Ctx $Ctx -Line $m -Level 'INFO' } -Ports @{}
        Add-WpfLogLine -Ctx $Ctx -Line ("ECM Preview: {0} würden dekomprimiert, {1} fehlgeschlagen" -f $ecmResult.Success, $ecmResult.Failed) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("ECM-Dekompression fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ── Archive-Repack (ISS-006) ──────────────────────────────────────────
  if ($Ctx.ContainsKey('btnArchiveRepack') -and $Ctx['btnArchiveRepack']) {
    $Ctx['btnArchiveRepack'].add_Click({
      try {
        $roots = Get-WpfRootsList -Ctx $Ctx
        if (-not $roots -or $roots.Count -eq 0) {
          Add-WpfLogLine -Ctx $Ctx -Line 'Repack: Keine Roots konfiguriert.' -Level 'WARNING'
          return
        }
        $targetFormat = 'zip'
        if ($Ctx.ContainsKey('cmbRepackFormat') -and $Ctx['cmbRepackFormat']) {
          $sel = $Ctx['cmbRepackFormat'].SelectedItem
          if ($sel) { $targetFormat = [string]$sel }
        }
        $archiveFiles = foreach ($r in $roots) {
          if (Test-Path -LiteralPath $r -PathType Container) {
            $sourceExts = if ($targetFormat -eq 'zip') { @('*.7z','*.rar') } else { @('*.zip','*.rar') }
            foreach ($ext in $sourceExts) {
              Get-ChildItem -LiteralPath $r -Filter $ext -Recurse -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
            }
          }
        }
        if (-not $archiveFiles) { Add-WpfLogLine -Ctx $Ctx -Line 'Repack: Keine umzuwandelnden Archive gefunden.' -Level 'INFO'; return }
        $repackResult = Invoke-RunArchiveRepackService -Files @($archiveFiles) -TargetFormat $targetFormat -Mode 'DryRun' -Log { param($m) Add-WpfLogLine -Ctx $Ctx -Line $m -Level 'INFO' } -Ports @{}
        Add-WpfLogLine -Ctx $Ctx -Line ("Repack Preview: {0} würden umgepackt, {1} übersprungen" -f $repackResult.Repacked, $repackResult.Skipped) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Archive-Repack fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ── Webhook Test (ISS-016) ────────────────────────────────────────────
  if ($Ctx.ContainsKey('btnTestWebhook') -and $Ctx['btnTestWebhook']) {
    $Ctx['btnTestWebhook'].add_Click({
      try {
        $url = ''
        if ($Ctx.ContainsKey('txtWebhookUrl') -and $Ctx['txtWebhookUrl']) { $url = $Ctx['txtWebhookUrl'].Text }
        if ([string]::IsNullOrWhiteSpace($url)) {
          Add-WpfLogLine -Ctx $Ctx -Line 'Webhook: Keine URL eingegeben.' -Level 'WARNING'
          return
        }
        $testSummary = @{ Status = 'test'; Message = 'RomCleanup Webhook-Test'; Timestamp = (Get-Date).ToString('o') }
        Invoke-RunWebhookService -WebhookUrl $url -Summary $testSummary -Log { param($m) Add-WpfLogLine -Ctx $Ctx -Line $m -Level 'INFO' } -Ports @{}
        Add-WpfLogLine -Ctx $Ctx -Line 'Webhook-Test erfolgreich gesendet.' -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Webhook-Test fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ── Run History (ISS-018) ──────────────────────────────────────────────
  if ($Ctx.ContainsKey('btnRunHistory') -and $Ctx['btnRunHistory']) {
    $Ctx['btnRunHistory'].add_Click({
      try {
        $reportsDir = Join-Path $PSScriptRoot 'reports'
        $history = Get-RunHistory -ReportsDir $reportsDir -MaxEntries 50
        if (-not $history -or $history.Count -eq 0) {
          Add-WpfLogLine -Ctx $Ctx -Line 'Run-History: Keine bisherigen Runs gefunden.' -Level 'INFO'
          return
        }
        foreach ($entry in $history | Select-Object -First 10) {
          $line = "{0} | {1} | {2}" -f $entry.Date, $entry.Mode, $entry.Status
          Add-WpfLogLine -Ctx $Ctx -Line $line -Level 'INFO'
        }
        Add-WpfLogLine -Ctx $Ctx -Line ("Run-History: {0} Einträge total" -f $history.Count) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Run-History fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ═══════════════════════════════════════════════════════════════════════
  # FEATURE-TAB HANDLERS (Phase 1-4 Feature Module)
  # ═══════════════════════════════════════════════════════════════════════

  # ── Analyse & Berichte ─────────────────────────────────────────────────

  # Konvertierungs-Schätzung (QW-04)
  if ($Ctx.ContainsKey('btnConversionEstimate') -and $Ctx['btnConversionEstimate']) {
    $Ctx['btnConversionEstimate'].add_Click({
      try {
        $roots = Get-AppStateValue 'RootPaths'
        if (-not $roots) { Add-WpfLogLine -Ctx $Ctx -Line 'Konvertierungs-Schätzung: Keine Roots konfiguriert.' -Level 'WARNING'; return }
        $files = foreach ($r in $roots) {
          if (Test-Path -LiteralPath $r -PathType Container) {
            Get-ChildItem -LiteralPath $r -Recurse -File -ErrorAction SilentlyContinue |
              Select-Object -ExpandProperty FullName
          }
        }
        if (-not $files) { Add-WpfLogLine -Ctx $Ctx -Line 'Konvertierungs-Schätzung: Keine Dateien gefunden.' -Level 'INFO'; return }
        $estimate = Get-ConversionSavingsEstimate -Files $files
        Add-WpfLogLine -Ctx $Ctx -Line ("Schätzung: {0:N0} MB einsparbar bei {1} Dateien" -f ($estimate.TotalSavingsBytes / 1MB), $estimate.FileCount) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Konvertierungs-Schätzung fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Junk-Bericht (QW-05)
  if ($Ctx.ContainsKey('btnJunkReport') -and $Ctx['btnJunkReport']) {
    $Ctx['btnJunkReport'].add_Click({
      try {
        $roots = Get-AppStateValue 'RootPaths'
        if (-not $roots) { Add-WpfLogLine -Ctx $Ctx -Line 'Junk-Bericht: Keine Roots konfiguriert.' -Level 'WARNING'; return }
        $result = Invoke-RunJunkReportService -Roots $roots -Log { param($m) Add-WpfLogLine -Ctx $Ctx -Line $m -Level 'INFO' } -Ports @{}
        Add-WpfLogLine -Ctx $Ctx -Line ("Junk-Bericht: {0} Junk-Dateien gefunden" -f $result.JunkCount) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Junk-Bericht fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ROM-Filter (QW-08)
  if ($Ctx.ContainsKey('btnRomFilter') -and $Ctx['btnRomFilter']) {
    $Ctx['btnRomFilter'].add_Click({
      try {
        $query = Show-WpfTextInputDialog -Window $Window -Title 'ROM-Filter' -Prompt 'Suchbegriff eingeben:' -DefaultValue ''
        if ([string]::IsNullOrWhiteSpace($query)) { return }
        $roots = Get-AppStateValue 'RootPaths'
        if (-not $roots) { Add-WpfLogLine -Ctx $Ctx -Line 'ROM-Filter: Keine Roots.' -Level 'WARNING'; return }
        $results = Search-RomCollection -Roots $roots -Query $query
        $count = ($results | Measure-Object).Count
        Add-WpfLogLine -Ctx $Ctx -Line ("ROM-Filter: {0} Treffer für '{1}'" -f $count, $query) -Level 'INFO'
        foreach ($item in $results | Select-Object -First 20) {
          Add-WpfLogLine -Ctx $Ctx -Line ("  {0}" -f $item.Name) -Level 'INFO'
        }
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("ROM-Filter fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Duplikat-Heatmap (QW-09)
  if ($Ctx.ContainsKey('btnDuplicateHeatmap') -and $Ctx['btnDuplicateHeatmap']) {
    $Ctx['btnDuplicateHeatmap'].add_Click({
      try {
        $roots = Get-AppStateValue 'RootPaths'
        if (-not $roots) { Add-WpfLogLine -Ctx $Ctx -Line 'Duplikat-Heatmap: Keine Roots.' -Level 'WARNING'; return }
        $data = Get-DuplicateHeatmapData -Roots $roots
        if (-not $data -or $data.Count -eq 0) { Add-WpfLogLine -Ctx $Ctx -Line 'Duplikat-Heatmap: Keine Duplikate gefunden.' -Level 'INFO'; return }
        foreach ($entry in $data | Select-Object -First 15) {
          Add-WpfLogLine -Ctx $Ctx -Line ("{0}: {1} Duplikate" -f $entry.Console, $entry.DuplicateCount) -Level 'INFO'
        }
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Duplikat-Heatmap fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Fehlende ROMs (MF-01)
  if ($Ctx.ContainsKey('btnMissingRom') -and $Ctx['btnMissingRom']) {
    $Ctx['btnMissingRom'].add_Click({
      try {
        Add-WpfLogLine -Ctx $Ctx -Line 'Fehlende ROMs: Analyse wird gestartet...' -Level 'INFO'
        $datIndex = Get-AppStateValue 'DatIndex'
        if (-not $datIndex) { Add-WpfLogLine -Ctx $Ctx -Line 'Fehlende ROMs: Kein DAT-Index geladen.' -Level 'WARNING'; return }
        $foundHashes = @(Get-AppStateValue 'FoundHashes')
        $result = Invoke-RunMissingRomService -DatIndex $datIndex -FoundHashes $foundHashes -Ports @{}
        $count = ($result | Measure-Object).Count
        Add-WpfLogLine -Ctx $Ctx -Line ("Fehlende ROMs: {0} fehlende Einträge gefunden" -f $count) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Fehlende ROMs fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Cross-Root-Duplikate (MF-02)
  if ($Ctx.ContainsKey('btnCrossRootDupe') -and $Ctx['btnCrossRootDupe']) {
    $Ctx['btnCrossRootDupe'].add_Click({
      try {
        Add-WpfLogLine -Ctx $Ctx -Line 'Cross-Root-Duplikate: Suche wird gestartet...' -Level 'INFO'
        $fileIndex = Get-AppStateValue 'FileIndex'
        if (-not $fileIndex) { Add-WpfLogLine -Ctx $Ctx -Line 'Cross-Root: Kein FileIndex vorhanden. Bitte zuerst DryRun ausführen.' -Level 'WARNING'; return }
        $result = Invoke-RunCrossRootDupeService -FileIndex $fileIndex -Progress { param($m) Add-WpfLogLine -Ctx $Ctx -Line $m -Level 'INFO' } -Ports @{}
        $count = ($result | Measure-Object).Count
        Add-WpfLogLine -Ctx $Ctx -Line ("Cross-Root: {0} Duplikatgruppen gefunden" -f $count) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Cross-Root-Duplikate fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Header-Analyse (MF-03)
  if ($Ctx.ContainsKey('btnHeaderAnalysis') -and $Ctx['btnHeaderAnalysis']) {
    $Ctx['btnHeaderAnalysis'].add_Click({
      try {
        $filePath = Show-WpfTextInputDialog -Window $Window -Title 'Header-Analyse' -Prompt 'ROM-Dateipfad eingeben:' -DefaultValue ''
        if ([string]::IsNullOrWhiteSpace($filePath)) { return }
        if (-not (Test-Path -LiteralPath $filePath)) { Add-WpfLogLine -Ctx $Ctx -Line 'Header-Analyse: Datei nicht gefunden.' -Level 'WARNING'; return }
        $header = Read-RomHeader -Path $filePath
        Add-WpfLogLine -Ctx $Ctx -Line ("Header: System={0}, Title={1}, Region={2}" -f $header.System, $header.Title, $header.Region) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Header-Analyse fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Vollständigkeit (MF-04)
  if ($Ctx.ContainsKey('btnCompleteness') -and $Ctx['btnCompleteness']) {
    $Ctx['btnCompleteness'].add_Click({
      try {
        $roots = Get-AppStateValue 'RootPaths'
        if (-not $roots) { Add-WpfLogLine -Ctx $Ctx -Line 'Vollständigkeit: Keine Roots.' -Level 'WARNING'; return }
        $report = Get-CompletenessReport -Roots $roots
        Add-WpfLogLine -Ctx $Ctx -Line ("Vollständigkeit: {0:P0} komplett ({1}/{2})" -f $report.Percentage, $report.OwnedCount, $report.TotalCount) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Vollständigkeit fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # DryRun-Vergleich (MF-21)
  if ($Ctx.ContainsKey('btnDryRunCompare') -and $Ctx['btnDryRunCompare']) {
    $Ctx['btnDryRunCompare'].add_Click({
      try {
        $reportsDir = Join-Path $PSScriptRoot 'reports'
        $plans = Get-ChildItem -Path $reportsDir -Filter 'move-plan-*.json' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 2
        if ($plans.Count -lt 2) { Add-WpfLogLine -Ctx $Ctx -Line 'DryRun-Vergleich: Mindestens 2 Move-Plans benötigt.' -Level 'WARNING'; return }
        $diff = Compare-DryRunResults -BaselinePath $plans[1].FullName -CurrentPath $plans[0].FullName
        Add-WpfLogLine -Ctx $Ctx -Line ("DryRun-Diff: +{0} neu, -{1} entfernt, ~{2} geändert" -f $diff.Added, $diff.Removed, $diff.Changed) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("DryRun-Vergleich fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Trend-Analyse (XL-06)
  if ($Ctx.ContainsKey('btnTrendAnalysis') -and $Ctx['btnTrendAnalysis']) {
    $Ctx['btnTrendAnalysis'].add_Click({
      try {
        $roots = Get-AppStateValue 'RootPaths'
        if (-not $roots) { Add-WpfLogLine -Ctx $Ctx -Line 'Trend-Analyse: Keine Roots.' -Level 'WARNING'; return }
        $snapshot = New-TrendSnapshot -Roots $roots
        Add-WpfLogLine -Ctx $Ctx -Line ("Trend-Snapshot erstellt: {0} Dateien, {1:N0} MB" -f $snapshot.FileCount, ($snapshot.TotalBytes / 1MB)) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Trend-Analyse fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Emulator-Kompatibilität (XL-07)
  if ($Ctx.ContainsKey('btnEmulatorCompat') -and $Ctx['btnEmulatorCompat']) {
    $Ctx['btnEmulatorCompat'].add_Click({
      try {
        $profile = New-EmulatorProfile
        Add-WpfLogLine -Ctx $Ctx -Line ("Emulator-Profil: {0} Konsolen, {1} Emulatoren konfiguriert" -f $profile.ConsoleCount, $profile.EmulatorCount) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Emulator-Kompatibilität fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ── Konvertierung & Hashing ────────────────────────────────────────────

  # Konvertierungs-Pipeline (MF-06)
  if ($Ctx.ContainsKey('btnConversionPipeline') -and $Ctx['btnConversionPipeline']) {
    $Ctx['btnConversionPipeline'].add_Click({
      try {
        $pipeline = New-ConversionPipeline
        Add-WpfLogLine -Ctx $Ctx -Line ("Konvertierungs-Pipeline erstellt: {0} Schritte" -f $pipeline.Steps.Count) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Konvertierungs-Pipeline fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # NKit-Konvertierung (MF-07)
  if ($Ctx.ContainsKey('btnNKitConvert') -and $Ctx['btnNKitConvert']) {
    $Ctx['btnNKitConvert'].add_Click({
      try {
        $filePath = Show-WpfTextInputDialog -Window $Window -Title 'NKit-Konvertierung' -Prompt 'NKit-Dateipfad eingeben:' -DefaultValue ''
        if ([string]::IsNullOrWhiteSpace($filePath)) { return }
        $result = Invoke-NKitConversion -Path $filePath -Mode 'DryRun'
        Add-WpfLogLine -Ctx $Ctx -Line ("NKit: {0} → {1}" -f $result.SourceFormat, $result.TargetFormat) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("NKit-Konvertierung fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Konvert-Warteschlange (MF-08)
  if ($Ctx.ContainsKey('btnConvertQueue') -and $Ctx['btnConvertQueue']) {
    $Ctx['btnConvertQueue'].add_Click({
      try {
        $queue = Invoke-RunConvertQueueService -Operation 'Create' -Items @() -Ports @{}
        Add-WpfLogLine -Ctx $Ctx -Line ("Konvert-Warteschlange: {0} Einträge" -f $queue.Items.Count) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Konvert-Warteschlange fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Konvertierung verifizieren (MF-09)
  if ($Ctx.ContainsKey('btnConversionVerify') -and $Ctx['btnConversionVerify']) {
    $Ctx['btnConversionVerify'].add_Click({
      try {
        $roots = Get-AppStateValue 'RootPaths'
        if (-not $roots) { Add-WpfLogLine -Ctx $Ctx -Line 'Konvertierungs-Verifizierung: Keine Roots.' -Level 'WARNING'; return }
        $result = Invoke-BatchVerify -Roots $roots
        Add-WpfLogLine -Ctx $Ctx -Line ("Verifizierung: {0} OK, {1} fehlerhaft" -f $result.Passed, $result.Failed) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Konvertierungs-Verifizierung fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Format-Priorität (MF-10)
  if ($Ctx.ContainsKey('btnFormatPriority') -and $Ctx['btnFormatPriority']) {
    $Ctx['btnFormatPriority'].add_Click({
      try {
        $priorities = Get-FormatPriority
        foreach ($p in $priorities | Select-Object -First 10) {
          Add-WpfLogLine -Ctx $Ctx -Line ("  {0}: Priorität {1}" -f $p.Format, $p.Priority) -Level 'INFO'
        }
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Format-Priorität fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Parallel-Hashing (MF-14)
  if ($Ctx.ContainsKey('btnParallelHashing') -and $Ctx['btnParallelHashing']) {
    $Ctx['btnParallelHashing'].add_Click({
      try {
        $roots = Get-AppStateValue 'RootPaths'
        if (-not $roots) { Add-WpfLogLine -Ctx $Ctx -Line 'Parallel-Hashing: Keine Roots.' -Level 'WARNING'; return }
        $files = foreach ($r in $roots) {
          if (Test-Path -LiteralPath $r -PathType Container) {
            Get-ChildItem -LiteralPath $r -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 100 -ExpandProperty FullName
          }
        }
        if (-not $files) { Add-WpfLogLine -Ctx $Ctx -Line 'Parallel-Hashing: Keine Dateien.' -Level 'INFO'; return }
        $result = Invoke-RunParallelHashService -Files @($files) -Algorithm 'SHA1' -Progress { param($m) Add-WpfLogLine -Ctx $Ctx -Line $m -Level 'INFO' } -Ports @{}
        $count = ($result | Measure-Object).Count
        Add-WpfLogLine -Ctx $Ctx -Line ("Parallel-Hashing: {0} Dateien gehasht" -f $count) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Parallel-Hashing fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # GPU-Hashing (XL-09)
  if ($Ctx.ContainsKey('btnGpuHashing') -and $Ctx['btnGpuHashing']) {
    $Ctx['btnGpuHashing'].add_Click({
      try {
        $available = Test-GpuHashingAvailable
        $status = if ($available) { 'GPU-Hashing verfügbar und aktiv' } else { 'GPU-Hashing nicht verfügbar (Fallback: CPU)' }
        Add-WpfLogLine -Ctx $Ctx -Line $status -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("GPU-Hashing fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ── DAT & Verifizierung ───────────────────────────────────────────────

  # DAT Auto-Update (MF-11)
  if ($Ctx.ContainsKey('btnDatAutoUpdate') -and $Ctx['btnDatAutoUpdate']) {
    $Ctx['btnDatAutoUpdate'].add_Click({
      try {
        Add-WpfLogLine -Ctx $Ctx -Line 'DAT Auto-Update: Prüfe auf Aktualisierungen...' -Level 'INFO'
        $updates = Test-DatUpdateAvailable
        if ($updates -and $updates.Count -gt 0) {
          Add-WpfLogLine -Ctx $Ctx -Line ("DAT-Updates verfügbar: {0} Quellen" -f $updates.Count) -Level 'INFO'
          foreach ($u in $updates | Select-Object -First 5) {
            Add-WpfLogLine -Ctx $Ctx -Line ("  {0}: {1}" -f $u.Source, $u.Status) -Level 'INFO'
          }
        } else {
          Add-WpfLogLine -Ctx $Ctx -Line 'DAT Auto-Update: Alle DATs sind aktuell.' -Level 'INFO'
        }
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("DAT Auto-Update fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # DAT-Diff-Viewer (MF-12)
  if ($Ctx.ContainsKey('btnDatDiffViewer') -and $Ctx['btnDatDiffViewer']) {
    $Ctx['btnDatDiffViewer'].add_Click({
      try {
        $datRoot = Get-AppStateValue 'DatRoot'
        if ([string]::IsNullOrWhiteSpace($datRoot)) { Add-WpfLogLine -Ctx $Ctx -Line 'DAT-Diff: Kein DAT-Root konfiguriert.' -Level 'WARNING'; return }
        $diff = Compare-DatVersions -DatRoot $datRoot
        Add-WpfLogLine -Ctx $Ctx -Line ("DAT-Diff: +{0} neu, -{1} entfernt, ~{2} geändert" -f $diff.Added, $diff.Removed, $diff.Changed) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("DAT-Diff fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # TOSEC-DAT (MF-13)
  if ($Ctx.ContainsKey('btnTosecDat') -and $Ctx['btnTosecDat']) {
    $Ctx['btnTosecDat'].add_Click({
      try {
        $filePath = Show-WpfTextInputDialog -Window $Window -Title 'TOSEC-DAT' -Prompt 'TOSEC-DAT-Dateipfad eingeben:' -DefaultValue ''
        if ([string]::IsNullOrWhiteSpace($filePath)) { return }
        $result = ConvertFrom-TosecDat -Path $filePath
        $count = ($result | Measure-Object).Count
        Add-WpfLogLine -Ctx $Ctx -Line ("TOSEC-DAT: {0} Einträge importiert" -f $count) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("TOSEC-DAT fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Custom-DAT-Editor (LF-09)
  if ($Ctx.ContainsKey('btnCustomDatEditor') -and $Ctx['btnCustomDatEditor']) {
    $Ctx['btnCustomDatEditor'].add_Click({
      try {
        $dat = New-CustomDat
        Add-WpfLogLine -Ctx $Ctx -Line ("Custom-DAT erstellt: {0}" -f $dat.Name) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Custom-DAT-Editor fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Hash-Datenbank (LF-11)
  if ($Ctx.ContainsKey('btnHashDatabaseExport') -and $Ctx['btnHashDatabaseExport']) {
    $Ctx['btnHashDatabaseExport'].add_Click({
      try {
        $roots = Get-AppStateValue 'RootPaths'
        if (-not $roots) { Add-WpfLogLine -Ctx $Ctx -Line 'Hash-DB: Keine Roots.' -Level 'WARNING'; return }
        $db = New-HashDatabase -Roots $roots
        Add-WpfLogLine -Ctx $Ctx -Line ("Hash-Datenbank: {0} Einträge exportiert" -f $db.EntryCount) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Hash-Datenbank fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ── Sammlungsverwaltung ────────────────────────────────────────────────

  # Smart Collection (MF-05)
  if ($Ctx.ContainsKey('btnCollectionManager') -and $Ctx['btnCollectionManager']) {
    $Ctx['btnCollectionManager'].add_Click({
      try {
        $name = Show-WpfTextInputDialog -Window $Window -Title 'Smart Collection' -Prompt 'Name der Sammlung:' -DefaultValue 'Meine Sammlung'
        if ([string]::IsNullOrWhiteSpace($name)) { return }
        $collection = New-SmartCollection -Name $name
        Add-WpfLogLine -Ctx $Ctx -Line ("Smart Collection '{0}' erstellt" -f $collection.Name) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Smart Collection fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Clone-Liste (LF-10)
  if ($Ctx.ContainsKey('btnCloneListViewer') -and $Ctx['btnCloneListViewer']) {
    $Ctx['btnCloneListViewer'].add_Click({
      try {
        $datIndex = Get-AppStateValue 'DatIndex'
        if (-not $datIndex) { Add-WpfLogLine -Ctx $Ctx -Line 'Clone-Liste: Kein DAT-Index geladen.' -Level 'WARNING'; return }
        $tree = Build-CloneTree -DatIndex $datIndex
        $count = ($tree | Measure-Object).Count
        Add-WpfLogLine -Ctx $Ctx -Line ("Clone-Baum: {0} Parent-Einträge" -f $count) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Clone-Liste fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Cover-Scraper (LF-01)
  if ($Ctx.ContainsKey('btnCoverScraper') -and $Ctx['btnCoverScraper']) {
    $Ctx['btnCoverScraper'].add_Click({
      try {
        $config = New-CoverScraperConfig
        Add-WpfLogLine -Ctx $Ctx -Line ("Cover-Scraper konfiguriert: Provider={0}" -f $config.Provider) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Cover-Scraper fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Genre-Klassifikation (LF-02)
  if ($Ctx.ContainsKey('btnGenreClassification') -and $Ctx['btnGenreClassification']) {
    $Ctx['btnGenreClassification'].add_Click({
      try {
        $taxonomy = Get-GenreTaxonomy
        $count = ($taxonomy | Measure-Object).Count
        Add-WpfLogLine -Ctx $Ctx -Line ("Genre-Taxonomie: {0} Genres verfügbar" -f $count) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Genre-Klassifikation fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Spielzeit-Tracker (LF-04)
  if ($Ctx.ContainsKey('btnPlaytimeTracker') -and $Ctx['btnPlaytimeTracker']) {
    $Ctx['btnPlaytimeTracker'].add_Click({
      try {
        $logPath = Show-WpfTextInputDialog -Window $Window -Title 'Spielzeit-Tracker' -Prompt 'RetroArch-Log-Pfad eingeben:' -DefaultValue ''
        if ([string]::IsNullOrWhiteSpace($logPath)) { return }
        $data = Import-RetroArchPlaytime -LogPath $logPath
        $count = ($data | Measure-Object).Count
        Add-WpfLogLine -Ctx $Ctx -Line ("Spielzeit: {0} Einträge importiert" -f $count) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Spielzeit-Tracker fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Sammlung teilen (XL-08)
  if ($Ctx.ContainsKey('btnCollectionSharing') -and $Ctx['btnCollectionSharing']) {
    $Ctx['btnCollectionSharing'].add_Click({
      try {
        $config = New-CollectionExportConfig
        Add-WpfLogLine -Ctx $Ctx -Line ("Sammlungs-Export konfiguriert: Format={0}" -f $config.Format) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Sammlung teilen fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Virtuelle Ordner (LF-12)
  if ($Ctx.ContainsKey('btnVirtualFolderPreview') -and $Ctx['btnVirtualFolderPreview']) {
    $Ctx['btnVirtualFolderPreview'].add_Click({
      try {
        $roots = Get-AppStateValue 'RootPaths'
        if (-not $roots) { Add-WpfLogLine -Ctx $Ctx -Line 'Virtuelle Ordner: Keine Roots.' -Level 'WARNING'; return }
        $data = Build-TreemapData -Roots $roots
        Add-WpfLogLine -Ctx $Ctx -Line ("Treemap: {0} Knoten, {1:N0} MB gesamt" -f $data.NodeCount, ($data.TotalBytes / 1MB)) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Virtuelle Ordner fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ── Sicherheit & Integrität ────────────────────────────────────────────

  # Integritäts-Monitor (MF-24)
  if ($Ctx.ContainsKey('btnIntegrityMonitor') -and $Ctx['btnIntegrityMonitor']) {
    $Ctx['btnIntegrityMonitor'].add_Click({
      try {
        $roots = Get-AppStateValue 'RootPaths'
        if (-not $roots) { Add-WpfLogLine -Ctx $Ctx -Line 'Integrität: Keine Roots.' -Level 'WARNING'; return }
        $files = foreach ($r in $roots) {
          if (Test-Path -LiteralPath $r -PathType Container) {
            Get-ChildItem -LiteralPath $r -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 200
          }
        }
        $baseline = Invoke-RunIntegrityCheckService -Operation 'Baseline' -Files @($files) -Ports @{}
        Add-WpfLogLine -Ctx $Ctx -Line ("Integrität: Baseline mit {0} Dateien erstellt" -f $baseline.FileCount) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Integritäts-Monitor fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Backup-Manager (MF-25)
  if ($Ctx.ContainsKey('btnBackupManager') -and $Ctx['btnBackupManager']) {
    $Ctx['btnBackupManager'].add_Click({
      try {
        $config = New-BackupConfig
        $session = Invoke-RunBackupService -Operation 'Create' -Config $config -Label 'GUI-Backup' -Ports @{}
        Add-WpfLogLine -Ctx $Ctx -Line ("Backup-Session erstellt: {0}" -f $session.SessionId) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Backup-Manager fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Quarantäne (MF-26)
  if ($Ctx.ContainsKey('btnQuarantine') -and $Ctx['btnQuarantine']) {
    $Ctx['btnQuarantine'].add_Click({
      try {
        $filePath = Show-WpfTextInputDialog -Window $Window -Title 'Quarantäne' -Prompt 'Dateipfad für Quarantäne:' -DefaultValue ''
        if ([string]::IsNullOrWhiteSpace($filePath)) { return }
        $qRoot = Join-Path (Get-AppStateValue 'TrashRoot') 'quarantine'
        $result = Invoke-RunQuarantineService -SourcePath $filePath -QuarantineRoot $qRoot -Reasons @('ManualReview') -Mode 'DryRun' -Ports @{}
        Add-WpfLogLine -Ctx $Ctx -Line ("Quarantäne (DryRun): {0} → {1}" -f $result.Source, $result.Destination) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Quarantäne fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Regel-Engine (MF-19)
  if ($Ctx.ContainsKey('btnRuleEngine') -and $Ctx['btnRuleEngine']) {
    $Ctx['btnRuleEngine'].add_Click({
      try {
        Add-WpfLogLine -Ctx $Ctx -Line 'Regel-Engine: Lade Standard-Regeln...' -Level 'INFO'
        $rules = Get-AppStateValue 'Rules'
        if (-not $rules) { $rules = @() }
        Add-WpfLogLine -Ctx $Ctx -Line ("Regel-Engine: {0} Regeln geladen" -f ($rules | Measure-Object).Count) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Regel-Engine fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Patch-Engine (LF-05)
  if ($Ctx.ContainsKey('btnPatchEngine') -and $Ctx['btnPatchEngine']) {
    $Ctx['btnPatchEngine'].add_Click({
      try {
        $patchPath = Show-WpfTextInputDialog -Window $Window -Title 'Patch-Engine' -Prompt 'Patch-Datei (.ips/.ups/.bps):' -DefaultValue ''
        if ([string]::IsNullOrWhiteSpace($patchPath)) { return }
        $format = Test-PatchFormat -Path $patchPath
        Add-WpfLogLine -Ctx $Ctx -Line ("Patch-Format erkannt: {0}" -f $format.Format) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Patch-Engine fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Header-Reparatur (LF-06)
  if ($Ctx.ContainsKey('btnHeaderRepair') -and $Ctx['btnHeaderRepair']) {
    $Ctx['btnHeaderRepair'].add_Click({
      try {
        $filePath = Show-WpfTextInputDialog -Window $Window -Title 'Header-Reparatur' -Prompt 'ROM-Dateipfad:' -DefaultValue ''
        if ([string]::IsNullOrWhiteSpace($filePath)) { return }
        $result = Repair-NesHeader -Path $filePath -Mode 'DryRun'
        Add-WpfLogLine -Ctx $Ctx -Line ("Header-Reparatur (DryRun): {0}" -f $result.Status) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Header-Reparatur fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ── Workflow & Automatisierung ─────────────────────────────────────────

  # Command-Palette (MF-15)
  if ($Ctx.ContainsKey('btnCommandPalette') -and $Ctx['btnCommandPalette']) {
    $Ctx['btnCommandPalette'].add_Click({
      try {
        $query = Show-WpfTextInputDialog -Window $Window -Title 'Command-Palette' -Prompt 'Befehl suchen:' -DefaultValue ''
        if ([string]::IsNullOrWhiteSpace($query)) { return }
        $results = Search-PaletteCommands -Query $query
        $count = ($results | Measure-Object).Count
        Add-WpfLogLine -Ctx $Ctx -Line ("Command-Palette: {0} Treffer für '{1}'" -f $count, $query) -Level 'INFO'
        foreach ($cmd in $results | Select-Object -First 10) {
          Add-WpfLogLine -Ctx $Ctx -Line ("  {0}: {1}" -f $cmd.Name, $cmd.Description) -Level 'INFO'
        }
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Command-Palette fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Split-Panel (MF-16)
  if ($Ctx.ContainsKey('btnSplitPanelPreview') -and $Ctx['btnSplitPanelPreview']) {
    $Ctx['btnSplitPanelPreview'].add_Click({
      try {
        $reportsDir = Join-Path $PSScriptRoot 'reports'
        $latestPlan = Get-ChildItem -Path $reportsDir -Filter 'move-plan-*.json' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if (-not $latestPlan) { Add-WpfLogLine -Ctx $Ctx -Line 'Split-Panel: Kein Move-Plan vorhanden.' -Level 'WARNING'; return }
        $data = ConvertTo-SplitPanelData -PlanPath $latestPlan.FullName
        Add-WpfLogLine -Ctx $Ctx -Line ("Split-Panel: {0} Einträge geladen" -f $data.EntryCount) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Split-Panel fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Filter-Builder (MF-17)
  if ($Ctx.ContainsKey('btnFilterBuilder') -and $Ctx['btnFilterBuilder']) {
    $Ctx['btnFilterBuilder'].add_Click({
      try {
        $filter = New-FilterCondition
        Add-WpfLogLine -Ctx $Ctx -Line ("Filter-Builder: Neue Filterbedingung erstellt (Typ={0})" -f $filter.Type) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Filter-Builder fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Sort-Templates (MF-22)
  if ($Ctx.ContainsKey('btnSortTemplates') -and $Ctx['btnSortTemplates']) {
    $Ctx['btnSortTemplates'].add_Click({
      try {
        $templates = Get-DefaultSortTemplates
        $count = ($templates | Measure-Object).Count
        Add-WpfLogLine -Ctx $Ctx -Line ("Sort-Templates: {0} Vorlagen verfügbar" -f $count) -Level 'INFO'
        foreach ($t in $templates | Select-Object -First 5) {
          Add-WpfLogLine -Ctx $Ctx -Line ("  {0}: {1}" -f $t.Name, $t.Description) -Level 'INFO'
        }
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Sort-Templates fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Pipeline-Engine (MF-20)
  if ($Ctx.ContainsKey('btnPipelineEngine') -and $Ctx['btnPipelineEngine']) {
    $Ctx['btnPipelineEngine'].add_Click({
      try {
        $step = New-PipelineStep
        Add-WpfLogLine -Ctx $Ctx -Line ("Pipeline-Step erstellt: {0}" -f $step.Name) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Pipeline-Engine fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # System-Tray (MF-18)
  if ($Ctx.ContainsKey('btnSystemTray') -and $Ctx['btnSystemTray']) {
    $Ctx['btnSystemTray'].add_Click({
      try {
        $config = New-TrayIconConfig
        Add-WpfLogLine -Ctx $Ctx -Line ("System-Tray konfiguriert: Icon={0}" -f $config.IconPath) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("System-Tray fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Scheduler (MF-23)
  if ($Ctx.ContainsKey('btnSchedulerAdvanced') -and $Ctx['btnSchedulerAdvanced']) {
    $Ctx['btnSchedulerAdvanced'].add_Click({
      try {
        $entry = New-ScheduleEntry
        Add-WpfLogLine -Ctx $Ctx -Line ("Scheduler: Neuer Eintrag erstellt ({0})" -f $entry.Cron) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Scheduler fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Regel-Pakete (LF-19)
  if ($Ctx.ContainsKey('btnRulePackSharing') -and $Ctx['btnRulePackSharing']) {
    $Ctx['btnRulePackSharing'].add_Click({
      try {
        $pack = New-RulePack
        Add-WpfLogLine -Ctx $Ctx -Line ("Regel-Paket erstellt: {0}" -f $pack.Name) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Regel-Pakete fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Arcade Merge/Split (LF-07)
  if ($Ctx.ContainsKey('btnArcadeMergeSplit') -and $Ctx['btnArcadeMergeSplit']) {
    $Ctx['btnArcadeMergeSplit'].add_Click({
      try {
        $types = Get-ArcadeSetTypes
        $count = ($types | Measure-Object).Count
        Add-WpfLogLine -Ctx $Ctx -Line ("Arcade Set-Typen: {0} Typen verfügbar" -f $count) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Arcade Merge/Split fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ── Export & Integration ───────────────────────────────────────────────

  # PDF-Report (LF-14)
  if ($Ctx.ContainsKey('btnPdfReport') -and $Ctx['btnPdfReport']) {
    $Ctx['btnPdfReport'].add_Click({
      try {
        $config = New-PdfReportConfig
        Add-WpfLogLine -Ctx $Ctx -Line ("PDF-Report konfiguriert: Template={0}" -f $config.Template) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("PDF-Report fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Launcher-Integration (LF-03)
  if ($Ctx.ContainsKey('btnLauncherIntegration') -and $Ctx['btnLauncherIntegration']) {
    $Ctx['btnLauncherIntegration'].add_Click({
      try {
        $formats = Get-SupportedLauncherFormats
        $count = ($formats | Measure-Object).Count
        Add-WpfLogLine -Ctx $Ctx -Line ("Launcher-Integration: {0} Formate unterstützt" -f $count) -Level 'INFO'
        foreach ($f in $formats | Select-Object -First 5) {
          Add-WpfLogLine -Ctx $Ctx -Line ("  {0}" -f $f.Name) -Level 'INFO'
        }
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Launcher-Integration fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Tool-Import (XL-12)
  if ($Ctx.ContainsKey('btnToolImport') -and $Ctx['btnToolImport']) {
    $Ctx['btnToolImport'].add_Click({
      try {
        $formats = Get-SupportedImportFormats
        $count = ($formats | Measure-Object).Count
        Add-WpfLogLine -Ctx $Ctx -Line ("Tool-Import: {0} Import-Formate unterstützt" -f $count) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Tool-Import fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ── Infrastruktur & Deployment ─────────────────────────────────────────

  # Storage-Tiering (LF-08)
  if ($Ctx.ContainsKey('btnStorageTiering') -and $Ctx['btnStorageTiering']) {
    $Ctx['btnStorageTiering'].add_Click({
      try {
        $config = Get-StorageTierConfig
        Add-WpfLogLine -Ctx $Ctx -Line ("Storage-Tiering: {0} Tier(s) konfiguriert" -f $config.Tiers.Count) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Storage-Tiering fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # NAS-Optimierung (LF-15)
  if ($Ctx.ContainsKey('btnNasOptimization') -and $Ctx['btnNasOptimization']) {
    $Ctx['btnNasOptimization'].add_Click({
      try {
        $profile = New-NasProfile
        Add-WpfLogLine -Ctx $Ctx -Line ("NAS-Profil erstellt: BufferSize={0}" -f $profile.BufferSize) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("NAS-Optimierung fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # FTP-Quelle (LF-16)
  if ($Ctx.ContainsKey('btnFtpSource') -and $Ctx['btnFtpSource']) {
    $Ctx['btnFtpSource'].add_Click({
      try {
        $config = New-FtpSourceConfig
        Add-WpfLogLine -Ctx $Ctx -Line ("FTP-Quelle konfiguriert: Protokoll={0}" -f $config.Protocol) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("FTP-Quelle fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Cloud-Sync (LF-17)
  if ($Ctx.ContainsKey('btnCloudSync') -and $Ctx['btnCloudSync']) {
    $Ctx['btnCloudSync'].add_Click({
      try {
        $config = New-CloudSyncConfig
        Add-WpfLogLine -Ctx $Ctx -Line ("Cloud-Sync konfiguriert: Provider={0}" -f $config.Provider) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Cloud-Sync fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Plugin-Marktplatz (LF-18)
  if ($Ctx.ContainsKey('btnPluginMarketplaceFeature') -and $Ctx['btnPluginMarketplaceFeature']) {
    $Ctx['btnPluginMarketplaceFeature'].add_Click({
      try {
        $config = New-PluginMarketplaceConfig
        Add-WpfLogLine -Ctx $Ctx -Line ("Plugin-Marktplatz: {0} Plugins verfügbar" -f $config.AvailableCount) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Plugin-Marktplatz fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Portable Modus (QW-12)
  if ($Ctx.ContainsKey('btnPortableMode') -and $Ctx['btnPortableMode']) {
    $Ctx['btnPortableMode'].add_Click({
      try {
        $root = Get-PortableSettingsRoot
        Add-WpfLogLine -Ctx $Ctx -Line ("Portable Modus: Settings-Root = {0}" -f $root) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Portable Modus fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Docker (XL-01)
  if ($Ctx.ContainsKey('btnDockerContainer') -and $Ctx['btnDockerContainer']) {
    $Ctx['btnDockerContainer'].add_Click({
      try {
        $config = New-DockerfileConfig
        Add-WpfLogLine -Ctx $Ctx -Line ("Dockerfile generiert: Base={0}" -f $config.BaseImage) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Docker fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Mobile Web UI (XL-02)
  if ($Ctx.ContainsKey('btnMobileWebUI') -and $Ctx['btnMobileWebUI']) {
    $Ctx['btnMobileWebUI'].add_Click({
      try {
        $config = New-WebUIConfig
        Add-WpfLogLine -Ctx $Ctx -Line ("Web UI: Port={0}, Bind={1}" -f $config.Port, $config.BindAddress) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Mobile Web UI fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Windows-Kontextmenü (XL-03)
  if ($Ctx.ContainsKey('btnWindowsContextMenu') -and $Ctx['btnWindowsContextMenu']) {
    $Ctx['btnWindowsContextMenu'].add_Click({
      try {
        $entry = New-ContextMenuEntry
        Add-WpfLogLine -Ctx $Ctx -Line ("Kontextmenü-Eintrag: {0}" -f $entry.Label) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Kontextmenü fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # PSGallery (XL-04)
  if ($Ctx.ContainsKey('btnPSGallery') -and $Ctx['btnPSGallery']) {
    $Ctx['btnPSGallery'].add_Click({
      try {
        $manifest = New-PSGalleryManifest
        Add-WpfLogLine -Ctx $Ctx -Line ("PSGallery-Manifest: Version={0}" -f $manifest.ModuleVersion) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("PSGallery fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Paketmanager (XL-05)
  if ($Ctx.ContainsKey('btnPackageManager') -and $Ctx['btnPackageManager']) {
    $Ctx['btnPackageManager'].add_Click({
      try {
        $manifest = New-WingetManifest
        Add-WpfLogLine -Ctx $Ctx -Line ("Winget-Manifest: PackageId={0}" -f $manifest.PackageIdentifier) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Paketmanager fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Hardlink-Modus (XL-11)
  if ($Ctx.ContainsKey('btnHardlinkMode') -and $Ctx['btnHardlinkMode']) {
    $Ctx['btnHardlinkMode'].add_Click({
      try {
        $supported = Test-HardlinkSupported
        $status = if ($supported) { 'Hardlinks werden unterstützt' } else { 'Hardlinks nicht verfügbar (Dateisystem oder Rechte)' }
        Add-WpfLogLine -Ctx $Ctx -Line ("Hardlink-Modus: {0}" -f $status) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Hardlink-Modus fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # USN-Journal (XL-10)
  if ($Ctx.ContainsKey('btnUsnJournal') -and $Ctx['btnUsnJournal']) {
    $Ctx['btnUsnJournal'].add_Click({
      try {
        $available = Test-UsnJournalAvailable
        $status = if ($available) { 'USN-Journal verfügbar' } else { 'USN-Journal nicht verfügbar (Admin-Rechte benötigt)' }
        Add-WpfLogLine -Ctx $Ctx -Line ("USN-Journal: {0}" -f $status) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("USN-Journal fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Multi-Instanz (XL-13)
  if ($Ctx.ContainsKey('btnMultiInstanceSync') -and $Ctx['btnMultiInstanceSync']) {
    $Ctx['btnMultiInstanceSync'].add_Click({
      try {
        $identity = New-InstanceIdentity
        Add-WpfLogLine -Ctx $Ctx -Line ("Multi-Instanz: ID={0}" -f $identity.InstanceId) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Multi-Instanz fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Telemetrie (XL-14)
  if ($Ctx.ContainsKey('btnTelemetry') -and $Ctx['btnTelemetry']) {
    $Ctx['btnTelemetry'].add_Click({
      try {
        $config = New-TelemetryConfig
        $status = if ($config.Enabled) { 'aktiviert' } else { 'deaktiviert' }
        Add-WpfLogLine -Ctx $Ctx -Line ("Telemetrie: {0}" -f $status) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Telemetrie fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # ── UI & Erscheinungsbild ──────────────────────────────────────────────

  # Barrierefreiheit (LF-13)
  if ($Ctx.ContainsKey('btnAccessibility') -and $Ctx['btnAccessibility']) {
    $Ctx['btnAccessibility'].add_Click({
      try {
        $defaults = Get-AccessibilityDefaults
        Add-WpfLogLine -Ctx $Ctx -Line ("Barrierefreiheit: HighContrast={0}, FontScale={1}" -f $defaults.HighContrast, $defaults.FontScale) -Level 'INFO'
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Barrierefreiheit fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }

  # Theme-Engine (LF-20)
  if ($Ctx.ContainsKey('btnThemeEngine') -and $Ctx['btnThemeEngine']) {
    $Ctx['btnThemeEngine'].add_Click({
      try {
        $themes = Get-BuiltinThemes
        $count = ($themes | Measure-Object).Count
        Add-WpfLogLine -Ctx $Ctx -Line ("Theme-Engine: {0} Themes verfügbar" -f $count) -Level 'INFO'
        foreach ($t in $themes | Select-Object -First 5) {
          Add-WpfLogLine -Ctx $Ctx -Line ("  {0}" -f $t.Name) -Level 'INFO'
        }
      } catch {
        Add-WpfLogLine -Ctx $Ctx -Line ("Theme-Engine fehlgeschlagen: {0}" -f $_.Exception.Message) -Level 'ERROR'
      }
    }.GetNewClosure())
  }
}
