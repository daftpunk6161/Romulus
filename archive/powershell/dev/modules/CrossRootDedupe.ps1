# ================================================================
#  CROSS-ROOT DUPLIKAT-FINDER (MF-02)
#  Dependencies: Dedupe.ps1, FormatScoring.ps1
# ================================================================

function Find-CrossRootDuplicates {
  <#
  .SYNOPSIS
    Findet identische ROMs ueber mehrere Root-Verzeichnisse (Hash-basiert).
  .PARAMETER FileIndex
    Array von Hashtables: @{ Path; Hash; Root; Size; Format }.
  .PARAMETER HashType
    Hash-Algorithmus.
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$FileIndex,
    [ValidateSet('SHA1','SHA256','MD5','CRC32')][string]$HashType = 'SHA1',
    [scriptblock]$Progress
  )

  if (-not $FileIndex -or $FileIndex.Count -eq 0) {
    return ,@()
  }

  # Nach Hash gruppieren
  $hashGroups = @{}
  foreach ($item in $FileIndex) {
    $hash = $item.Hash
    if ([string]::IsNullOrWhiteSpace($hash)) { continue }
    if (-not $hashGroups.ContainsKey($hash)) {
      $hashGroups[$hash] = [System.Collections.Generic.List[hashtable]]::new()
    }
    $hashGroups[$hash].Add($item)
  }

  # Nur Duplikate (>1 Datei pro Hash UND aus verschiedenen Roots)
  $duplicates = [System.Collections.Generic.List[hashtable]]::new()
  foreach ($hash in $hashGroups.Keys) {
    $files = $hashGroups[$hash]
    if ($files.Count -lt 2) { continue }

    $roots = @($files | ForEach-Object { $_.Root } | Select-Object -Unique)
    if ($roots.Count -lt 2) { continue }

    $duplicates.Add(@{
      Hash     = $hash
      HashType = $HashType
      Files    = @($files)
      Count    = $files.Count
      Roots    = $roots
    })
  }

  if ($Progress) { & $Progress "Gefunden: $($duplicates.Count) Cross-Root-Duplikate" }

  return ,@($duplicates.ToArray())
}

function Get-CrossRootMergeAdvice {
  <#
  .SYNOPSIS
    Gibt Merge-Empfehlungen fuer Cross-Root-Duplikate.
    Behaelt die Datei mit dem besten Format-Score.
  .PARAMETER DuplicateGroup
    Ein Duplikat-Gruppen-Objekt aus Find-CrossRootDuplicates.
  .PARAMETER FormatScores
    Hashtable: Extension → Score (hoeher = besser).
  #>
  param(
    [Parameter(Mandatory)][hashtable]$DuplicateGroup,
    [hashtable]$FormatScores = @{ '.chd'=850; '.iso'=700; '.zip'=500; '.7z'=480; '.rar'=400 }
  )

  $files = $DuplicateGroup.Files
  if ($files.Count -lt 2) {
    return @{ Keep = $files[0]; Remove = @() }
  }

  # Score pro Datei
  $scored = $files | ForEach-Object {
    $ext = if ($_.Format) { $_.Format.ToLowerInvariant() } else { '' }
    if (-not $ext.StartsWith('.')) { $ext = ".$ext" }
    $score = if ($FormatScores.ContainsKey($ext)) { $FormatScores[$ext] } else { 100 }
    @{ File = $_; Score = $score }
  } | Sort-Object { $_.Score } -Descending

  $keep = $scored[0].File
  $remove = @($scored | Select-Object -Skip 1 | ForEach-Object { $_.File })

  return @{
    Keep   = $keep
    Remove = $remove
    Reason = "Format-Score: $($scored[0].Score)"
  }
}
