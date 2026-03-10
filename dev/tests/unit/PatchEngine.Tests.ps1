BeforeAll {
  . "$PSScriptRoot/../../modules/PatchEngine.ps1"
}

Describe 'LF-05: PatchEngine' {
  Describe 'Test-PatchFormat' {
    It 'erkennt IPS-Format' {
      $tmp = Join-Path $TestDrive 'test.ips'
      # IPS magic: PATCH (0x50 0x41 0x54 0x43 0x48)
      $bytes = [byte[]](0x50, 0x41, 0x54, 0x43, 0x48, 0x00, 0x00, 0x10)
      [System.IO.File]::WriteAllBytes($tmp, $bytes)
      Test-PatchFormat -PatchPath $tmp | Should -Be 'IPS'
    }

    It 'erkennt BPS-Format' {
      $tmp = Join-Path $TestDrive 'test.bps'
      $bytes = [byte[]](0x42, 0x50, 0x53, 0x31, 0x00, 0x00)
      [System.IO.File]::WriteAllBytes($tmp, $bytes)
      Test-PatchFormat -PatchPath $tmp | Should -Be 'BPS'
    }

    It 'gibt Unknown bei unbekanntem Format' {
      $tmp = Join-Path $TestDrive 'test.bin'
      [System.IO.File]::WriteAllBytes($tmp, [byte[]](0x00, 0x01, 0x02, 0x03, 0x04))
      Test-PatchFormat -PatchPath $tmp | Should -Be 'Unknown'
    }
  }

  Describe 'Read-IpsPatch' {
    It 'liest Normal-Record' {
      # PATCH + offset(000010) + size(0003) + data(AA BB CC) + EOF
      $patch = [byte[]](0x50,0x41,0x54,0x43,0x48, 0x00,0x00,0x10, 0x00,0x03, 0xAA,0xBB,0xCC, 0x45,0x4F,0x46)
      $records = Read-IpsPatch -PatchBytes $patch
      $records.Count | Should -Be 1
      $records[0].Offset | Should -Be 16
      $records[0].Size | Should -Be 3
      $records[0].Type | Should -Be 'Normal'
    }

    It 'liest RLE-Record' {
      # PATCH + offset(000020) + size(0000) + rleSize(0005) + rleByte(FF) + EOF
      $patch = [byte[]](0x50,0x41,0x54,0x43,0x48, 0x00,0x00,0x20, 0x00,0x00, 0x00,0x05,0xFF, 0x45,0x4F,0x46)
      $records = Read-IpsPatch -PatchBytes $patch
      $records.Count | Should -Be 1
      $records[0].Type | Should -Be 'RLE'
      $records[0].RleByte | Should -Be 0xFF
    }
  }

  Describe 'Invoke-IpsPatch' {
    It 'wendet Normal-Patch an' {
      $rom = [byte[]](0x00, 0x00, 0x00, 0x00, 0x00)
      $records = @(
        @{ Offset = 1; Size = 2; Data = [byte[]](0xAA, 0xBB); Type = 'Normal' }
      )
      $result = Invoke-IpsPatch -RomData $rom -Records $records
      $result[1] | Should -Be 0xAA
      $result[2] | Should -Be 0xBB
    }

    It 'wendet RLE-Patch an' {
      $rom = [byte[]](0x00, 0x00, 0x00, 0x00, 0x00)
      $records = @(
        @{ Offset = 0; Size = 3; RleByte = [byte]0xFF; Type = 'RLE' }
      )
      $result = Invoke-IpsPatch -RomData $rom -Records $records
      $result[0] | Should -Be 0xFF
      $result[1] | Should -Be 0xFF
      $result[2] | Should -Be 0xFF
      $result[3] | Should -Be 0x00
    }
  }

  Describe 'New-PatchOperation' {
    It 'erstellt Operation mit Format-Erkennung' {
      $tmp = Join-Path $TestDrive 'patch.ips'
      [System.IO.File]::WriteAllBytes($tmp, [byte[]](0x50,0x41,0x54,0x43,0x48,0x45,0x4F,0x46))
      $op = New-PatchOperation -RomPath 'rom.sfc' -PatchPath $tmp
      $op.Format | Should -Be 'IPS'
      $op.Status | Should -Be 'Pending'
    }
  }

  Describe 'Get-PatchSummary' {
    It 'zaehlt nach Format' {
      $ops = @(
        @{ Format = 'IPS'; Status = 'Pending' }
        @{ Format = 'IPS'; Status = 'Completed' }
        @{ Format = 'BPS'; Status = 'Pending' }
      )
      $sum = Get-PatchSummary -Operations $ops
      $sum.Total | Should -Be 3
      $sum.ByFormat['IPS'] | Should -Be 2
      $sum.Pending | Should -Be 2
    }
  }
}
