# ================================================================
#  ROM HEADER ANALYSIS – NES/SNES/GBA/N64 Header-Parsing (MF-03)
#  Dependencies: (standalone)
# ================================================================

function Read-RomHeader {
  <#
  .SYNOPSIS
    Liest und analysiert den Header einer ROM-Datei.
  .PARAMETER FilePath
    Pfad zur ROM-Datei.
  .PARAMETER MaxBytes
    Maximale Bytes die gelesen werden (Default: 64KB).
  #>
  param(
    [Parameter(Mandatory)][string]$FilePath,
    [int]$MaxBytes = 65536
  )

  $result = @{
    FilePath   = $FilePath
    Platform   = 'Unknown'
    HeaderType = 'Unknown'
    Title      = ''
    Valid      = $false
    Warnings   = [System.Collections.Generic.List[string]]::new()
    Details    = @{}
  }

  if (-not (Test-Path -LiteralPath $FilePath)) {
    $result.Warnings.Add("Datei nicht gefunden: $FilePath")
    return $result
  }

  try {
    $fileSize = (Get-Item -LiteralPath $FilePath).Length
    $readSize = [math]::Min($fileSize, $MaxBytes)
    $bytes = [System.IO.File]::ReadAllBytes($FilePath) | Select-Object -First $readSize

    # NES (iNES): Bytes 0-3 = "NES\x1A"
    if ($bytes.Count -ge 16 -and $bytes[0] -eq 0x4E -and $bytes[1] -eq 0x45 -and $bytes[2] -eq 0x53 -and $bytes[3] -eq 0x1A) {
      return Read-NesHeader -Bytes $bytes -Result $result
    }

    # N64: Big-Endian, Byte-Swapped oder Little-Endian (Byte-Level-Vergleich fuer PS 5.1 Kompatibilitaet)
    if ($bytes.Count -ge 4) {
      $b0 = [int]$bytes[0]; $b1 = [int]$bytes[1]; $b2 = [int]$bytes[2]; $b3 = [int]$bytes[3]
      $isN64 = ($b0 -eq 0x80 -and $b1 -eq 0x37 -and $b2 -eq 0x12 -and $b3 -eq 0x40) -or  # Big-Endian (.z64)
               ($b0 -eq 0x40 -and $b1 -eq 0x12 -and $b2 -eq 0x37 -and $b3 -eq 0x80) -or  # Byte-Swapped (.n64)
               ($b0 -eq 0x37 -and $b1 -eq 0x80 -and $b2 -eq 0x40 -and $b3 -eq 0x12)      # Little-Endian (.v64)
      if ($isN64) {
        # Format direkt aus erstem Byte bestimmen (vermeidet PS 5.1 signed-int-Probleme bei -shl)
        $n64Format = switch ($b0) {
          0x80 { 'Big-Endian (.z64)' }
          0x40 { 'Byte-Swapped (.n64)' }
          0x37 { 'Little-Endian (.v64)' }
          default { 'Unknown' }
        }
        return Read-N64Header -Bytes $bytes -Result $result -Format $n64Format
      }
    }

    # GBA: Nintendo Logo at 0x04 (156 bytes), Title at 0xA0
    if ($bytes.Count -ge 0xC0 -and $bytes[0xB2] -eq 0x96) {
      return Read-GbaHeader -Bytes $bytes -Result $result
    }

    # SNES: LoROM (0x7FC0) oder HiROM (0xFFC0)
    if ($bytes.Count -ge 0x8000) {
      $loromResult = Read-SnesHeader -Bytes $bytes -Result $result -Offset 0x7FC0 -Type 'LoROM'
      if ($loromResult.Valid) { return $loromResult }
    }
    if ($bytes.Count -ge 0x10000) {
      $hiromResult = Read-SnesHeader -Bytes $bytes -Result $result -Offset 0xFFC0 -Type 'HiROM'
      if ($hiromResult.Valid) { return $hiromResult }
    }

    $result.Warnings.Add('Kein bekanntes Header-Format erkannt')
    return $result
  } catch {
    $result.Warnings.Add("Lesefehler: $($_.Exception.Message)")
    return $result
  }
}

function Read-NesHeader {
  param([byte[]]$Bytes, [hashtable]$Result)

  $Result.Platform   = 'NES'
  $Result.HeaderType = 'iNES'
  $Result.Valid      = $true

  $prgSize = $Bytes[4] * 16  # in KB
  $chrSize = $Bytes[5] * 8   # in KB
  $mirroring = if ($Bytes[6] -band 1) { 'Vertical' } else { 'Horizontal' }
  $battery   = [bool]($Bytes[6] -band 2)
  $trainer   = [bool]($Bytes[6] -band 4)
  $mapperLow = ($Bytes[6] -band 0xF0) -shr 4
  $mapperHigh = $Bytes[7] -band 0xF0
  $mapper = $mapperHigh -bor $mapperLow

  # NES 2.0 Check (Byte 7 Bits 2-3 = 0b10)
  $isNes2 = (($Bytes[7] -band 0x0C) -eq 0x08)
  if ($isNes2) {
    $Result.HeaderType = 'NES 2.0'
  }

  $Result.Details = @{
    PrgSizeKB  = $prgSize
    ChrSizeKB  = $chrSize
    Mirroring  = $mirroring
    Battery    = $battery
    Trainer    = $trainer
    Mapper     = $mapper
    IsNes2     = $isNes2
  }

  # Anomalie-Check
  if ($prgSize -eq 0) {
    $Result.Warnings.Add('PRG-Size ist 0 — moeglicher Bad-Dump')
  }
  if ($mapper -gt 255) {
    $Result.Warnings.Add("Ungewoehnlicher Mapper: $mapper")
  }

  $Result.Title = "NES ROM (Mapper $mapper)"
  return $Result
}

