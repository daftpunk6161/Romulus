# ================================================================
#  CLI EXPORT – PowerShell-Script-Generator (QW-10)
#  Dependencies: Settings.ps1
# ================================================================

function Export-CliCommand {
  <#
  .SYNOPSIS
    Generiert ein reproduzierbares CLI-Kommando aus den aktuellen GUI-Settings.
  .PARAMETER Settings
    Hashtable mit den relevanten Settings (Roots, Mode, PreferRegions, etc.).
  .PARAMETER ScriptPath
    Pfad zum Invoke-RomCleanup.ps1 Script.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Settings,
    [string]$ScriptPath = './Invoke-RomCleanup.ps1'
  )

  $parts = [System.Collections.Generic.List[string]]::new()
  [void]$parts.Add('pwsh -NoProfile -File')
  [void]$parts.Add((ConvertTo-SafeCliArg -Value $ScriptPath))

  # Roots
  $roots = $null
  if ($Settings.ContainsKey('roots') -and $Settings.roots) {
    $roots = $Settings.roots
  } elseif ($Settings.ContainsKey('Roots') -and $Settings.Roots) {
    $roots = $Settings.Roots
  }

  if ($roots) {
    $rootValues = @($roots) | ForEach-Object { ConvertTo-SafeCliArg -Value $_ }
    [void]$parts.Add('-Roots')
    [void]$parts.Add(($rootValues -join ','))
  }

  # Mode
  $mode = $null
  if ($Settings.ContainsKey('mode')) { $mode = $Settings.mode }
  elseif ($Settings.ContainsKey('Mode')) { $mode = $Settings.Mode }
  if ($mode) {
    [void]$parts.Add('-Mode')
    [void]$parts.Add($mode)
  }

  # PreferRegions
  $regions = $null
  if ($Settings.ContainsKey('preferredRegions')) { $regions = $Settings.preferredRegions }
  elseif ($Settings.ContainsKey('PreferRegions')) { $regions = $Settings.PreferRegions }
  if ($regions -and @($regions).Count -gt 0) {
    [void]$parts.Add('-PreferRegions')
    [void]$parts.Add((@($regions) -join ','))
  }

  # AggressiveJunk
  $aggressive = $false
  if ($Settings.ContainsKey('aggressiveJunk')) { $aggressive = $Settings.aggressiveJunk }
  elseif ($Settings.ContainsKey('AggressiveJunk')) { $aggressive = $Settings.AggressiveJunk }
  if ($aggressive) {
    [void]$parts.Add('-AggressiveJunk')
  }

  # UseDat
  $useDat = $null
  if ($Settings.ContainsKey('useDat')) { $useDat = $Settings.useDat }
  elseif ($Settings.ContainsKey('dat') -and $Settings.dat.ContainsKey('useDat')) { $useDat = $Settings.dat.useDat }
  if ($useDat -eq $true) {
    [void]$parts.Add('-UseDat')
  }

  return ($parts -join ' ')
}

function ConvertTo-SafeCliArg {
  <#
  .SYNOPSIS
    Quotet einen CLI-Argument-Wert sicher (Sonderzeichen, Leerzeichen).
  #>
  param(
    [Parameter(Mandatory)][string]$Value
  )

  if ($Value -match '[\s"''&|<>^]') {
    # Innere Anfuehrungszeichen escapen
    $escaped = $Value.Replace('"', '\"')
    return ('"' + $escaped + '"')
  }

  return $Value
}

function ConvertFrom-CliCommand {
  <#
  .SYNOPSIS
    Parst ein CLI-Kommando zurueck in eine Settings-Hashtable (Round-Trip-Test).
  .PARAMETER Command
    CLI-Kommandostring.
  #>
  param(
    [Parameter(Mandatory)][string]$Command
  )

  $settings = @{}

  if ($Command -match '-Roots\s+([^\-]+)') {
    $rootStr = $Matches[1].Trim().TrimEnd(',')
    $settings['Roots'] = @($rootStr -split ',' | ForEach-Object { $_.Trim().Trim('"') } | Where-Object { $_ })
  }

  if ($Command -match '-Mode\s+(\S+)') {
    $settings['Mode'] = $Matches[1]
  }

  if ($Command -match '-PreferRegions\s+(\S+)') {
    $settings['PreferRegions'] = @($Matches[1] -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
  }

  if ($Command -match '-AggressiveJunk') {
    $settings['AggressiveJunk'] = $true
  }

  if ($Command -match '-UseDat') {
    $settings['UseDat'] = $true
  }

  return $settings
}
