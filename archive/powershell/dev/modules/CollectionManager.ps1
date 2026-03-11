# ================================================================
#  COLLECTION MANAGER – Smart Collections / Auto-Playlists (MF-05)
#  Dependencies: Classification.ps1, Dat.ps1
# ================================================================

function New-SmartCollection {
  <#
  .SYNOPSIS
    Erstellt eine dynamische Smart-Collection basierend auf Filter-Kriterien.
  .PARAMETER Name
    Name der Collection.
  .PARAMETER Filters
    Array von Filter-Hashtables: @{ Field; Operator; Value }.
  .PARAMETER Logic
    Verknuepfungslogik: AND oder OR.
  #>
  param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][object[]]$Filters,
    [ValidateSet('AND','OR')][string]$Logic = 'AND'
  )

  return @{
    Id       = [guid]::NewGuid().ToString('N').Substring(0, 8)
    Name     = $Name
    Filters  = $Filters
    Logic    = $Logic
    Created  = (Get-Date).ToString('o')
    Type     = 'Smart'
  }
}

function Test-CollectionMatch {
  <#
  .SYNOPSIS
    Prueft ob ein Item die Kriterien einer Smart-Collection erfuellt.
  .PARAMETER Item
    ROM-Eintrag als Hashtable.
  .PARAMETER Collection
    Smart-Collection-Definition.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Item,
    [Parameter(Mandatory)][hashtable]$Collection
  )

  $filters = $Collection.Filters
  $logic = if ($Collection.Logic) { $Collection.Logic } else { 'AND' }

  if (-not $filters -or $filters.Count -eq 0) { return $true }

  foreach ($filter in $filters) {
    $field = $filter.Field
    $op = $filter.Operator
    $val = $filter.Value

    $itemVal = $null
    if ($Item.ContainsKey($field)) { $itemVal = $Item[$field] }

    $match = switch ($op) {
      'eq'       { "$itemVal" -eq "$val" }
      'neq'      { "$itemVal" -ne "$val" }
      'contains' { "$itemVal" -like "*$val*" }
      'gt'       { [double]$itemVal -gt [double]$val }
      'lt'       { [double]$itemVal -lt [double]$val }
      'gte'      { [double]$itemVal -ge [double]$val }
      'lte'      { [double]$itemVal -le [double]$val }
      'like'     { "$itemVal" -like "$val" }
      'regex'    { "$itemVal" -match "$val" }
      default    { $false }
    }

    if ($logic -eq 'OR' -and $match) { return $true }
    if ($logic -eq 'AND' -and -not $match) { return $false }
  }

  return ($logic -eq 'AND')
}

function Invoke-SmartCollectionFilter {
  <#
  .SYNOPSIS
    Filtert Items nach einer Smart-Collection-Definition.
  .PARAMETER Items
    Array von ROM-Eintraegen.
  .PARAMETER Collection
    Smart-Collection-Definition.
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Items,
    [Parameter(Mandatory)][hashtable]$Collection
  )

  if (-not $Items -or $Items.Count -eq 0) { return ,@() }

  $result = [System.Collections.Generic.List[hashtable]]::new()
  foreach ($item in $Items) {
    if (Test-CollectionMatch -Item $item -Collection $Collection) {
      $result.Add($item)
    }
  }

  return ,@($result.ToArray())
}

function Export-CollectionDefinition {
  <#
  .SYNOPSIS
    Exportiert eine Collection-Definition als JSON.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Collection
  )

  return ($Collection | ConvertTo-Json -Depth 5 -Compress)
}

function Import-CollectionDefinition {
  <#
  .SYNOPSIS
    Importiert eine Collection-Definition aus JSON.
  #>
  param(
    [Parameter(Mandatory)][string]$Json
  )

  return ($Json | ConvertFrom-Json -AsHashtable -ErrorAction Stop)
}
