# ================================================================
#  ARCHIVE REPACK – ZIP↔7z Repack (QW-03)
#  Dependencies: Tools.ps1, FileOps.ps1
# ================================================================

function Invoke-ArchiveRepack {
  <#
  .SYNOPSIS
    Packt ein Archiv von einem Format in ein anderes um (ZIP↔7z, RAR→ZIP/7z).
  .PARAMETER ArchivePath
    Pfad zum Quell-Archiv.
  .PARAMETER TargetFormat
    Zielformat: zip oder 7z.
  .PARAMETER CompressionLevel
    Kompressionsstufe 1-9.
  .PARAMETER Mode
    DryRun = nur Preview, Move = tatsaechlich repacken.
  .PARAMETER Log
    Optionaler Logging-Callback.
  #>
  param(
    [Parameter(Mandatory)][string]$ArchivePath,
    [ValidateSet('zip','7z')][string]$TargetFormat = 'zip',
    [ValidateRange(1,9)][int]$CompressionLevel = 5,
    [ValidateSet('DryRun','Move')][string]$Mode = 'DryRun',
    [scriptblock]$Log
  )

  $result = @{
    SourcePath  = $ArchivePath
    TargetPath  = $null
    SourceSize  = 0
    TargetSize  = 0
    Status      = 'Unknown'
    FileCount   = 0
  }

  if (-not (Test-Path -LiteralPath $ArchivePath -PathType Leaf)) {
    $result.Status = 'FileNotFound'
    return $result
  }

  $sourceItem = Get-Item -LiteralPath $ArchivePath -ErrorAction Stop
  $result.SourceSize = $sourceItem.Length
  $sourceExt = $sourceItem.Extension.ToLowerInvariant()

  if ($sourceExt -notin @('.zip', '.7z', '.rar')) {
    $result.Status = 'UnsupportedFormat'
    return $result
  }

  $targetExt = ".${TargetFormat}"
  if ($sourceExt -eq $targetExt) {
    $result.Status = 'AlreadyTargetFormat'
    return $result
  }

  $targetPath = [System.IO.Path]::ChangeExtension($ArchivePath, $targetExt)
  $result.TargetPath = $targetPath

  # 7z-Tool finden
  $sevenZipPath = $null
  if (Get-Command Find-7Zip -ErrorAction SilentlyContinue) {
    $sevenZipPath = Find-7Zip
  } elseif (Get-Command Get-AppStateValue -ErrorAction SilentlyContinue) {
    $tp = Get-AppStateValue -Key 'toolPaths' -Default @{}
    if ($tp.'7z') { $sevenZipPath = $tp.'7z' }
  }
  if (-not $sevenZipPath) {
    $sevenZipPath = Get-Command '7z' -ErrorAction SilentlyContinue |
                    Select-Object -First 1 -ExpandProperty Source
  }

  if (-not $sevenZipPath) {
    $result.Status = 'ToolNotFound'
    if ($Log) { & $Log '7z-Tool nicht gefunden.' }
    return $result
  }

  # DryRun
  if ($Mode -eq 'DryRun') {
    $result.Status = 'WouldRepack'
    return $result
  }

  # Temp-Verzeichnis fuer Entpacken
  $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ('repack-' + [guid]::NewGuid().ToString('N'))

  try {
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

    # Entpacken
    $extractArgs = @('x', ('-o{0}' -f $tempDir), '-y', $ArchivePath)
    $extractProc = Start-Process -FilePath $sevenZipPath -ArgumentList $extractArgs `
                     -Wait -PassThru -NoNewWindow -RedirectStandardOutput 'NUL' -ErrorAction Stop

    if ($extractProc.ExitCode -ne 0) {
      $result.Status = 'ExtractError'
      if ($Log) { & $Log ("Entpacken fehlgeschlagen (Exit {0}): {1}" -f $extractProc.ExitCode, $ArchivePath) }
      return $result
    }

    # Dateien zaehlen
    $extractedFiles = @(Get-ChildItem -LiteralPath $tempDir -Recurse -File -ErrorAction SilentlyContinue)
    $result.FileCount = $extractedFiles.Count

    if ($extractedFiles.Count -eq 0) {
      $result.Status = 'EmptyArchive'
      if ($Log) { & $Log ("Archiv ist leer: {0}" -f $ArchivePath) }
      return $result
    }

    # Neu packen
    $packArgs = @('a', "-t${TargetFormat}", "-mx=${CompressionLevel}", $targetPath, (Join-Path $tempDir '*'))
    $packProc = Start-Process -FilePath $sevenZipPath -ArgumentList $packArgs `
                  -Wait -PassThru -NoNewWindow -RedirectStandardOutput 'NUL' -ErrorAction Stop

    if ($packProc.ExitCode -ne 0) {
      $result.Status = 'PackError'
      if ($Log) { & $Log ("Packen fehlgeschlagen (Exit {0})" -f $packProc.ExitCode) }
      if (Test-Path -LiteralPath $targetPath) {
        Remove-Item -LiteralPath $targetPath -Force -ErrorAction SilentlyContinue
      }
      return $result
    }

    if (Test-Path -LiteralPath $targetPath -PathType Leaf) {
      $result.TargetSize = (Get-Item -LiteralPath $targetPath).Length
      $result.Status = 'Repacked'
      if ($Log) { & $Log ("Repack: {0} → {1} ({2:N0} → {3:N0} Bytes)" -f $sourceItem.Name, [System.IO.Path]::GetFileName($targetPath), $result.SourceSize, $result.TargetSize) }
    } else {
      $result.Status = 'OutputMissing'
    }
  } catch {
    $result.Status = 'Error'
    if ($Log) { & $Log ("Repack-Fehler: {0}" -f $_.Exception.Message) }
  } finally {
    # Temp-Verzeichnis aufraumen
    if (Test-Path -LiteralPath $tempDir) {
      Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
  }

  return $result
}
