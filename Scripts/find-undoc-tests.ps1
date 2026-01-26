$projectRoot = 'd:\\source\\SkyCMS'
$files = Get-ChildItem -Path $projectRoot -Recurse -Include *Tests*.cs -File | Sort-Object FullName
$results = @()
foreach ($file in $files) {
    $lines = Get-Content -LiteralPath $file.FullName
    for ($i=0; $i -lt $lines.Count; $i++) {
        if ($lines[$i].TrimStart().StartsWith('[TestMethod]')) {
            $hasDoc = $false
            for ($j=1; $j -le 10; $j++) {
                if ($i - $j -ge 0) {
                    if ($lines[$i - $j].TrimStart().StartsWith('///')) { $hasDoc = $true; break }
                    if (-not ($lines[$i - $j].TrimStart().StartsWith('[') -or $lines[$i - $j].TrimStart().StartsWith('///') -or [string]::IsNullOrWhiteSpace($lines[$i - $j]))) { break }
                }
            }
            if (-not $hasDoc) {
                # get method signature next non-empty line
                $methodLine = ''
                for ($k = $i+1; $k -lt [Math]::Min($lines.Count, $i+12); $k++) {
                    if ($lines[$k].Trim() -ne '') { $methodLine = $lines[$k].Trim(); break }
                }
                $methodName = $methodLine
                if ($methodLine -match 'public\s+(?:async\s+)?(?:[\w<>]+)\s+([\w_]+)\s*\(') { $methodName = $Matches[1] }
                $results += "$($file.FullName):$($i+1): $methodName"
            }
        }
    }
}
$results | Set-Content -LiteralPath "$projectRoot\scripts\find-undoc-tests-output.txt" -Encoding UTF8
if ($results.Count -eq 0) { Write-Host 'All test methods appear to have XML summaries.' } else { Write-Host "$($results.Count) undocumented test methods found. See scripts/find-undoc-tests-output.txt." }
