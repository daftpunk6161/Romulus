#  FTP/SFTP SOURCE (LF-16)
#  ROM-Roots koennen FTP/SFTP-Pfade sein (Download → Process → Upload-Back).

function New-FtpSourceConfig {
  <#
  .SYNOPSIS
    Erstellt eine FTP/SFTP-Quell-Konfiguration.
  #>
  param(
    [Parameter(Mandatory)][string]$HostName,
    [int]$Port = 21,
    [Parameter(Mandatory)][string]$Username,
    [ValidateSet('FTP','SFTP')]
    [string]$Protocol = 'FTP',
    [string]$RemotePath = '/',
    [string]$LocalCachePath = ''
  )

  return @{
    Host           = $HostName
    Port           = $Port
    Username       = $Username
    Protocol       = $Protocol
    RemotePath     = $RemotePath
    LocalCachePath = $LocalCachePath
    Connected      = $false
    LastSync       = $null
  }
}

function Test-FtpUri {
  <#
  .SYNOPSIS
    Prueft ob ein String eine gueltige FTP/SFTP-URI ist.
  #>
  param(
    [Parameter(Mandatory)][string]$Uri
  )

  $isFtp  = $Uri -match '^ftp://[^/]+(/|$)'
  $isSftp = $Uri -match '^sftp://[^/]+(/|$)'

  return @{
    Valid    = ($isFtp -or $isSftp)
    Protocol = if ($isSftp) { 'SFTP' } elseif ($isFtp) { 'FTP' } else { 'Unknown' }
    Host     = if ($Uri -match '://([\w.\-]+)') { $Matches[1] } else { '' }
    Path     = if ($Uri -match '://[^/]+(/.*)$') { $Matches[1] } else { '/' }
  }
}

function New-FtpSyncPlan {
  <#
  .SYNOPSIS
    Erstellt einen Synchronisationsplan fuer FTP-Quellen.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Config,
    [Parameter(Mandatory)][array]$RemoteFiles,
    [array]$LocalFiles = @()
  )

  $toDownload = [System.Collections.Generic.List[hashtable]]::new()
  $toUpload   = [System.Collections.Generic.List[hashtable]]::new()
  $unchanged  = [System.Collections.Generic.List[string]]::new()

  $localIndex = @{}
  foreach ($lf in $LocalFiles) {
    $localIndex[$lf.Name] = $lf
  }

  foreach ($rf in $RemoteFiles) {
    if ($localIndex.ContainsKey($rf.Name)) {
      $local = $localIndex[$rf.Name]
      if ($rf.ContainsKey('Size') -and $local.ContainsKey('Size') -and $rf.Size -ne $local.Size) {
        $toDownload.Add(@{ Name = $rf.Name; Size = $rf.Size; Reason = 'SizeMismatch' })
      } else {
        $unchanged.Add($rf.Name)
      }
    } else {
      $toDownload.Add(@{ Name = $rf.Name; Size = if ($rf.ContainsKey('Size')) { $rf.Size } else { 0 }; Reason = 'New' })
    }
  }

  return @{
    Download   = ,$toDownload.ToArray()
    Upload     = ,$toUpload.ToArray()
    Unchanged  = ,$unchanged.ToArray()
    TotalRemote = $RemoteFiles.Count
    TotalLocal  = $LocalFiles.Count
  }
}

function Get-FtpTransferProgress {
  <#
  .SYNOPSIS
    Berechnet Fortschritt eines FTP-Transfers.
  #>
  param(
    [long]$BytesTransferred,
    [long]$TotalBytes,
    [int]$FilesCompleted = 0,
    [int]$TotalFiles = 0
  )

  $percent = if ($TotalBytes -gt 0) { [math]::Round(($BytesTransferred / $TotalBytes) * 100, 1) } else { 0 }
  $filePercent = if ($TotalFiles -gt 0) { [math]::Round(($FilesCompleted / $TotalFiles) * 100, 1) } else { 0 }

  return @{
    BytesTransferred = $BytesTransferred
    TotalBytes       = $TotalBytes
    BytePercent      = $percent
    FilesCompleted   = $FilesCompleted
    TotalFiles       = $TotalFiles
    FilePercent      = $filePercent
  }
}
