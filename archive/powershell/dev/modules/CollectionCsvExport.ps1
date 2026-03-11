# ================================================================
#  COLLECTION CSV EXPORT – Sammlung als Excel-CSV (QW-13)
#  Dependencies: Report.ps1
# ================================================================

function Protect-CsvField {
  <#
  .SYNOPSIS
    Schuetzt einen CSV-Feldwert gegen CSV-Injection.
    Felder die mit =, +, -, @ beginnen, werden mit ' prefixed.
  .PARAMETER Value
    Feldwert.
  #>
  param(
    [string]$Value
  )

  if ([string]::IsNullOrWhiteSpace($Value)) { return '' }

  $trimmed = $Value.TrimStart()

  # CSV-Injection-Schutz: gefaehrliche Startzeichen
  if ($trimmed.Length -gt 0 -and $trimmed[0] -in @('=', '+', '-', '@', [char]0x09, [char]0x0D)) {
    return "'" + $Value
  }

  return $Value
}

function Export-CollectionCsv {
  <#
  .SYNOPSIS
    Exportiert eine ROM-Sammlung als strukturiertes CSV mit allen Metadaten.
  .PARAMETER Items
    Array von ROM-Eintraegen.
  .PARAMETER OutputPath
    Pfad fuer die Ausgabe-CSV-Datei.
  .PARAMETER Delimiter
    CSV-Trennzeichen (Default: Semikolon fuer Excel DE).
  .PARAMETER IncludeHash
    Ob SHA1-Hash einbezogen werden soll.
  .PARAMETER Log
    Optionaler Logging-Callback.
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Items,
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$Delimiter = ';',
    [bool]$IncludeHash = $true,
    [scriptblock]$Log
  )

  $result = @{
    OutputPath = $OutputPath
    RowCount   = 0
    Status     = 'Unknown'
  }

  if ($Items.Count -eq 0) {
    $result.Status = 'Empty'
    return $result
  }

  try {
    $sb = New-Object System.Text.StringBuilder

    # UTF-8 BOM fuer Excel-Kompatibilitaet
    # (wird beim Schreiben via Encoding gehandhabt)

    # Header
    $headers = @('Dateiname', 'Konsole', 'Region', 'Format', 'Groesse_MB', 'Kategorie', 'DAT_Status', 'Pfad')
    if ($IncludeHash) { $headers += 'Hash_SHA1' }
    [void]$sb.AppendLine(($headers -join $Delimiter))

    foreach ($item in $Items) {
      $row = @()

      # Dateiname
      $name = ''
      if ($item -is [hashtable]) {
        $name = if ($item.ContainsKey('Name')) { $item.Name }
                elseif ($item.ContainsKey('FileName')) { $item.FileName }
                elseif ($item.ContainsKey('MainPath')) { [System.IO.Path]::GetFileName($item.MainPath) }
                else { '' }
      } else {
        $name = if ($item.PSObject.Properties.Name -contains 'Name') { $item.Name }
                elseif ($item.PSObject.Properties.Name -contains 'FileName') { $item.FileName }
                elseif ($item.PSObject.Properties.Name -contains 'MainPath') { [System.IO.Path]::GetFileName($item.MainPath) }
                else { '' }
      }
      $row += Protect-CsvField -Value ([string]$name)

      # Konsole
      $console = ''
      if ($item -is [hashtable]) {
        $console = if ($item.ContainsKey('Console')) { $item.Console }
                   elseif ($item.ContainsKey('ConsoleType')) { $item.ConsoleType }
                   else { '' }
      } else {
        $console = if ($item.PSObject.Properties.Name -contains 'Console') { $item.Console }
                   elseif ($item.PSObject.Properties.Name -contains 'ConsoleType') { $item.ConsoleType }
                   else { '' }
      }
      $row += Protect-CsvField -Value ([string]$console)

      # Region, Format, Groesse, Kategorie, DAT-Status
      $region = Get-ItemFieldValue -Item $item -Fields @('Region','Regions') -Default ''
      $format = Get-ItemFieldValue -Item $item -Fields @('Format','Extension') -Default ''
      $sizeBytes = [long](Get-ItemFieldValue -Item $item -Fields @('Size','Length','SizeBytes') -Default 0)
      $sizeMb = [math]::Round($sizeBytes / 1MB, 2)
      $category = Get-ItemFieldValue -Item $item -Fields @('Category','Action') -Default ''
      $datStatus = Get-ItemFieldValue -Item $item -Fields @('DatStatus','DatMatch') -Default ''
      $path = Get-ItemFieldValue -Item $item -Fields @('Path','MainPath','FullPath') -Default ''

      $row += Protect-CsvField -Value ([string]$region)
      $row += Protect-CsvField -Value ([string]$format)
      $row += [string]$sizeMb
      $row += Protect-CsvField -Value ([string]$category)
      $row += Protect-CsvField -Value ([string]$datStatus)
      $row += Protect-CsvField -Value ([string]$path)

      if ($IncludeHash) {
        $hash = Get-ItemFieldValue -Item $item -Fields @('Hash','HashSHA1','SHA1') -Default ''
        $row += Protect-CsvField -Value ([string]$hash)
      }

      [void]$sb.AppendLine(($row -join $Delimiter))
      $result.RowCount++
    }

    # Verzeichnis sicherstellen
    $dir = Split-Path -Parent $OutputPath
    if ($dir -and -not (Test-Path -LiteralPath $dir -PathType Container)) {
      New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    # UTF-8 mit BOM schreiben
    $encoding = New-Object System.Text.UTF8Encoding $true
    [System.IO.File]::WriteAllText($OutputPath, $sb.ToString(), $encoding)

    $result.Status = 'Success'
    if ($Log) { & $Log ("CSV exportiert: {0} ({1} Zeilen)" -f $OutputPath, $result.RowCount) }
  } catch {
    $result.Status = 'Error'
    $result.Error = $_.Exception.Message
    if ($Log) { & $Log ("CSV-Export Fehler: {0}" -f $_.Exception.Message) }
  }

  return $result
}

function Get-ItemFieldValue {
  <# Helper: Liest Feldwert aus Hashtable oder PSObject. #>
  param(
    [object]$Item,
    [string[]]$Fields,
    [object]$Default = $null
  )

  foreach ($f in $Fields) {
    if ($Item -is [hashtable]) {
      if ($Item.ContainsKey($f) -and $null -ne $Item[$f]) { return $Item[$f] }
    } elseif ($Item.PSObject.Properties.Name -contains $f -and $null -ne $Item.$f) {
      return $Item.$f
    }
  }

  return $Default
}
