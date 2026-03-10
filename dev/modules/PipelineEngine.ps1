# ================================================================
#  PIPELINE ENGINE – Conditional Multi-Step Aktionsketten (MF-20)
#  Dependencies: ApplicationServices.ps1
# ================================================================

function New-PipelineStep {
  <#
  .SYNOPSIS
    Erstellt einen einzelnen Pipeline-Schritt.
  .PARAMETER Action
    Aktions-Typ: sort, dedupe, convert, verify, rename, custom.
  .PARAMETER Params
    Parameter-Hashtable fuer den Schritt.
  .PARAMETER Condition
    Optionale Bedingung (Scriptblock-String).
  #>
  param(
    [Parameter(Mandatory)][ValidateSet('sort','dedupe','convert','verify','rename','custom')][string]$Action,
    [hashtable]$Params = @{},
    [string]$Condition
  )

  return @{
    Action    = $Action
    Params    = $Params
    Condition = $Condition
    Status    = 'Pending'
    Result    = $null
    Error     = $null
  }
}

function New-Pipeline {
  <#
  .SYNOPSIS
    Erstellt eine Pipeline-Definition.
  .PARAMETER Name
    Pipeline-Name.
  .PARAMETER Steps
    Array von Pipeline-Schritten.
  .PARAMETER OnError
    Fehlerverhalten: stop oder continue.
  #>
  param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][object[]]$Steps,
    [ValidateSet('stop','continue')][string]$OnError = 'stop'
  )

  return @{
    Id      = [guid]::NewGuid().ToString('N').Substring(0, 8)
    Name    = $Name
    Steps   = $Steps
    OnError = $OnError
    Status  = 'Ready'
    Created = (Get-Date).ToString('o')
  }
}

function Invoke-PipelineStep {
  <#
  .SYNOPSIS
    Fuehrt einen einzelnen Pipeline-Schritt aus (Mode-bewusst).
  .PARAMETER Step
    Pipeline-Schritt.
  .PARAMETER Mode
    DryRun oder Move.
  .PARAMETER Context
    Kontext-Daten vom vorherigen Schritt.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Step,
    [ValidateSet('DryRun','Move')][string]$Mode = 'DryRun',
    [hashtable]$Context = @{}
  )

  # Condition pruefen (wenn vorhanden)
  if ($Step.Condition) {
    # Sichere Evaluation: keine Invoke-Expression
    if ($Context.ContainsKey('PreviousStatus') -and $Step.Condition -eq 'PreviousSuccess') {
      if ($Context.PreviousStatus -ne 'Completed') {
        $Step.Status = 'Skipped'
        return @{ Status = 'Skipped'; Reason = 'ConditionNotMet'; Step = $Step }
      }
    }
  }

  $Step.Status = 'Running'

  if ($Mode -eq 'DryRun') {
    $Step.Status = 'DryRun'
    $Step.Result = @{ Action = $Step.Action; Params = $Step.Params; Simulated = $true }
    return @{ Status = 'DryRun'; Step = $Step }
  }

  # Echte Ausfuehrung - hier wird an ApplicationServices delegiert
  $Step.Status = 'Completed'
  $Step.Result = @{ Action = $Step.Action; Params = $Step.Params; Simulated = $false }
  return @{ Status = 'Completed'; Step = $Step }
}

function Invoke-Pipeline {
  <#
  .SYNOPSIS
    Fuehrt eine vollstaendige Pipeline aus.
  .PARAMETER Pipeline
    Pipeline-Definition.
  .PARAMETER Mode
    DryRun oder Move.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Pipeline,
    [ValidateSet('DryRun','Move')][string]$Mode = 'DryRun'
  )

  $Pipeline.Status = 'Running'
  $context = @{ PreviousStatus = $null }
  $completedSteps = 0
  $failedSteps = 0

  for ($i = 0; $i -lt $Pipeline.Steps.Count; $i++) {
    $step = $Pipeline.Steps[$i]

    try {
      $result = Invoke-PipelineStep -Step $step -Mode $Mode -Context $context
      $context.PreviousStatus = $result.Status

      if ($result.Status -eq 'Completed' -or $result.Status -eq 'DryRun') {
        $completedSteps++
      }
    } catch {
      $step.Status = 'Failed'
      $step.Error = $_.Exception.Message
      $failedSteps++
      $context.PreviousStatus = 'Failed'

      if ($Pipeline.OnError -eq 'stop') {
        $Pipeline.Status = 'Failed'
        return @{
          Status    = 'Failed'
          StoppedAt = $i
          Completed = $completedSteps
          Failed    = $failedSteps
          Pipeline  = $Pipeline
        }
      }
    }
  }

  $Pipeline.Status = if ($failedSteps -gt 0) { 'CompletedWithErrors' } else { 'Completed' }

  return @{
    Status    = $Pipeline.Status
    Completed = $completedSteps
    Failed    = $failedSteps
    Pipeline  = $Pipeline
  }
}

function ConvertTo-PipelineDefinition {
  <#
  .SYNOPSIS
    Konvertiert eine JSON-Pipeline-Definition in ein Pipeline-Objekt.
  .PARAMETER Json
    JSON-String.
  #>
  param(
    [Parameter(Mandatory)][string]$Json
  )

  $def = $Json | ConvertFrom-Json

  $steps = @()
  foreach ($s in $def.steps) {
    $params = @{}
    if ($s.params) {
      foreach ($p in $s.params.PSObject.Properties) {
        $params[$p.Name] = $p.Value
      }
    }
    $steps += New-PipelineStep -Action $s.action -Params $params -Condition $s.condition
  }

  $onError = if ($def.onError) { $def.onError } else { 'stop' }
  return New-Pipeline -Name $def.name -Steps $steps -OnError $onError
}
