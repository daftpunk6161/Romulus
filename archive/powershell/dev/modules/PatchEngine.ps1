#  IPS/BPS/UPS PATCH ENGINE (LF-05)
#  Automatische Patch-Anwendung mit Backup + Verifizierung.

function Test-PatchFormat {
  <#
  .SYNOPSIS
    Erkennt das Patch-Format anhand der Magic-Bytes.
  #>
  param(
    [Parameter(Mandatory)][string]$PatchPath
  )

  if (-not (Test-Path -LiteralPath $PatchPath)) {
    return 'Unknown'
  }

  try {
    $bytes = [byte[]]::new(5)
    $stream = [System.IO.File]::OpenRead($PatchPath)
    try {
      $read = $stream.Read($bytes, 0, 5)
    } finally {
      $stream.Close()
    }

    if ($read -ge 5 -and $bytes[0] -eq 0x50 -and $bytes[1] -eq 0x41 -and $bytes[2] -eq 0x54 -and $bytes[3] -eq 0x43 -and $bytes[4] -eq 0x48) {
      return 'IPS'
    }
    if ($read -ge 4 -and $bytes[0] -eq 0x42 -and $bytes[1] -eq 0x50 -and $bytes[2] -eq 0x53 -and $bytes[3] -eq 0x31) {
      return 'BPS'
    }
    if ($read -ge 4 -and $bytes[0] -eq 0x55 -and $bytes[1] -eq 0x50 -and $bytes[2] -eq 0x53 -and $bytes[3] -eq 0x31) {
      return 'UPS'
    }
  } catch {
    # Lesefehler
  }

  return 'Unknown'
}

function Read-IpsPatch {
  <#
  .SYNOPSIS
    Liest ein IPS-Patch-File und gibt die Records zurueck.
  #>
  param(
    [Parameter(Mandatory)][byte[]]$PatchBytes
  )

  $records = [System.Collections.Generic.List[hashtable]]::new()

  # Header: "PATCH" (5 bytes)
  if ($PatchBytes.Count -lt 8) { return ,$records.ToArray() }

  $pos = 5  # Skip "PATCH"

  while ($pos + 3 -le $PatchBytes.Count) {
    # Check for EOF marker "EOF" (0x45 0x4F 0x46)
    if ($PatchBytes[$pos] -eq 0x45 -and $PatchBytes[$pos+1] -eq 0x4F -and $PatchBytes[$pos+2] -eq 0x46) {
      break
    }

    # Offset: 3 bytes big-endian
    $offset = ([int]$PatchBytes[$pos] -shl 16) -bor ([int]$PatchBytes[$pos+1] -shl 8) -bor [int]$PatchBytes[$pos+2]
    $pos += 3

    if ($pos + 2 -gt $PatchBytes.Count) { break }

    # Size: 2 bytes big-endian
    $size = ([int]$PatchBytes[$pos] -shl 8) -bor [int]$PatchBytes[$pos+1]
    $pos += 2

    if ($size -gt 0) {
      # Normal record
      if ($pos + $size -gt $PatchBytes.Count) { break }
      $data = $PatchBytes[$pos..($pos + $size - 1)]
      $pos += $size
      $records.Add(@{ Offset = $offset; Size = $size; Data = $data; Type = 'Normal' })
    } else {
      # RLE record
      if ($pos + 3 -gt $PatchBytes.Count) { break }
      $rleSize = ([int]$PatchBytes[$pos] -shl 8) -bor [int]$PatchBytes[$pos+1]
      $pos += 2
      $rleByte = $PatchBytes[$pos]
      $pos += 1
      $records.Add(@{ Offset = $offset; Size = $rleSize; RleByte = $rleByte; Type = 'RLE' })
    }
  }

  return ,$records.ToArray()
}

function Invoke-IpsPatch {
  <#
  .SYNOPSIS
    Wendet IPS-Patch-Records auf ROM-Daten an (in-memory).
  #>
  param(
    [Parameter(Mandatory)][byte[]]$RomData,
    [Parameter(Mandatory)][array]$Records
  )

  $output = [byte[]]::new([math]::Max($RomData.Count, 1))
  [Array]::Copy($RomData, $output, $RomData.Count)

  foreach ($rec in $Records) {
    $neededSize = $rec.Offset + $rec.Size
    if ($neededSize -gt $output.Count) {
      $newOutput = [byte[]]::new($neededSize)
      [Array]::Copy($output, $newOutput, $output.Count)
      $output = $newOutput
    }

    if ($rec.Type -eq 'Normal') {
      [Array]::Copy($rec.Data, 0, $output, $rec.Offset, $rec.Size)
    } elseif ($rec.Type -eq 'RLE') {
      for ($i = 0; $i -lt $rec.Size; $i++) {
        $output[$rec.Offset + $i] = $rec.RleByte
      }
    }
  }

  return ,$output
}

function New-PatchOperation {
  <#
  .SYNOPSIS
    Erstellt eine Patch-Operation mit Backup-Info.
  #>
  param(
    [Parameter(Mandatory)][string]$RomPath,
    [Parameter(Mandatory)][string]$PatchPath,
    [string]$BackupDir = '',
    [switch]$Verify
  )

  $format = Test-PatchFormat -PatchPath $PatchPath

  return @{
    RomPath   = $RomPath
    PatchPath = $PatchPath
    Format    = $format
    BackupDir = $BackupDir
    Verify    = [bool]$Verify
    Status    = 'Pending'
    Error     = ''
  }
}

function Get-PatchSummary {
  <#
  .SYNOPSIS
    Zusammenfassung ueber Patch-Operationen.
  #>
  param(
    [Parameter(Mandatory)][array]$Operations
  )

  $byFormat = @{}
  foreach ($op in $Operations) {
    if (-not $byFormat.ContainsKey($op.Format)) { $byFormat[$op.Format] = 0 }
    $byFormat[$op.Format]++
  }

  return @{
    Total     = $Operations.Count
    ByFormat  = $byFormat
    Pending   = @($Operations | Where-Object { $_.Status -eq 'Pending' }).Count
    Completed = @($Operations | Where-Object { $_.Status -eq 'Completed' }).Count
    Failed    = @($Operations | Where-Object { $_.Status -eq 'Failed' }).Count
  }
}
