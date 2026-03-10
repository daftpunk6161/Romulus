function Get-RomCleanupModuleFiles {
  param(
    [ValidateSet('all','core','wpf')]
    [string]$Profile = 'all'
  )

  $coreModules = @(
    'Settings.ps1',
    'ConfigProfiles.ps1',
    'ConfigMerge.ps1',
    'AppState.ps1',

    'Compatibility.ps1',
    'Localization.ps1',
    'LruCache.ps1',
    'DataContracts.ps1',
    'ErrorContracts.ps1',
    'UseCaseContracts.ps1',
    'Tools.ps1',
    'FileOps.ps1',
    'Report.ps1',
    'FormatScoring.ps1',
    'SetParsing.ps1',
    'FolderDedupe.ps1',
    'ZipSort.ps1',
    'Sets.ps1',
    'Core.ps1',
    'Classification.ps1',
    'RunspaceLifecycle.ps1',
    'ConsolePlugins.ps1',
    'Convert.ps1',
    'Dedupe.ps1',
    'Dat.ps1',
    'DatSources.ps1',
    'RunIndex.ps1',
    'SafetyToolsService.ps1',
    'DiagnosticsService.ps1',

    'ReportBuilder.ps1',
    'RunHelpers.ps1',
    'PortInterfaces.ps1',
    'ApplicationServices.ps1',
    'OperationAdapters.ps1',
    'ApiServer.ps1',
    'ConsoleSort.ps1',
    'Scheduler.ps1',
    'Notifications.ps1',
    'UpdateCheck.ps1',
    'EventBus.ps1',
    'Logging.ps1',
    'CatchGuard.ps1',
    'MemoryGuard.ps1',
    'PhaseMetrics.ps1',
    'SecurityEventStream.ps1',
    'AppStateSchema.ps1',
    'BackgroundOps.ps1',
    'OpsBundle.ps1',

    # --- Phase 1 Quick-Win Module (QW-01 bis QW-16) ---
    'DatRename.ps1',
    'EcmDecompress.ps1',
    'ArchiveRepack.ps1',
    'ConversionEstimate.ps1',
    'JunkReport.ps1',
    'KeyboardShortcuts.ps1',
    'ThemeManager.ps1',
    'RomFilter.ps1',
    'DuplicateHeatmap.ps1',
    'CliExport.ps1',
    'WebhookNotification.ps1',
    'PortableMode.ps1',
    'CollectionCsvExport.ps1',
    'RunHistory.ps1',
    'M3uGenerator.ps1',
    'RetroArchPlaylist.ps1',

    # --- Phase 2 Medium-Feature Module (MF-01 bis MF-26) ---
    'MissingRomTracker.ps1',
    'CrossRootDedupe.ps1',
    'HeaderAnalysis.ps1',
    'CompletenessTracker.ps1',
    'CollectionManager.ps1',
    'ConversionPipeline.ps1',
    'NKitConvert.ps1',
    'ConvertQueue.ps1',
    'ConversionVerify.ps1',
    'FormatPriority.ps1',
    'DatAutoUpdate.ps1',
    'DatDiffViewer.ps1',
    'TosecDatSupport.ps1',
    'ParallelHashing.ps1',
    'CommandPalette.ps1',
    'SplitPanelPreview.ps1',
    'FilterBuilder.ps1',
    'SystemTray.ps1',
    'RuleEngine.ps1',
    'PipelineEngine.ps1',
    'DryRunCompare.ps1',
    'SortTemplates.ps1',
    'SchedulerAdvanced.ps1',
    'IntegrityMonitor.ps1',
    'BackupManager.ps1',
    'Quarantine.ps1',

    # --- Phase 3 Large-Feature Module (LF-01 bis LF-20) ---
    'CoverScraper.ps1',
    'GenreClassification.ps1',
    'LauncherIntegration.ps1',
    'PlaytimeTracker.ps1',
    'PatchEngine.ps1',
    'HeaderRepair.ps1',
    'ArcadeMergeSplit.ps1',
    'StorageTiering.ps1',
    'CustomDatEditor.ps1',
    'CloneListViewer.ps1',
    'HashDatabaseExport.ps1',
    'VirtualFolderPreview.ps1',
    'Accessibility.ps1',
    'PdfReportExport.ps1',
    'NasOptimization.ps1',
    'FtpSource.ps1',
    'CloudSettingsSync.ps1',
    'PluginMarketplace.ps1',
    'RulePackSharing.ps1',
    'ThemeEngine.ps1',

    # --- Phase 4 Extra-Large-Feature Module (XL-01 bis XL-14) ---
    'DockerContainer.ps1',
    'MobileWebUI.ps1',
    'WindowsContextMenu.ps1',
    'PSGalleryModule.ps1',
    'PackageManagerIntegration.ps1',
    'TrendAnalysis.ps1',
    'EmulatorCompatReport.ps1',
    'CollectionSharing.ps1',
    'GpuHashing.ps1',
    'UsnJournalScan.ps1',
    'HardlinkMode.ps1',
    'ToolImport.ps1',
    'MultiInstanceSync.ps1',
    'Telemetry.ps1'
  )

  $wpfModules = @(
    'WpfXaml.ps1',
    'WpfHost.ps1',
    'WpfSelectionConfig.ps1',
    'WpfMainViewModel.ps1',
    'WpfSlice.Roots.ps1',
    'WpfSlice.RunControl.ps1',
    'WpfSlice.Settings.ps1',
    'WpfSlice.DatMapping.ps1',
    'WpfSlice.ReportPreview.ps1',
    'WpfSlice.AdvancedFeatures.ps1',
    'WpfWizard.ps1',
    'WpfEventHandlers.ps1',
    'SimpleSort.WpfMain.ps1',
    'WpfApp.ps1'
  )

  switch ($Profile) {
    'core' { return $coreModules }
    'wpf' { return $wpfModules }
    default { return @($coreModules + $wpfModules) }
  }
}