function Read-N64Header {
  param([byte[]]$Bytes, [hashtable]$Result, [string]$Format)

  $Result.Platform   = 'N64'
  $Result.Valid      = $true
  $Result.HeaderType = $Format

  # Title at offset 0x20 (20 bytes, Big-Endian)
  if ($Bytes.Count -ge 0x34) {
    $titleBytes = $Bytes[0x20..0x33]
    $title = [System.Text.Encoding]::ASCII.GetString($titleBytes).Trim(([char]0), ' ')
    $Result.Title = $title
  }

  # Game Code at 0x3B-0x3E
  if ($Bytes.Count -ge 0x3F) {
    $gameCode = [System.Text.Encoding]::ASCII.GetString($Bytes[0x3B..0x3E]).Trim(([char]0))
    $Result.Details = @{
      Format   = $format
      GameCode = $gameCode
    }
  }

  return $Result
}

function Read-GbaHeader {
  param([byte[]]$Bytes, [hashtable]$Result)

  $Result.Platform   = 'GBA'
  $Result.HeaderType = 'GBA'
  $Result.Valid      = $true

  # Title at 0xA0 (12 bytes)
  $titleBytes = $Bytes[0xA0..0xAB]
  $title = [System.Text.Encoding]::ASCII.GetString($titleBytes).Trim(([char]0), ' ')
  $Result.Title = $title

  # Game Code at 0xAC (4 bytes)
  $gameCode = [System.Text.Encoding]::ASCII.GetString($Bytes[0xAC..0xAF]).Trim(([char]0))

  # Maker Code at 0xB0 (2 bytes)
  $makerCode = [System.Text.Encoding]::ASCII.GetString($Bytes[0xB0..0xB1]).Trim(([char]0))

  # Complement Check at 0xBD
  $complement = $Bytes[0xBD]

  # Validate complement
  $sum = 0
  for ($i = 0xA0; $i -le 0xBC; $i++) { $sum += $Bytes[$i] }
  $expectedComplement = (-(($sum + 0x19) -band 0xFF)) -band 0xFF

  if ($complement -ne $expectedComplement) {
    $Result.Warnings.Add('Header-Complement stimmt nicht — moeglicherweise modifiziert')
  }

  $Result.Details = @{
    GameCode   = $gameCode
    MakerCode  = $makerCode
    Complement = $complement
  }

  return $Result
}

function Read-SnesHeader {
  param([byte[]]$Bytes, [hashtable]$Result, [int]$Offset, [string]$Type)

  # Kopiere Result fuer separaten Versuch
  $r = $Result.Clone()
  $r.Warnings = [System.Collections.Generic.List[string]]::new()

  if ($Bytes.Count -lt ($Offset + 32)) {
    $r.Valid = $false
    return $r
  }

  # Title at offset +0 (21 bytes)
  $titleBytes = $Bytes[$Offset..($Offset + 20)]
  $title = [System.Text.Encoding]::ASCII.GetString($titleBytes).Trim(([char]0), ' ')

  # Pruefe ob Title druckbar ist
  $printable = $true
  foreach ($b in $titleBytes) {
    if ($b -ne 0 -and ($b -lt 0x20 -or $b -gt 0x7E)) {
      $printable = $false
      break
    }
  }

  if (-not $printable -or [string]::IsNullOrWhiteSpace($title)) {
    $r.Valid = $false
    return $r
  }

  $r.Platform   = 'SNES'
  $r.HeaderType = $Type
  $r.Title      = $title
  $r.Valid      = $true

  # Map Mode byte at offset +21
  $mapMode = $Bytes[$Offset + 21]
  # ROM Type at offset +22
  $romType = $Bytes[$Offset + 22]
  # ROM Size at offset +23 (2^n KB)
  $romSizeRaw = $Bytes[$Offset + 23]
  $romSizeKB = if ($romSizeRaw -le 13) { [math]::Pow(2, $romSizeRaw) } else { 0 }

  $r.Details = @{
    MappingType = $Type
    MapMode     = $mapMode
    RomType     = $romType
    RomSizeKB   = $romSizeKB
  }

  return $r
}

function Test-RomHeaderIntegrity {
  <#
  .SYNOPSIS
    Prueft ob ein ROM-Header Anomalien aufweist.
  .PARAMETER HeaderResult
    Ergebnis von Read-RomHeader.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$HeaderResult
  )

  $status = 'OK'
  if (-not $HeaderResult.Valid) { $status = 'Unknown' }
  elseif ($HeaderResult.Warnings.Count -gt 0) { $status = 'Anomaly' }

  return @{
    Status   = $status
    Platform = $HeaderResult.Platform
    Title    = $HeaderResult.Title
    Warnings = @($HeaderResult.Warnings)
  }
}
