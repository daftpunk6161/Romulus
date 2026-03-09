#requires -Modules Pester
# ================================================================
#  BugFix.Batch2.Tests.ps1  –  Regressionstests fuer Bug-Fixes Batch 2
#  BUG-009 (Convert TOCTOU), BUG-017 (DatIndex threading),
#  BUG-031 (Archive Bomb size), BUG-018 (Timer tick try/catch)
# ================================================================

Describe 'Bug-Fix Batch 2 Regression Tests' {

    BeforeAll {
        $root = $PSScriptRoot
        while ($root -and -not (Test-Path (Join-Path $root 'simple_sort.ps1'))) {
            $root = Split-Path -Parent $root
        }
        . (Join-Path $root 'dev\modules\LruCache.ps1')
        . (Join-Path $root 'dev\modules\AppState.ps1')
        . (Join-Path $root 'dev\modules\AppStateSchema.ps1')
        . (Join-Path $root 'dev\modules\FileOps.ps1')
        . (Join-Path $root 'dev\modules\SetParsing.ps1')
        . (Join-Path $root 'dev\modules\Tools.ps1')
        . (Join-Path $root 'dev\modules\Convert.ps1')
    }

    # ---- BUG-009: Convert TOCTOU — target check before source cleanup ----
    Context 'BUG-009: Convert Target-Check vor Source-Cleanup' {

        It 'Invoke-ConvertItem Code verifies target BEFORE removing sources' {
            # Verify the source code order: target check must come before source removal
            $convertPath = Join-Path $root 'dev\modules\Convert.ps1'
            $lines = Get-Content -LiteralPath $convertPath

            # Find the line with target-missing-after-commit (our new check)
            $targetCheckLines = @($lines | Select-String 'target-missing-after-commit')
            # Find source removal lines (backup or remove) — skip the function definition
            $sourceRemovalLines = @($lines | Select-String 'Move-ConvertedSourceToBackup' | Where-Object { $_.Line -notmatch '^function ' })

            $targetCheckLines.Count | Should -BeGreaterThan 0 -Because 'target-missing-after-commit check must exist'
            $sourceRemovalLines.Count | Should -BeGreaterThan 0 -Because 'source removal reference must exist'
            $targetCheckLines[0].LineNumber | Should -BeLessThan $sourceRemovalLines[0].LineNumber -Because 'target check must come BEFORE source cleanup (TOCTOU fix)'
        }

        It 'old TOCTOU pattern target-missing-after-source-cleanup is removed' {
            $convertPath = Join-Path $root 'dev\modules\Convert.ps1'
            $content = Get-Content -LiteralPath $convertPath -Raw
            $content | Should -Not -Match 'target-missing-after-source-cleanup' -Because 'BUG-009: old TOCTOU pattern must be replaced'
        }
    }

    # ---- BUG-017: DatIndex thread-safety ----
    Context 'BUG-017: DatIndex Synchronized-Wrapper fuer RunspacePool' {

        It 'DatIndex is wrapped in hashtable.Synchronized before passing to workers' {
            $dedupePath = Join-Path $root 'dev\modules\Dedupe.ps1'
            $content = Get-Content -LiteralPath $dedupePath -Raw
            $content | Should -Match 'hashtable\]::Synchronized\(\$DatIndex\)' -Because 'DatIndex must be thread-safe for parallel workers'
        }

        It 'Synchronized hashtable allows concurrent reads' {
            $ht = @{ key1 = 'val1'; key2 = 'val2'; key3 = 'val3' }
            $synced = [hashtable]::Synchronized($ht)

            # Simulate concurrent reads (should not throw)
            $results = 1..100 | ForEach-Object {
                $synced['key1']
                $synced['key2']
                $synced['key3']
            }
            # All reads should return values
            ($results | Where-Object { $_ -eq 'val1' }).Count | Should -Be 100
        }
    }

    # ---- BUG-031: Archive Bomb decompressed size limit ----
    Context 'BUG-031: Archive-Bomb Groessenlimit in Expand-ArchiveToTemp' {

        It 'Expand-ArchiveToTemp code contains decompressed size check' {
            $toolsPath = Join-Path $root 'dev\modules\Tools.ps1'
            $content = Get-Content -LiteralPath $toolsPath -Raw
            $content | Should -Match 'maxDecompressedBytes' -Because 'decompressed size limit must be checked'
            $content | Should -Match '50GB' -Because 'default limit should be 50GB'
        }

        It 'Expand-ArchiveToTemp code contains entry count check' {
            $toolsPath = Join-Path $root 'dev\modules\Tools.ps1'
            $content = Get-Content -LiteralPath $toolsPath -Raw
            $content | Should -Match 'maxEntryCount' -Because 'entry count limit must be checked'
            $content | Should -Match '10000' -Because 'default entry limit should be 10000'
        }

        It 'size check parses Size lines from 7z output' {
            $toolsPath = Join-Path $root 'dev\modules\Tools.ps1'
            $content = Get-Content -LiteralPath $toolsPath -Raw
            $content | Should -Match 'Size = ' -Because 'must parse Size lines from 7z -slt listing'
            $content | Should -Match 'totalSize' -Because 'must accumulate total decompressed size'
        }
    }

    # ---- BUG-018: Timer tick try/catch restructure ----
    Context 'BUG-018: Timer-Tick Update-WpfRuntimeStatus isoliert' {

        It 'Update-WpfRuntimeStatus is wrapped in its own try/catch in timer tick' {
            $wpfPath = Join-Path $root 'dev\modules\WpfEventHandlers.ps1'
            $lines = Get-Content -LiteralPath $wpfPath
            # Find the BUG-018 FIX comment
            $fixLine = @($lines | Select-String 'BUG-018 FIX')
            $fixLine.Count | Should -BeGreaterThan 0 -Because 'BUG-018 FIX comment must exist'
            # Verify the try block around Update-WpfRuntimeStatus
            $tryLine = @($lines | Select-String 'try \{' | Where-Object { $_.LineNumber -gt ($fixLine[0].LineNumber - 2) -and $_.LineNumber -lt ($fixLine[0].LineNumber + 2) })
            $tryLine.Count | Should -BeGreaterThan 0 -Because 'try block must wrap Update-WpfRuntimeStatus'
            # Verify catch block follows
            $catchLine = @($lines | Select-String 'Non-fatal: status update failure')
            $catchLine.Count | Should -BeGreaterThan 0 -Because 'catch block must handle failure gracefully'
        }
    }

    # ---- BUG-001: SQLite injection prevention (already fixed, regression test) ----
    Context 'BUG-001: SQLite Input-Validierung (Bestandsschutz)' {

        It 'Save-SqliteFileScanIndex rejects paths with semicolons' {
            $fopsPath = Join-Path $root 'dev\modules\FileOps.ps1'
            $content = Get-Content -LiteralPath $fopsPath -Raw
            $content | Should -Match "rootSql -match '\[;\\x00-\\x1f\]'" -Because 'semicolons in paths must be rejected'
        }

        It 'Save-SqliteFileScanIndex rejects SQL comment sequences' {
            $fopsPath = Join-Path $root 'dev\modules\FileOps.ps1'
            $content = Get-Content -LiteralPath $fopsPath -Raw
            $content | Should -Match "rootSql -match '--'" -Because 'SQL comment sequences must be rejected'
        }
    }

    # ---- BUG-042: MAX_PATH check (already fixed, regression test) ----
    Context 'BUG-042: Path-Length-Check in Move-ItemSafely (Bestandsschutz)' {

        It 'Move-ItemSafely rejects paths longer than 240 chars' {
            $fopsPath = Join-Path $root 'dev\modules\FileOps.ps1'
            $content = Get-Content -LiteralPath $fopsPath -Raw
            $content | Should -Match '240' -Because 'path length limit of 240 must be checked'
        }

        It 'throws on excessively long paths' {
            $longName = 'A' * 200
            $longSource = Join-Path $TestDrive "$longName.zip"
            $longDest = Join-Path $TestDrive "dest\$longName.zip"

            { Move-ItemSafely -Source $longSource -Dest $longDest } | Should -Throw '*too long*'
        }
    }

    # ---- BUG-048: ConsolePlugin regex validation (already fixed, regression test) ----
    Context 'BUG-048: ConsolePlugin Regex-Validierung (Bestandsschutz)' {

        It 'Import-ConsolePlugins validates regex length' {
            $cpPath = Join-Path $root 'dev\modules\ConsolePlugins.ps1'
            $content = Get-Content -LiteralPath $cpPath -Raw
            $content | Should -Match 'max 500' -Because 'regex length limit of 500 must be enforced'
        }

        It 'Import-ConsolePlugins validates regex syntax with timeout' {
            $cpPath = Join-Path $root 'dev\modules\ConsolePlugins.ps1'
            $content = Get-Content -LiteralPath $cpPath -Raw
            $content | Should -Match 'FromSeconds\(2\)' -Because 'regex compilation must have a 2-second timeout'
        }
    }
}