function Get-RomCleanupFeatureModuleFiles {
  param([Parameter(Mandatory)][string]$Feature)

  switch ($Feature.ToLowerInvariant()) {
    'wpf' { return (Get-RomCleanupModuleFiles -Profile 'wpf') }
    default { return @() }
  }
}

function Get-RomCleanupModuleDependencies {
  $deps = [ordered]@{}
  $deps['WpfHost.ps1'] = @('WpfXaml.ps1')
  $deps['WpfMainViewModel.ps1'] = @('WpfHost.ps1')
  $deps['WpfSlice.Roots.ps1'] = @('WpfMainViewModel.ps1')
  $deps['WpfSlice.RunControl.ps1'] = @('WpfSlice.Roots.ps1')
  $deps['WpfSlice.Settings.ps1'] = @('WpfSlice.RunControl.ps1')
  $deps['WpfSlice.DatMapping.ps1'] = @('WpfMainViewModel.ps1')
  $deps['WpfSlice.ReportPreview.ps1'] = @('WpfMainViewModel.ps1')
  $deps['WpfSlice.AdvancedFeatures.ps1'] = @('WpfMainViewModel.ps1')
  $deps['WpfWizard.ps1'] = @('WpfHost.ps1')
  $deps['WpfEventHandlers.ps1'] = @('WpfSelectionConfig.ps1','WpfMainViewModel.ps1','WpfSlice.Roots.ps1','WpfSlice.RunControl.ps1','WpfSlice.Settings.ps1','WpfSlice.DatMapping.ps1','WpfSlice.ReportPreview.ps1','WpfSlice.AdvancedFeatures.ps1','WpfWizard.ps1')
  $deps['SimpleSort.WpfMain.ps1'] = @('WpfHost.ps1','WpfMainViewModel.ps1','WpfEventHandlers.ps1')
  $deps['WpfApp.ps1'] = @('SimpleSort.WpfMain.ps1')

  # Application-Layer dependencies
  $deps['ApplicationServices.ps1'] = @('PortInterfaces.ps1')
  $deps['OperationAdapters.ps1'] = @('ApplicationServices.ps1')

  # Settings split modules
  $deps['ConfigProfiles.ps1'] = @('Settings.ps1')
  $deps['ConfigMerge.ps1'] = @('Settings.ps1', 'ConfigProfiles.ps1')
  $deps['AppState.ps1'] = @('Settings.ps1')

  # Infrastructure modules with implicit dependencies
  $deps['MemoryGuard.ps1'] = @('Settings.ps1', 'AppState.ps1')
  $deps['PhaseMetrics.ps1'] = @('Settings.ps1', 'AppState.ps1')

  return $deps
}

function Resolve-RomCleanupModuleOrder {
  param([Parameter(Mandatory)][string[]]$ModuleFiles)

  $deps = Get-RomCleanupModuleDependencies
  $requested = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
  foreach ($module in $ModuleFiles) {
    if (-not [string]::IsNullOrWhiteSpace([string]$module)) {
      [void]$requested.Add([string]$module)
    }
  }

  $result = New-Object System.Collections.Generic.List[string]
  $visited = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
  $stack = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

  $visit = {
    param([string]$moduleName)

    if ([string]::IsNullOrWhiteSpace($moduleName)) { return }
    if ($visited.Contains($moduleName)) { return }
    if ($stack.Contains($moduleName)) {
      throw ("Zyklische Modulabhängigkeit erkannt: {0}" -f $moduleName)
    }

    [void]$stack.Add($moduleName)

    $depList = @()
    if ($deps.Contains($moduleName)) {
      $depList = @($deps[$moduleName])
    }

    foreach ($dep in $depList) {
      if (-not $requested.Contains([string]$dep)) {
        throw ("Fehlende Modulabhängigkeit: {0} benötigt {1}" -f $moduleName, $dep)
      }
      & $visit ([string]$dep)
    }

    [void]$stack.Remove($moduleName)
    [void]$visited.Add($moduleName)
    [void]$result.Add($moduleName)
  }

  foreach ($module in $ModuleFiles) {
    & $visit ([string]$module)
  }

  return @($result)
}
