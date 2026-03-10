#requires -Modules Pester

<#
  DatRename Unit Tests (QW-01)
  Tests fuer Rename-RomToDatName und Invoke-BatchDatRename.
#>

BeforeAll {
    $root = $PSScriptRoot
    while ($root -and -not (Test-Path (Join-Path $root 'simple_sort.ps1'))) {
        $root = Split-Path -Parent $root
    }
    . (Join-Path $root 'dev\modules\DatRename.ps1')
}

Describe 'Rename-RomToDatName' {

    BeforeEach {
        $script:tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ('DatRenameTest-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $script:tempDir -Force | Out-Null
    }

    AfterEach {
        if (Test-Path $script:tempDir) {
            Remove-Item -LiteralPath $script:tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Context 'Hash-Match und Rename' {
        It 'Benennt Datei korrekt um bei DAT-Match (Move-Modus)' {
            $testFile = Join-Path $script:tempDir 'wrong-name.bin'
            [System.IO.File]::WriteAllBytes($testFile, [byte[]](1,2,3,4,5))

            $hash = (Get-FileHash -LiteralPath $testFile -Algorithm SHA1).Hash
            $datIndex = @{ $hash = 'Correct Game Name (Europe)' }

            $result = Rename-RomToDatName -FilePath $testFile -DatIndex $datIndex -Mode Move

            $result.Status | Should -Be 'Renamed'
            $result.OldName | Should -Be 'wrong-name.bin'
            $result.NewName | Should -Be 'Correct Game Name (Europe).bin'
            $result.Hash | Should -Be $hash
            Test-Path (Join-Path $script:tempDir 'Correct Game Name (Europe).bin') | Should -BeTrue
            Test-Path $testFile | Should -BeFalse
        }

        It 'DryRun veraendert keine Datei' {
            $testFile = Join-Path $script:tempDir 'test.sfc'
            [System.IO.File]::WriteAllBytes($testFile, [byte[]](10,20,30))

            $hash = (Get-FileHash -LiteralPath $testFile -Algorithm SHA1).Hash
            $datIndex = @{ $hash = 'Super Mario World (Europe)' }

            $result = Rename-RomToDatName -FilePath $testFile -DatIndex $datIndex -Mode DryRun

            $result.Status | Should -Be 'WouldRename'
            $result.NewName | Should -Be 'Super Mario World (Europe).sfc'
            Test-Path $testFile | Should -BeTrue
        }
    }

    Context 'Kein Match' {
        It 'Status NoMatch wenn Hash nicht im Index' {
            $testFile = Join-Path $script:tempDir 'unknown.rom'
            [System.IO.File]::WriteAllBytes($testFile, [byte[]](99,99,99))

            $datIndex = @{ 'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA' = 'Some Game' }

            $result = Rename-RomToDatName -FilePath $testFile -DatIndex $datIndex

            $result.Status | Should -Be 'NoMatch'
        }
    }

    Context 'Dateiname-Konflikt' {
        It 'Status Conflict wenn Zielname existiert' {
            $testFile = Join-Path $script:tempDir 'old-name.bin'
            [System.IO.File]::WriteAllBytes($testFile, [byte[]](1,2,3))

            $existingFile = Join-Path $script:tempDir 'Target Name.bin'
            [System.IO.File]::WriteAllBytes($existingFile, [byte[]](4,5,6))

            $hash = (Get-FileHash -LiteralPath $testFile -Algorithm SHA1).Hash
            $datIndex = @{ $hash = 'Target Name' }

            $result = Rename-RomToDatName -FilePath $testFile -DatIndex $datIndex -Mode Move

            $result.Status | Should -Be 'Conflict'
            Test-Path $testFile | Should -BeTrue
        }
    }

    Context 'Ungueltige Zeichen im DAT-Namen' {
        It 'Sanitisiert ungueltige Zeichen zu Underscore' {
            $testFile = Join-Path $script:tempDir 'test.nes'
            [System.IO.File]::WriteAllBytes($testFile, [byte[]](7,8,9))

            $hash = (Get-FileHash -LiteralPath $testFile -Algorithm SHA1).Hash
            $datIndex = @{ $hash = 'Game: Sub/Title?' }

            $result = Rename-RomToDatName -FilePath $testFile -DatIndex $datIndex -Mode DryRun

            $result.NewName | Should -Not -Match '[:/\\?]'
            $result.Status | Should -Be 'WouldRename'
        }
    }

    Context 'Datei existiert nicht' {
        It 'Status FileNotFound bei fehlender Datei' {
            $result = Rename-RomToDatName -FilePath 'C:\nonexistent\file.rom' -DatIndex @{}

            $result.Status | Should -Be 'FileNotFound'
        }
    }

    Context 'Already Correct' {
        It 'Status AlreadyCorrect wenn Name bereits stimmt' {
            $testFile = Join-Path $script:tempDir 'Final Fantasy VII (Europe).bin'
            [System.IO.File]::WriteAllBytes($testFile, [byte[]](11,22,33))

            $hash = (Get-FileHash -LiteralPath $testFile -Algorithm SHA1).Hash
            $datIndex = @{ $hash = 'Final Fantasy VII (Europe)' }

            $result = Rename-RomToDatName -FilePath $testFile -DatIndex $datIndex

            $result.Status | Should -Be 'AlreadyCorrect'
        }
    }

    Context 'DAT-Entry als Objekt' {
        It 'Unterstuetzt DAT-Index mit Name-Property' {
            $testFile = Join-Path $script:tempDir 'test.gba'
            [System.IO.File]::WriteAllBytes($testFile, [byte[]](44,55,66))

            $hash = (Get-FileHash -LiteralPath $testFile -Algorithm SHA1).Hash
            $datIndex = @{ $hash = [pscustomobject]@{ Name = 'Pokemon Emerald (USA)'; Size = 16384 } }

            $result = Rename-RomToDatName -FilePath $testFile -DatIndex $datIndex -Mode DryRun

            $result.NewName | Should -Be 'Pokemon Emerald (USA).gba'
            $result.Status | Should -Be 'WouldRename'
        }
    }
}

Describe 'Invoke-BatchDatRename' {

    BeforeEach {
        $script:tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ('DatRenameBatch-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $script:tempDir -Force | Out-Null
    }

    AfterEach {
        if (Test-Path $script:tempDir) {
            Remove-Item -LiteralPath $script:tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'Verarbeitet mehrere Dateien und gibt Summary zurueck' {
        $file1 = Join-Path $script:tempDir 'a.bin'
        $file2 = Join-Path $script:tempDir 'b.bin'
        [System.IO.File]::WriteAllBytes($file1, [byte[]](1,1,1))
        [System.IO.File]::WriteAllBytes($file2, [byte[]](2,2,2))

        $hash1 = (Get-FileHash -LiteralPath $file1 -Algorithm SHA1).Hash
        $datIndex = @{ $hash1 = 'Game A (Europe)' }

        $summary = Invoke-BatchDatRename -Files @($file1, $file2) -DatIndex $datIndex -Mode DryRun

        $summary.Total | Should -Be 2
        $summary.Renamed | Should -Be 1
        $summary.NoMatch | Should -Be 1
    }
}
