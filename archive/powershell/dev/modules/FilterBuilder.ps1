# ================================================================
#  FILTER BUILDER – Visueller Query-Builder (MF-17)
#  Dependencies: Classification
# ================================================================

function New-FilterCondition {
  <#
  .SYNOPSIS
    Erstellt eine einzelne Filter-Bedingung.
  .PARAMETER Field
    Feldname (z.B. Console, Region, SizeMB, DatStatus).
  .PARAMETER Operator
    Vergleichsoperator.
  .PARAMETER Value
    Vergleichswert.
  #>
  param(
    [Parameter(Mandatory)][string]$Field,
    [Parameter(Mandatory)][ValidateSet('eq','neq','gt','lt','gte','lte','contains','like','regex')][string]$Operator,
    [Parameter(Mandatory)][AllowEmptyString()][string]$Value
  )

  return @{
    Field    = $Field
    Operator = $Operator
    Value    = $Value
  }
}

function New-FilterQuery {
  <#
  .SYNOPSIS
    Erstellt eine Filter-Query aus mehreren Bedingungen.
  .PARAMETER Conditions
    Array von Filter-Bedingungen.
  .PARAMETER Logic
    Verknuepfungslogik: AND oder OR.
  #>
  param(
    [Parameter(Mandatory)][object[]]$Conditions,
    [ValidateSet('AND','OR')][string]$Logic = 'AND'
  )

  return @{
    Conditions = $Conditions
    Logic      = $Logic
  }
}

function Test-FilterCondition {
  <#
  .SYNOPSIS
    Prueft eine einzelne Bedingung gegen einen Wert.
  .PARAMETER Condition
    Filter-Bedingung.
  .PARAMETER ItemValue
    Wert des Items.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Condition,
    $ItemValue
  )

  $val = $Condition.Value
  $iv = "$ItemValue"

  switch ($Condition.Operator) {
    'eq'       { return ($iv -eq $val) }
    'neq'      { return ($iv -ne $val) }
    'gt'       { return ([double]$iv -gt [double]$val) }
    'lt'       { return ([double]$iv -lt [double]$val) }
    'gte'      { return ([double]$iv -ge [double]$val) }
    'lte'      { return ([double]$iv -le [double]$val) }
    'contains' { return ($iv -like "*$val*") }
    'like'     { return ($iv -like $val) }
    'regex'    { return ($iv -match $val) }
    default    { return $false }
  }
}

function Invoke-FilterQuery {
  <#
  .SYNOPSIS
    Fuehrt eine Filter-Query auf einem Itemset aus.
  .PARAMETER Items
    Array von Items (Hashtables).
  .PARAMETER Query
    Filter-Query.
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Items,
    [Parameter(Mandatory)][hashtable]$Query
  )

  if (-not $Items -or $Items.Count -eq 0) { return ,@() }

  $result = [System.Collections.Generic.List[object]]::new()

  foreach ($item in $Items) {
    $matches = @()
    foreach ($cond in $Query.Conditions) {
      $fieldVal = $null
      if ($item -is [hashtable] -and $item.ContainsKey($cond.Field)) {
        $fieldVal = $item[$cond.Field]
      } elseif ($item.PSObject -and $item.PSObject.Properties[$cond.Field]) {
        $fieldVal = $item.($cond.Field)
      }
      $matches += (Test-FilterCondition -Condition $cond -ItemValue $fieldVal)
    }

    $pass = if ($Query.Logic -eq 'OR') {
      $matches -contains $true
    } else {
      -not ($matches -contains $false)
    }

    if ($pass) { $result.Add($item) }
  }

  return ,@($result.ToArray())
}

function ConvertTo-FilterQueryString {
  <#
  .SYNOPSIS
    Konvertiert eine Query in einen menschenlesbaren String.
  .PARAMETER Query
    Filter-Query.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Query
  )

  $parts = @()
  foreach ($cond in $Query.Conditions) {
    $opMap = @{ eq = '='; neq = '!='; gt = '>'; lt = '<'; gte = '>='; lte = '<='; contains = 'contains'; like = 'like'; regex = 'matches' }
    $opStr = if ($opMap.ContainsKey($cond.Operator)) { $opMap[$cond.Operator] } else { $cond.Operator }
    $parts += "$($cond.Field) $opStr '$($cond.Value)'"
  }

  $logic = if ($Query.Logic -eq 'OR') { ' OR ' } else { ' AND ' }
  return ($parts -join $logic)
}

function Get-FilterableFields {
  <#
  .SYNOPSIS
    Gibt die verfuegbaren filterbaren Felder zurueck.
  #>
  return @(
    @{ Name = 'Console'; Type = 'String'; Description = 'Konsolen-Key' }
    @{ Name = 'Region'; Type = 'String'; Description = 'Region (EU, US, JP, ...)' }
    @{ Name = 'SizeMB'; Type = 'Number'; Description = 'Dateigroesse in MB' }
    @{ Name = 'Format'; Type = 'String'; Description = 'Dateiformat (CHD, ISO, ZIP, ...)' }
    @{ Name = 'Category'; Type = 'String'; Description = 'Kategorie (GAME, JUNK, BIOS)' }
    @{ Name = 'DatStatus'; Type = 'String'; Description = 'DAT-Status (Verified, NoMatch, Missing)' }
    @{ Name = 'FileName'; Type = 'String'; Description = 'Dateiname' }
    @{ Name = 'GameKey'; Type = 'String'; Description = 'Normalisierter Spielname' }
  )
}
