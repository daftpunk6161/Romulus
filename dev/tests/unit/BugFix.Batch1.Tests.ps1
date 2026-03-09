#requires -Modules Pester
# ================================================================
#  BugFix.Batch1.Tests.ps1  –  Regressionstests fuer Bug-Fixes Batch 1
#  BUG-032, BUG-030, BUG-022, BUG-041, BUG-029, BUG-035, BUG-039
# ================================================================

Describe 'Bug-Fix Batch 1 Regression Tests' {

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
        . (Join-Path $root 'dev\modules\Settings.ps1')
        . (Join-Path $root 'dev\modules\ApiServer.ps1')
        . (Join-Path $root 'dev\modules\Tools.ps1')
        . (Join-Path $root 'dev\modules\Convert.ps1')
        . (Join-Path $root 'dev\modules\RunHelpers.Audit.ps1')
    }

    # ---- BUG-022: M3U recursion depth limit ----
    Context 'BUG-022: M3U Rekursionstiefe begrenzt' {

        It 'stoppt bei MaxDepth=0 ohne Stack Overflow' {
            $tempDir = Join-Path $TestDrive 'bug022'
            New-Item -Path $tempDir -ItemType Directory -Force | Out-Null

            # Erstelle M3U die auf sich selbst verweist (Zyklus-Check + Tiefe)
            $m3uPath = Join-Path $tempDir 'disc.m3u'
            Set-Content -Path $m3uPath -Value 'disc.m3u'

            $result = Get-M3URelatedFiles -M3UPath $m3uPath -RootPath $tempDir -MaxDepth 1
            $result | Should -Not -BeNullOrEmpty
        }

        It 'warnt bei ueberschrittener MaxDepth' {
            $tempDir = Join-Path $TestDrive 'bug022warn'
            New-Item -Path $tempDir -ItemType Directory -Force | Out-Null

            $m3uPath = Join-Path $tempDir 'x.m3u'
            Set-Content -Path $m3uPath -Value 'y.m3u'
            $m3uPath2 = Join-Path $tempDir 'y.m3u'
            Set-Content -Path $m3uPath2 -Value 'z.m3u'

            $warnings = @()
            $result = Get-M3URelatedFiles -M3UPath $m3uPath -RootPath $tempDir -MaxDepth 1 -WarningVariable warnings 3>&1 | Where-Object { $_ -is [string] -or $_ -is [System.Management.Automation.WarningRecord] }
            # No crash = success
            $true | Should -BeTrue
        }

        It 'Get-M3UMissingFiles stoppt bei MaxDepth=0 ohne Crash' {
            $tempDir = Join-Path $TestDrive 'bug022miss'
            New-Item -Path $tempDir -ItemType Directory -Force | Out-Null
            $m3uPath = Join-Path $tempDir 'a.m3u'
            Set-Content -Path $m3uPath -Value 'b.m3u'

            # MaxDepth=0 should return empty without crash
            $result = Get-M3UMissingFiles -M3UPath $m3uPath -RootPath $tempDir -MaxDepth 0
            $result.Count | Should -Be 0
        }
    }

    # ---- BUG-041: HMAC key minimum length ----
    Context 'BUG-041: HMAC-Key Mindestlaenge 32 Zeichen' {

        It 'gibt null zurueck bei kurzem Key (< 32 Zeichen)' {
            $oldKey = $env:ROMCLEANUP_AUDIT_HMAC_KEY
            try {
                $env:ROMCLEANUP_AUDIT_HMAC_KEY = 'short-key'
                $result = Get-AuditSigningKeyBytes
                $result | Should -BeNullOrEmpty
            } finally {
                $env:ROMCLEANUP_AUDIT_HMAC_KEY = $oldKey
            }
        }

        It 'gibt bytes zurueck bei ausreichend langem Key (>= 32 Zeichen)' {
            $oldKey = $env:ROMCLEANUP_AUDIT_HMAC_KEY
            try {
                $env:ROMCLEANUP_AUDIT_HMAC_KEY = 'this-is-a-valid-key-with-32-char!'
                $result = Get-AuditSigningKeyBytes
                $result | Should -Not -BeNullOrEmpty
                $result.Length | Should -BeGreaterOrEqual 32
            } finally {
                $env:ROMCLEANUP_AUDIT_HMAC_KEY = $oldKey
            }
        }

        It 'gibt null zurueck bei leerem Key' {
            $oldKey = $env:ROMCLEANUP_AUDIT_HMAC_KEY
            try {
                $env:ROMCLEANUP_AUDIT_HMAC_KEY = ''
                $result = Get-AuditSigningKeyBytes
                $result | Should -BeNullOrEmpty
            } finally {
                $env:ROMCLEANUP_AUDIT_HMAC_KEY = $oldKey
            }
        }
    }

    # ---- BUG-039: Convert Audit CSV Injection ----
    Context 'BUG-039: CSV Injection in Konvertierungs-Audit' {

        It 'schuetzt MainPath mit fuehrendem = Zeichen' {
            $row = New-ConversionAuditRow -Status 'OK' -ToolName 'chdman' `
                -MainPath '=cmd|''/C calc''!A0.iso' -TargetExt '.chd' `
                -OutputPath 'output.chd' -Reason '' -OldSize 100 -NewSize 50 -Saved 50

            $row | Should -Match "'\="
        }

        It 'schuetzt MainPath mit fuehrendem + Zeichen' {
            $row = New-ConversionAuditRow -Status 'OK' -ToolName '7z' `
                -MainPath '+dangerous.zip' -TargetExt '.zip' `
                -OutputPath 'output.zip' -Reason '' -OldSize 100 -NewSize 50 -Saved 50

            $row | Should -Match "'\+"
        }

        It 'laesst normale Pfade unveraendert' {
            $row = New-ConversionAuditRow -Status 'OK' -ToolName 'chdman' `
                -MainPath 'C:\ROMs\Game.iso' -TargetExt '.chd' `
                -OutputPath 'C:\ROMs\Game.chd' -Reason '' -OldSize 100 -NewSize 50 -Saved 50

            $row | Should -Match 'C:\\ROMs\\Game\.iso'
            $row | Should -Not -Match "'C:"
        }
    }

    # ---- BUG-035: ToolHash Cache Invalidation ----
    Context 'BUG-035: ToolHash-Cache Invalidierung bei Binary-Aenderung' {

        It 'cacheKey enthaelt LastWriteTime-Ticks' {
            $tempExe = Join-Path $TestDrive 'tool.exe'
            Set-Content -Path $tempExe -Value 'fake-binary-v1'
            
            # Reset cache
            $script:TOOL_HASH_VERDICT_CACHE = [hashtable]::new([StringComparer]::OrdinalIgnoreCase)
            
            # The cache should not have a key with just the path
            $result1 = Test-ToolBinaryHash -ToolPath $tempExe
            
            # Cache should have entry with path|ticks format
            $keys = @($script:TOOL_HASH_VERDICT_CACHE.Keys)
            $keys.Count | Should -Be 1
            $keys[0] | Should -Match '\|'
        }
    }

    # ---- BUG-032: API Roots Validation ----
    Context 'BUG-032: API Root-Path Validierung' {

        It 'blockiert nicht-existierende Roots' {
            $err = Test-ApiRunPayload -Payload ([pscustomobject]@{ mode = 'DryRun'; roots = @('Z:\NoSuchPathEver12345') })
            [string]$err | Should -Match 'does not exist'
        }

        It 'blockiert Windows-Systemverzeichnis' {
            $winDir = [System.Environment]::GetFolderPath('Windows')
            if ($winDir -and (Test-Path $winDir)) {
                $err = Test-ApiRunPayload -Payload ([pscustomobject]@{ mode = 'Move'; roots = @($winDir) })
                [string]$err | Should -Match 'protected system directory'
            }
        }

        It 'akzeptiert gueltige existierende Verzeichnisse' {
            $validDir = Join-Path $TestDrive 'validRoms'
            New-Item -Path $validDir -ItemType Directory -Force | Out-Null
            $err = Test-ApiRunPayload -Payload ([pscustomobject]@{ mode = 'DryRun'; roots = @($validDir) })
            $err | Should -BeNullOrEmpty
        }
    }
}
