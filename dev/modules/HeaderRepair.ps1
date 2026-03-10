#  ROM HEADER REPAIR (LF-06)
#  Bekannte Header-Probleme automatisch fixen (NES-Header, SNES-Copier-Header).

function Test-NesHeaderValid {
  <#
  .SYNOPSIS
    Prueft ob ein NES iNES-Header gueltig ist.
  #>
  param(
    [Parameter(Mandatory)][byte[]]$HeaderBytes
  )

  if ($HeaderBytes.Count -lt 16) { return @{ Valid = $false; Issue = 'Header zu kurz' } }
  if ($HeaderBytes[0] -ne 0x4E -or $HeaderBytes[1] -ne 0x45 -or $HeaderBytes[2] -ne 0x53 -or $HeaderBytes[3] -ne 0x1A) {
    return @{ Valid = $false; Issue = 'Kein iNES-Magic' }
  }

  $issues = [System.Collections.Generic.List[string]]::new()

  # Bytes 12-15 sollten in iNES 1.0 Null sein (ausser NES 2.0)
  $isNes2 = (($HeaderBytes[7] -band 0x0C) -eq 0x08)
  if (-not $isNes2) {
    for ($i = 12; $i -le 15; $i++) {
      if ($HeaderBytes[$i] -ne 0) {
        $issues.Add("Byte $i nicht null: $($HeaderBytes[$i])")
      }
    }
  }

  return @{
    Valid  = ($issues.Count -eq 0)
    Issues = ,$issues.ToArray()
    IsNes2 = $isNes2
  }
}

function Repair-NesHeader {
  <#
  .SYNOPSIS
    Repariert bekannte NES iNES-Header-Probleme (Dirty Bytes 12-15).
  #>
  param(
    [Parameter(Mandatory)][byte[]]$RomData
  )

  if ($RomData.Count -lt 16) {
    return @{ Success = $false; Error = 'ROM zu kurz fuer Header-Reparatur' }
  }

  $repaired = [byte[]]::new($RomData.Count)
  [Array]::Copy($RomData, $repaired, $RomData.Count)

  $isNes2 = (($repaired[7] -band 0x0C) -eq 0x08)
  $changes = 0

  if (-not $isNes2) {
    for ($i = 12; $i -le 15; $i++) {
      if ($repaired[$i] -ne 0) {
        $repaired[$i] = 0
        $changes++
      }
    }
  }

  return @{
    Success  = $true
    Data     = ,$repaired
    Changes  = $changes
    Repaired = ($changes -gt 0)
  }
}

function Test-SnesCopierHeader {
  <#
  .SYNOPSIS
    Prueft ob ein SNES-ROM einen Copier-Header (512 Bytes) hat.
  #>
  param(
    [Parameter(Mandatory)][byte[]]$RomData
  )

  if ($RomData.Count -lt 1024) {
    return @{ HasCopierHeader = $false; Confidence = 'Low' }
  }

  # Standard SNES ROM sizes sind Vielfache von 0x8000 (32 KB)
  $sizeWithout = $RomData.Count - 512
  $hasExtraBytes = ($RomData.Count % 0x8000) -eq 512

  # Copier-Header: erste 512 Bytes pruefen
  $allZero = $true
  for ($i = 2; $i -lt 512; $i++) {
    if ($RomData[$i] -ne 0) { $allZero = $false; break }
  }

  $confidence = 'Low'
  if ($hasExtraBytes) { $confidence = 'Medium' }
  if ($hasExtraBytes -and $allZero) { $confidence = 'High' }

  return @{
    HasCopierHeader = $hasExtraBytes
    Confidence      = $confidence
    RomSizeWithout  = $sizeWithout
  }
}

function Remove-SnesCopierHeader {
  <#
  .SYNOPSIS
    Entfernt den 512-Byte Copier-Header von einem SNES-ROM.
  #>
  param(
    [Parameter(Mandatory)][byte[]]$RomData
  )

  $check = Test-SnesCopierHeader -RomData $RomData
  if (-not $check.HasCopierHeader) {
    return @{ Success = $true; Data = ,$RomData; Removed = $false }
  }

  $newData = [byte[]]::new($RomData.Count - 512)
  [Array]::Copy($RomData, 512, $newData, 0, $newData.Count)

  return @{
    Success = $true
    Data    = ,$newData
    Removed = $true
  }
}

function Get-HeaderRepairPlan {
  <#
  .SYNOPSIS
    Analysiert mehrere ROMs und erstellt einen Reparaturplan.
  #>
  param(
    [Parameter(Mandatory)][array]$RomFiles
  )

  $plan = [System.Collections.Generic.List[hashtable]]::new()

  foreach ($file in $RomFiles) {
    $entry = @{
      Path    = $file.Path
      Console = $file.Console
      Action  = 'None'
      Issue   = ''
    }

    if ($file.Console -eq 'nes' -and $file.HeaderBytes) {
      $check = Test-NesHeaderValid -HeaderBytes $file.HeaderBytes
      if (-not $check.Valid) {
        $entry.Action = 'RepairNesHeader'
        $entry.Issue = ($check.Issues -join '; ')
      }
    }

    if ($file.Console -eq 'snes' -and $file.RomData) {
      $check = Test-SnesCopierHeader -RomData $file.RomData
      if ($check.HasCopierHeader) {
        $entry.Action = 'RemoveCopierHeader'
        $entry.Issue = "Copier-Header ($($check.Confidence) Confidence)"
      }
    }

    $plan.Add($entry)
  }

  return ,$plan.ToArray()
}
