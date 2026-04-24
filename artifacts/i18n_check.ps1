$de = Get-Content c:/Code/Sortierung/data/i18n/de.json -Raw | ConvertFrom-Json -AsHashtable
$en = Get-Content c:/Code/Sortierung/data/i18n/en.json -Raw | ConvertFrom-Json -AsHashtable
$fr = Get-Content c:/Code/Sortierung/data/i18n/fr.json -Raw | ConvertFrom-Json -AsHashtable
"counts: de=$($de.Count) en=$($en.Count) fr=$($fr.Count)"

$missingInEn = @($de.Keys | Where-Object { -not $en.ContainsKey($_) })
$missingInFr = @($de.Keys | Where-Object { -not $fr.ContainsKey($_) })
$extraInEn = @($en.Keys | Where-Object { -not $de.ContainsKey($_) })
$extraInFr = @($fr.Keys | Where-Object { -not $de.ContainsKey($_) })

"missing-in-en=$($missingInEn.Count)"
"missing-in-fr=$($missingInFr.Count)"
"extra-in-en=$($extraInEn.Count)"
"extra-in-fr=$($extraInFr.Count)"

"=== MISSING IN EN (first 25) ==="
$missingInEn | Select-Object -First 25

"=== MISSING IN FR (first 25) ==="
$missingInFr | Select-Object -First 25

"=== Placeholder mismatch check (DE vs EN format args) ==="
foreach ($k in $de.Keys) {
    if ($en.ContainsKey($k)) {
        $deVal = [string]$de[$k]
        $enVal = [string]$en[$k]
        $deArgs = ([regex]::Matches($deVal, '\{(\d+)[^}]*\}') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique) -join ','
        $enArgs = ([regex]::Matches($enVal, '\{(\d+)[^}]*\}') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique) -join ','
        if ($deArgs -ne $enArgs) {
            "PLACEHOLDER MISMATCH: $k  de=[$deArgs]  en=[$enArgs]"
        }
    }
}
