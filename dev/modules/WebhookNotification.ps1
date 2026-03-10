# ================================================================
#  WEBHOOK NOTIFICATION – Discord/Slack/Teams Webhook (QW-11)
#  Dependencies: Notifications.ps1
# ================================================================

function Test-WebhookUrlSafe {
  <#
  .SYNOPSIS
    Prueft ob eine Webhook-URL sicher ist (SSRF-Schutz).
    Nur HTTPS erlaubt, keine lokalen/privaten IPs.
  .PARAMETER Url
    Zu pruefende URL.
  #>
  param(
    [Parameter(Mandatory)][AllowEmptyString()][string]$Url
  )

  $result = @{
    Valid   = $false
    Reason = $null
  }

  # Leere URL
  if ([string]::IsNullOrWhiteSpace($Url)) {
    $result.Reason = 'URL ist leer'
    return $result
  }

  # URL parsen
  $uri = $null
  try {
    $uri = [System.Uri]::new($Url)
  } catch {
    $result.Reason = 'Ungueltige URL'
    return $result
  }

  # Nur HTTPS erlaubt
  if ($uri.Scheme -ne 'https') {
    $result.Reason = 'Nur HTTPS-URLs sind erlaubt'
    return $result
  }

  # Private/lokale Adressen blocken (SSRF-Schutz)
  $host_ = $uri.Host.ToLowerInvariant()
  $blockedHosts = @('localhost', '127.0.0.1', '::1', '0.0.0.0', '[::1]')
  if ($host_ -in $blockedHosts) {
    $result.Reason = 'Lokale Adressen sind nicht erlaubt (SSRF-Schutz)'
    return $result
  }

  # Private IP-Ranges pruefen
  try {
    $ip = [System.Net.IPAddress]::Parse($host_)
    $bytes = $ip.GetAddressBytes()
    $isPrivate = $false

    if ($bytes.Count -eq 4) {
      # 10.x.x.x
      if ($bytes[0] -eq 10) { $isPrivate = $true }
      # 172.16.0.0 - 172.31.255.255
      if ($bytes[0] -eq 172 -and $bytes[1] -ge 16 -and $bytes[1] -le 31) { $isPrivate = $true }
      # 192.168.x.x
      if ($bytes[0] -eq 192 -and $bytes[1] -eq 168) { $isPrivate = $true }
      # 169.254.x.x (Link-local)
      if ($bytes[0] -eq 169 -and $bytes[1] -eq 254) { $isPrivate = $true }
    }

    if ($isPrivate) {
      $result.Reason = 'Private IP-Adressen sind nicht erlaubt (SSRF-Schutz)'
      return $result
    }
  } catch {
    # Host ist kein IP — DNS-Name, das ist OK
  }

  $result.Valid = $true
  return $result
}

function Invoke-WebhookNotification {
  <#
  .SYNOPSIS
    Sendet eine Benachrichtigung an einen Webhook-Endpunkt.
  .PARAMETER WebhookUrl
    Webhook-URL (nur HTTPS).
  .PARAMETER Summary
    Zusammenfassung als Hashtable.
  .PARAMETER TimeoutSeconds
    Timeout fuer HTTP-Request.
  .PARAMETER MaxRetries
    Maximale Wiederholungsversuche.
  .PARAMETER Log
    Optionaler Logging-Callback.
  #>
  param(
    [Parameter(Mandatory)][string]$WebhookUrl,
    [Parameter(Mandatory)][hashtable]$Summary,
    [int]$TimeoutSeconds = 10,
    [int]$MaxRetries = 3,
    [scriptblock]$Log
  )

  $result = @{
    Success    = $false
    StatusCode = 0
    Error      = $null
    Retries    = 0
  }

  # URL-Sicherheitspruefung
  $urlCheck = Test-WebhookUrlSafe -Url $WebhookUrl
  if (-not $urlCheck.Valid) {
    $result.Error = $urlCheck.Reason
    if ($Log) { & $Log ("Webhook blockiert: {0}" -f $urlCheck.Reason) }
    return $result
  }

  # Payload aufbauen
  $payload = @{
    text      = "ROM Cleanup abgeschlossen"
    timestamp = (Get-Date).ToUniversalTime().ToString('o')
  }

  foreach ($key in $Summary.Keys) {
    $payload[$key] = $Summary[$key]
  }

  $jsonBody = $payload | ConvertTo-Json -Depth 5 -Compress

  # Mit Retry senden
  for ($attempt = 1; $attempt -le $MaxRetries; $attempt++) {
    $result.Retries = $attempt
    try {
      $response = Invoke-WebRequest -Uri $WebhookUrl -Method POST `
        -Body $jsonBody -ContentType 'application/json; charset=utf-8' `
        -TimeoutSec $TimeoutSeconds -UseBasicParsing -ErrorAction Stop

      $result.StatusCode = $response.StatusCode
      $result.Success = ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300)

      if ($result.Success) {
        if ($Log) { & $Log ("Webhook gesendet: {0} (Status {1})" -f $WebhookUrl, $response.StatusCode) }
        return $result
      }
    } catch {
      $result.Error = $_.Exception.Message
      if ($Log) { & $Log ("Webhook Versuch {0}/{1} fehlgeschlagen: {2}" -f $attempt, $MaxRetries, $_.Exception.Message) }

      # Kurz warten vor Retry (exponentielles Backoff)
      if ($attempt -lt $MaxRetries) {
        Start-Sleep -Milliseconds (1000 * $attempt)
      }
    }
  }

  if ($Log) { & $Log ("Webhook endgueltig fehlgeschlagen nach {0} Versuchen" -f $MaxRetries) }
  return $result
}
