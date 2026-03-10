# ================================================================
#  DRYRUN COMPARE – Side-by-Side DryRun-Vergleich (MF-21)
#  Dependencies: ReportBuilder.ps1
# ================================================================

function Compare-DryRunResults {
  <#
  .SYNOPSIS
    Vergleicht zwei DryRun-Ergebnisse Side-by-Side.
  .PARAMETER ResultA
    Ergebnis des ersten DryRuns (Array von Items).
  .PARAMETER ResultB
    Ergebnis des zweiten DryRuns (Array von Items).
  .PARAMETER KeyField
    Feld fuer den Abgleich (z.B. 'SourcePath' oder 'OldPath').
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$ResultA,
    [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$ResultB,
    [string]$KeyField = 'OldPath'
  )

  $itemsA = @{}
  $itemsB = @{}

  if ($ResultA) {
    foreach ($item in $ResultA) {
      $key = if ($item -is [hashtable] -and $item.ContainsKey($KeyField)) { $item[$KeyField] } else { $null }
      if ($key) { $itemsA[$key] = $item }
    }
  }

  if ($ResultB) {
    foreach ($item in $ResultB) {
      $key = if ($item -is [hashtable] -and $item.ContainsKey($KeyField)) { $item[$KeyField] } else { $null }
      if ($key) { $itemsB[$key] = $item }
    }
  }

  $allKeys = @(@($itemsA.Keys) + @($itemsB.Keys) | Sort-Object -Unique)

  $onlyA = @()
  $onlyB = @()
  $different = @()
  $identical = @()

  foreach ($key in $allKeys) {
    $inA = $itemsA.ContainsKey($key)
    $inB = $itemsB.ContainsKey($key)

    if ($inA -and -not $inB) {
      $onlyA += $itemsA[$key]
    } elseif ($inB -and -not $inA) {
      $onlyB += $itemsB[$key]
    } else {
      # Beide vorhanden - vergleiche Zielverhalten
      $targetA = if ($itemsA[$key].ContainsKey('NewPath')) { $itemsA[$key].NewPath } else { '' }
      $targetB = if ($itemsB[$key].ContainsKey('NewPath')) { $itemsB[$key].NewPath } else { '' }
      $actionA = if ($itemsA[$key].ContainsKey('Action')) { $itemsA[$key].Action } else { '' }
      $actionB = if ($itemsB[$key].ContainsKey('Action')) { $itemsB[$key].Action } else { '' }

      if ($targetA -eq $targetB -and $actionA -eq $actionB) {
        $identical += @{ Key = $key; ItemA = $itemsA[$key]; ItemB = $itemsB[$key] }
      } else {
        $different += @{
          Key    = $key
          ItemA  = $itemsA[$key]
          ItemB  = $itemsB[$key]
          DiffTarget = ($targetA -ne $targetB)
          DiffAction = ($actionA -ne $actionB)
        }
      }
    }
  }

  return @{
    OnlyInA   = $onlyA
    OnlyInB   = $onlyB
    Different = $different
    Identical = $identical
    Summary   = @{
      TotalKeys  = $allKeys.Count
      OnlyA      = $onlyA.Count
      OnlyB      = $onlyB.Count
      Different  = $different.Count
      Identical  = $identical.Count
    }
  }
}

function Get-DryRunComparisonSummary {
  <#
  .SYNOPSIS
    Erstellt eine menschenlesbare Zusammenfassung eines DryRun-Vergleichs.
  .PARAMETER Comparison
    Ergebnis von Compare-DryRunResults.
  .PARAMETER LabelA
    Bezeichnung fuer DryRun A.
  .PARAMETER LabelB
    Bezeichnung fuer DryRun B.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Comparison,
    [string]$LabelA = 'DryRun A',
    [string]$LabelB = 'DryRun B'
  )

  $s = $Comparison.Summary

  $lines = @()
  $lines += "Vergleich: $LabelA vs. $LabelB"
  $lines += "  Identisch:       $($s.Identical)"
  $lines += "  Unterschiedlich: $($s.Different)"
  $lines += "  Nur in $($LabelA): $($s.OnlyA)"
  $lines += "  Nur in $($LabelB): $($s.OnlyB)"

  return @{
    Text       = ($lines -join "`n")
    HasChanges = ($s.Different -gt 0 -or $s.OnlyA -gt 0 -or $s.OnlyB -gt 0)
  }
}
