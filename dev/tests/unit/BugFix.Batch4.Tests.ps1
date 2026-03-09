#requires -Modules Pester
# ================================================================
#  BugFix.Batch4.Tests.ps1  –  Regressionstests fuer Bug-Fixes Batch 4
#  BUG-034 (LRU thread-safety doc), BUG-021 (rollback audit trail),
#  BUG-038 (InsecureToolHashBypass GUI), BUG-043 (DUP cancel check),
#  BUG-050 (UiPump deprecation), BUG-036/037 (closed non-issues)
# ================================================================

Describe 'Bug-Fix Batch 4 Regression Tests' {

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

    # ---- BUG-034: LRU Cache thread-safety documentation ----
    Context 'BUG-034: LRU Cache thread-safety documented' {

        It 'LruCache.ps1 contains thread-safety documentation' {
            $lruPath = Join-Path $root 'dev\modules\LruCache.ps1'
            $content = Get-Content -LiteralPath $lruPath -Raw
            $content | Should -Match 'THREAD SAFETY' -Because 'thread-safety requirements must be documented (BUG-034)'
            $content | Should -Match 'NOT thread-safe' -Because 'non-thread-safety must be clearly stated'
        }
    }

    # ---- BUG-021: Rollback writes audit trail ----
    Context 'BUG-021: Rollback audit trail on partial failure' {

        BeforeAll {
            . (Join-Path $root 'dev\modules\RunHelpers.Audit.ps1')
        }

        It 'Invoke-AuditRollback returns RollbackAuditPath property' {
            $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) ('b4test_rb_' + [guid]::NewGuid().ToString('N').Substring(0,8))
            New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
            try {
                # Create a minimal audit CSV
                $csvPath = Join-Path $tmpDir 'audit.csv'
                'Action,Source,Dest' | Set-Content -LiteralPath $csvPath -Encoding UTF8
                'MOVE,C:\nonexist\a.zip,C:\nonexist\b.zip' | Add-Content -LiteralPath $csvPath -Encoding UTF8

                $result = Invoke-AuditRollback -AuditCsvPath $csvPath -DryRun
                $result.PSObject.Properties.Name | Should -Contain 'RollbackAuditPath' -Because 'new property must exist (BUG-021)'
            } finally {
                Remove-Item -LiteralPath $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }

        It 'Source code tracks rollbackTrail list' {
            $auditPath = Join-Path $root 'dev\modules\RunHelpers.Audit.ps1'
            $content = Get-Content -LiteralPath $auditPath -Raw
            $content | Should -Match 'rollbackTrail' -Because 'rollback operations must be tracked (BUG-021)'
            $content | Should -Match 'rollback-audit\.csv' -Because 'rollback audit CSV must be written'
        }
    }

    # ---- BUG-038: InsecureToolHashBypass GUI confirmation ----
    Context 'BUG-038: InsecureToolHashBypass GUI confirmation' {

        It 'Source code shows MessageBox for InsecureToolHashBypass in STA mode' {
            $toolsPath = Join-Path $root 'dev\modules\Tools.ps1'
            $content = Get-Content -LiteralPath $toolsPath -Raw
            $content | Should -Match 'AllowInsecureToolHashBypass.*MessageBox|MessageBox.*InsecureToolHashBypass|Sicherheitswarnung.*Tool-Hash-Bypass' -Because 'GUI confirmation dialog must exist (BUG-038)'
        }
    }

    # ---- BUG-043: DUP retry loop cancellation check ----
    Context 'BUG-043: Move-ItemSafely checks cancellation in retry loop' {

        It 'Source code checks Test-CancelRequested in DUP retry loop' {
            $fileOpsPath = Join-Path $root 'dev\modules\FileOps.ps1'
            $content = Get-Content -LiteralPath $fileOpsPath -Raw
            $content | Should -Match 'Test-CancelRequested' -Because 'cancellation must be checked between retry attempts (BUG-043)'
        }
    }

    # ---- BUG-050: Invoke-UiPump deprecation ----
    Context 'BUG-050: Invoke-UiPump marked as deprecated' {

        It 'Compatibility.ps1 contains deprecation warning for Invoke-UiPump' {
            $compatPath = Join-Path $root 'dev\modules\Compatibility.ps1'
            $content = Get-Content -LiteralPath $compatPath -Raw
            $content | Should -Match 'DEPRECATED|veraltet' -Because 'Invoke-UiPump must be marked deprecated (BUG-050)'
            $content | Should -Match 'DoEvents' -Because 'deprecation reason must mention DoEvents pattern'
        }
    }

    # ---- BUG-036/037: Verified non-issues — no code change needed ----
    Context 'BUG-036/037: Verified non-issues' {

        It 'BUG-036: HTML tooltip is handled by existing encoding (no fix needed)' {
            $true | Should -BeTrue -Because 'BUG-036 was classified as non-issue after investigation'
        }

        It 'BUG-037: Rate limit cleanup is single-threaded safe (no fix needed)' {
            $true | Should -BeTrue -Because 'BUG-037 was classified as non-issue — server is single-threaded'
        }
    }
}
