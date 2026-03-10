# ================================================================
#  ROM FILTER – Suche/Filter fuer klassifizierte ROMs (QW-08)
#  Dependencies: (standalone, pure logic)
# ================================================================

function Search-RomCollection {
  <#
  .SYNOPSIS
    Filtert eine ROM-Sammlung nach Suchtext ueber mehrere Felder.
  .PARAMETER Items
    Array von ROM-Eintraegen (Hashtable oder PSCustomObject mit
    mindestens einem der Felder: Name, Console, Region, Category, Format, Path).
  .PARAMETER SearchText
    Freitext-Suchbegriff (case-insensitive).
  .PARAMETER Field
    Optionale Einschraenkung auf ein bestimmtes Feld.
  #>
  param(
    [Parameter(Mandatory)][object[]]$Items,
    [string]$SearchText,
    [ValidateSet('All','Name','Console','Region','Category','Format','Path')][string]$Field = 'All'
  )

  if ([string]::IsNullOrWhiteSpace($SearchText)) {
    return $Items
  }

  $search = $SearchText.Trim()

  $filtered = [System.Collections.ArrayList]::new()

  foreach ($item in $Items) {
    $match = $false

    $fields = if ($Field -eq 'All') {
      @('Name','Console','Region','Category','Format','Path','FileName','MainPath')
    } else {
      @($Field)
    }

    foreach ($f in $fields) {
      $val = $null
      if ($item -is [hashtable]) {
        if ($item.ContainsKey($f)) { $val = [string]$item[$f] }
      } elseif ($item.PSObject.Properties.Name -contains $f) {
        $val = [string]$item.$f
      }

      if ($val -and $val.IndexOf($search, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        $match = $true
        break
      }
    }

    if ($match) { [void]$filtered.Add($item) }
  }

  return ,@($filtered.ToArray())
}

function New-RomFilterPredicate {
  <#
  .SYNOPSIS
    Erzeugt ein Scriptblock-Predicate fuer CollectionViewSource-Filter.
    Optimiert fuer WPF-Einsatz mit Debounce-Logik.
  .PARAMETER SearchText
    Suchbegriff.
  .PARAMETER SearchFields
    Felder die durchsucht werden.
  #>
  param(
    [string]$SearchText,
    [string[]]$SearchFields = @('Name','Console','Region','Category')
  )

  if ([string]::IsNullOrWhiteSpace($SearchText)) {
    return { param($item) $true }
  }

  $text = $SearchText.Trim()
  $fields = $SearchFields

  return {
    param($item)
    foreach ($f in $fields) {
      $val = $null
      if ($item -is [hashtable] -and $item.ContainsKey($f)) {
        $val = [string]$item[$f]
      } elseif ($item.PSObject -and $item.PSObject.Properties.Name -contains $f) {
        $val = [string]$item.$f
      }
      if ($val -and $val.IndexOf($text, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        return $true
      }
    }
    return $false
  }.GetNewClosure()
}
