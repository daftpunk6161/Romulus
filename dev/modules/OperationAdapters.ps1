function Invoke-CliRunAdapter {
  <# CLI adapter: orchestrates sort + dedupe + conversion + post-run features via application services. #>
  param(
    [bool]$SortConsole,
    [string]$Mode,
    [string[]]$Roots,
    [string[]]$Extensions,
    [bool]$UseDat,
    [string]$DatRoot,
    [string]$DatHashType,
    [hashtable]$DatMap,
    [hashtable]$ToolOverrides,
    [scriptblock]$Log,
    [bool]$Convert,
    [hashtable]$DedupeParams,
    [hashtable]$Ports,
    # ── Post-Run Feature Flags ──
    [bool]$DatRename = $false,
    [bool]$EcmDecompress = $false,
    [bool]$ArchiveRepack = $false,
    [string]$ArchiveRepackFormat = 'zip',
    [int]$ArchiveCompressionLevel = 5,
    [bool]$GenerateM3u = $false,
    [bool]$ExportRetroArch = $false,
    [string]$RetroArchOutputPath,
    [bool]$ExportCsv = $false,
    [string]$CsvOutputPath,
    [string]$WebhookUrl,
    [bool]$ParallelHash = $false,
    [int]$ParallelHashThreads = 0
  )

  $consoleSortUnknownReasons = $null
  if ($SortConsole -and $Mode -eq 'Move') {
    $sortResult = Invoke-RunSortService -Enabled $true -Mode $Mode -Roots $Roots -Extensions $Extensions -UseDat $UseDat -DatRoot $DatRoot -DatHashType $DatHashType -DatMap $DatMap -ToolOverrides $ToolOverrides -Log $Log -Ports $Ports
    if ($sortResult -and $sortResult.Value) {
      $consoleSortUnknownReasons = $sortResult.Value
    }
  }

  if ($consoleSortUnknownReasons) {
    $DedupeParams['ConsoleSortUnknownReasons'] = $consoleSortUnknownReasons
  }

  $result = Invoke-RunDedupeService -Parameters $DedupeParams -Ports $Ports

  # Auto folder-level dedupe (PS3/DOS/AMIGA etc.) - runs after region dedupe
  try {
    $folderDedupeResult = Invoke-RunFolderDedupeService `
      -Roots $Roots `
      -Mode $Mode `
      -Log $Log `
      -Ports $Ports

    if ($result -and $folderDedupeResult) {
      $result | Add-Member -NotePropertyName FolderDedupeResult -NotePropertyValue $folderDedupeResult -Force
    }
  } catch {
    if ($Log) { & $Log ("Auto folder-dedupe error: {0}" -f $_.Exception.Message) }
  }

  if ($Convert -and $Mode -eq 'Move' -and $result) {
    Invoke-RunConversionService -Operation 'WinnerMove' -Parameters @{
      Enabled = $true
      Mode = $Mode
      Result = $result
      Roots = $Roots
      ToolOverrides = $ToolOverrides
      Log = $Log
    } -Ports $Ports | Out-Null
  }

  if ($Convert -and $Mode -eq 'DryRun' -and $result) {
    Invoke-RunConversionService -Operation 'Preview' -Parameters @{
      Enabled = $true
      Mode = $Mode
      Result = $result
      Log = $Log
    } -Ports $Ports | Out-Null
  }

  # ── Post-Run Feature Pipeline ──────────────────────────────────────────────

  # ECM decompression (pre-processing: decompress .ecm files found in roots)
  if ($EcmDecompress) {
    try {
      $ecmFiles = foreach ($r in $Roots) {
        if (Test-Path -LiteralPath $r -PathType Container) {
          Get-ChildItem -LiteralPath $r -Filter '*.ecm' -Recurse -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
        }
      }
      if ($ecmFiles) {
        $ecmResult = Invoke-RunEcmDecompressService -Files @($ecmFiles) -Mode $Mode -Log $Log -Ports $Ports
        $result | Add-Member -NotePropertyName EcmDecompressResult -NotePropertyValue $ecmResult -Force
        if ($Log) { & $Log ("ECM: {0} decompressed, {1} failed" -f $ecmResult.Success, $ecmResult.Failed) }
      }
    } catch {
      if ($Log) { & $Log ("ECM decompression error: {0}" -f $_.Exception.Message) }
    }
  }

  # DAT-based rename (requires UseDat + DatRoot)
  if ($DatRename -and $UseDat -and $result) {
    try {
      if (Get-Command Get-DatIndex -ErrorAction SilentlyContinue) {
        $datIdx = Get-DatIndex -DatRoot $DatRoot -HashType $DatHashType
        if ($datIdx -and $datIdx.Count -gt 0) {
          $winnerFiles = @()
          if ($result.PSObject.Properties.Name -contains 'Winners') {
            $winnerFiles = @($result.Winners | Where-Object { $_.PSObject.Properties.Name -contains 'Path' } | Select-Object -ExpandProperty Path)
          }
          if ($winnerFiles.Count -gt 0) {
            $renameResult = Invoke-RunDatRenameService -Operation $(if ($Mode -eq 'Move') { 'Execute' } else { 'Preview' }) -Files $winnerFiles -DatIndex $datIdx -HashType $DatHashType -Log $Log -Ports $Ports
            $result | Add-Member -NotePropertyName DatRenameResult -NotePropertyValue $renameResult -Force
            if ($Log) { & $Log ("DAT-Rename: {0} renamed, {1} no-match, {2} conflicts" -f $renameResult.Renamed, $renameResult.NoMatch, $renameResult.Conflicts) }
          }
        }
      }
    } catch {
      if ($Log) { & $Log ("DAT-Rename error: {0}" -f $_.Exception.Message) }
    }
  }

  # Archive repack
  if ($ArchiveRepack -and $result) {
    try {
      $archiveFiles = foreach ($r in $Roots) {
        if (Test-Path -LiteralPath $r -PathType Container) {
          $sourceExts = if ($ArchiveRepackFormat -eq 'zip') { @('*.7z','*.rar') } else { @('*.zip','*.rar') }
          foreach ($ext in $sourceExts) {
            Get-ChildItem -LiteralPath $r -Filter $ext -Recurse -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
          }
        }
      }
      if ($archiveFiles) {
        $repackResult = Invoke-RunArchiveRepackService -Files @($archiveFiles) -TargetFormat $ArchiveRepackFormat -CompressionLevel $ArchiveCompressionLevel -Mode $Mode -Log $Log -Ports $Ports
        $result | Add-Member -NotePropertyName ArchiveRepackResult -NotePropertyValue $repackResult -Force
        if ($Log) { & $Log ("Repack: {0} repacked, {1} skipped, {2} failed" -f $repackResult.Repacked, $repackResult.Skipped, $repackResult.Failed) }
      }
    } catch {
      if ($Log) { & $Log ("Archive repack error: {0}" -f $_.Exception.Message) }
    }
  }

  # M3U generation for multi-disc games
  if ($GenerateM3u -and $result) {
    try {
      $romFiles = foreach ($r in $Roots) {
        if (Test-Path -LiteralPath $r -PathType Container) {
          Get-ChildItem -LiteralPath $r -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Extension -imatch '^\.(chd|cue|ccd|gdi|iso|pbp)$' } |
            Select-Object -ExpandProperty FullName
        }
      }
      if ($romFiles) {
        $m3uOutputDir = $Roots[0]
        $m3uResult = Invoke-RunM3uGenerationService -Files @($romFiles) -OutputDir $m3uOutputDir -Mode $Mode -Log $Log -Ports $Ports
        $result | Add-Member -NotePropertyName M3uResult -NotePropertyValue $m3uResult -Force
        if ($Log) { & $Log ("M3U: {0} generated, {1} skipped" -f $m3uResult.Generated, $m3uResult.Skipped) }
      }
    } catch {
      if ($Log) { & $Log ("M3U generation error: {0}" -f $_.Exception.Message) }
    }
  }

  # RetroArch playlist export
  if ($ExportRetroArch -and $result) {
    try {
      $raOutputPath = if ($RetroArchOutputPath) { $RetroArchOutputPath } else { Join-Path $Roots[0] '_playlists' }
      $exportItems = @()
      if ($result.PSObject.Properties.Name -contains 'Winners') {
        $exportItems = @($result.Winners)
      }
      if ($exportItems.Count -gt 0) {
        $raResult = Invoke-RunRetroArchExportService -Items $exportItems -OutputPath $raOutputPath -Log $Log -Ports $Ports
        $result | Add-Member -NotePropertyName RetroArchExportResult -NotePropertyValue $raResult -Force
        if ($Log) { & $Log ("RetroArch-Export: playlist written to $raOutputPath") }
      }
    } catch {
      if ($Log) { & $Log ("RetroArch export error: {0}" -f $_.Exception.Message) }
    }
  }

  # CSV export
  if ($ExportCsv -and $result) {
    try {
      $csvPath = if ($CsvOutputPath) { $CsvOutputPath } else { Join-Path (Join-Path $PSScriptRoot 'reports') ('collection-export-{0}.csv' -f (Get-Date -Format 'yyyyMMdd-HHmmss')) }
      $csvItems = @()
      if ($result.PSObject.Properties.Name -contains 'AllItems') { $csvItems = @($result.AllItems) }
      elseif ($result.PSObject.Properties.Name -contains 'Winners') { $csvItems = @($result.Winners) }
      if ($csvItems.Count -gt 0) {
        $csvResult = Invoke-RunCsvExportService -Items $csvItems -OutputPath $csvPath -Log $Log -Ports $Ports
        $result | Add-Member -NotePropertyName CsvExportResult -NotePropertyValue $csvResult -Force
        if ($Log) { & $Log ("CSV-Export: $csvPath") }
      }
    } catch {
      if ($Log) { & $Log ("CSV export error: {0}" -f $_.Exception.Message) }
    }
  }

  # Webhook notification
  if (-not [string]::IsNullOrWhiteSpace($WebhookUrl) -and $result) {
    try {
      $summary = @{
        Mode = $Mode
        Roots = @($Roots)
        Status = 'completed'
        Timestamp = (Get-Date).ToString('o')
      }
      if ($result.PSObject.Properties.Name -contains 'FilesScanned') { $summary['FilesScanned'] = $result.FilesScanned }
      if ($result.PSObject.Properties.Name -contains 'Groups') { $summary['Groups'] = $result.Groups }
      if ($result.PSObject.Properties.Name -contains 'Moves') { $summary['Moves'] = $result.Moves }
      Invoke-RunWebhookService -WebhookUrl $WebhookUrl -Summary $summary -Log $Log -Ports $Ports | Out-Null
    } catch {
      if ($Log) { & $Log ("Webhook error: {0}" -f $_.Exception.Message) }
    }
  }

  return $result
}

function Register-SchedulerTaskAdapter {
  <# Scheduler adapter: forwards registration to scheduler module. #>
  param(
    [Parameter(Mandatory=$true)][string]$TaskName,
    [Parameter(Mandatory=$true)][string[]]$Roots,
    [ValidateSet('DryRun','Move')][string]$Mode = 'DryRun',
    [string[]]$Prefer = @('EU','US','WORLD','JP'),
    [string]$Time = '03:00',
    [string]$WorkingDirectory = (Get-Location).Path
  )

  return (Register-RomCleanupScheduledTask -TaskName $TaskName -Roots $Roots -Mode $Mode -Prefer $Prefer -Time $Time -WorkingDirectory $WorkingDirectory)
}


