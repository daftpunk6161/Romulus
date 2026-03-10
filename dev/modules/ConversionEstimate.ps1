# ================================================================
#  CONVERSION SAVINGS ESTIMATE – Speicherplatz-Prognose (QW-04)
#  Dependencies: FormatScoring.ps1
# ================================================================

# Kompressions-Ratios (statistischer Durchschnitt)
$script:COMPRESSION_RATIOS = @{
  # Source-Ext → Target-Ext → Ratio (0.0–1.0, Anteil der Zielgroesse)
  'bin_chd'  = 0.50   # BIN/CUE → CHD: ~50% der Originalgroesse
  'cue_chd'  = 0.50
  'iso_chd'  = 0.60   # ISO → CHD: ~60%
  'gdi_chd'  = 0.50
  'iso_rvz'  = 0.40   # ISO → RVZ: ~40%
  'gcz_rvz'  = 0.70   # GCZ → RVZ: ~70%
  'wbfs_rvz' = 0.60   # WBFS → RVZ: ~60%
  'zip_7z'   = 0.90   # ZIP → 7z: ~90%
  'rar_zip'  = 1.05   # RAR → ZIP: leicht groesser
  'rar_7z'   = 0.95   # RAR → 7z: ~95%
  'cso_chd'  = 0.80   # CSO → CHD (via ISO): ~80%
  'pbp_chd'  = 0.70   # PBP → CHD: ~70%
}

function Get-CompressionRatio {
  <#
  .SYNOPSIS
    Gibt das geschaetzte Kompressionsverhältnis fuer eine Format-Konvertierung zurueck.
  #>
  param(
    [Parameter(Mandatory)][string]$SourceExtension,
    [Parameter(Mandatory)][string]$TargetExtension
  )

  $srcExt = $SourceExtension.TrimStart('.').ToLowerInvariant()
  $tgtExt = $TargetExtension.TrimStart('.').ToLowerInvariant()
  $key = "${srcExt}_${tgtExt}"

  if ($script:COMPRESSION_RATIOS.ContainsKey($key)) {
    return $script:COMPRESSION_RATIOS[$key]
  }

  # Fallback fuer unbekannte Kombinationen
  return 0.75
}

function Get-ConversionSavingsEstimate {
  <#
  .SYNOPSIS
    Berechnet die geschaetzte Speicherersparnis bei Format-Konvertierung.
  .PARAMETER Files
    Array von Datei-Objekten (muessen mindestens Length und Extension haben).
  .PARAMETER TargetFormat
    Zielformat-Extension (z.B. '.chd', '.rvz', '.7z').
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Files,
    [Parameter(Mandatory)][string]$TargetFormat
  )

  $result = @{
    TotalSourceSizeBytes     = [long]0
    EstimatedTargetSizeBytes = [long]0
    EstimatedSavingsBytes    = [long]0
    Ratio                    = 0.0
    FileCount                = 0
    SkippedCount             = 0
    Details                  = [System.Collections.Generic.List[hashtable]]::new()
  }

  $targetExt = $TargetFormat.TrimStart('.').ToLowerInvariant()

  foreach ($file in $Files) {
    $size = 0
    $ext = ''

    if ($file -is [System.IO.FileInfo]) {
      $size = $file.Length
      $ext = $file.Extension
    } elseif ($file -is [hashtable]) {
      $size = if ($file.ContainsKey('Length')) { [long]$file.Length }
              elseif ($file.ContainsKey('Size')) { [long]$file.Size }
              else { 0 }
      $ext = if ($file.ContainsKey('Extension')) { [string]$file.Extension }
             elseif ($file.ContainsKey('Format')) { '.' + [string]$file.Format }
             else { '' }
      if ($size -eq 0 -or [string]::IsNullOrWhiteSpace($ext)) {
        $result.SkippedCount++
        continue
      }
    } elseif ($file.Length -and $file.Extension) {
      $size = [long]$file.Length
      $ext = [string]$file.Extension
    } elseif ($file.Size -and $file.Format) {
      $size = [long]$file.Size
      $ext = '.' + [string]$file.Format
    } else {
      $result.SkippedCount++
      continue
    }

    $srcExt = $ext.TrimStart('.').ToLowerInvariant()

    # Gleiche Extension = kein Gewinn
    if ($srcExt -eq $targetExt) {
      $result.SkippedCount++
      continue
    }

    $ratio = Get-CompressionRatio -SourceExtension $srcExt -TargetExtension $targetExt
    if ($null -eq $ratio) {
      $result.SkippedCount++
      continue
    }

    $estimatedSize = [long]([double]$size * $ratio)
    $savings = $size - $estimatedSize

    $result.TotalSourceSizeBytes += $size
    $result.EstimatedTargetSizeBytes += $estimatedSize
    $result.EstimatedSavingsBytes += $savings
    $result.FileCount++

    [void]$result.Details.Add(@{
      FileName      = if ($file.Name) { $file.Name } else { "File" }
      SourceSize    = $size
      EstimatedSize = $estimatedSize
      Savings       = $savings
      Ratio         = $ratio
    })
  }

  if ($result.TotalSourceSizeBytes -gt 0) {
    $result.Ratio = [math]::Round($result.EstimatedTargetSizeBytes / $result.TotalSourceSizeBytes, 3)
  }

  return $result
}
