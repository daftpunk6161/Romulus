# ================================================================
#  M3U GENERATOR – Multi-Disc-Playlist-Generierung (QW-15)
#  Dependencies: SetParsing.ps1, Core.ps1
# ================================================================

# Disc-Erkennung-Pattern
$script:RX_DISC_NUMBER = [regex]::new('\(Disc\s*(\d+)\)', 'IgnoreCase, Compiled')

function Group-MultiDiscFiles {
  <#
  .SYNOPSIS
    Gruppiert Dateien nach Spielname und erkennt Multi-Disc-Sets.
  .PARAMETER Files
    Array von Dateipfaden oder FileInfo-Objekten.
  #>
  param(
    [Parameter(Mandatory)][object[]]$Files
  )

  $groups = [hashtable]::new([StringComparer]::OrdinalIgnoreCase)

  foreach ($file in $Files) {
    $path = if ($file -is [string]) { $file }
            elseif ($file.FullName) { $file.FullName }
            else { [string]$file }

    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($path)

    # Disc-Nummer extrahieren
    $discMatch = $script:RX_DISC_NUMBER.Match($fileName)
    if (-not $discMatch.Success) { continue }

    $discNum = [int]$discMatch.Groups[1].Value

    # Spielname = alles vor dem "(Disc X)" Tag
    $gameName = $fileName.Substring(0, $discMatch.Index).TrimEnd(' ', '-', '_')
    # Alles nach "(Disc X)" anhaengen (z.B. Region-Tags)
    $suffix = $fileName.Substring($discMatch.Index + $discMatch.Length).TrimStart(' ')
    $groupKey = $gameName + '|' + $suffix

    if (-not $groups.ContainsKey($groupKey)) {
      $groups[$groupKey] = @{
        GameName = $gameName
        Suffix   = $suffix
        Discs    = [System.Collections.Generic.SortedDictionary[int,string]]::new()
      }
    }

    $groups[$groupKey].Discs[$discNum] = $path
  }

  return $groups
}

function Test-DiscSequenceComplete {
  <#
  .SYNOPSIS
    Prueft ob eine Disc-Sequenz lueckenlos ist (1, 2, 3, ...).
  .PARAMETER DiscNumbers
    Array von Disc-Nummern.
  #>
  param(
    [Parameter(Mandatory)][int[]]$DiscNumbers
  )

  $sorted = $DiscNumbers | Sort-Object
  $min = $sorted[0]
  $max = $sorted[-1]

  $missing = [System.Collections.Generic.List[int]]::new()
  for ($i = $min; $i -le $max; $i++) {
    if ($i -notin $sorted) {
      [void]$missing.Add($i)
    }
  }

  return @{
    Complete = ($missing.Count -eq 0)
    Missing  = @($missing)
    Min      = $min
    Max      = $max
  }
}

