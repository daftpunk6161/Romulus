BeforeAll {
  $root = $PSScriptRoot
  while ($root -and -not (Test-Path (Join-Path $root 'simple_sort.ps1'))) {
    $root = Split-Path -Parent $root
  }
  . (Join-Path $root 'dev\modules\WebhookNotification.ps1')
}

Describe 'WebhookNotification (QW-11)' {

  Context 'Test-WebhookUrlSafe - SSRF-Schutz' {

    It 'akzeptiert HTTPS-URL' {
      $r = Test-WebhookUrlSafe -Url 'https://hooks.slack.com/services/test123'
      $r.Valid | Should -BeTrue
    }

    It 'blockiert HTTP-URL' {
      $r = Test-WebhookUrlSafe -Url 'http://hooks.slack.com/services/test123'
      $r.Valid | Should -BeFalse
      $r.Reason | Should -BeLike '*HTTPS*'
    }

    It 'blockiert localhost' {
      $r = Test-WebhookUrlSafe -Url 'https://localhost/webhook'
      $r.Valid | Should -BeFalse
      $r.Reason | Should -BeLike '*SSRF*'
    }

    It 'blockiert 127.0.0.1' {
      $r = Test-WebhookUrlSafe -Url 'https://127.0.0.1/webhook'
      $r.Valid | Should -BeFalse
      $r.Reason | Should -BeLike '*SSRF*'
    }

    It 'blockiert private IP 10.x' {
      $r = Test-WebhookUrlSafe -Url 'https://10.0.0.1/webhook'
      $r.Valid | Should -BeFalse
      $r.Reason | Should -BeLike '*SSRF*'
    }

    It 'blockiert private IP 192.168.x' {
      $r = Test-WebhookUrlSafe -Url 'https://192.168.1.1/webhook'
      $r.Valid | Should -BeFalse
      $r.Reason | Should -BeLike '*SSRF*'
    }

    It 'blockiert private IP 172.16.x' {
      $r = Test-WebhookUrlSafe -Url 'https://172.16.0.1/webhook'
      $r.Valid | Should -BeFalse
    }

    It 'blockiert leere URL' {
      $r = Test-WebhookUrlSafe -Url ''
      $r.Valid | Should -BeFalse
    }

    It 'blockiert ungueltige URL' {
      $r = Test-WebhookUrlSafe -Url 'not-a-url'
      $r.Valid | Should -BeFalse
    }
  }

  Context 'Invoke-WebhookNotification' {

    It 'blockiert unsichere URL und sendet nicht' {
      $r = Invoke-WebhookNotification -WebhookUrl 'http://evil.com/hook' -Summary @{ Status = 'ok' }
      $r.Success | Should -BeFalse
      $r.Error | Should -BeLike '*HTTPS*'
    }

    It 'blockiert localhost URL' {
      $r = Invoke-WebhookNotification -WebhookUrl 'https://localhost/hook' -Summary @{ Status = 'ok' }
      $r.Success | Should -BeFalse
    }

    It 'gibt Result-Hashtable mit korrekten Feldern zurueck' {
      $r = Invoke-WebhookNotification -WebhookUrl 'http://bad.url' -Summary @{ Status = 'ok' }
      $r.Keys | Should -Contain 'Success'
      $r.Keys | Should -Contain 'StatusCode'
      $r.Keys | Should -Contain 'Error'
      $r.Keys | Should -Contain 'Retries'
    }
  }
}
