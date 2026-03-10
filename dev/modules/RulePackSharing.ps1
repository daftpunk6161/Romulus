#  RULE PACK SHARING (LF-19)
#  Community Regions-/Junk-/Alias-Regeln teilen und importieren.

function New-RulePack {
  <#
  .SYNOPSIS
    Erstellt ein neues Rule-Pack.
  #>
  param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][string]$Author,
    [string]$Description = '',
    [string]$Version = '1.0.0',
    [ValidateSet('region','junk','alias','mixed')]
    [string]$Type = 'mixed'
  )

  return @{
    Name        = $Name
    Author      = $Author
    Description = $Description
    Version     = $Version
    Type        = $Type
    Created     = (Get-Date).ToString('o')
    Rules       = [System.Collections.Generic.List[hashtable]]::new()
    Trusted     = $false
    Signature   = ''
  }
}

function Add-RuleToRulePack {
  <#
  .SYNOPSIS
    Fuegt eine Regel zum Rule-Pack hinzu.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$RulePack,
    [Parameter(Mandatory)][string]$Pattern,
    [Parameter(Mandatory)][ValidateSet('region','junk','alias','version')]
    [string]$Category,
    [string]$Description = '',
    [string]$Action = 'tag'
  )

  $RulePack.Rules.Add(@{
    Pattern     = $Pattern
    Category    = $Category
    Description = $Description
    Action      = $Action
    Enabled     = $true
  })

  return $RulePack
}

function Test-RulePackValid {
  <#
  .SYNOPSIS
    Validiert ein Rule-Pack auf korrekte Struktur.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$RulePack
  )

  $errors = [System.Collections.Generic.List[string]]::new()

  if (-not $RulePack.Name) { $errors.Add('Name fehlt') }
  if (-not $RulePack.Author) { $errors.Add('Author fehlt') }
  if (-not $RulePack.Version) { $errors.Add('Version fehlt') }
  if ($null -eq $RulePack.Rules -or $RulePack.Rules.Count -eq 0) { $errors.Add('Keine Regeln definiert') }

  # Pattern-Validierung
  if ($RulePack.Rules) {
    foreach ($rule in $RulePack.Rules) {
      if (-not $rule.Pattern) { $errors.Add("Regel ohne Pattern gefunden") }
      if (-not $rule.Category) { $errors.Add("Regel '$($rule.Pattern)' ohne Category") }
      try {
        [regex]::new($rule.Pattern) | Out-Null
      } catch {
        $errors.Add("Ungueltiges Regex-Pattern: $($rule.Pattern)")
      }
    }
  }

  return @{
    Valid  = ($errors.Count -eq 0)
    Errors = ,$errors.ToArray()
  }
}

function Merge-RulePacks {
  <#
  .SYNOPSIS
    Merged mehrere Rule-Packs zu einem.
  #>
  param(
    [Parameter(Mandatory)][array]$RulePacks,
    [string]$MergedName = 'Merged Rules'
  )

  $merged = New-RulePack -Name $MergedName -Author 'Merged' -Type 'mixed'

  $seen = @{}
  foreach ($pack in $RulePacks) {
    foreach ($rule in $pack.Rules) {
      $key = "$($rule.Category):$($rule.Pattern)"
      if (-not $seen.ContainsKey($key)) {
        $seen[$key] = $true
        $merged.Rules.Add($rule)
      }
    }
  }

  return $merged
}

function Export-RulePackJson {
  <#
  .SYNOPSIS
    Exportiert ein Rule-Pack als JSON-String.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$RulePack
  )

  $export = @{
    name        = $RulePack.Name
    author      = $RulePack.Author
    description = $RulePack.Description
    version     = $RulePack.Version
    type        = $RulePack.Type
    created     = $RulePack.Created
    rules       = @($RulePack.Rules | ForEach-Object {
      @{ pattern = $_.Pattern; category = $_.Category; description = $_.Description; action = $_.Action }
    })
  }

  return ($export | ConvertTo-Json -Depth 5)
}

function Import-RulePackJson {
  <#
  .SYNOPSIS
    Importiert ein Rule-Pack aus JSON-String.
  #>
  param(
    [Parameter(Mandatory)][string]$JsonString
  )

  $data = $JsonString | ConvertFrom-Json
  $pack = New-RulePack -Name $data.name -Author $data.author -Description $data.description -Version $data.version -Type $data.type

  foreach ($rule in $data.rules) {
    [void](Add-RuleToRulePack -RulePack $pack -Pattern $rule.pattern -Category $rule.category -Description $rule.description -Action $rule.action)
  }

  return $pack
}

function Get-RulePackStatistics {
  <#
  .SYNOPSIS
    Statistik ueber ein Rule-Pack.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$RulePack
  )

  $byCategory = @{}
  foreach ($rule in $RulePack.Rules) {
    $c = $rule.Category
    if (-not $byCategory.ContainsKey($c)) { $byCategory[$c] = 0 }
    $byCategory[$c]++
  }

  return @{
    Name       = $RulePack.Name
    TotalRules = $RulePack.Rules.Count
    ByCategory = $byCategory
    Trusted    = $RulePack.Trusted
  }
}
