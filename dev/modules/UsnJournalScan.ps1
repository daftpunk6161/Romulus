# ================================================================
#  USN JOURNAL DIFFERENTIAL SCAN (XL-10)
#  NTFS-Journal statt FileSystemWatcher fuer blitzschnelle Aenderungserkennung
# ================================================================

function Test-UsnJournalAvailable {
  <#
  .SYNOPSIS
    Prueft ob USN-Journal auf einem Laufwerk verfuegbar ist.
  .PARAMETER DriveLetter
    Laufwerksbuchstabe (z.B. 'C').
  #>
  param(
    [Parameter(Mandatory)][string]$DriveLetter
  )

  $letter = $DriveLetter.TrimEnd(':').ToUpper()

  # Pruefe NTFS
  $driveInfo = @{
    DriveLetter  = $letter
    IsNTFS       = $false
    IsAvailable  = $false
    Reason       = ''
  }

  try {
    $drive = [System.IO.DriveInfo]::new("${letter}:")
    if ($drive.IsReady) {
      $driveInfo.IsNTFS = ($drive.DriveFormat -eq 'NTFS')
      $driveInfo.IsAvailable = $driveInfo.IsNTFS
      if (-not $driveInfo.IsNTFS) {
        $driveInfo.Reason = "Laufwerk $letter ist $($drive.DriveFormat), nicht NTFS"
      }
    } else {
      $driveInfo.Reason = "Laufwerk $letter ist nicht bereit"
    }
  } catch {
    $driveInfo.Reason = "Laufwerk $letter nicht gefunden"
  }

  return $driveInfo
}

function New-UsnScanState {
  <#
  .SYNOPSIS
    Erstellt einen neuen USN-Scan-State.
  .PARAMETER DriveLetter
    Laufwerksbuchstabe.
  .PARAMETER LastUsn
    Letzter gelesener USN-Wert.
  .PARAMETER ScanRoot
    Wurzelverzeichnis.
  #>
  param(
    [Parameter(Mandatory)][string]$DriveLetter,
    [long]$LastUsn = 0,
    [string]$ScanRoot = ''
  )

  return @{
    DriveLetter = $DriveLetter.TrimEnd(':').ToUpper()
    LastUsn     = $LastUsn
    ScanRoot    = $ScanRoot
    Timestamp   = [datetime]::UtcNow.ToString('o')
    Version     = 1
  }
}

function New-UsnChangeRecord {
  <#
  .SYNOPSIS
    Erstellt einen USN-Change-Record.
  .PARAMETER FileName
    Dateiname.
  .PARAMETER Reason
    Aenderungsgrund (Create/Delete/Rename/Modify).
  .PARAMETER Usn
    USN-Wert.
  .PARAMETER ParentPath
    Pfad des uebergeordneten Ordners.
  #>
  param(
    [Parameter(Mandatory)][string]$FileName,
    [Parameter(Mandatory)][ValidateSet('Create','Delete','Rename','Modify','SecurityChange','Unknown')]
    [string]$Reason,
    [long]$Usn = 0,
    [string]$ParentPath = ''
  )

  return @{
    FileName   = $FileName
    Reason     = $Reason
    Usn        = $Usn
    ParentPath = $ParentPath
    Timestamp  = [datetime]::UtcNow.ToString('o')
  }
}

function Group-UsnChangesByType {
  <#
  .SYNOPSIS
    Gruppiert USN-Changes nach Aenderungstyp.
  .PARAMETER Changes
    Array von USN-Change-Records.
  #>
  param(
    [Parameter(Mandatory)][array]$Changes
  )

  $groups = @{
    Create         = @()
    Delete         = @()
    Rename         = @()
    Modify         = @()
    SecurityChange = @()
    Unknown        = @()
  }

  foreach ($change in $Changes) {
    $reason = $change.Reason
    if ($groups.ContainsKey($reason)) {
      $groups[$reason] += $change
    } else {
      $groups['Unknown'] += $change
    }
  }

  return $groups
}

function Get-UsnDifferentialScanPlan {
  <#
  .SYNOPSIS
    Erstellt einen Plan fuer den differentiellen Scan.
  .PARAMETER State
    Aktueller USN-Scan-State.
  .PARAMETER Changes
    Erkannte Aenderungen.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$State,
    [Parameter(Mandatory)][array]$Changes
  )

  $grouped = Group-UsnChangesByType -Changes $Changes
  $filesToRescan = @()

  foreach ($change in ($grouped['Create'] + $grouped['Modify'] + $grouped['Rename'])) {
    if ($change.FileName -and $change.FileName -notin $filesToRescan) {
      $filesToRescan += $change.FileName
    }
  }

  return @{
    DriveLetter   = $State.DriveLetter
    LastUsn       = $State.LastUsn
    TotalChanges  = $Changes.Count
    FilesToRescan = $filesToRescan
    RescanCount   = $filesToRescan.Count
    DeletedFiles  = @($grouped['Delete'] | ForEach-Object { $_.FileName })
    SkippedCount  = @($grouped['SecurityChange']).Count + @($grouped['Unknown']).Count
  }
}

function Get-UsnScanStatistics {
  <#
  .SYNOPSIS
    Gibt Statistiken ueber den USN-Scan zurueck.
  .PARAMETER State
    USN-Scan-State.
  .PARAMETER Changes
    Erkannte Aenderungen.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$State,
    [Parameter(Mandatory)][array]$Changes
  )

  $grouped = Group-UsnChangesByType -Changes $Changes

  return @{
    DriveLetter    = $State.DriveLetter
    LastUsn        = $State.LastUsn
    TotalChanges   = $Changes.Count
    Creates        = @($grouped['Create']).Count
    Deletes        = @($grouped['Delete']).Count
    Renames        = @($grouped['Rename']).Count
    Modifies       = @($grouped['Modify']).Count
    ScanRoot       = $State.ScanRoot
  }
}
