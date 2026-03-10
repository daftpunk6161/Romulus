#  ACCESSIBILITY / BARRIEREFREIHEIT (LF-13)
#  Screen-Reader-Support, High-Contrast, skalierbare Schrift.

function Get-AccessibilityDefaults {
  <#
  .SYNOPSIS
    Gibt Standard-Barrierefreiheits-Einstellungen zurueck.
  #>
  return @{
    FontScale         = 1.0
    HighContrast      = $false
    ReducedMotion     = $false
    ScreenReaderHints = $true
    FocusIndicator    = $true
    MinTouchTarget    = 44
    ColorBlindMode    = 'None'
  }
}

function Test-HighContrastActive {
  <#
  .SYNOPSIS
    Prueft ob Windows High-Contrast-Modus aktiv ist.
  #>
  try {
    $key = 'HKCU:\Control Panel\Accessibility\HighContrast'
    if (Test-Path $key) {
      $flags = (Get-ItemProperty -Path $key -Name 'Flags' -ErrorAction SilentlyContinue).Flags
      return (($flags -band 1) -eq 1)
    }
  } catch {
    # Nicht verfuegbar
  }
  return $false
}

function Get-ScaledFontSize {
  <#
  .SYNOPSIS
    Berechnet skalierte Schriftgroesse.
  #>
  param(
    [Parameter(Mandatory)][double]$BaseSizePt,
    [double]$Scale = 1.0,
    [double]$MinSize = 8.0,
    [double]$MaxSize = 72.0
  )

  $scaled = $BaseSizePt * $Scale
  return [math]::Max($MinSize, [math]::Min($MaxSize, [math]::Round($scaled, 1)))
}

function Get-AccessibleColorPair {
  <#
  .SYNOPSIS
    Gibt ein barrierefreies Farbpaar mit ausreichendem Kontrast zurueck.
  #>
  param(
    [Parameter(Mandatory)][string]$Theme,
    [string]$ColorBlindMode = 'None'
  )

  $pairs = @{
    'Dark' = @{
      Foreground  = '#FFFFFF'
      Background  = '#1E1E1E'
      Accent      = '#569CD6'
      Error       = '#F44747'
      Success     = '#6A9955'
      Warning     = '#DCDCAA'
    }
    'Light' = @{
      Foreground  = '#1E1E1E'
      Background  = '#FFFFFF'
      Accent      = '#0066B8'
      Error       = '#CD3131'
      Success     = '#008000'
      Warning     = '#795E26'
    }
    'HighContrast' = @{
      Foreground  = '#FFFFFF'
      Background  = '#000000'
      Accent      = '#FFD700'
      Error       = '#FF0000'
      Success     = '#00FF00'
      Warning     = '#FFFF00'
    }
  }

  $pair = if ($pairs.ContainsKey($Theme)) { $pairs[$Theme] } else { $pairs['Dark'] }

  # Protanopia/Deuteranopia: Rot/Gruen ersetzen
  if ($ColorBlindMode -eq 'Protanopia' -or $ColorBlindMode -eq 'Deuteranopia') {
    $pair.Error   = '#FF6600'
    $pair.Success = '#0066FF'
  }

  return $pair
}

function New-AriaLabel {
  <#
  .SYNOPSIS
    Erstellt ein ARIA-artiges Label fuer Screen-Reader.
  #>
  param(
    [Parameter(Mandatory)][string]$ElementName,
    [Parameter(Mandatory)][string]$Description,
    [string]$Role = 'button',
    [string]$State = ''
  )

  return @{
    Name        = $ElementName
    Description = $Description
    Role        = $Role
    State       = $State
    Label       = "$ElementName - $Description"
  }
}

function Get-FocusOrder {
  <#
  .SYNOPSIS
    Gibt eine empfohlene Focus-Reihenfolge fuer UI-Elemente zurueck.
  #>
  param(
    [Parameter(Mandatory)][array]$Elements
  )

  $ordered = [System.Collections.Generic.List[hashtable]]::new()
  $index = 1

  foreach ($el in $Elements) {
    $ordered.Add(@{
      Name     = $el
      TabIndex = $index
    })
    $index++
  }

  return ,$ordered.ToArray()
}

function Test-ContrastRatio {
  <#
  .SYNOPSIS
    Prueft ob zwei Farben die WCAG AA Kontrastanforderung erfuellen (4.5:1).
  #>
  param(
    [Parameter(Mandatory)][double]$LuminanceFg,
    [Parameter(Mandatory)][double]$LuminanceBg
  )

  $lighter = [math]::Max($LuminanceFg, $LuminanceBg)
  $darker  = [math]::Min($LuminanceFg, $LuminanceBg)
  $ratio   = ($lighter + 0.05) / ($darker + 0.05)

  return @{
    Ratio    = [math]::Round($ratio, 2)
    PassAA   = ($ratio -ge 4.5)
    PassAAA  = ($ratio -ge 7.0)
  }
}
