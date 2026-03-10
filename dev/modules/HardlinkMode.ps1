# ================================================================
#  HARDLINK/SYMLINK MODE – Alternative Ordnerstrukturen (XL-11)
#  Nach Konsole UND Genre ohne Extra-Speicher via Hardlinks
# ================================================================

function Test-HardlinkSupported {
  <#
  .SYNOPSIS
    Prueft ob Hardlinks auf einem Laufwerk unterstuetzt werden.
  .PARAMETER DriveLetter
    Laufwerksbuchstabe.
  #>
  param(
    [Parameter(Mandatory)][string]$DriveLetter
  )

  $letter = $DriveLetter.TrimEnd(':').ToUpper()

  try {
    $drive = [System.IO.DriveInfo]::new("${letter}:")
    $isNTFS = $drive.IsReady -and ($drive.DriveFormat -eq 'NTFS')
    return @{
      DriveLetter   = $letter
      IsSupported   = $isNTFS
      FileSystem    = if ($drive.IsReady) { $drive.DriveFormat } else { 'Unknown' }
      Reason        = if ($isNTFS) { 'NTFS unterstuetzt Hardlinks' } else { 'Nur NTFS unterstuetzt Hardlinks' }
    }
  } catch {
    return @{ DriveLetter = $letter; IsSupported = $false; FileSystem = 'Unknown'; Reason = 'Laufwerk nicht gefunden' }
  }
}

function New-LinkStructureConfig {
  <#
  .SYNOPSIS
    Erstellt eine Konfiguration fuer die Link-Struktur.
  .PARAMETER SourceRoot
    Quellverzeichnis.
  .PARAMETER TargetRoot
    Zielverzeichnis fuer Links.
  .PARAMETER LinkType
    Art der Links.
  .PARAMETER GroupBy
    Gruppierungskriterium.
  #>
  param(
    [Parameter(Mandatory)][string]$SourceRoot,
    [Parameter(Mandatory)][string]$TargetRoot,
    [ValidateSet('Hardlink','Symlink','Junction')][string]$LinkType = 'Hardlink',
    [ValidateSet('Console','Genre','Region','ConsoleAndGenre')][string]$GroupBy = 'ConsoleAndGenre'
  )

  return @{
    SourceRoot = $SourceRoot
    TargetRoot = $TargetRoot
    LinkType   = $LinkType
    GroupBy    = $GroupBy
    Created    = [datetime]::UtcNow.ToString('o')
  }
}

function New-LinkOperation {
  <#
  .SYNOPSIS
    Erstellt eine einzelne Link-Operation.
  .PARAMETER SourceFile
    Pfad zur Quelldatei.
  .PARAMETER TargetPath
    Pfad des zu erstellenden Links.
  .PARAMETER LinkType
    Art des Links.
  #>
  param(
    [Parameter(Mandatory)][string]$SourceFile,
    [Parameter(Mandatory)][string]$TargetPath,
    [ValidateSet('Hardlink','Symlink','Junction')][string]$LinkType = 'Hardlink'
  )

  return @{
    SourceFile = $SourceFile
    TargetPath = $TargetPath
    LinkType   = $LinkType
    Status     = 'Pending'
    Error      = $null
  }
}

function Build-LinkPlan {
  <#
  .SYNOPSIS
    Erstellt einen Link-Plan basierend auf Dateien und Gruppierung.
  .PARAMETER Config
    Link-Struktur-Konfiguration.
  .PARAMETER Files
    Array von Datei-Eintraegen mit GameName, ConsoleKey, Genre.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Config,
    [Parameter(Mandatory)][array]$Files
  )

  $operations = @()

  foreach ($file in $Files) {
    $subPath = switch ($Config.GroupBy) {
      'Console'         { $file.ConsoleKey }
      'Genre'           { if ($file.Genre) { $file.Genre } else { '_Uncategorized' } }
      'Region'          { if ($file.Region) { $file.Region } else { '_Unknown' } }
      'ConsoleAndGenre' {
        $genre = if ($file.Genre) { $file.Genre } else { '_Uncategorized' }
        "$($file.ConsoleKey)\$genre"
      }
    }

    $targetDir = [System.IO.Path]::Combine($Config.TargetRoot, $subPath)
    $targetPath = [System.IO.Path]::Combine($targetDir, $file.FileName)

    $operations += New-LinkOperation -SourceFile $file.FullPath -TargetPath $targetPath -LinkType $Config.LinkType
  }

  return @{
    Config      = $Config
    Operations  = $operations
    TotalLinks  = $operations.Count
    GroupBy     = $Config.GroupBy
    LinkType    = $Config.LinkType
  }
}

function Get-LinkSavingsEstimate {
  <#
  .SYNOPSIS
    Schaetzt die Speichereinsparungen durch Links.
  .PARAMETER Plan
    Link-Plan.
  .PARAMETER Files
    Array von Datei-Eintraegen mit SizeBytes.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Plan,
    [Parameter(Mandatory)][array]$Files
  )

  $totalSize = [long]0
  foreach ($f in $Files) {
    $totalSize += $f.SizeBytes
  }

  $savedBytes = if ($Plan.LinkType -eq 'Hardlink') { $totalSize } else { 0 }

  return @{
    TotalFiles       = @($Files).Count
    TotalSizeBytes   = $totalSize
    SavedBytes       = $savedBytes
    LinkType         = $Plan.LinkType
    SavingsPercent   = if ($totalSize -gt 0 -and $savedBytes -gt 0) { 100.0 } else { 0 }
  }
}

function Get-HardlinkStatistics {
  <#
  .SYNOPSIS
    Gibt Statistiken ueber die Hardlink-Struktur zurueck.
  .PARAMETER Plan
    Link-Plan.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Plan
  )

  $completed = @($Plan.Operations | Where-Object { $_.Status -eq 'Completed' }).Count
  $pending = @($Plan.Operations | Where-Object { $_.Status -eq 'Pending' }).Count
  $failed = @($Plan.Operations | Where-Object { $_.Status -eq 'Failed' }).Count

  return @{
    TotalLinks  = $Plan.TotalLinks
    Completed   = $completed
    Pending     = $pending
    Failed      = $failed
    GroupBy     = $Plan.GroupBy
    LinkType    = $Plan.LinkType
  }
}
