function Resolve-ApplicationPorts {
  <# Resolve optional ports; fallback to default OperationPorts when available. #>
  param([hashtable]$Ports)

  if ($Ports) { return $Ports }
  if (Get-Command New-OperationPorts -ErrorAction SilentlyContinue) {
    return (New-OperationPorts)
  }
  return @{}
}

function Test-ApplicationPortKeys {
  <# Validate required port keys when a port map is supplied. #>
  param(
    [hashtable]$Ports,
    [string[]]$RequiredKeys
  )

  if (-not $Ports -or $Ports.Count -eq 0 -or -not $RequiredKeys) { return }
  foreach ($key in $RequiredKeys) {
    if (-not $Ports.ContainsKey($key) -or -not $Ports[$key]) {
      throw "Port-Contract verletzt: '$key' fehlt."
    }
  }
}

function Invoke-RunDedupeService {
  <# Application service facade for core region dedupe runs. #>
  param(
    [Parameter(Mandatory = $true)][hashtable]$Parameters,
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports
  Test-ApplicationPortKeys -Ports $activePorts -RequiredKeys @('FileSystem','ToolRunner','DatRepository','AuditStore')

  if ($activePorts.ContainsKey('RegionDedupe') -and $activePorts['RegionDedupe']) {
    $regionDedupePort = $activePorts['RegionDedupe']
    if (($regionDedupePort.PSObject.Properties.Name -contains 'Invoke') -and $regionDedupePort.Invoke) {
      return (& $regionDedupePort.Invoke $Parameters)
    }
  }

  return (Invoke-RegionDedupe @Parameters)
}

function Invoke-RunSortService {
  <# Application service facade for optional pre-dedupe console sorting. #>
  param(
    [bool]$Enabled,
    [string]$Mode,
    [string[]]$Roots,
    [string[]]$Extensions,
    [bool]$UseDat,
    [string]$DatRoot,
    [string]$DatHashType,
    [hashtable]$DatMap,
    [hashtable]$ToolOverrides,
    [ValidateSet('None','PS1PS2')][string]$ZipSortStrategy = 'None',
    [scriptblock]$Log,
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports
  Test-ApplicationPortKeys -Ports $activePorts -RequiredKeys @('FileSystem','ToolRunner')

  return (Invoke-OptionalConsoleSort -Enabled $Enabled -Mode $Mode -Roots $Roots -Extensions $Extensions -UseDat $UseDat -DatRoot $DatRoot -DatHashType $DatHashType -DatMap $DatMap -ToolOverrides $ToolOverrides -ZipSortStrategy $ZipSortStrategy -Log $Log)
}

function Invoke-RunConversionService {
  <# Application service facade for conversion operations. #>
  param(
    [ValidateSet('WinnerMove', 'Preview', 'Standalone')]
    [string]$Operation,
    [hashtable]$Parameters = @{},
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports
  Test-ApplicationPortKeys -Ports $activePorts -RequiredKeys @('FileSystem','ToolRunner')

  switch ($Operation) {
    'WinnerMove' {
      return (Invoke-WinnerConversionMove @Parameters)
    }
    'Preview' {
      return (Invoke-ConvertPreviewDryRun @Parameters)
    }
    'Standalone' {
      return (Invoke-StandaloneConversion @Parameters)
    }
    default {
      throw "Unbekannte Conversion-Operation: $Operation"
    }
  }
}

function Invoke-RunRollbackService {
  <# Application service facade for audit rollback operations. #>
  param(
    [Parameter(Mandatory = $true)][hashtable]$Parameters,
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports
  Test-ApplicationPortKeys -Ports $activePorts -RequiredKeys @('AuditStore')

  $auditStore = $null
  if ($activePorts -and $activePorts.ContainsKey('AuditStore')) {
    $auditStore = $activePorts['AuditStore']
  }
  if ($auditStore -and ($auditStore.PSObject.Properties.Name -contains 'Rollback') -and $auditStore.Rollback) {
    return (& $auditStore.Rollback @Parameters)
  }

  return (Invoke-AuditRollback @Parameters)
}

function Invoke-RunFolderDedupeService {
  <# Application service facade for automatic folder-level deduplication.
     Auto-detects console type per root and dispatches to PS3 hash-based
     or base-name folder dedupe as appropriate. #>
  param(
    [Parameter(Mandatory = $true)][string[]]$Roots,
    [ValidateSet('DryRun','Move')][string]$Mode = 'DryRun',
    [string]$DupeRoot,
    [scriptblock]$Log,
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports

  if ($activePorts.ContainsKey('FolderDedupe') -and $activePorts['FolderDedupe']) {
    $port = $activePorts['FolderDedupe']
    if (($port.PSObject.Properties.Name -contains 'Invoke') -and $port.Invoke) {
      return (& $port.Invoke @{ Roots = $Roots; Mode = $Mode; DupeRoot = $DupeRoot; Log = $Log })
    }
  }

  return (Invoke-AutoFolderDedupe -Roots $Roots -Mode $Mode -DupeRoot $DupeRoot -Log $Log)
}

# ── Feature-Service-Facades (PRD P0/P1) ──────────────────────────────────────

function Invoke-RunDatRenameService {
  <# Application service facade for DAT-based ROM renaming (ISS-003/FR-001). #>
  param(
    [ValidateSet('Preview','Execute')]
    [string]$Operation = 'Preview',
    [Parameter(Mandatory)][string[]]$Files,
    [Parameter(Mandatory)][hashtable]$DatIndex,
    [string]$HashType = 'SHA1',
    [scriptblock]$Log,
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports
  $mode = if ($Operation -eq 'Execute') { 'Move' } else { 'DryRun' }
  $results = [System.Collections.Generic.List[object]]::new()

  foreach ($file in $Files) {
    $r = Rename-RomToDatName -FilePath $file -DatIndex $DatIndex -Mode $mode -HashType $HashType -Log $Log
    $results.Add($r)
  }

  $renamed = @($results | Where-Object { $_.Status -eq 'Renamed' -or $_.Status -eq 'WouldRename' })
  $conflicts = @($results | Where-Object { $_.Status -eq 'Conflict' })
  $noMatch = @($results | Where-Object { $_.Status -eq 'NoMatch' })

  return [pscustomobject]@{
    Operation = $Operation
    Total     = $results.Count
    Renamed   = $renamed.Count
    Conflicts = $conflicts.Count
    NoMatch   = $noMatch.Count
    Details   = @($results)
  }
}

function Invoke-RunM3uGenerationService {
  <# Application service facade for multi-disc M3U playlist generation (ISS-004/FR-004). #>
  param(
    [Parameter(Mandatory)][object[]]$Files,
    [Parameter(Mandatory)][string]$OutputDir,
    [ValidateSet('DryRun','Move')][string]$Mode = 'DryRun',
    [scriptblock]$Log,
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports
  $groups = Group-MultiDiscFiles -Files $Files
  $results = [System.Collections.Generic.List[object]]::new()

  foreach ($group in $groups.Values) {
    $gameName = $group.GameName
    $discFiles = $group.Discs
    $r = New-M3uPlaylist -DiscFiles $discFiles -OutputDir $OutputDir -GameName $gameName -Mode $Mode -Log $Log
    $results.Add($r)
  }

  return [pscustomobject]@{
    Mode      = $Mode
    Generated = @($results | Where-Object { $_.Status -eq 'Created' -or $_.Status -eq 'WouldCreate' }).Count
    Skipped   = @($results | Where-Object { $_.Status -eq 'Skipped' -or $_.Status -eq 'Exists' }).Count
    Warnings  = @($results | Where-Object { $_.Warnings.Count -gt 0 }).Count
    Details   = @($results)
  }
}

function Invoke-RunEcmDecompressService {
  <# Application service facade for ECM decompression (ISS-005/FR-002). #>
  param(
    [Parameter(Mandatory)][string[]]$Files,
    [ValidateSet('DryRun','Move')][string]$Mode = 'DryRun',
    [string]$ToolPath,
    [scriptblock]$Log,
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports
  $results = [System.Collections.Generic.List[object]]::new()

  foreach ($file in $Files) {
    if ([System.IO.Path]::GetExtension($file) -ieq '.ecm') {
      $r = Invoke-EcmDecompress -FilePath $file -Mode $Mode -ToolPath $ToolPath -Log $Log
      $results.Add($r)
    }
  }

  return [pscustomobject]@{
    Mode        = $Mode
    Total       = $results.Count
    Success     = @($results | Where-Object { $_.Status -eq 'Decompressed' -or $_.Status -eq 'WouldDecompress' }).Count
    Failed      = @($results | Where-Object { $_.Status -eq 'Failed' }).Count
    Details     = @($results)
  }
}

function Invoke-RunArchiveRepackService {
  <# Application service facade for archive repacking (ISS-006/FR-003). #>
  param(
    [Parameter(Mandatory)][string[]]$Files,
    [Parameter(Mandatory)][ValidateSet('zip','7z')][string]$TargetFormat,
    [int]$CompressionLevel = 5,
    [ValidateSet('DryRun','Move')][string]$Mode = 'DryRun',
    [scriptblock]$Log,
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports
  $results = [System.Collections.Generic.List[object]]::new()

  foreach ($file in $Files) {
    $r = Invoke-ArchiveRepack -ArchivePath $file -TargetFormat $TargetFormat -CompressionLevel $CompressionLevel -Mode $Mode -Log $Log
    $results.Add($r)
  }

  return [pscustomobject]@{
    Mode        = $Mode
    TargetFormat = $TargetFormat
    Total       = $results.Count
    Repacked    = @($results | Where-Object { $_.Status -eq 'Repacked' -or $_.Status -eq 'WouldRepack' }).Count
    Skipped     = @($results | Where-Object { $_.Status -eq 'Skipped' }).Count
    Failed      = @($results | Where-Object { $_.Status -eq 'Failed' }).Count
    Details     = @($results)
  }
}

function Invoke-RunJunkReportService {
  <# Application service facade for detailed junk classification reporting (ISS-007/FR-005). #>
  param(
    [Parameter(Mandatory)][object[]]$Files,
    [bool]$AggressiveJunk = $false,
    [hashtable]$JunkPatterns,
    [scriptblock]$Log,
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports
  $results = [System.Collections.Generic.List[object]]::new()

  foreach ($file in $Files) {
    $name = if ($file -is [string]) { [System.IO.Path]::GetFileNameWithoutExtension($file) }
            elseif ($file.PSObject.Properties.Name -contains 'BaseName') { $file.BaseName }
            else { [string]$file }
    $reason = Get-JunkClassificationReason -BaseName $name -AggressiveJunk $AggressiveJunk -JunkPatterns $JunkPatterns
    if ($reason -and $reason.IsJunk) {
      $results.Add([pscustomobject]@{ File = $file; Reason = $reason })
    }
  }

  return [pscustomobject]@{
    TotalChecked = $Files.Count
    JunkFound    = $results.Count
    Details      = @($results)
  }
}

function Invoke-RunCsvExportService {
  <# Application service facade for collection CSV export (ISS-009/FR-022). #>
  param(
    [Parameter(Mandatory)][object[]]$Items,
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$Delimiter = ',',
    [bool]$IncludeHash = $false,
    [scriptblock]$Log,
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports
  return (Export-CollectionCsv -Items $Items -OutputPath $OutputPath -Delimiter $Delimiter -IncludeHash $IncludeHash -Log $Log)
}

function Invoke-RunCliExportService {
  <# Application service facade for CLI command export (ISS-008/FR-014). #>
  param(
    [Parameter(Mandatory)][hashtable]$Settings,
    [string]$ScriptPath,
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports
  $exportParams = @{ Settings = $Settings }
  if ($ScriptPath) { $exportParams.ScriptPath = $ScriptPath }
  return (Export-CliCommand @exportParams)
}

function Invoke-RunWebhookService {
  <# Application service facade for webhook notifications (ISS-016/FR-015). #>
  param(
    [Parameter(Mandatory)][string]$WebhookUrl,
    [Parameter(Mandatory)][hashtable]$Summary,
    [int]$TimeoutSeconds = 10,
    [int]$MaxRetries = 3,
    [scriptblock]$Log,
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports
  $urlCheck = Test-WebhookUrlSafe -Url $WebhookUrl
  if (-not $urlCheck.Valid) {
    throw "Webhook-URL '$WebhookUrl' ist nicht sicher: $($urlCheck.Reason)"
  }
  return (Invoke-WebhookNotification -WebhookUrl $WebhookUrl -Summary $Summary -TimeoutSeconds $TimeoutSeconds -MaxRetries $MaxRetries -Log $Log)
}

function Invoke-RunRetroArchExportService {
  <# Application service facade for RetroArch playlist export (ISS-015/FR-005). #>
  param(
    [Parameter(Mandatory)][object[]]$Items,
    [Parameter(Mandatory)][string]$OutputPath,
    [hashtable]$CustomCoreMappings,
    [scriptblock]$Log,
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports
  return (Export-RetroArchPlaylist -Items $Items -OutputPath $OutputPath -CustomCoreMappings $CustomCoreMappings -Log $Log)
}

function Invoke-RunMissingRomService {
  <# Application service facade for missing ROM tracking (ISS-020/FR-025). #>
  param(
    [Parameter(Mandatory)][hashtable]$DatIndex,
    [Parameter(Mandatory)][string[]]$FoundHashes,
    [string]$ConsoleKey,
    [string[]]$FilterRegions,
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports
  return (Get-MissingReport -DatIndex $DatIndex -FoundHashes $FoundHashes -ConsoleKey $ConsoleKey -FilterRegions $FilterRegions)
}

function Invoke-RunCrossRootDupeService {
  <# Application service facade for cross-root duplicate detection (ISS-021/FR-039). #>
  param(
    [Parameter(Mandatory)][hashtable]$FileIndex,
    [string]$HashType = 'SHA1',
    [scriptblock]$Progress,
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports
  return (Find-CrossRootDuplicates -FileIndex $FileIndex -HashType $HashType -Progress $Progress)
}

function Invoke-RunConvertQueueService {
  <# Application service facade for conversion queue management (ISS-022/FR-033). #>
  param(
    [ValidateSet('Create','Save','Load')]
    [string]$Operation,
    [object[]]$Items,
    [string]$QueuePath,
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports

  switch ($Operation) {
    'Create' { return (New-ConvertQueue -Items $Items) }
    'Save'   {
      $queue = if ($Items.Count -eq 1 -and $Items[0] -is [hashtable]) { $Items[0] } else { $Items }
      return (Save-ConvertQueue -Queue $queue -Path $QueuePath)
    }
    'Load'   { return (Import-ConvertQueue -Path $QueuePath) }
    default  { throw "Unbekannte Queue-Operation: $Operation" }
  }
}

function Invoke-RunRuleEngineService {
  <# Application service facade for rule engine evaluation (ISS-024/FR-017). #>
  param(
    [Parameter(Mandatory)][object[]]$Rules,
    [Parameter(Mandatory)][object]$Item,
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports
  return (Invoke-RuleEngine -Rules $Rules -Item $Item)
}

function Invoke-RunIntegrityCheckService {
  <# Application service facade for integrity monitoring (ISS-032/FR-041). #>
  param(
    [ValidateSet('Baseline','Check')]
    [string]$Operation,
    [object[]]$Files,
    [object]$Baseline,
    [string]$Algorithm = 'SHA256',
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports

  switch ($Operation) {
    'Baseline' { return (New-IntegrityBaseline -Files $Files -Algorithm $Algorithm) }
    'Check'    { return (Test-IntegrityAgainstBaseline -Baseline $Baseline -Algorithm $Algorithm) }
    default    { throw "Unbekannte Integrity-Operation: $Operation" }
  }
}

function Invoke-RunQuarantineService {
  <# Application service facade for ROM quarantine (ISS-033/FR-043). #>
  param(
    [Parameter(Mandatory)][string]$SourcePath,
    [Parameter(Mandatory)][string]$QuarantineRoot,
    [string[]]$Reasons = @('Unknown'),
    [ValidateSet('DryRun','Move')][string]$Mode = 'DryRun',
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports
  return (New-QuarantineAction -SourcePath $SourcePath -QuarantineRoot $QuarantineRoot -Reasons $Reasons -Mode $Mode)
}

function Invoke-RunParallelHashService {
  <# Application service facade for multi-threaded hashing (ISS-027/FR-029). #>
  param(
    [Parameter(Mandatory)][string[]]$Files,
    [string]$Algorithm = 'SHA1',
    [int]$MaxThreads = 0,
    [scriptblock]$Progress,
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports
  return (Invoke-ParallelHashing -Files $Files -Algorithm $Algorithm -MaxThreads $MaxThreads -Progress $Progress)
}

function Invoke-RunBackupService {
  <# Application service facade for automated backup strategy (ISS-025-analog/FR-042). #>
  param(
    [ValidateSet('Create','AddFile')]
    [string]$Operation,
    [object]$Config,
    [object]$Session,
    [string]$SourcePath,
    [string]$Label,
    [ValidateSet('DryRun','Move')][string]$Mode = 'DryRun',
    [hashtable]$Ports
  )

  $activePorts = Resolve-ApplicationPorts -Ports $Ports

  switch ($Operation) {
    'Create'  { return (New-BackupSession -Config $Config -Label $Label) }
    'AddFile' { return (Add-FileToBackup -Session $Session -SourcePath $SourcePath -Mode $Mode) }
    default   { throw "Unbekannte Backup-Operation: $Operation" }
  }
}

