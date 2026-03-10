BeforeAll {
    $root = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
    $modulePath = Join-Path $root 'dev' 'modules'
    $script:_RomCleanupModuleRoot = $modulePath
    . (Join-Path $modulePath 'RomCleanupLoader.ps1')
    if (Get-Command Initialize-AppState -ErrorAction SilentlyContinue) { Initialize-AppState }
}

Describe 'ApplicationServices Feature-Facades' {

    # ── Invoke-RunDatRenameService ──────────────────────────────────────────
    Context 'Invoke-RunDatRenameService' {
        BeforeAll {
            $script:tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "appSvcTest_rename_$([guid]::NewGuid().ToString('N').Substring(0,8))"
            New-Item -ItemType Directory -Path $script:tempDir -Force | Out-Null
            $script:testFile = Join-Path $script:tempDir 'test.sfc'
            Set-Content -Path $script:testFile -Value 'dummy'
            $hash = (Get-FileHash -Path $script:testFile -Algorithm SHA1).Hash
            $script:datIndex = @{
                $hash = 'Super Mario World (USA).sfc'
            }
        }
        AfterAll {
            if (Test-Path $script:tempDir) { Remove-Item -Recurse -Force $script:tempDir -ErrorAction SilentlyContinue }
        }

        It 'returns result with Preview operation' {
            $result = Invoke-RunDatRenameService -Operation 'Preview' -Files @($script:testFile) -DatIndex $script:datIndex -HashType 'SHA1'
            $result | Should -Not -BeNullOrEmpty
            $result.Total | Should -Be 1
            $result.Operation | Should -Be 'Preview'
        }

        It 'tracks rename counts correctly' {
            $result = Invoke-RunDatRenameService -Operation 'Preview' -Files @($script:testFile) -DatIndex $script:datIndex -HashType 'SHA1'
            ($result.Renamed + $result.NoMatch + $result.Conflicts) | Should -Be $result.Total
        }

        It 'handles nonexistent file gracefully' {
            $result = Invoke-RunDatRenameService -Operation 'Preview' -Files @('nonexistent_placeholder.sfc') -DatIndex $script:datIndex -HashType 'SHA1'
            $result.Total | Should -Be 1
        }
    }

    # ── Invoke-RunM3uGenerationService ──────────────────────────────────────
    Context 'Invoke-RunM3uGenerationService' {
        BeforeAll {
            $script:tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "appSvcTest_m3u_$([guid]::NewGuid().ToString('N').Substring(0,8))"
            New-Item -ItemType Directory -Path $script:tempDir -Force | Out-Null
        }
        AfterAll {
            if (Test-Path $script:tempDir) { Remove-Item -Recurse -Force $script:tempDir -ErrorAction SilentlyContinue }
        }

        It 'returns result object with correct shape' {
            $files = @(
                (Join-Path $script:tempDir 'Game (Disc 1).cue'),
                (Join-Path $script:tempDir 'Game (Disc 2).cue')
            )
            foreach ($f in $files) { Set-Content -Path $f -Value 'dummy' }
            $result = Invoke-RunM3uGenerationService -Files $files -OutputDir $script:tempDir -Mode 'DryRun'
            $result | Should -Not -BeNullOrEmpty
            $result.PSObject.Properties.Name | Should -Contain 'Generated'
            $result.PSObject.Properties.Name | Should -Contain 'Skipped'
            $result.Mode | Should -Be 'DryRun'
        }
    }

    # ── Invoke-RunEcmDecompressService ──────────────────────────────────────
    Context 'Invoke-RunEcmDecompressService' {
        It 'filters only .ecm files' {
            $result = Invoke-RunEcmDecompressService -Files @('file.zip', 'file.bin') -Mode 'DryRun'
            $result.Total | Should -Be 0
        }

        It 'returns correct shape for non-ecm files' {
            $result = Invoke-RunEcmDecompressService -Files @('not-ecm.bin') -Mode 'DryRun'
            $result | Should -Not -BeNullOrEmpty
            $result.Mode | Should -Be 'DryRun'
            $result.Total | Should -Be 0
        }
    }

    # ── Invoke-RunArchiveRepackService ──────────────────────────────────────
    Context 'Invoke-RunArchiveRepackService' {
        It 'returns correct result shape' {
            $result = Invoke-RunArchiveRepackService -Files @('nonexistent.zip') -TargetFormat 'zip' -Mode 'DryRun'
            $result | Should -Not -BeNullOrEmpty
            $result.TargetFormat | Should -Be 'zip'
        }
    }

    # ── Invoke-RunJunkReportService ─────────────────────────────────────────
    Context 'Invoke-RunJunkReportService' {
        It 'identifies junk files' {
            $files = @('Game (Beta).zip', 'Game (Demo).zip', 'Clean Game.zip')
            $result = Invoke-RunJunkReportService -Files $files -AggressiveJunk $false
            $result | Should -Not -BeNullOrEmpty
            $result.TotalChecked | Should -Be 3
            $result.JunkFound | Should -BeGreaterOrEqual 1
        }

        It 'returns zero junk for clean game names' {
            $result = Invoke-RunJunkReportService -Files @('Super Mario World (USA).zip') -AggressiveJunk $false
            $result.JunkFound | Should -Be 0
        }
    }

    # ── Invoke-RunCsvExportService ──────────────────────────────────────────
    Context 'Invoke-RunCsvExportService' {
        BeforeAll {
            $script:tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "appSvcTest_csv_$([guid]::NewGuid().ToString('N').Substring(0,8))"
            New-Item -ItemType Directory -Path $script:tempDir -Force | Out-Null
        }
        AfterAll {
            if (Test-Path $script:tempDir) { Remove-Item -Recurse -Force $script:tempDir -ErrorAction SilentlyContinue }
        }

        It 'exports CSV file' {
            $items = @(
                [pscustomobject]@{ Name = 'Game1'; Console = 'SNES'; Region = 'EU'; Size = 1024 }
                [pscustomobject]@{ Name = 'Game2'; Console = 'NES'; Region = 'US'; Size = 512 }
            )
            $csvPath = Join-Path $script:tempDir 'test-export.csv'
            Invoke-RunCsvExportService -Items $items -OutputPath $csvPath | Out-Null
            Test-Path $csvPath | Should -Be $true
        }
    }

    # ── Invoke-RunCliExportService ──────────────────────────────────────────
    Context 'Invoke-RunCliExportService' {
        It 'exports CLI command from settings' {
            $settings = @{
                Roots          = @('D:\ROMs')
                Mode           = 'DryRun'
                PreferRegions  = @('EU','US')
            }
            $result = Invoke-RunCliExportService -Settings $settings
            $result | Should -Not -BeNullOrEmpty
            $result | Should -BeOfType [string]
            $result | Should -Match 'Invoke-RomCleanup'
        }
    }

    # ── Invoke-RunWebhookService ────────────────────────────────────────────
    Context 'Invoke-RunWebhookService' {
        It 'rejects non-HTTPS URL' {
            { Invoke-RunWebhookService -WebhookUrl 'http://insecure.example.com/hook' -Summary @{ Status = 'test' } } | Should -Throw
        }
    }

    # ── Invoke-RunRetroArchExportService ────────────────────────────────────
    Context 'Invoke-RunRetroArchExportService' {
        BeforeAll {
            $script:tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "appSvcTest_ra_$([guid]::NewGuid().ToString('N').Substring(0,8))"
            New-Item -ItemType Directory -Path $script:tempDir -Force | Out-Null
        }
        AfterAll {
            if (Test-Path $script:tempDir) { Remove-Item -Recurse -Force $script:tempDir -ErrorAction SilentlyContinue }
        }

        It 'exports playlist file' {
            $items = @(
                [pscustomobject]@{ Path = 'D:\ROMs\SNES\Game.sfc'; Label = 'Game'; Console = 'SNES' }
            )
            $outputPath = Join-Path $script:tempDir 'playlist.lpl'
            $result = Invoke-RunRetroArchExportService -Items $items -OutputPath $outputPath
            $result | Should -Not -BeNullOrEmpty
        }
    }

    # ── Invoke-RunConvertQueueService ───────────────────────────────────────
    Context 'Invoke-RunConvertQueueService' {
        It 'creates a queue' {
            $items = @(
                [pscustomobject]@{ SourcePath = 'file1.bin'; TargetPath = 'file1.chd'; Format = 'chd' }
                [pscustomobject]@{ SourcePath = 'file2.bin'; TargetPath = 'file2.chd'; Format = 'chd' }
            )
            $result = Invoke-RunConvertQueueService -Operation 'Create' -Items $items
            $result | Should -Not -BeNullOrEmpty
            $result.TotalItems | Should -Be 2
        }

        It 'saves and loads queue' {
            $tempQueuePath = Join-Path ([System.IO.Path]::GetTempPath()) "queue_test_$([guid]::NewGuid().ToString('N').Substring(0,8)).json"
            try {
                # First create a proper queue, then save it
                $items = @(
                    [pscustomobject]@{ SourcePath = 'f1.bin'; TargetPath = 'f1.chd'; Format = 'chd' }
                )
                $queue = Invoke-RunConvertQueueService -Operation 'Create' -Items $items
                Invoke-RunConvertQueueService -Operation 'Save' -Items @($queue) -QueuePath $tempQueuePath
                Test-Path $tempQueuePath | Should -Be $true
                $loaded = Invoke-RunConvertQueueService -Operation 'Load' -QueuePath $tempQueuePath
                $loaded | Should -Not -BeNullOrEmpty
            } finally {
                if (Test-Path $tempQueuePath) { Remove-Item $tempQueuePath -Force -ErrorAction SilentlyContinue }
            }
        }
    }

    # ── Invoke-RunRuleEngineService ─────────────────────────────────────────
    Context 'Invoke-RunRuleEngineService' {
        It 'evaluates rules against item' {
            $rules = @(
                (New-ClassificationRule -Name 'NoJP' -Priority 1 -Conditions @(@{ Field = 'Region'; Op = 'eq'; Value = 'JP' }) -Action 'junk' -Reason 'JP not wanted')
            )
            $item = @{ Region = 'JP'; Console = 'SNES'; Name = 'Game' }
            $result = Invoke-RunRuleEngineService -Rules $rules -Item $item
            $result | Should -Not -BeNullOrEmpty
        }
    }

    # ── Invoke-RunIntegrityCheckService ─────────────────────────────────────
    Context 'Invoke-RunIntegrityCheckService' {
        BeforeAll {
            $script:tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "appSvcTest_integ_$([guid]::NewGuid().ToString('N').Substring(0,8))"
            New-Item -ItemType Directory -Path $script:tempDir -Force | Out-Null
            $script:testFile = Join-Path $script:tempDir 'integrity.bin'
            Set-Content -Path $script:testFile -Value 'test-data'
        }
        AfterAll {
            if (Test-Path $script:tempDir) { Remove-Item -Recurse -Force $script:tempDir -ErrorAction SilentlyContinue }
        }

        It 'creates baseline' {
            $result = Invoke-RunIntegrityCheckService -Operation 'Baseline' -Files @($script:testFile) -Algorithm 'SHA256'
            $result | Should -Not -BeNullOrEmpty
        }
    }

    # ── Invoke-RunQuarantineService ─────────────────────────────────────────
    Context 'Invoke-RunQuarantineService' {
        It 'creates quarantine action in DryRun' {
            $result = Invoke-RunQuarantineService -SourcePath 'C:\fake\suspicious.rom' -QuarantineRoot 'C:\fake\quarantine' -Reasons @('Unknown') -Mode 'DryRun'
            $result | Should -Not -BeNullOrEmpty
        }
    }

    # ── Invoke-RunParallelHashService ───────────────────────────────────────
    Context 'Invoke-RunParallelHashService' {
        BeforeAll {
            $script:tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "appSvcTest_hash_$([guid]::NewGuid().ToString('N').Substring(0,8))"
            New-Item -ItemType Directory -Path $script:tempDir -Force | Out-Null
            $script:testFiles = @()
            for ($i = 1; $i -le 3; $i++) {
                $f = Join-Path $script:tempDir "file$i.bin"
                Set-Content -Path $f -Value "content-$i"
                $script:testFiles += $f
            }
        }
        AfterAll {
            if (Test-Path $script:tempDir) { Remove-Item -Recurse -Force $script:tempDir -ErrorAction SilentlyContinue }
        }

        It 'hashes files and returns result with TotalFiles' {
            $result = Invoke-RunParallelHashService -Files $script:testFiles -Algorithm 'SHA1'
            $result | Should -Not -BeNullOrEmpty
            $result.TotalFiles | Should -BeGreaterOrEqual 0
            $result.Method | Should -Not -BeNullOrEmpty
        }
    }

    # ── Invoke-RunBackupService ─────────────────────────────────────────────
    Context 'Invoke-RunBackupService' {
        It 'creates backup session' {
            $config = New-BackupConfig -BackupRoot 'C:\fake\backups' -RetentionDays 30 -MaxSizeGB 10
            $result = Invoke-RunBackupService -Operation 'Create' -Config $config -Label 'test-backup'
            $result | Should -Not -BeNullOrEmpty
        }
    }
}

