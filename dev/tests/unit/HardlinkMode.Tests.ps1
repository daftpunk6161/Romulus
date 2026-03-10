BeforeAll {
  . "$PSScriptRoot/../../modules/HardlinkMode.ps1"
}

Describe 'HardlinkMode (XL-11)' {

  Describe 'Test-HardlinkSupported' {
    It 'prueft Laufwerk C' {
      $result = Test-HardlinkSupported -DriveLetter 'C'
      $result.DriveLetter | Should -Be 'C'
      $result.ContainsKey('IsSupported') | Should -Be $true
      $result.ContainsKey('FileSystem') | Should -Be $true
    }

    It 'trimmt Doppelpunkt' {
      $result = Test-HardlinkSupported -DriveLetter 'C:'
      $result.DriveLetter | Should -Be 'C'
    }
  }

  Describe 'New-LinkStructureConfig' {
    It 'erstellt Konfiguration mit Standardwerten' {
      $cfg = New-LinkStructureConfig -SourceRoot 'D:\Roms' -TargetRoot 'D:\Links'
      $cfg.SourceRoot | Should -Be 'D:\Roms'
      $cfg.TargetRoot | Should -Be 'D:\Links'
      $cfg.LinkType | Should -Be 'Hardlink'
      $cfg.GroupBy | Should -Be 'ConsoleAndGenre'
    }

    It 'akzeptiert Symlink-Typ' {
      $cfg = New-LinkStructureConfig -SourceRoot 'S' -TargetRoot 'T' -LinkType 'Symlink'
      $cfg.LinkType | Should -Be 'Symlink'
    }
  }

  Describe 'New-LinkOperation' {
    It 'erstellt Link-Operation' {
      $op = New-LinkOperation -SourceFile 'D:\src\game.rom' -TargetPath 'D:\links\NES\game.rom'
      $op.SourceFile | Should -Be 'D:\src\game.rom'
      $op.TargetPath | Should -Be 'D:\links\NES\game.rom'
      $op.Status | Should -Be 'Pending'
      $op.LinkType | Should -Be 'Hardlink'
    }
  }

  Describe 'Build-LinkPlan' {
    BeforeAll {
      $script:cfg = New-LinkStructureConfig -SourceRoot 'D:\Roms' -TargetRoot 'D:\Links' -GroupBy 'Console'
      $script:files = @(
        @{ FileName = 'mario.rom'; FullPath = 'D:\Roms\mario.rom'; ConsoleKey = 'NES'; Genre = 'Platformer'; Region = 'EU' }
        @{ FileName = 'zelda.rom'; FullPath = 'D:\Roms\zelda.rom'; ConsoleKey = 'SNES'; Genre = 'RPG'; Region = 'US' }
      )
    }

    It 'erstellt Plan mit korrekter Anzahl Operationen' {
      $plan = Build-LinkPlan -Config $cfg -Files $files
      $plan.TotalLinks | Should -Be 2
      @($plan.Operations).Count | Should -Be 2
    }

    It 'gruppiert nach Konsole' {
      $plan = Build-LinkPlan -Config $cfg -Files $files
      $plan.Operations[0].TargetPath | Should -BeLike '*NES*'
      $plan.Operations[1].TargetPath | Should -BeLike '*SNES*'
    }

    It 'gruppiert nach Genre' {
      $genreCfg = New-LinkStructureConfig -SourceRoot 'D:\Roms' -TargetRoot 'D:\Links' -GroupBy 'Genre'
      $plan = Build-LinkPlan -Config $genreCfg -Files $files
      $plan.Operations[0].TargetPath | Should -BeLike '*Platformer*'
    }

    It 'verwendet _Uncategorized bei fehlendem Genre' {
      $genreCfg = New-LinkStructureConfig -SourceRoot 'D:\Roms' -TargetRoot 'D:\Links' -GroupBy 'Genre'
      $noGenreFiles = @(@{ FileName = 'x.rom'; FullPath = 'D:\x.rom'; ConsoleKey = 'GB'; Genre = ''; Region = '' })
      $plan = Build-LinkPlan -Config $genreCfg -Files $noGenreFiles
      $plan.Operations[0].TargetPath | Should -BeLike '*_Uncategorized*'
    }
  }

  Describe 'Get-LinkSavingsEstimate' {
    It 'berechnet Einsparung fuer Hardlinks' {
      $cfg = New-LinkStructureConfig -SourceRoot 'S' -TargetRoot 'T' -LinkType 'Hardlink'
      $files = @(@{ FileName = 'a'; FullPath = 'a'; ConsoleKey = 'NES'; Genre = ''; Region = '' ; SizeBytes = 1000 })
      $plan = Build-LinkPlan -Config $cfg -Files $files
      $savings = Get-LinkSavingsEstimate -Plan $plan -Files $files
      $savings.SavedBytes | Should -Be 1000
      $savings.SavingsPercent | Should -Be 100.0
    }

    It 'keine Einsparung fuer Symlinks' {
      $cfg = New-LinkStructureConfig -SourceRoot 'S' -TargetRoot 'T' -LinkType 'Symlink'
      $files = @(@{ FileName = 'a'; FullPath = 'a'; ConsoleKey = 'NES'; Genre = ''; Region = ''; SizeBytes = 1000 })
      $plan = Build-LinkPlan -Config $cfg -Files $files
      $savings = Get-LinkSavingsEstimate -Plan $plan -Files $files
      $savings.SavedBytes | Should -Be 0
    }
  }

  Describe 'Get-HardlinkStatistics' {
    It 'zaehlt Status korrekt' {
      $cfg = New-LinkStructureConfig -SourceRoot 'S' -TargetRoot 'T'
      $files = @(
        @{ FileName = 'a'; FullPath = 'a'; ConsoleKey = 'NES'; Genre = ''; Region = '' },
        @{ FileName = 'b'; FullPath = 'b'; ConsoleKey = 'NES'; Genre = ''; Region = '' }
      )
      $plan = Build-LinkPlan -Config $cfg -Files $files
      $stats = Get-HardlinkStatistics -Plan $plan
      $stats.TotalLinks | Should -Be 2
      $stats.Pending | Should -Be 2
      $stats.Completed | Should -Be 0
    }
  }
}
