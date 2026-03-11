# ================================================================
#  TOSEC DAT SUPPORT – TOSEC-Katalog-Parser (MF-13)
#  Dependencies: Dat.ps1, DatSources.ps1
# ================================================================

function Test-TosecDatFormat {
  <#
  .SYNOPSIS
    Prueft ob eine DAT-Datei im TOSEC-Format vorliegt.
  .PARAMETER XmlContent
    XML-Inhalt als String.
  #>
  param(
    [Parameter(Mandatory)][string]$XmlContent
  )

  # TOSEC-DATs nutzen <header><name> mit TOSEC-Prefix oder <datafile>
  return ($XmlContent -match '<header>' -and $XmlContent -match 'TOSEC') -or
         ($XmlContent -match '<datafile>' -and $XmlContent -match 'TOSEC')
}

function ConvertFrom-TosecDat {
  <#
  .SYNOPSIS
    Parst eine TOSEC-DAT-Datei in ein einheitliches Index-Format.
  .PARAMETER Path
    Pfad zur DAT-Datei.
  #>
  param(
    [Parameter(Mandatory)][string]$Path
  )

  if (-not (Test-Path $Path)) {
    return @{ Status = 'Error'; Reason = 'FileNotFound'; Entries = @{} }
  }

  $content = Get-Content -Path $Path -Raw -Encoding UTF8

  if (-not (Test-TosecDatFormat -XmlContent $content)) {
    return @{ Status = 'Error'; Reason = 'NotTosecFormat'; Entries = @{} }
  }

  # Sicheres XML-Parsing ohne XXE
  $xmlSettings = New-Object System.Xml.XmlReaderSettings
  $xmlSettings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
  $xmlSettings.XmlResolver = $null

  try {
    $reader = [System.Xml.XmlReader]::Create((New-Object System.IO.StringReader($content)), $xmlSettings)
    $xml = New-Object System.Xml.XmlDocument
    $xml.Load($reader)
    $reader.Close()
  } catch {
    return @{ Status = 'Error'; Reason = 'XmlParseError'; Message = $_.Exception.Message; Entries = @{} }
  }

  $entries = @{}
  $gameNodes = $xml.SelectNodes('//game')

  foreach ($game in $gameNodes) {
    $name = $game.GetAttribute('name')
    if (-not $name) { continue }

    $romNode = $game.SelectSingleNode('rom')
    $entry = @{
      Name   = $name
      Source = 'TOSEC'
    }

    if ($romNode) {
      if ($romNode.GetAttribute('size')) { $entry['Size'] = $romNode.GetAttribute('size') }
      if ($romNode.GetAttribute('crc'))  { $entry['CRC32'] = $romNode.GetAttribute('crc') }
      if ($romNode.GetAttribute('md5'))  { $entry['MD5'] = $romNode.GetAttribute('md5') }
      if ($romNode.GetAttribute('sha1')) { $entry['SHA1'] = $romNode.GetAttribute('sha1') }
      $entry['hash'] = if ($romNode.GetAttribute('sha1')) { $romNode.GetAttribute('sha1') } elseif ($romNode.GetAttribute('md5')) { $romNode.GetAttribute('md5') } else { $romNode.GetAttribute('crc') }
    }

    # TOSEC-Namensschema parsen: Title (Year)(Publisher)(Region)
    $parsed = ConvertFrom-TosecName -TosecName $name
    if ($parsed) {
      $entry += $parsed
    }

    $entries[$name] = $entry
  }

  return @{
    Status  = 'OK'
    Entries = $entries
    Count   = $entries.Count
    Source  = 'TOSEC'
  }
}

function ConvertFrom-TosecName {
  <#
  .SYNOPSIS
    Parst einen TOSEC-Dateinamen in seine Bestandteile.
    Format: Title (Year)(Publisher)(Region)(other tags)
  .PARAMETER TosecName
    TOSEC-formatierter Name.
  #>
  param(
    [Parameter(Mandatory)][string]$TosecName
  )

  $result = @{}

  # Title extrahieren (alles vor dem ersten Klammer-Block)
  if ($TosecName -match '^([^(]+)') {
    $result['Title'] = $Matches[1].Trim()
  }

  # Jahr: (YYYY) oder (19xx) oder (20xx)
  if ($TosecName -match '\((\d{4})\)') {
    $result['Year'] = $Matches[1]
  }

  # Region: gängige TOSEC-Codes
  $regionPatterns = @('EU', 'US', 'JP', 'DE', 'FR', 'ES', 'IT', 'NL', 'SE', 'AU', 'BR', 'CA', 'CN', 'KR', 'TW', 'World')
  foreach ($rp in $regionPatterns) {
    if ($TosecName -match "\($rp\)") {
      $result['Region'] = $rp
      break
    }
  }

  # Publisher: zweite Klammer nach Jahr
  if ($TosecName -match '\(\d{4}\)\(([^)]+)\)') {
    $result['Publisher'] = $Matches[1]
  }

  return $result
}

function Merge-TosecWithDatIndex {
  <#
  .SYNOPSIS
    Fuegt TOSEC-Eintraege in einen bestehenden DAT-Index ein.
  .PARAMETER ExistingIndex
    Bestehender DAT-Index.
  .PARAMETER TosecIndex
    TOSEC-Eintraege.
  .PARAMETER OverwriteExisting
    Ob bestehende Eintraege ueberschrieben werden sollen.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$ExistingIndex,
    [Parameter(Mandatory)][hashtable]$TosecIndex,
    [bool]$OverwriteExisting = $false
  )

  $merged = @{}
  $added = 0
  $skipped = 0

  foreach ($key in $ExistingIndex.Keys) {
    $merged[$key] = $ExistingIndex[$key]
  }

  foreach ($key in $TosecIndex.Keys) {
    if ($merged.ContainsKey($key) -and -not $OverwriteExisting) {
      $skipped++
      continue
    }
    $merged[$key] = $TosecIndex[$key]
    $added++
  }

  return @{
    MergedIndex = $merged
    TotalCount  = $merged.Count
    Added       = $added
    Skipped     = $skipped
  }
}