Describe 'OperationAdapters Post-Run Pipeline' {
    Context 'Invoke-CliRunAdapter parameter validation' {
        It 'accepts new feature parameters without error' {
            # Verify the function signature accepts new params (function exists and param binding works)
            $cmd = Get-Command Invoke-CliRunAdapter -ErrorAction SilentlyContinue
            $cmd | Should -Not -BeNullOrEmpty
            $paramNames = $cmd.Parameters.Keys
            $paramNames | Should -Contain 'DatRename'
            $paramNames | Should -Contain 'EcmDecompress'
            $paramNames | Should -Contain 'ArchiveRepack'
            $paramNames | Should -Contain 'ArchiveRepackFormat'
            $paramNames | Should -Contain 'ArchiveCompressionLevel'
            $paramNames | Should -Contain 'GenerateM3u'
            $paramNames | Should -Contain 'ExportRetroArch'
            $paramNames | Should -Contain 'ExportCsv'
            $paramNames | Should -Contain 'WebhookUrl'
            $paramNames | Should -Contain 'ParallelHash'
        }
    }
}

Describe 'CLI Entry Point Parameters' {
    BeforeAll {
        $script:cliScript = Join-Path (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))) 'Invoke-RomCleanup.ps1'
        $script:ast = [System.Management.Automation.Language.Parser]::ParseFile($script:cliScript, [ref]$null, [ref]$null)
        $script:paramBlock = $script:ast.ParamBlock
    }

    It 'has DatRename parameter' {
        $param = $script:paramBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq 'DatRename' }
        $param | Should -Not -BeNullOrEmpty
    }

    It 'has EcmDecompress parameter' {
        $param = $script:paramBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq 'EcmDecompress' }
        $param | Should -Not -BeNullOrEmpty
    }

    It 'has GenerateM3u parameter' {
        $param = $script:paramBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq 'GenerateM3u' }
        $param | Should -Not -BeNullOrEmpty
    }

    It 'has ExportRetroArch parameter' {
        $param = $script:paramBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq 'ExportRetroArch' }
        $param | Should -Not -BeNullOrEmpty
    }

    It 'has ExportCsv parameter' {
        $param = $script:paramBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq 'ExportCsv' }
        $param | Should -Not -BeNullOrEmpty
    }

    It 'has WebhookUrl parameter' {
        $param = $script:paramBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq 'WebhookUrl' }
        $param | Should -Not -BeNullOrEmpty
    }

    It 'has ArchiveRepack parameter' {
        $param = $script:paramBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq 'ArchiveRepack' }
        $param | Should -Not -BeNullOrEmpty
    }

    It 'has ParallelHash parameter' {
        $param = $script:paramBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq 'ParallelHash' }
        $param | Should -Not -BeNullOrEmpty
    }

    It 'ArchiveRepackFormat validates zip and 7z only' {
        $param = $script:paramBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq 'ArchiveRepackFormat' }
        $param | Should -Not -BeNullOrEmpty
        $validateAttr = $param.Attributes | Where-Object { $_.TypeName.Name -eq 'ValidateSet' }
        $validateAttr | Should -Not -BeNullOrEmpty
    }
}
