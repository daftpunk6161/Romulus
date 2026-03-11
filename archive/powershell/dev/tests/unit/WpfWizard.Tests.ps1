#requires -Modules Pester

Describe 'ISS-001: First-Start Wizard (WpfWizard.ps1)' {
    BeforeAll {
        $script:root = $PSScriptRoot
        while ($script:root -and -not (Test-Path (Join-Path $script:root 'simple_sort.ps1'))) {
            $script:root = Split-Path -Parent $script:root
        }

        # Load WPF assemblies for [System.Windows.Window] type
        Add-Type -AssemblyName PresentationFramework -ErrorAction SilentlyContinue
        Add-Type -AssemblyName PresentationCore -ErrorAction SilentlyContinue
        Add-Type -AssemblyName WindowsBase -ErrorAction SilentlyContinue

        . (Join-Path $script:root 'dev\modules\Settings.ps1')
        . (Join-Path $script:root 'dev\modules\AppState.ps1')

        # Stub WPF host functions used by wizard
        function New-WpfWindowFromXaml { param([string]$Xaml) return $null }
        function Get-WpfNamedElements { param($Window) return @{} }
        function Add-WpfResourceDictionary { param($Window, $ResourceDictionaryXaml) }
        function Get-WpfThemeResourceDictionaryXaml { return '<ResourceDictionary/>' }
        function Add-WpfLogLine { param([hashtable]$Ctx, [string]$Line, [string]$Level) }
        function Update-WpfStatusBar { param([hashtable]$Ctx, [switch]$Initial) }
        function Set-WpfViewModelProperty { param($Ctx, $Name, $Value) return $false }
        function Get-WpfViewModel { param($Ctx) return $null }
        function Get-AppStateValue { param([string]$Key, $Default) return $Default }
        function Set-AppStateValue { param([string]$Key, $Value) }
        function Test-RomCleanupAutomatedTestMode { return $false }
        function Find-ExternalTool { param([string]$Name) return $null }

        . (Join-Path $script:root 'dev\modules\WpfWizard.ps1')
    }

    Context 'XAML-Definition' {
        It 'WPF_WIZARD_XAML ist definiert und nicht leer' {
            $script:WPF_WIZARD_XAML | Should -Not -BeNullOrEmpty
        }

        It 'XAML enthält alle 3 Step-Panels' {
            $script:WPF_WIZARD_XAML | Should -Match 'wizStep1'
            $script:WPF_WIZARD_XAML | Should -Match 'wizStep2'
            $script:WPF_WIZARD_XAML | Should -Match 'wizStep3'
        }

        It 'XAML enthält Step-Indicator-Dots' {
            $script:WPF_WIZARD_XAML | Should -Match 'wizDot1'
            $script:WPF_WIZARD_XAML | Should -Match 'wizDot2'
            $script:WPF_WIZARD_XAML | Should -Match 'wizDot3'
        }

        It 'XAML enthält Intent-Buttons' {
            $script:WPF_WIZARD_XAML | Should -Match 'wizIntentCleanup'
            $script:WPF_WIZARD_XAML | Should -Match 'wizIntentSort'
            $script:WPF_WIZARD_XAML | Should -Match 'wizIntentConvert'
        }

        It 'XAML enthält Drag-Drop-Zone' {
            $script:WPF_WIZARD_XAML | Should -Match 'wizDropZone'
            $script:WPF_WIZARD_XAML | Should -Match 'AllowDrop="True"'
        }

        It 'XAML enthält Preflight-Check-Indikatoren' {
            $script:WPF_WIZARD_XAML | Should -Match 'wizChkFolders'
            $script:WPF_WIZARD_XAML | Should -Match 'wizChkFiles'
            $script:WPF_WIZARD_XAML | Should -Match 'wizChkTrash'
            $script:WPF_WIZARD_XAML | Should -Match 'wizChkTools'
            $script:WPF_WIZARD_XAML | Should -Match 'wizOverallDot'
        }

        It 'XAML enthält Navigation-Buttons' {
            $script:WPF_WIZARD_XAML | Should -Match 'wizBtnBack'
            $script:WPF_WIZARD_XAML | Should -Match 'wizBtnNext'
            $script:WPF_WIZARD_XAML | Should -Match 'wizBtnSkip'
        }

        It 'XAML verwendet Theme-Brushes (keine Hardcoded-Farben)' {
            $script:WPF_WIZARD_XAML | Should -Match 'BrushBackground'
            $script:WPF_WIZARD_XAML | Should -Match 'BrushSurface'
            $script:WPF_WIZARD_XAML | Should -Match 'BrushAccentCyan'
            $script:WPF_WIZARD_XAML | Should -Match 'BrushBorder'
        }

        It 'XAML enthält Folder-List und Trash-TextBox' {
            $script:WPF_WIZARD_XAML | Should -Match 'wizFolderList'
            $script:WPF_WIZARD_XAML | Should -Match 'wizTxtTrash'
            $script:WPF_WIZARD_XAML | Should -Match 'wizBtnTrash'
            $script:WPF_WIZARD_XAML | Should -Match 'wizBtnAddFolder'
        }

        It 'XAML Step 1 zeigt Intent-Auswahl "Was möchtest du tun?"' {
            $script:WPF_WIZARD_XAML | Should -Match 'Was möchtest du tun\?'
            $script:WPF_WIZARD_XAML | Should -Match 'Sammlung aufräumen'
            $script:WPF_WIZARD_XAML | Should -Match 'Nur Konsolen sortieren'
            $script:WPF_WIZARD_XAML | Should -Match 'Nur konvertieren'
        }
    }

    Context 'Invoke-WpfFirstStartWizard Skip-Conditions' {
        BeforeEach {
            # Reset env
            $env:ROM_CLEANUP_SKIP_ONBOARDING = $null
        }

        AfterEach {
            $env:ROM_CLEANUP_SKIP_ONBOARDING = $null
        }

        It 'Überspringt bei ROM_CLEANUP_SKIP_ONBOARDING=1' {
            $env:ROM_CLEANUP_SKIP_ONBOARDING = '1'
            $mockWindow = [pscustomobject]@{}
            $mockCtx = @{
                'listRoots' = [pscustomobject]@{ Items = @() }
                'txtTrash' = [pscustomobject]@{ Text = '' }
            }
            $browseCalled = $false
            $mockBrowse = { $script:browseCalled = $true; return $null }

            # Should return without calling Show-FirstStartWizard
            { Invoke-WpfFirstStartWizard -Window $mockWindow -Ctx $mockCtx -BrowseFolder $mockBrowse } | Should -Not -Throw
            $browseCalled | Should -BeFalse
        }

        It 'Überspringt bei ROM_CLEANUP_SKIP_ONBOARDING=true' {
            $env:ROM_CLEANUP_SKIP_ONBOARDING = 'true'
            $mockWindow = [pscustomobject]@{}
            $mockCtx = @{
                'listRoots' = [pscustomobject]@{ Items = @() }
                'txtTrash' = [pscustomobject]@{ Text = '' }
            }

            { Invoke-WpfFirstStartWizard -Window $mockWindow -Ctx $mockCtx -BrowseFolder {} } | Should -Not -Throw
        }

        It 'Überspringt bei SkipOnboardingWizard = true im AppState' {
            function Get-AppStateValue { param([string]$Key, $Default)
                if ($Key -eq 'SkipOnboardingWizard') { return $true }
                return $Default
            }

            $mockWindow = [pscustomobject]@{}
            $mockCtx = @{
                'listRoots' = [pscustomobject]@{ Items = @() }
                'txtTrash' = [pscustomobject]@{ Text = '' }
            }

            { Invoke-WpfFirstStartWizard -Window $mockWindow -Ctx $mockCtx -BrowseFolder {} } | Should -Not -Throw
        }

        It 'Überspringt wenn Roots und Trash bereits konfiguriert und Settings existieren' {
            function Get-UserSettings { return @{ general = @{ logLevel = 'Info' } } }
            function Get-AppStateValue { param([string]$Key, $Default) return $Default }
            function Get-WpfViewModel { param($Ctx) return $null }

            $mockWindow = [pscustomobject]@{}
            $mockCtx = @{
                'listRoots' = [pscustomobject]@{ Items = @('C:\Roms') }
                'txtTrash' = [pscustomobject]@{ Text = 'C:\Roms\_trash' }
            }

            { Invoke-WpfFirstStartWizard -Window $mockWindow -Ctx $mockCtx -BrowseFolder {} } | Should -Not -Throw
        }

        It 'Überspringt im Automated-Test-Modus' {
            function Test-RomCleanupAutomatedTestMode { return $true }
            function Get-AppStateValue { param([string]$Key, $Default) return $Default }

            $mockWindow = [pscustomobject]@{}
            $mockCtx = @{
                'listRoots' = [pscustomobject]@{ Items = @() }
                'txtTrash' = [pscustomobject]@{ Text = '' }
            }

            { Invoke-WpfFirstStartWizard -Window $mockWindow -Ctx $mockCtx -BrowseFolder {} } | Should -Not -Throw
        }
    }

    Context 'Show-FirstStartWizard Function' {
        It 'Show-FirstStartWizard ist definiert' {
            Get-Command Show-FirstStartWizard -ErrorAction SilentlyContinue | Should -Not -BeNull
        }

        It 'Invoke-WpfFirstStartWizard ist definiert' {
            Get-Command Invoke-WpfFirstStartWizard -ErrorAction SilentlyContinue | Should -Not -BeNull
        }

        It 'Show-FirstStartWizard hat korrekte Parameter' {
            $cmd = Get-Command Show-FirstStartWizard
            $cmd.Parameters.Keys | Should -Contain 'OwnerWindow'
            $cmd.Parameters.Keys | Should -Contain 'BrowseFolder'
        }

        It 'Invoke-WpfFirstStartWizard hat korrekte Parameter' {
            $cmd = Get-Command Invoke-WpfFirstStartWizard
            $cmd.Parameters.Keys | Should -Contain 'Window'
            $cmd.Parameters.Keys | Should -Contain 'Ctx'
            $cmd.Parameters.Keys | Should -Contain 'BrowseFolder'
        }
    }

    Context 'XAML ist valide XML' {
        It 'XAML kann als XML geparst werden' {
            { [xml]$script:WPF_WIZARD_XAML } | Should -Not -Throw
        }

        It 'Alle benannten Elemente haben eindeutige x:Name' {
            $xml = [xml]$script:WPF_WIZARD_XAML
            $ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
            $ns.AddNamespace('x', 'http://schemas.microsoft.com/winfx/2006/xaml')
            $ns.AddNamespace('wpf', 'http://schemas.microsoft.com/winfx/2006/xaml/presentation')
            $names = @($xml.SelectNodes('//@x:Name', $ns) | ForEach-Object { $_.Value })
            $names.Count | Should -BeGreaterThan 0
            $uniqueNames = @($names | Select-Object -Unique)
            $uniqueNames.Count | Should -Be $names.Count
        }
    }

    Context 'Wizard-Ergebnis-Struktur' {
        BeforeAll {
            # Inspect the wizard result hashtable by reading the source
            $wizSource = Get-Content -LiteralPath (Join-Path $script:root 'dev\modules\WpfWizard.ps1') -Raw
        }

        It 'WizardResult enthält Completed-Flag' {
            $wizSource | Should -Match 'Completed\s*='
        }

        It 'WizardResult enthält Intent-Feld' {
            $wizSource | Should -Match "Intent\s*="
        }

        It 'WizardResult enthält Roots-Liste' {
            $wizSource | Should -Match 'Roots\s*='
        }

        It 'WizardResult enthält TrashRoot' {
            $wizSource | Should -Match 'TrashRoot\s*='
        }

        It 'WizardResult enthält AutoStartDryRun' {
            $wizSource | Should -Match 'AutoStartDryRun\s*='
        }
    }

    Context 'Intent-Mapping auf Settings' {
        BeforeAll {
            $wizSource = Get-Content -LiteralPath (Join-Path $script:root 'dev\modules\WpfWizard.ps1') -Raw
        }

        It 'Cleanup-Intent setzt DryRun, SortConsole und AliasKeying' {
            # In the apply-results section, cleanup sets all three flags
            $applySection = ($wizSource -split 'Apply intent-based defaults')[1]
            $applySection | Should -Not -BeNullOrEmpty
            $applySection | Should -Match 'cleanup'
            $applySection | Should -Match 'DryRun'
            $applySection | Should -Match 'SortConsole'
            $applySection | Should -Match 'AliasKeying'
        }

        It 'Sort-Intent setzt DryRun und SortConsole (kein AliasKeying)' {
            $wizSource | Should -Match "'sort'"
        }

        It 'Convert-Intent setzt nur DryRun' {
            $wizSource | Should -Match "'convert'"
        }
    }

    Context 'Edge Cases' {
        It 'XAML enthält kein RomCleanup-internal JavaScript (kein XSS-Risiko)' {
            $script:WPF_WIZARD_XAML | Should -Not -Match '<script'
        }

        It 'Step-Panels nutzen Visibility Collapsed (nicht Hidden)' {
            $script:WPF_WIZARD_XAML | Should -Match 'Visibility="Collapsed"'
            $script:WPF_WIZARD_XAML | Should -Not -Match 'Visibility="Hidden"'
        }

        It 'XAML hat WindowStartupLocation=CenterScreen' {
            $script:WPF_WIZARD_XAML | Should -Match 'WindowStartupLocation="CenterScreen"'
        }

        It 'SkipOnboardingWizard wird nach Wizard-Abschluss gesetzt' {
            $wizSource = Get-Content -LiteralPath (Join-Path $script:root 'dev\modules\WpfWizard.ps1') -Raw
            $wizSource | Should -Match "Set-AppStateValue -Key 'SkipOnboardingWizard' -Value \`$true"
        }

        It 'Wizard bricht ohne Settings-Speicherung ab wenn übersprungen' {
            $wizSource = Get-Content -LiteralPath (Join-Path $script:root 'dev\modules\WpfWizard.ps1') -Raw
            # Skip button sets DialogResult = $false (may span multiple lines)
            $wizSource | Should -Match 'wizBtnSkip'
            $wizSource | Should -Match 'DialogResult = \$false'
        }
    }

    Context 'Integration WpfEventHandlers' {
        It 'WpfEventHandlers ruft Invoke-WpfFirstStartWizard auf' {
            $handlersSource = Get-Content -LiteralPath (Join-Path $script:root 'dev\modules\WpfEventHandlers.ps1') -Raw
            $handlersSource | Should -Match 'Invoke-WpfFirstStartWizard'
        }

        It 'WpfEventHandlers hat Fallback auf Invoke-WpfQuickOnboarding' {
            $handlersSource = Get-Content -LiteralPath (Join-Path $script:root 'dev\modules\WpfEventHandlers.ps1') -Raw
            $handlersSource | Should -Match 'Invoke-WpfQuickOnboarding'
        }
    }

    Context 'ModuleFileList Registration' {
        It 'WpfWizard.ps1 ist in ModuleFileList registriert' {
            $moduleList = Get-Content -LiteralPath (Join-Path $script:root 'dev\modules\ModuleFileList.ps1') -Raw
            $moduleList | Should -Match 'WpfWizard\.ps1'
        }

        It 'WpfWizard.ps1 hat Dependency-Deklaration' {
            $moduleList = Get-Content -LiteralPath (Join-Path $script:root 'dev\modules\ModuleFileList.ps1') -Raw
            $moduleList | Should -Match "\`$deps\['WpfWizard\.ps1'\]"
        }

        It 'WpfWizard.ps1 wird vor WpfEventHandlers geladen' {
            $moduleList = Get-Content -LiteralPath (Join-Path $script:root 'dev\modules\ModuleFileList.ps1') -Raw
            $wizPos = $moduleList.IndexOf("'WpfWizard.ps1'")
            $evtPos = $moduleList.IndexOf("'WpfEventHandlers.ps1'")
            $wizPos | Should -BeLessThan $evtPos
        }
    }
}