function New-M3uPlaylist {
  <#
  .SYNOPSIS
    Erstellt eine .m3u-Playlist fuer ein Multi-Disc-Spiel.
  .PARAMETER DiscFiles
    Sortierte Disc-Dateien (Hashtable: DiscNumber → FilePath).
  .PARAMETER OutputDir
    Ausgabeverzeichnis fuer die .m3u-Datei.
  .PARAMETER GameName
    Name des Spiels (wird als Dateiname verwendet).
  .PARAMETER Mode
    DryRun = nur Preview, Move = tatsaechlich schreiben.
  .PARAMETER Log
    Optionaler Logging-Callback.
  #>
  param(
    [Parameter(Mandatory)][System.Collections.IDictionary]$DiscFiles,
    [Parameter(Mandatory)][string]$OutputDir,
    [Parameter(Mandatory)][string]$GameName,
    [ValidateSet('DryRun','Move')][string]$Mode = 'DryRun',
    [scriptblock]$Log
  )

  $result = @{
    M3uPath   = $null
    DiscCount = $DiscFiles.Count
    GameName  = $GameName
    Status    = 'Unknown'
    Warnings  = [System.Collections.Generic.List[string]]::new()
  }

  if ($DiscFiles.Count -eq 0) {
    $result.Status = 'NoDiscs'
    return $result
  }

  # Disc-Sequenz pruefen
  $discNums = @($DiscFiles.Keys | ForEach-Object { [int]$_ })
  $seqCheck = Test-DiscSequenceComplete -DiscNumbers $discNums

  if (-not $seqCheck.Complete) {
    foreach ($missing in $seqCheck.Missing) {
      [void]$result.Warnings.Add("Disc $missing fehlt")
    }
    if ($Log) { & $Log ("Warnung: {0} - fehlende Discs: {1}" -f $GameName, ($seqCheck.Missing -join ', ')) }
  }

  # M3U-Dateiname
  $m3uFileName = $GameName + '.m3u'
  # Ungueltige Zeichen entfernen
  foreach ($c in [System.IO.Path]::GetInvalidFileNameChars()) {
    $m3uFileName = $m3uFileName.Replace([string]$c, '_')
  }

  $m3uPath = Join-Path $OutputDir $m3uFileName
  $result.M3uPath = $m3uPath

  # M3U existiert bereits
  if (Test-Path -LiteralPath $m3uPath -PathType Leaf) {
    $result.Status = 'AlreadyExists'
    [void]$result.Warnings.Add('M3U existiert bereits')
    if ($Log) { & $Log ("M3U existiert bereits: {0}" -f $m3uPath) }
    return $result
  }

  # DryRun
  if ($Mode -eq 'DryRun') {
    $result.Status = 'WouldCreate'
    return $result
  }

  # M3U schreiben
  try {
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('#EXTM3U')

    foreach ($discNum in ($DiscFiles.Keys | Sort-Object { [int]$_ })) {
      $filePath = $DiscFiles[$discNum]
      $relativeName = [System.IO.Path]::GetFileName($filePath)
      [void]$sb.AppendLine($relativeName)
    }

    # Verzeichnis sicherstellen
    if (-not (Test-Path -LiteralPath $OutputDir -PathType Container)) {
      New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    }

    $sb.ToString().TrimEnd() | Out-File -LiteralPath $m3uPath -Encoding utf8 -Force -NoNewline
    $result.Status = 'Created'
    if ($Log) { & $Log ("M3U erstellt: {0} ({1} Discs)" -f $m3uPath, $DiscFiles.Count) }
  } catch {
    $result.Status = 'Error'
    if ($Log) { & $Log ("M3U-Fehler: {0}" -f $_.Exception.Message) }
  }

  return $result
}

function Invoke-M3uAutoGeneration {
  <#
  .SYNOPSIS
    Scannt ein Verzeichnis und erstellt M3U-Playlists fuer alle Multi-Disc-Spiele.
  .PARAMETER Directory
    Verzeichnis mit ROM-Dateien.
  .PARAMETER Mode
    DryRun oder Move.
  .PARAMETER Log
    Optionaler Logging-Callback.
  #>
  param(
    [Parameter(Mandatory)][string]$Directory,
    [ValidateSet('DryRun','Move')][string]$Mode = 'DryRun',
    [scriptblock]$Log
  )

  if (-not (Test-Path -LiteralPath $Directory -PathType Container)) {
    return @{ Status = 'DirectoryNotFound'; Created = 0; Skipped = 0 }
  }

  $files = @(Get-ChildItem -LiteralPath $Directory -File -ErrorAction SilentlyContinue |
             Where-Object { $_.Extension -match '\.(chd|cue|bin|iso|img|pbp|cso)$' })

  $groups = Group-MultiDiscFiles -Files $files
  $created = 0
  $skipped = 0
  $results = [System.Collections.Generic.List[hashtable]]::new()

  foreach ($key in $groups.Keys) {
    $group = $groups[$key]
    if ($group.Discs.Count -lt 2) {
      $skipped++
      continue
    }

    $r = New-M3uPlaylist -DiscFiles $group.Discs -OutputDir $Directory `
           -GameName $group.GameName -Mode $Mode -Log $Log

    [void]$results.Add($r)

    if ($r.Status -eq 'Created' -or $r.Status -eq 'WouldCreate') {
      $created++
    } else {
      $skipped++
    }
  }

  return @{
    Status   = 'OK'
    Created  = $created
    Skipped  = $skipped
    Results  = @($results)
  }
}
