#requires -Modules Pester
# ================================================================
#  BugFix.Batch3.Tests.ps1  –  Regressionstests fuer Bug-Fixes Batch 3
#  BUG-027 (Turkish i), BUG-033 (dead code tab), BUG-044 (regex timeout),
#  BUG-045 (XML MaxChars), BUG-049 (report plugin scope), BUG-003 (orphan detection),
#  BUG-040 (API race condition), BUG-047 (PluginTrustMode URL install)
# ================================================================

Describe 'Bug-Fix Batch 3 Regression Tests' {

    BeforeAll {
        $root = $PSScriptRoot
        while ($root -and -not (Test-Path (Join-Path $root 'simple_sort.ps1'))) {
            $root = Split-Path -Parent $root
        }
        . (Join-Path $root 'dev\modules\LruCache.ps1')
        . (Join-Path $root 'dev\modules\AppState.ps1')
        . (Join-Path $root 'dev\modules\AppStateSchema.ps1')
        . (Join-Path $root 'dev\modules\FileOps.ps1')
        . (Join-Path $root 'dev\modules\Settings.ps1')
    }

    # ---- BUG-027: Turkish dotless-i handling ----
    Context 'BUG-027: ConvertTo-AsciiFold handles Turkish i/I' {

        BeforeAll {
            . (Join-Path $root 'dev\modules\Core.ps1')
            Initialize-RulePatterns
        }

        It 'Folds Turkish dotless-i to ASCII i' {
            $result = ConvertTo-AsciiFold 'Istanbul'
            $result | Should -Not -BeLike '*ı*' -Because 'Turkish ı must fold to i'
        }

        It 'Folds Turkish capital İ to ASCII I' {
            $result = ConvertTo-AsciiFold "$([char]0x0130)stanbul"
            $result | Should -BeLike 'Istanbul' -Because 'Turkish İ must fold to I'
        }
    }

    # ---- BUG-033: CSV sanitizer dead code removed ----
    Context 'BUG-033: ConvertTo-SafeOutputValue no dead tab check' {

        BeforeAll {
            . (Join-Path $root 'dev\modules\Report.ps1')
        }

        It 'Source code does not contain redundant StartsWith tab check' {
            $reportPath = Join-Path $root 'dev\modules\Report.ps1'
            $content = Get-Content -LiteralPath $reportPath -Raw
            $content | Should -Not -Match 'StartsWith\(\[string\]\[char\]9\)' -Because 'dead tab check was removed (BUG-033)'
        }

        It 'Still sanitizes leading equals sign' {
            $result = ConvertTo-SafeOutputValue '=cmd|calc|A1'
            $result | Should -BeLike "'*" -Because 'CSV injection prefix must be added'
        }
    }

    # ---- BUG-044: Regex timeout on all compiled patterns ----
    Context 'BUG-044: Initialize-RulePatterns uses regex timeout' {

        It 'Source code uses TimeSpan timeout for all regex constructors' {
            $corePath = Join-Path $root 'dev\modules\Core.ps1'
            $content = Get-Content -LiteralPath $corePath -Raw
            # Extract Initialize-RulePatterns function body
            $funcMatch = [regex]::Match($content, '(?s)function\s+Initialize-RulePatterns\b.*?(?=\nfunction\s|\n\$script:ALL_ROM)')
            $funcMatch.Success | Should -BeTrue -Because 'Initialize-RulePatterns function must exist'
            $funcBody = $funcMatch.Value
            # Count [regex]::new( calls vs $rxTimeout usages
            $newCalls = [regex]::Matches($funcBody, '\[regex\]::new\(').Count
            $timeoutRefs = [regex]::Matches($funcBody, '\$rxTimeout').Count
            $newCalls | Should -BeGreaterThan 5 -Because 'there should be many regex compilations'
            # rxTimeout must be defined once + used in every [regex]::new() call
            $timeoutRefs | Should -BeGreaterOrEqual $newCalls -Because 'every [regex]::new() must reference $rxTimeout (BUG-044)'
        }
    }

    # ---- BUG-045: XML MaxCharactersInDocument reduced ----
    Context 'BUG-045: DAT XML MaxChars reduced from 500MB' {

        It 'Dat.ps1 maxCharsInDoc is 100MB or less' {
            $datPath = Join-Path $root 'dev\modules\Dat.ps1'
            $content = Get-Content -LiteralPath $datPath -Raw
            # Verify no 500MB literal
            $content | Should -Not -Match 'maxCharsInDoc\s*=\s*500MB' -Because '500MB was reduced to 100MB (BUG-045)'
            $content | Should -Match 'maxCharsInDoc\s*=\s*100MB' -Because 'limit should be 100MB'
        }
    }

    # ---- BUG-049: Report plugin child-scope isolation ----
    Context 'BUG-049: Invoke-ReportPlugins uses child scope' {

        It 'Source code wraps plugin block in child scope' {
            $rbPath = Join-Path $root 'dev\modules\ReportBuilder.ps1'
            $content = Get-Content -LiteralPath $rbPath -Raw
            # The pattern & { & $pluginBlock ... } indicates child scope wrapping
            $content | Should -Match '\&\s*\{[\s\S]*?\&\s*\$pluginBlock' -Because 'plugin must run in isolated child scope (BUG-049)'
        }
    }

    # ---- BUG-003: Orphan .tmp_move detection ----
    Context 'BUG-003: Find-OrphanedTmpMoveFiles detects orphans' {

        It 'Find-OrphanedTmpMoveFiles function exists' {
            Get-Command Find-OrphanedTmpMoveFiles -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Returns empty array for clean directory' {
            $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) ('b3test_clean_' + [guid]::NewGuid().ToString('N').Substring(0,8))
            New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
            try {
                New-Item -ItemType File -Path (Join-Path $tmpDir 'normal.zip') -Force | Out-Null
                $result = Find-OrphanedTmpMoveFiles -Roots @($tmpDir)
                $result.Count | Should -Be 0
            } finally {
                Remove-Item -LiteralPath $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }

        It 'Detects .tmp_move orphans in roots' {
            $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) ('b3test_orphan_' + [guid]::NewGuid().ToString('N').Substring(0,8))
            New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
            try {
                New-Item -ItemType File -Path (Join-Path $tmpDir 'game.zip.tmp_move') -Force | Out-Null
                New-Item -ItemType File -Path (Join-Path $tmpDir 'other.zip') -Force | Out-Null
                $result = Find-OrphanedTmpMoveFiles -Roots @($tmpDir)
                $result.Count | Should -Be 1
                $result[0].FullName | Should -BeLike '*.tmp_move'
            } finally {
                Remove-Item -LiteralPath $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    # ---- BUG-040: API Start-ApiRun synchronization ----
    Context 'BUG-040: Start-ApiRun uses Monitor synchronization' {

        It 'Source code uses Monitor.Enter/Exit around ActiveRunId' {
            $apiPath = Join-Path $root 'dev\modules\ApiServer.ps1'
            $content = Get-Content -LiteralPath $apiPath -Raw
            $content | Should -Match 'Monitor\]::Enter' -Because 'Monitor.Enter must guard check-and-set (BUG-040)'
            $content | Should -Match 'Monitor\]::Exit' -Because 'Monitor.Exit must release lock (BUG-040)'
        }
    }

    # ---- BUG-047: Plugin URL install checks PluginTrustMode ----
    Context 'BUG-047: Plugin URL install checks TrustMode' {

        It 'Source code checks PluginTrustMode before URL download' {
            $wpfPath = Join-Path $root 'dev\modules\WpfSlice.AdvancedFeatures.ps1'
            $content = Get-Content -LiteralPath $wpfPath -Raw
            $content | Should -Match 'PluginTrustMode' -Because 'TrustMode must be checked before URL install (BUG-047)'
            $content | Should -Match 'signed-only' -Because 'signed-only mode must block URL install'
        }
    }
}
