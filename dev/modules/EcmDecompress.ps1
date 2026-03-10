# ================================================================
#  ECM DECOMPRESS – .ecm-Dateien zu .bin entpacken (QW-02)
#  Dependencies: Tools.ps1
# ================================================================

function Find-Ecm2Bin {
  <#
  .SYNOPSIS
    Sucht das ecm2bin-Tool in bekannten Pfaden und Settings.
  #>
  param(
    [string]$CustomPath
  )

  if ($CustomPath -and (Test-Path -LiteralPath $CustomPath -PathType Leaf)) {
    return $CustomPath
  }

  # Aus Settings (toolPaths.ecm2bin)
  if (Get-Command Get-AppStateValue -ErrorAction SilentlyContinue) {
    $configured = Get-AppStateValue -Key 'toolPaths' -Default @{}
    if ($configured.ecm2bin -and (Test-Path -LiteralPath $configured.ecm2bin -PathType Leaf)) {
      return $configured.ecm2bin
    }
  }

  # PATH-Suche
  $found = Get-Command 'ecm2bin' -ErrorAction SilentlyContinue |
           Select-Object -First 1 -ExpandProperty Source
  if ($found) { return $found }

  $found = Get-Command 'ecm2bin.exe' -ErrorAction SilentlyContinue |
           Select-Object -First 1 -ExpandProperty Source
  if ($found) { return $found }

  return $null
}

function Invoke-EcmDecompress {
  <#
  .SYNOPSIS
    Dekomprimiert eine .ecm-Datei zu .bin via ecm2bin-Tool.
  .PARAMETER FilePath
    Pfad zur .ecm-Datei.
  .PARAMETER Mode
    DryRun = nur Preview, Move = tatsaechlich konvertieren.
  .PARAMETER ToolPath
    Optionaler Pfad zum ecm2bin-Tool.
  .PARAMETER Log
    Optionaler Logging-Callback.
  #>
  param(
    [Parameter(Mandatory)][string]$FilePath,
    [ValidateSet('DryRun','Move')][string]$Mode = 'DryRun',
    [string]$ToolPath,
    [scriptblock]$Log
  )

  $result = @{
    SourcePath = $FilePath
    TargetPath = $null
    Status     = 'Unknown'
    SourceSize = 0
    TargetSize = 0
  }

  # Validierung
  if (-not (Test-Path -LiteralPath $FilePath -PathType Leaf)) {
    $result.Status = 'FileNotFound'
    return $result
  }

  $ext = [System.IO.Path]::GetExtension($FilePath).ToLowerInvariant()
  if ($ext -ne '.ecm') {
    $result.Status = 'NotEcmFile'
    return $result
  }

  $sourceItem = Get-Item -LiteralPath $FilePath -ErrorAction Stop
  $result.SourceSize = $sourceItem.Length

  # Ziel-Pfad: .ecm entfernen → .bin
  $baseName = [System.IO.Path]::GetFileNameWithoutExtension($FilePath)
  $targetExt = if ($baseName -match '\.(bin|img|iso)$') { '' } else { '.bin' }
  $targetPath = Join-Path $sourceItem.DirectoryName ($baseName + $targetExt)
  $result.TargetPath = $targetPath

  # Tool finden
  $ecm2binPath = Find-Ecm2Bin -CustomPath $ToolPath
  if (-not $ecm2binPath) {
    $result.Status = 'ToolNotFound'
    if ($Log) { & $Log 'ecm2bin-Tool nicht gefunden. Bitte in Settings konfigurieren.' }
    return $result
  }

  # Hash-Verifikation des Tools (wenn verfuegbar)
  if (Get-Command Test-ToolBinaryHash -ErrorAction SilentlyContinue) {
    $hashOk = Test-ToolBinaryHash -ToolPath $ecm2binPath -Log $Log
    if (-not $hashOk) {
      $result.Status = 'ToolHashMismatch'
      if ($Log) { & $Log ("ecm2bin Hash-Pruefung fehlgeschlagen: {0}" -f $ecm2binPath) }
      return $result
    }
  }

  # DryRun
  if ($Mode -eq 'DryRun') {
    $result.Status = 'WouldConvert'
    return $result
  }

  # Ziel existiert bereits
  if (Test-Path -LiteralPath $targetPath -PathType Leaf) {
    $result.Status = 'TargetExists'
    if ($Log) { & $Log ("Zieldatei existiert bereits: {0}" -f $targetPath) }
    return $result
  }

  # ecm2bin ausfuehren
  try {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $ecm2binPath
    $psi.Arguments = ('"{0}"' -f $FilePath)
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    $proc = [System.Diagnostics.Process]::Start($psi)
    $proc.WaitForExit(300000)  # 5 min timeout

    if ($proc.ExitCode -ne 0) {
      $stderr = $proc.StandardError.ReadToEnd()
      $result.Status = 'ToolError'
      if ($Log) { & $Log ("ecm2bin Fehler (Exit {0}): {1}" -f $proc.ExitCode, $stderr) }
      # Cleanup bei Fehler
      if (Test-Path -LiteralPath $targetPath -PathType Leaf) {
        Remove-Item -LiteralPath $targetPath -Force -ErrorAction SilentlyContinue
      }
      return $result
    }

    if (Test-Path -LiteralPath $targetPath -PathType Leaf) {
      $result.TargetSize = (Get-Item -LiteralPath $targetPath).Length
      $result.Status = 'Converted'
      if ($Log) { & $Log ("ECM dekomprimiert: {0} → {1}" -f $sourceItem.Name, [System.IO.Path]::GetFileName($targetPath)) }
    } else {
      $result.Status = 'OutputMissing'
      if ($Log) { & $Log ("ecm2bin hat keine Ausgabedatei erzeugt: {0}" -f $targetPath) }
    }
  } catch {
    $result.Status = 'Error'
    if ($Log) { & $Log ("ECM-Fehler: {0}" -f $_.Exception.Message) }
  }

  return $result
}
