$filesToCheck = @(
  'C:\Code\Sortierung\dev\modules\ApplicationServices.ps1',
  'C:\Code\Sortierung\dev\modules\OperationAdapters.ps1',
  'C:\Code\Sortierung\dev\modules\WpfSlice.AdvancedFeatures.ps1',
  'C:\Code\Sortierung\Invoke-RomCleanup.ps1',
  'C:\Code\Sortierung\dev\modules\Report.ps1'
)
foreach ($f in $filesToCheck) {
  $t = $null; $e = $null
  [System.Management.Automation.Language.Parser]::ParseFile($f, [ref]$t, [ref]$e) | Out-Null
  if ($e.Count -gt 0) {
    Write-Output "FAIL: $f"
    foreach ($err in $e) { Write-Output "  L$($err.Extent.StartLineNumber): $($err.Message)" }
  } else {
    Write-Output "OK: $f"
  }
}
