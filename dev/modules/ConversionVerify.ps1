# ================================================================
#  CONVERSION VERIFY – Batch-Verify nach Konvertierung (MF-09)
#  Dependencies: Dat.ps1, Convert.ps1
# ================================================================

function New-ConversionVerifyRecord {
  <#
  .SYNOPSIS
    Erstellt einen Verifizierungs-Datensatz fuer eine Konvertierung.
  .PARAMETER SourcePath
    Original-Datei.
  .PARAMETER SourceHash
    Hash der Quelldatei.
  .PARAMETER TargetPath
    Konvertierte Datei.
  .PARAMETER HashType
    Hash-Algorithmus.
  #>
  param(
    [Parameter(Mandatory)][string]$SourcePath,
    [Parameter(Mandatory)][string]$SourceHash,
    [Parameter(Mandatory)][string]$TargetPath,
    [ValidateSet('SHA1','SHA256','MD5','CRC32')][string]$HashType = 'SHA1'
  )

  return @{
    SourcePath = $SourcePath
    SourceHash = $SourceHash
    TargetPath = $TargetPath
    HashType   = $HashType
    Status     = 'Pending'
    TargetHash = $null
    Verified   = $null
    Timestamp  = $null
  }
}

function Test-ConversionIntegrity {
  <#
  .SYNOPSIS
    Verifiziert eine konvertierte Datei (existiert, nicht leer, optional Tool-Verify).
  .PARAMETER TargetPath
    Pfad zur konvertierten Datei.
  .PARAMETER ExpectedMinSize
    Minimale erwartete Groesse in Bytes.
  .PARAMETER ToolVerifyAvailable
    Ob ein Tool-spezifischer Verify-Befehl moeglich ist (z.B. chdman verify).
  #>
  param(
    [Parameter(Mandatory)][string]$TargetPath,
    [long]$ExpectedMinSize = 1,
    [bool]$ToolVerifyAvailable = $false
  )

  if (-not (Test-Path $TargetPath)) {
    return @{ Valid = $false; Reason = 'FileNotFound'; Path = $TargetPath }
  }

  $fileInfo = Get-Item $TargetPath
  if ($fileInfo.Length -lt $ExpectedMinSize) {
    return @{ Valid = $false; Reason = 'FileTooSmall'; Path = $TargetPath; Size = $fileInfo.Length; Expected = $ExpectedMinSize }
  }

  return @{ Valid = $true; Reason = 'OK'; Path = $TargetPath; Size = $fileInfo.Length }
}

function Invoke-BatchVerify {
  <#
  .SYNOPSIS
    Batch-Verifizierung mehrerer konvertierter Dateien.
  .PARAMETER Records
    Array von Verify-Records (aus New-ConversionVerifyRecord).
  .PARAMETER HashType
    Hash-Algorithmus.
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Records,
    [ValidateSet('SHA1','SHA256','MD5')][string]$HashType = 'SHA1'
  )

  if (-not $Records -or $Records.Count -eq 0) {
    return @{ TotalChecked = 0; Passed = 0; Failed = 0; Missing = 0; Results = @() }
  }

  $results = @()
  $passed = 0
  $failed = 0
  $missing = 0

  foreach ($record in $Records) {
    $targetPath = $record.TargetPath

    if (-not (Test-Path $targetPath)) {
      $record.Status = 'Missing'
      $record.Verified = $false
      $missing++
      $results += $record
      continue
    }

    $integrity = Test-ConversionIntegrity -TargetPath $targetPath
    if (-not $integrity.Valid) {
      $record.Status = 'Failed'
      $record.Verified = $false
      $failed++
    } else {
      $record.Status = 'Passed'
      $record.Verified = $true
      $passed++
    }

    $record.Timestamp = (Get-Date).ToString('o')
    $results += $record
  }

  return @{
    TotalChecked = $Records.Count
    Passed       = $passed
    Failed       = $failed
    Missing      = $missing
    Results      = $results
  }
}

function Get-VerifyReport {
  <#
  .SYNOPSIS
    Erstellt einen Verify-Bericht als strukturiertes Objekt.
  .PARAMETER BatchResult
    Ergebnis von Invoke-BatchVerify.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$BatchResult
  )

  $failedItems = @($BatchResult.Results | Where-Object { $_.Status -ne 'Passed' })

  return @{
    Summary = @{
      Total   = $BatchResult.TotalChecked
      Passed  = $BatchResult.Passed
      Failed  = $BatchResult.Failed
      Missing = $BatchResult.Missing
      Rate    = if ($BatchResult.TotalChecked -gt 0) { [math]::Round(($BatchResult.Passed / $BatchResult.TotalChecked) * 100, 1) } else { 0 }
    }
    FailedItems = $failedItems
    Timestamp   = (Get-Date).ToString('o')
  }
}
