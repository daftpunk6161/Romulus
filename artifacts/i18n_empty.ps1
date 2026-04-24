$de = Get-Content c:/Code/Sortierung/data/i18n/de.json -Raw | ConvertFrom-Json -AsHashtable
$en = Get-Content c:/Code/Sortierung/data/i18n/en.json -Raw | ConvertFrom-Json -AsHashtable
$fr = Get-Content c:/Code/Sortierung/data/i18n/fr.json -Raw | ConvertFrom-Json -AsHashtable
$emptyDe = @($de.GetEnumerator() | Where-Object { -not $_.Value })
$emptyEn = @($en.GetEnumerator() | Where-Object { -not $_.Value })
$emptyFr = @($fr.GetEnumerator() | Where-Object { -not $_.Value })
"empty-de=$($emptyDe.Count) empty-en=$($emptyEn.Count) empty-fr=$($emptyFr.Count)"
$emptyDe | Select-Object -First 5 | ForEach-Object { "DE_EMPTY: $($_.Key)" }
$emptyFr | Select-Object -First 5 | ForEach-Object { "FR_EMPTY: $($_.Key)" }
"=== identical FR == EN (untranslated) sample ==="
$untranslatedFr = @($fr.GetEnumerator() | Where-Object { $en.ContainsKey($_.Key) -and $en[$_.Key] -eq $_.Value })
"untranslated-fr-vs-en=$($untranslatedFr.Count)"
$untranslatedFr | Select-Object -First 10 | ForEach-Object { "FR_EQ_EN: $($_.Key) = '$($_.Value)'" }
