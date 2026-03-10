# ================================================================
#  CONVERT QUEUE – Pausierbare Konvertierungs-Queue (MF-08)
#  Dependencies: Convert.ps1
# ================================================================

function New-ConvertQueue {
  <#
  .SYNOPSIS
    Erstellt eine neue Konvertierungs-Queue.
  .PARAMETER Items
    Array von Queue-Items: @{ SourcePath; TargetPath; Format }.
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Items
  )

  $queueItems = @()
  foreach ($item in $Items) {
    $queueItems += @{
      Source = $item.SourcePath
      Target = $item.TargetPath
      Format = $item.Format
      Status = 'Pending'
      Error  = $null
    }
  }

  return @{
    QueueId      = [guid]::NewGuid().ToString('N').Substring(0, 8)
    Created      = (Get-Date).ToString('o')
    Status       = 'Ready'
    CurrentIndex = 0
    TotalItems   = $queueItems.Count
    Items        = $queueItems
  }
}

function Save-ConvertQueue {
  <#
  .SYNOPSIS
    Persistiert die Queue als JSON-Datei.
  .PARAMETER Queue
    Queue-Objekt.
  .PARAMETER Path
    Speicherpfad.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Queue,
    [Parameter(Mandatory)][string]$Path
  )

  $dir = [System.IO.Path]::GetDirectoryName($Path)
  if ($dir -and -not (Test-Path $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
  }

  $Queue | ConvertTo-Json -Depth 5 | Set-Content -Path $Path -Encoding UTF8
  return @{ Status = 'Saved'; Path = $Path }
}

function Import-ConvertQueue {
  <#
  .SYNOPSIS
    Laedt eine persistierte Queue aus einer JSON-Datei.
  .PARAMETER Path
    Pfad zur Queue-Datei.
  #>
  param(
    [Parameter(Mandatory)][string]$Path
  )

  if (-not (Test-Path $Path)) {
    return @{ Status = 'Error'; Reason = 'FileNotFound' }
  }

  $json = Get-Content -Path $Path -Raw -Encoding UTF8
  $queue = $json | ConvertFrom-Json

  # ConvertFrom-Json liefert PSCustomObject, in Hashtable konvertieren
  $result = @{
    QueueId      = $queue.QueueId
    Created      = $queue.Created
    Status       = $queue.Status
    CurrentIndex = [int]$queue.CurrentIndex
    TotalItems   = [int]$queue.TotalItems
    Items        = @()
  }

  foreach ($item in $queue.Items) {
    $result.Items += @{
      Source = $item.Source
      Target = $item.Target
      Format = $item.Format
      Status = $item.Status
      Error  = $item.Error
    }
  }

  return $result
}

function Suspend-ConvertQueue {
  <#
  .SYNOPSIS
    Pausiert die Queue am aktuellen Index.
  .PARAMETER Queue
    Queue-Objekt (wird in-place modifiziert).
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Queue
  )

  $Queue.Status = 'Paused'
  return $Queue
}

function Resume-ConvertQueue {
  <#
  .SYNOPSIS
    Setzt eine pausierte Queue fort.
  .PARAMETER Queue
    Queue-Objekt.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Queue
  )

  if ($Queue.Status -ne 'Paused') {
    return @{ Status = 'Error'; Reason = 'QueueNotPaused'; CurrentStatus = $Queue.Status }
  }

  $Queue.Status = 'Running'
  return $Queue
}

function Step-ConvertQueue {
  <#
  .SYNOPSIS
    Verarbeitet das naechste Item in der Queue.
  .PARAMETER Queue
    Queue-Objekt.
  .PARAMETER Mode
    DryRun oder Move.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Queue,
    [ValidateSet('DryRun','Move')][string]$Mode = 'DryRun'
  )

  if ($Queue.Status -eq 'Paused') {
    return @{ Status = 'Paused'; CurrentIndex = $Queue.CurrentIndex }
  }

  $idx = $Queue.CurrentIndex
  if ($idx -ge $Queue.Items.Count) {
    $Queue.Status = 'Completed'
    return @{ Status = 'Completed'; ProcessedCount = $Queue.Items.Count }
  }

  $item = $Queue.Items[$idx]

  if ($Mode -eq 'DryRun') {
    $item.Status = 'DryRun'
  } else {
    $item.Status = 'Completed'
  }

  $Queue.CurrentIndex = $idx + 1
  $Queue.Status = 'Running'

  if ($Queue.CurrentIndex -ge $Queue.Items.Count) {
    $Queue.Status = 'Completed'
  }

  return @{
    Status       = $item.Status
    CurrentIndex = $Queue.CurrentIndex
    Item         = $item
  }
}

function Get-ConvertQueueProgress {
  <#
  .SYNOPSIS
    Gibt den Fortschritt der Queue als Prozent und Zaehler zurueck.
  .PARAMETER Queue
    Queue-Objekt.
  #>
  param(
    [Parameter(Mandatory)][hashtable]$Queue
  )

  $total = $Queue.Items.Count
  if ($total -eq 0) {
    return @{ Percent = 100; Completed = 0; Total = 0; Failed = 0; Pending = 0 }
  }

  $completed = @($Queue.Items | Where-Object { $_.Status -eq 'Completed' -or $_.Status -eq 'DryRun' }).Count
  $failed = @($Queue.Items | Where-Object { $_.Status -eq 'Failed' }).Count
  $pending = $total - $completed - $failed

  return @{
    Percent   = [math]::Round(($completed / $total) * 100, 1)
    Completed = $completed
    Total     = $total
    Failed    = $failed
    Pending   = $pending
  }
}
