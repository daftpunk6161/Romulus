BeforeAll {
  . "$PSScriptRoot/../../modules/PSGalleryModule.ps1"
}

Describe 'PSGalleryModule (XL-04)' {

  Describe 'New-PSGalleryManifest' {
    It 'erstellt Standard-Manifest' {
      $m = New-PSGalleryManifest
      $m.ModuleName | Should -Be 'RomCleanup'
      $m.ModuleVersion | Should -Be '2.0.0'
      $m.PowerShellVersion | Should -Be '5.1'
      @($m.Tags).Count | Should -BeGreaterThan 0
      @($m.FunctionsToExport).Count | Should -BeGreaterThan 0
    }

    It 'akzeptiert benutzerdefinierte Version' {
      $m = New-PSGalleryManifest -Version '3.1.0'
      $m.ModuleVersion | Should -Be '3.1.0'
    }

    It 'enthaelt CompatiblePSEditions' {
      $m = New-PSGalleryManifest
      @($m.CompatiblePSEditions) | Should -Contain 'Desktop'
      @($m.CompatiblePSEditions) | Should -Contain 'Core'
    }
  }

  Describe 'Test-PSGalleryManifestValid' {
    It 'validiert gueltiges Manifest' {
      $m = New-PSGalleryManifest
      $result = Test-PSGalleryManifestValid -Manifest $m
      $result.Valid | Should -Be $true
      $result.Errors.Count | Should -Be 0
    }

    It 'erkennt fehlenden ModuleName' {
      $m = New-PSGalleryManifest
      $m.ModuleName = ''
      $result = Test-PSGalleryManifestValid -Manifest $m
      $result.Valid | Should -Be $false
      $result.Errors | Should -Contain 'ModuleName ist leer'
    }

    It 'erkennt ungueltiges Versionsformat' {
      $m = New-PSGalleryManifest
      $m.ModuleVersion = 'abc'
      $result = Test-PSGalleryManifestValid -Manifest $m
      $result.Valid | Should -Be $false
    }

    It 'erkennt leere Tags' {
      $m = New-PSGalleryManifest
      $m.Tags = @()
      $result = Test-PSGalleryManifestValid -Manifest $m
      $result.Valid | Should -Be $false
    }

    It 'erkennt leere FunctionsToExport' {
      $m = New-PSGalleryManifest
      $m.FunctionsToExport = @()
      $result = Test-PSGalleryManifestValid -Manifest $m
      $result.Valid | Should -Be $false
    }
  }

  Describe 'Compare-ModuleVersions' {
    It 'erkennt Major-Update' {
      $r = Compare-ModuleVersions -Current '1.0.0' -Available '2.0.0'
      $r.UpdateType | Should -Be 'major'
      $r.UpdateAvailable | Should -Be $true
    }

    It 'erkennt Minor-Update' {
      $r = Compare-ModuleVersions -Current '2.0.0' -Available '2.1.0'
      $r.UpdateType | Should -Be 'minor'
    }

    It 'erkennt Patch-Update' {
      $r = Compare-ModuleVersions -Current '2.1.0' -Available '2.1.1'
      $r.UpdateType | Should -Be 'patch'
    }

    It 'erkennt kein Update bei gleicher Version' {
      $r = Compare-ModuleVersions -Current '2.0.0' -Available '2.0.0'
      $r.UpdateType | Should -Be 'none'
      $r.UpdateAvailable | Should -Be $false
    }

    It 'erkennt kein Update bei aelterer Version' {
      $r = Compare-ModuleVersions -Current '3.0.0' -Available '2.0.0'
      $r.UpdateType | Should -Be 'none'
    }
  }

  Describe 'New-PublishConfig' {
    It 'erstellt Publish-Konfiguration' {
      $m = New-PSGalleryManifest
      $p = New-PublishConfig -Manifest $m
      $p.ModuleName | Should -Be 'RomCleanup'
      $p.Repository | Should -Be 'PSGallery'
      $p.PreRelease | Should -Be $false
    }
  }

  Describe 'Get-PSGalleryStatistics' {
    It 'gibt korrekte Statistiken' {
      $m = New-PSGalleryManifest
      $stats = Get-PSGalleryStatistics -Manifest $m
      $stats.ModuleName | Should -Be 'RomCleanup'
      $stats.TagCount | Should -BeGreaterThan 0
      $stats.ExportedFunctions | Should -BeGreaterThan 0
    }
  }
}
