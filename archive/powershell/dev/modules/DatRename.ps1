# ================================================================
#  DAT RENAME – ROM-Dateien nach DAT-Standard umbenennen (QW-01)
#  Dependencies: Dat.ps1, FileOps.ps1
# ================================================================

function Rename-RomToDatName {
  <#
  .SYNOPSIS
    Benennt eine ROM-Datei nach No-Intro/Redump-Nomenklatur um,
    basierend auf Hash-Match im DAT-Index.
  .PARAMETER FilePath
    Pfad zur ROM-Datei.
  .PARAMETER DatIndex
    DAT-Index-Hashtable (Hash → GameName).
  .PARAMETER Mode
    DryRun = nur Preview, Move = tatsaechlich umbenennen.
  .PARAMETER HashType
    Hash-Algorithmus fuer Datei-Hashing.
  .PARAMETER Log
    Optionaler Logging-Callback.
  #>
  param(
    [Parameter(Mandatory)][string]$FilePath,
    [Parameter(Mandatory)][hashtable]$DatIndex,
    [ValidateSet('DryRun','Move')][string]$Mode = 'DryRun',
    [ValidateSet('SHA1','SHA256','MD5')][string]$HashType = 'SHA1',
    [scriptblock]$Log
  )

  $result = @{
    OldName  = $null
    NewName  = $null
    Status   = 'Unknown'
    Hash     = $null
    FilePath = $FilePath
  }

  # Validierung: Datei existiert
  if (-not (Test-Path -LiteralPath $FilePath -PathType Leaf)) {
    $result.Status = 'FileNotFound'
    return $result
  }

  $fileItem = Get-Item -LiteralPath $FilePath -ErrorAction Stop
  $result.OldName = $fileItem.Name

  # Hash berechnen
  try {
    $hashObj = Get-FileHash -LiteralPath $FilePath -Algorithm $HashType -ErrorAction Stop
    $result.Hash = $hashObj.Hash
  } catch {
    $result.Status = 'HashError'
    if ($Log) { & $Log ("Hash-Fehler fuer: {0} — {1}" -f $FilePath, $_.Exception.Message) }
    return $result
  }

  # Lookup im DAT-Index
  $datEntry = $null
  if ($DatIndex.ContainsKey($result.Hash)) {
    $datEntry = $DatIndex[$result.Hash]
  }

  if (-not $datEntry) {
    $result.Status = 'NoMatch'
    if ($Log) { & $Log ("Kein DAT-Match: {0} (Hash: {1})" -f $fileItem.Name, $result.Hash) }
    return $result
  }

  # DAT-Name ermitteln
  $datName = if ($datEntry -is [string]) {
    $datEntry
  } elseif ($datEntry.Name) {
    [string]$datEntry.Name
  } else {
    [string]$datEntry
  }

  # Dateiname sanitisieren (ungueltige Zeichen entfernen)
  $sanitized = $datName
  foreach ($c in [System.IO.Path]::GetInvalidFileNameChars()) {
    $sanitized = $sanitized.Replace([string]$c, '_')
  }

  # Extension beibehalten
  $newFileName = $sanitized + $fileItem.Extension

  # Prüfen ob Rename noetig
  if ($newFileName -eq $fileItem.Name) {
    $result.NewName = $newFileName
    $result.Status = 'AlreadyCorrect'
    return $result
  }

  $result.NewName = $newFileName
  $newPath = Join-Path $fileItem.DirectoryName $newFileName

  # Pfadlaenge pruefen (Windows MAX_PATH)
  if ($newPath.Length -gt 260) {
    $result.Status = 'PathTooLong'
    if ($Log) { & $Log ("Pfad zu lang ({0} Zeichen): {1}" -f $newPath.Length, $newFileName) }
    return $result
  }

  # Konflikt: Zielname existiert bereits
  if (Test-Path -LiteralPath $newPath -PathType Leaf) {
    $result.Status = 'Conflict'
    if ($Log) { & $Log ("Zielname existiert bereits: {0}" -f $newFileName) }
    return $result
  }

  # DryRun: Keine Aenderung
  if ($Mode -eq 'DryRun') {
    $result.Status = 'WouldRename'
    return $result
  }

  # Move-Modus: Tatsaechlich umbenennen
  try {
    Rename-Item -LiteralPath $FilePath -NewName $newFileName -ErrorAction Stop
    $result.Status = 'Renamed'
    if ($Log) { & $Log ("Umbenannt: {0} → {1}" -f $fileItem.Name, $newFileName) }
  } catch {
    $result.Status = 'Error'
    if ($Log) { & $Log ("Rename-Fehler: {0} — {1}" -f $fileItem.Name, $_.Exception.Message) }
  }

  return $result
}

function Invoke-BatchDatRename {
  <#
  .SYNOPSIS
    Batch-Rename mehrerer ROM-Dateien nach DAT-Standard.
  .PARAMETER Files
    Array von Dateipfaden.
  .PARAMETER DatIndex
    DAT-Index-Hashtable.
  .PARAMETER Mode
    DryRun oder Move.
  .PARAMETER HashType
    Hash-Algorithmus.
  .PARAMETER Log
    Optionaler Logging-Callback.
  #>
  param(
    [Parameter(Mandatory)][string[]]$Files,
    [Parameter(Mandatory)][hashtable]$DatIndex,
    [ValidateSet('DryRun','Move')][string]$Mode = 'DryRun',
    [ValidateSet('SHA1','SHA256','MD5')][string]$HashType = 'SHA1',
    [scriptblock]$Log
  )

  $results = [System.Collections.Generic.List[hashtable]]::new()

  foreach ($file in $Files) {
    $r = Rename-RomToDatName -FilePath $file -DatIndex $DatIndex -Mode $Mode -HashType $HashType -Log $Log
    [void]$results.Add($r)
  }

  $summary = @{
    Total          = $results.Count
    Renamed        = @($results | Where-Object { $_.Status -eq 'Renamed' -or $_.Status -eq 'WouldRename' }).Count
    NoMatch        = @($results | Where-Object { $_.Status -eq 'NoMatch' }).Count
    Conflicts      = @($results | Where-Object { $_.Status -eq 'Conflict' }).Count
    AlreadyCorrect = @($results | Where-Object { $_.Status -eq 'AlreadyCorrect' }).Count
    Errors         = @($results | Where-Object { $_.Status -eq 'Error' -or $_.Status -eq 'HashError' }).Count
    Results        = $results
  }

  return $summary
}
