BeforeAll {
  . "$PSScriptRoot/../../modules/SchedulerAdvanced.ps1"
}

Describe 'MF-23: SchedulerAdvanced' {
  Describe 'ConvertFrom-CronExpression' {
    It 'parst gueltige Cron-Expression' {
      $result = ConvertFrom-CronExpression -Expression '30 2 * * 1'
      $result.Valid | Should -BeTrue
      $result.Minute | Should -Be '30'
      $result.Hour | Should -Be '2'
      $result.DayOfMonth | Should -Be '*'
      $result.Month | Should -Be '*'
      $result.DayOfWeek | Should -Be '1'
    }

    It 'erkennt ungueltige Expression (zu wenig Felder)' {
      $result = ConvertFrom-CronExpression -Expression '30 2 *'
      $result.Valid | Should -BeFalse
    }
  }

  Describe 'Test-CronFieldMatch' {
    It 'matcht Wildcard' {
      Test-CronFieldMatch -Field '*' -Value 15 | Should -BeTrue
    }

    It 'matcht Einzelwert' {
      Test-CronFieldMatch -Field '5' -Value 5 | Should -BeTrue
      Test-CronFieldMatch -Field '5' -Value 6 | Should -BeFalse
    }

    It 'matcht Komma-Liste' {
      Test-CronFieldMatch -Field '1,3,5' -Value 3 | Should -BeTrue
      Test-CronFieldMatch -Field '1,3,5' -Value 4 | Should -BeFalse
    }

    It 'matcht Bereich' {
      Test-CronFieldMatch -Field '1-5' -Value 3 | Should -BeTrue
      Test-CronFieldMatch -Field '1-5' -Value 6 | Should -BeFalse
    }

    It 'matcht Intervall' {
      Test-CronFieldMatch -Field '*/5' -Value 10 | Should -BeTrue
      Test-CronFieldMatch -Field '*/5' -Value 7 | Should -BeFalse
    }
  }

  Describe 'Test-CronMatch' {
    It 'matcht Datum gegen Expression' {
      $cron = ConvertFrom-CronExpression -Expression '0 12 * * *'
      $dt = [datetime]::new(2026, 3, 9, 12, 0, 0)
      Test-CronMatch -CronParsed $cron -DateTime $dt | Should -BeTrue
    }

    It 'matcht nicht bei falscher Stunde' {
      $cron = ConvertFrom-CronExpression -Expression '0 12 * * *'
      $dt = [datetime]::new(2026, 3, 9, 15, 0, 0)
      Test-CronMatch -CronParsed $cron -DateTime $dt | Should -BeFalse
    }
  }

  Describe 'Get-NextCronOccurrence' {
    It 'findet naechste Ausfuehrung' {
      $cron = ConvertFrom-CronExpression -Expression '0 * * * *'
      $after = [datetime]::new(2026, 3, 9, 14, 30, 0)
      $result = Get-NextCronOccurrence -CronParsed $cron -After $after -MaxSearchMinutes 120
      $result.Found | Should -BeTrue
      $result.NextRun.Hour | Should -Be 15
      $result.NextRun.Minute | Should -Be 0
    }
  }

  Describe 'New-ScheduleEntry' {
    It 'erstellt Schedule-Eintrag' {
      $entry = New-ScheduleEntry -Name 'Nachtlauf' -CronExpression '0 2 * * *'
      $entry.Name | Should -Be 'Nachtlauf'
      $entry.CronExpression | Should -Be '0 2 * * *'
      $entry.Enabled | Should -BeTrue
    }

    It 'gibt Fehler bei ungueltiger Cron-Expression' {
      $entry = New-ScheduleEntry -Name 'Bad' -CronExpression 'invalid'
      $entry.Status | Should -Be 'Error'
    }
  }
}
