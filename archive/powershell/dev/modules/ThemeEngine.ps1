#  THEME ENGINE (LF-20)
#  Custom WPF-Themes als installierbare Plugins (ResourceDictionary-basiert).

function Get-BuiltinThemes {
  <#
  .SYNOPSIS
    Gibt die eingebauten Theme-Definitionen zurueck.
  #>
  return @(
    @{
      Key         = 'retro-dark'
      Name        = 'Retro Dark'
      Description = 'Standard Dark-Theme mit Neon-Akzenten'
      Colors      = @{
        Background      = '#1A1A2E'
        Surface         = '#16213E'
        Primary         = '#0F3460'
        Accent          = '#E94560'
        TextPrimary     = '#EAEAEA'
        TextSecondary   = '#A0A0A0'
        Success         = '#00C853'
        Warning         = '#FFD600'
        Error           = '#FF1744'
        Border          = '#333366'
      }
      FontFamily  = 'Segoe UI'
      FontSize    = 13
      IsBuiltin   = $true
    }
    @{
      Key         = 'retro-light'
      Name        = 'Retro Light'
      Description = 'Helles Theme mit weichen Farben'
      Colors      = @{
        Background      = '#F5F5F5'
        Surface         = '#FFFFFF'
        Primary         = '#1565C0'
        Accent          = '#FF4081'
        TextPrimary     = '#212121'
        TextSecondary   = '#757575'
        Success         = '#2E7D32'
        Warning         = '#F57F17'
        Error           = '#C62828'
        Border          = '#BDBDBD'
      }
      FontFamily  = 'Segoe UI'
      FontSize    = 13
      IsBuiltin   = $true
    }
    @{
      Key         = 'high-contrast'
      Name        = 'High Contrast'
      Description = 'Maximaler Kontrast fuer Barrierefreiheit'
      Colors      = @{
        Background      = '#000000'
        Surface         = '#1A1A1A'
        Primary         = '#FFFFFF'
        Accent          = '#FFD700'
        TextPrimary     = '#FFFFFF'
        TextSecondary   = '#CCCCCC'
        Success         = '#00FF00'
        Warning         = '#FFFF00'
        Error           = '#FF0000'
        Border          = '#FFFFFF'
      }
      FontFamily  = 'Segoe UI'
      FontSize    = 14
      IsBuiltin   = $true
    }
  )
}

function New-CustomTheme {
  <#
  .SYNOPSIS
    Erstellt ein neues benutzerdefiniertes Theme.
  #>
  param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][hashtable]$Colors,
    [string]$FontFamily = 'Segoe UI',
    [int]$FontSize = 13,
    [string]$Author = '',
    [string]$Description = ''
  )

  $requiredKeys = @('Background','Surface','Primary','Accent','TextPrimary')
  foreach ($key in $requiredKeys) {
    if (-not $Colors.ContainsKey($key)) {
      throw "Fehlende Farbe: $key"
    }
  }

  $safeKey = ($Name.ToLowerInvariant() -replace '[^a-z0-9]', '-') -replace '-+', '-'

  return @{
    Key         = "custom-$safeKey"
    Name        = $Name
    Description = $Description
    Author      = $Author
    Colors      = $Colors
    FontFamily  = $FontFamily
    FontSize    = $FontSize
    IsBuiltin   = $false
    Version     = '1.0'
  }
}

function ConvertTo-ResourceDictionary {
  <#
  .SYNOPSIS
    Konvertiert ein Theme in XAML ResourceDictionary-Fragmente.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Theme
  )

  $entries = [System.Collections.Generic.List[hashtable]]::new()

  foreach ($colorKey in $Theme.Colors.Keys) {
    $entries.Add(@{
      Key   = "Theme.$colorKey"
      Value = $Theme.Colors[$colorKey]
      Type  = 'SolidColorBrush'
    })
  }

  $entries.Add(@{ Key = 'Theme.FontFamily'; Value = $Theme.FontFamily; Type = 'FontFamily' })
  $entries.Add(@{ Key = 'Theme.FontSize'; Value = $Theme.FontSize; Type = 'Double' })

  return @{
    ThemeKey = $Theme.Key
    Entries  = ,$entries.ToArray()
    Count    = $entries.Count
  }
}

function Test-ThemeValid {
  <#
  .SYNOPSIS
    Validiert ein Theme auf korrekte Struktur und Farben.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Theme
  )

  $errors = [System.Collections.Generic.List[string]]::new()

  if (-not $Theme.Name) { $errors.Add('Name fehlt') }
  if (-not $Theme.Colors) { $errors.Add('Colors fehlt') }

  $requiredColors = @('Background','Surface','Primary','Accent','TextPrimary')
  foreach ($c in $requiredColors) {
    if (-not $Theme.Colors.ContainsKey($c)) {
      $errors.Add("Farbe '$c' fehlt")
    } elseif ($Theme.Colors[$c] -notmatch '^#[0-9A-Fa-f]{6}$') {
      $errors.Add("Farbe '$c' hat ungueltiges Format: $($Theme.Colors[$c])")
    }
  }

  return @{
    Valid  = ($errors.Count -eq 0)
    Errors = ,$errors.ToArray()
  }
}

function Export-ThemeJson {
  <#
  .SYNOPSIS
    Exportiert ein Theme als JSON-String.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Theme
  )

  $export = @{
    key         = $Theme.Key
    name        = $Theme.Name
    description = $Theme.Description
    author      = $Theme.Author
    version     = $Theme.Version
    fontFamily  = $Theme.FontFamily
    fontSize    = $Theme.FontSize
    colors      = $Theme.Colors
  }

  return ($export | ConvertTo-Json -Depth 3)
}

function Import-ThemeJson {
  <#
  .SYNOPSIS
    Importiert ein Theme aus JSON-String.
  #>
  param(
    [Parameter(Mandatory)][string]$JsonString
  )

  $data = $JsonString | ConvertFrom-Json
  $colors = @{}
  foreach ($prop in $data.colors.PSObject.Properties) {
    $colors[$prop.Name] = $prop.Value
  }

  return New-CustomTheme -Name $data.name -Colors $colors -FontFamily $data.fontFamily -FontSize $data.fontSize -Author $data.author -Description $data.description
}
