BeforeAll {
  . "$PSScriptRoot/../../modules/SystemTray.ps1"
}

Describe 'MF-18: SystemTray' {
  Describe 'New-TrayIconConfig' {
    It 'erstellt Standard-Konfiguration' {
      $cfg = New-TrayIconConfig
      $cfg.ToolTip | Should -Be 'RomCleanup'
      $cfg.IconState | Should -Be 'Idle'
      $cfg.Visible | Should -BeTrue
      $cfg.MenuItems.Count | Should -Be 0
    }

    It 'akzeptiert benutzerdefinierte Werte' {
      $cfg = New-TrayIconConfig -ToolTip 'Mein Tool' -IconState 'Running'
      $cfg.ToolTip | Should -Be 'Mein Tool'
      $cfg.IconState | Should -Be 'Running'
    }
  }

  Describe 'Add-TrayMenuItem' {
    It 'fuegt Menuepunkt hinzu' {
      $cfg = New-TrayIconConfig
      $cfg = Add-TrayMenuItem -Config $cfg -Label 'Test' -Key 'test'
      $cfg.MenuItems.Count | Should -Be 1
      $cfg.MenuItems[0].Label | Should -Be 'Test'
      $cfg.MenuItems[0].Key | Should -Be 'test'
    }

    It 'fuegt Trennstrich hinzu' {
      $cfg = New-TrayIconConfig
      $cfg = Add-TrayMenuItem -Config $cfg -Label '-' -Key 'sep' -IsSeparator $true
      $cfg.MenuItems[0].IsSeparator | Should -BeTrue
    }
  }

  Describe 'Get-DefaultTrayMenu' {
    It 'erstellt Standard-Kontextmenu mit korrekten Items' {
      $cfg = Get-DefaultTrayMenu
      $cfg.MenuItems.Count | Should -BeGreaterOrEqual 5
      $cfg.MenuItems[0].Key | Should -Be 'show'
      $cfg.MenuItems[-1].Key | Should -Be 'exit'
    }
  }

  Describe 'Set-TrayIconState' {
    It 'aendert den Icon-Status' {
      $cfg = New-TrayIconConfig -IconState 'Idle'
      $cfg = Set-TrayIconState -Config $cfg -IconState 'Running' -ToolTip 'Laeuft...'
      $cfg.IconState | Should -Be 'Running'
      $cfg.ToolTip | Should -Be 'Laeuft...'
    }
  }

  Describe 'New-TrayBalloonNotification' {
    It 'erstellt Balloon-Notification' {
      $balloon = New-TrayBalloonNotification -Title 'Fertig' -Text 'DryRun abgeschlossen'
      $balloon.Title | Should -Be 'Fertig'
      $balloon.Text | Should -Be 'DryRun abgeschlossen'
      $balloon.Icon | Should -Be 'Info'
      $balloon.TimeoutMs | Should -Be 5000
    }

    It 'akzeptiert benutzerdefinierte Werte' {
      $balloon = New-TrayBalloonNotification -Title 'Fehler' -Text 'Fehler aufgetreten' -Icon 'Error' -TimeoutMs 10000
      $balloon.Icon | Should -Be 'Error'
      $balloon.TimeoutMs | Should -Be 10000
    }
  }
}
