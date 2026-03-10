# ================================================================
#  THEME MANAGER – Dark/Light/Auto Theme-Toggle (QW-07)
#  Dependencies: Settings.ps1
# ================================================================

function Get-SystemThemePreference {
  <#
  .SYNOPSIS
    Erkennt das System-Theme (Dark/Light) aus der Windows-Registry.
  #>

  try {
    $regPath = 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize'
    if (Test-Path $regPath) {
      $value = Get-ItemProperty -Path $regPath -Name 'AppsUseLightTheme' -ErrorAction SilentlyContinue
      if ($null -ne $value -and $null -ne $value.AppsUseLightTheme) {
        if ($value.AppsUseLightTheme -eq 0) { return 'dark' }
        return 'light'
      }
    }
  } catch { }

  return 'dark'  # Fallback
}

function Resolve-EffectiveTheme {
  <#
  .SYNOPSIS
    Loest den effektiven Theme-Modus auf (dark/light/auto → dark oder light).
  .PARAMETER ThemeSetting
    Konfigurierter Theme-Wert: dark, light oder auto.
  #>
  param(
    [ValidateSet('dark','light','auto')][string]$ThemeSetting = 'dark'
  )

  if ($ThemeSetting -eq 'auto') {
    return (Get-SystemThemePreference)
  }

  return $ThemeSetting
}

function Get-ThemeColors {
  <#
  .SYNOPSIS
    Gibt die Farbpalette fuer das angegebene Theme zurueck.
  .PARAMETER Theme
    dark oder light.
  #>
  param(
    [ValidateSet('dark','light')][string]$Theme = 'dark'
  )

  if ($Theme -eq 'light') {
    return @{
      Background       = '#FAFAFA'
      Surface          = '#FFFFFF'
      SurfaceAlt       = '#F0F0F0'
      TextPrimary      = '#1A1A2E'
      TextSecondary    = '#555555'
      Accent           = '#00D4AA'
      AccentHover      = '#00B894'
      Danger           = '#E74C3C'
      Warning          = '#F39C12'
      Success          = '#27AE60'
      Border           = '#DDDDDD'
      ButtonBackground = '#E8E8E8'
      ButtonText       = '#1A1A2E'
      HeaderBackground = '#2C3E50'
      HeaderText       = '#FFFFFF'
    }
  }

  # Dark Theme (Standard, retro-modern)
  return @{
    Background       = '#0F0F23'
    Surface          = '#1A1A2E'
    SurfaceAlt       = '#16213E'
    TextPrimary      = '#E0E0E0'
    TextSecondary    = '#A0A0A0'
    Accent           = '#00D4AA'
    AccentHover      = '#00FFD0'
    Danger           = '#FF6B6B'
    Warning          = '#FFD93D'
    Success          = '#6BCB77'
    Border           = '#2A2A4A'
    ButtonBackground = '#2A2A4A'
    ButtonText       = '#E0E0E0'
    HeaderBackground = '#1A1A2E'
    HeaderText       = '#00D4AA'
  }
}

function Get-ThemeResourceDictionary {
  <#
  .SYNOPSIS
    Erzeugt ein WPF-kompatibles ResourceDictionary-XAML fuer das angegebene Theme.
  .PARAMETER Theme
    dark oder light.
  #>
  param(
    [ValidateSet('dark','light')][string]$Theme = 'dark'
  )

  $colors = Get-ThemeColors -Theme $Theme

  $xaml = @"
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <SolidColorBrush x:Key="BackgroundBrush" Color="$($colors.Background)" />
  <SolidColorBrush x:Key="SurfaceBrush" Color="$($colors.Surface)" />
  <SolidColorBrush x:Key="SurfaceAltBrush" Color="$($colors.SurfaceAlt)" />
  <SolidColorBrush x:Key="TextPrimaryBrush" Color="$($colors.TextPrimary)" />
  <SolidColorBrush x:Key="TextSecondaryBrush" Color="$($colors.TextSecondary)" />
  <SolidColorBrush x:Key="AccentBrush" Color="$($colors.Accent)" />
  <SolidColorBrush x:Key="AccentHoverBrush" Color="$($colors.AccentHover)" />
  <SolidColorBrush x:Key="DangerBrush" Color="$($colors.Danger)" />
  <SolidColorBrush x:Key="WarningBrush" Color="$($colors.Warning)" />
  <SolidColorBrush x:Key="SuccessBrush" Color="$($colors.Success)" />
  <SolidColorBrush x:Key="BorderBrush" Color="$($colors.Border)" />
  <SolidColorBrush x:Key="ButtonBackgroundBrush" Color="$($colors.ButtonBackground)" />
  <SolidColorBrush x:Key="ButtonTextBrush" Color="$($colors.ButtonText)" />
  <SolidColorBrush x:Key="HeaderBackgroundBrush" Color="$($colors.HeaderBackground)" />
  <SolidColorBrush x:Key="HeaderTextBrush" Color="$($colors.HeaderText)" />
</ResourceDictionary>
"@

  return $xaml
}
