# ================================================================
#  DAT DIFF VIEWER – DAT-Versions-Vergleich (MF-12)
#  Dependencies: Dat.ps1
# ================================================================

function Compare-DatVersions {
  <#
  .SYNOPSIS
    Vergleicht zwei DAT-Indizes und zeigt Unterschiede.
  .PARAMETER OldIndex
    Aelterer DAT-Index (Hashtable: GameName → Eintrag).
  .PARAMETER NewIndex
    Neuerer DAT-Index.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$OldIndex,
    [Parameter(Mandatory)][hashtable]$NewIndex
  )

  $added = @()
  $removed = @()
  $renamed = @()

  # Finde neue Eintraege
  foreach ($key in $NewIndex.Keys) {
    if (-not $OldIndex.ContainsKey($key)) {
      $added += $key
    }
  }

  # Finde entfernte Eintraege
  foreach ($key in $OldIndex.Keys) {
    if (-not $NewIndex.ContainsKey($key)) {
      $removed += $key
    }
  }

  # Finde umbenannte Eintraege (gleicher Hash, anderer Name)
  if ($removed.Count -gt 0 -and $added.Count -gt 0) {
    $oldByHash = @{}
    foreach ($name in $removed) {
      $entry = $OldIndex[$name]
      if ($entry -and $entry.ContainsKey('hash')) {
        $oldByHash[$entry.hash] = $name
      }
    }

    $detectedRenames = @()
    foreach ($name in $added) {
      $entry = $NewIndex[$name]
      if ($entry -and $entry.ContainsKey('hash') -and $oldByHash.ContainsKey($entry.hash)) {
        $oldName = $oldByHash[$entry.hash]
        $detectedRenames += @{ Old = $oldName; New = $name }
      }
    }

    # Renames aus Added/Removed entfernen
    foreach ($rename in $detectedRenames) {
      $added = @($added | Where-Object { $_ -ne $rename.New })
      $removed = @($removed | Where-Object { $_ -ne $rename.Old })
      $renamed += $rename
    }
  }

  return @{
    Added   = $added
    Removed = $removed
    Renamed = $renamed
    Count   = @{
      Added   = $added.Count
      Removed = $removed.Count
      Renamed = $renamed.Count
      Total   = $added.Count + $removed.Count + $renamed.Count
    }
  }
}

function Get-DatDiffSummary {
  <#
  .SYNOPSIS
    Erstellt eine menschenlesbare Zusammenfassung des DAT-Diffs.
  .PARAMETER Diff
    Diff-Ergebnis von Compare-DatVersions.
  .PARAMETER SourceName
    Name der DAT-Quelle.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Diff,
    [string]$SourceName = 'Unknown'
  )

  $lines = @()
  $lines += "DAT-Diff: $SourceName"
  $lines += "  Hinzugefuegt: $($Diff.Count.Added)"
  $lines += "  Entfernt:     $($Diff.Count.Removed)"
  $lines += "  Umbenannt:    $($Diff.Count.Renamed)"
  $lines += "  Gesamt:       $($Diff.Count.Total)"

  return @{
    Summary    = $lines -join "`n"
    SourceName = $SourceName
    HasChanges = ($Diff.Count.Total -gt 0)
  }
}

function Compare-DatEntryDetail {
  <#
  .SYNOPSIS
    Vergleicht zwei einzelne DAT-Eintraege auf Detail-Ebene.
  .PARAMETER OldEntry
    Alter Eintrag.
  .PARAMETER NewEntry
    Neuer Eintrag.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$OldEntry,
    [Parameter(Mandatory)][hashtable]$NewEntry
  )

  $changes = @()
  $allKeys = @($OldEntry.Keys) + @($NewEntry.Keys) | Sort-Object -Unique

  foreach ($key in $allKeys) {
    $oldVal = if ($OldEntry.ContainsKey($key)) { $OldEntry[$key] } else { $null }
    $newVal = if ($NewEntry.ContainsKey($key)) { $NewEntry[$key] } else { $null }

    if ("$oldVal" -ne "$newVal") {
      $changes += @{ Field = $key; OldValue = $oldVal; NewValue = $newVal }
    }
  }

  return @{
    HasChanges = ($changes.Count -gt 0)
    Changes    = $changes
  }
}
