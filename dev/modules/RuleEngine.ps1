# ================================================================
#  RULE ENGINE – User-definierbare Klassifikationsregeln (MF-19)
#  Dependencies: Classification.ps1, rules.json
# ================================================================

function New-ClassificationRule {
  <#
  .SYNOPSIS
    Erstellt eine User-Klassifikationsregel.
  .PARAMETER Name
    Regelname.
  .PARAMETER Priority
    Prioritaet (hoeher = wird zuerst evaluiert).
  .PARAMETER Conditions
    Array von Bedingungen: @{ Field; Op; Value }.
  .PARAMETER Action
    Aktion: junk, keep, quarantine, custom.
  .PARAMETER Reason
    Begruendung.
  #>
  param(
    [Parameter(Mandatory)][string]$Name,
    [int]$Priority = 10,
    [Parameter(Mandatory)][object[]]$Conditions,
    [Parameter(Mandatory)][ValidateSet('junk','keep','quarantine','custom')][string]$Action,
    [string]$Reason = ''
  )

  return @{
    Name       = $Name
    Priority   = $Priority
    Conditions = $Conditions
    Action     = $Action
    Reason     = $Reason
    Enabled    = $true
  }
}

function Test-ClassificationRule {
  <#
  .SYNOPSIS
    Evaluiert eine einzelne Regel gegen ein Item.
  .PARAMETER Rule
    Regel-Definition.
  .PARAMETER Item
    ROM-Item als Hashtable.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Rule,
    [Parameter(Mandatory)][hashtable]$Item
  )

  if (-not $Rule.Enabled) { return $false }

  foreach ($cond in $Rule.Conditions) {
    $field = $cond.Field
    $op = $cond.Op
    $val = $cond.Value

    $itemVal = $null
    if ($Item.ContainsKey($field)) { $itemVal = $Item[$field] }

    $match = switch ($op) {
      'eq'       { "$itemVal" -eq "$val" }
      'neq'      { "$itemVal" -ne "$val" }
      'contains' { "$itemVal" -like "*$val*" }
      'gt'       { [double]$itemVal -gt [double]$val }
      'lt'       { [double]$itemVal -lt [double]$val }
      'regex'    { "$itemVal" -match "$val" }
      default    { $false }
    }

    if (-not $match) { return $false }
  }

  return $true
}

function Invoke-RuleEngine {
  <#
  .SYNOPSIS
    Evaluiert alle Regeln gegen ein Item. Hoechste Prioritaet gewinnt.
  .PARAMETER Rules
    Array von Regeln (sortiert nach Prioritaet absteigend).
  .PARAMETER Item
    ROM-Item als Hashtable.
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Rules,
    [Parameter(Mandatory)][hashtable]$Item
  )

  if (-not $Rules -or $Rules.Count -eq 0) {
    return @{ Matched = $false; Rule = $null; Action = $null }
  }

  # Sortiere nach Prioritaet (hoeher zuerst)
  $sorted = @($Rules | Sort-Object { -$_.Priority })

  foreach ($rule in $sorted) {
    if (Test-ClassificationRule -Rule $rule -Item $Item) {
      return @{
        Matched  = $true
        Rule     = $rule
        Action   = $rule.Action
        Reason   = $rule.Reason
        RuleName = $rule.Name
      }
    }
  }

  return @{ Matched = $false; Rule = $null; Action = $null }
}

function Test-RuleSyntax {
  <#
  .SYNOPSIS
    Validiert die Syntax einer Regel-Definition.
  .PARAMETER Rule
    Regel als Hashtable.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Rule
  )

  $errors = @()

  if (-not $Rule.ContainsKey('Name') -or -not $Rule.Name) {
    $errors += 'Name ist erforderlich'
  }

  if (-not $Rule.ContainsKey('Conditions') -or -not $Rule.Conditions -or $Rule.Conditions.Count -eq 0) {
    $errors += 'Mindestens eine Bedingung erforderlich'
  }

  if (-not $Rule.ContainsKey('Action') -or $Rule.Action -notin @('junk','keep','quarantine','custom')) {
    $errors += "Ungueltige Action: '$($Rule.Action)' (erlaubt: junk, keep, quarantine, custom)"
  }

  $validOps = @('eq','neq','contains','gt','lt','regex')
  if ($Rule.ContainsKey('Conditions') -and $Rule.Conditions) {
    foreach ($cond in $Rule.Conditions) {
      if (-not $cond.Field) { $errors += 'Bedingung ohne Field' }
      if ($cond.Op -notin $validOps) { $errors += "Ungueltiger Operator: '$($cond.Op)'" }
    }
  }

  return @{
    Valid  = ($errors.Count -eq 0)
    Errors = $errors
  }
}

function Invoke-BatchRuleEngine {
  <#
  .SYNOPSIS
    Evaluiert Regeln fuer eine Batch von Items mit Warnungen.
  .PARAMETER Rules
    Array von Regeln.
  .PARAMETER Items
    Array von ROM-Items.
  .PARAMETER WarnAllMatchPercent
    Warnung wenn ein Regel-Prozentsatz diesen Wert erreicht.
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Rules,
    [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Items,
    [int]$WarnAllMatchPercent = 100
  )

  if (-not $Items -or $Items.Count -eq 0) {
    return @{ Results = @(); Warnings = @(); Stats = @{ Total = 0; Matched = 0; Unmatched = 0 } }
  }

  $results = @()
  $matchCount = 0
  $warnings = @()

  foreach ($item in $Items) {
    $evalResult = Invoke-RuleEngine -Rules $Rules -Item $item
    $results += @{ Item = $item; Result = $evalResult }
    if ($evalResult.Matched) { $matchCount++ }
  }

  # Warnung wenn alle Items von einer Regel getroffen werden
  if ($Items.Count -gt 0 -and $WarnAllMatchPercent -gt 0) {
    $matchPercent = [math]::Round(($matchCount / $Items.Count) * 100, 1)
    if ($matchPercent -ge $WarnAllMatchPercent) {
      $warnings += "Regeln treffen $matchPercent% aller Items"
    }
  }

  return @{
    Results  = $results
    Warnings = $warnings
    Stats    = @{
      Total     = $Items.Count
      Matched   = $matchCount
      Unmatched = $Items.Count - $matchCount
    }
  }
}
