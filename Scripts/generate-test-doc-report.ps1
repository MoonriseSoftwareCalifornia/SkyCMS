$projectRoot = 'd:\\source\\SkyCMS'
$target = Join-Path $projectRoot 'Tests\TEST_DOC_REPORT.md'
$files = Get-ChildItem -Path $projectRoot -Recurse -Include *Tests*.cs -File | Sort-Object FullName
$rows = @()
foreach ($file in $files) {
    $lines = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
    if (-not $lines) { continue }
    $arr = $lines -split "\r?\n"
    for ($i=0; $i -lt $arr.Length; $i++) {
        if ($arr[$i].TrimStart().StartsWith('[TestMethod]')) {
            $hasDoc = $false
            for ($j=1; $j -le 12; $j++) {
                if ($i - $j -ge 0) {
                    $t = $arr[$i-$j].TrimStart()
                    if ($t.StartsWith('///')) { $hasDoc = $true; break }
                    if (-not ($t.StartsWith('[') -or $t -eq '')) { break }
                }
            }
            # find method signature
            $methodLine = ''
            for ($k=$i+1; $k -lt [Math]::Min($arr.Length, $i+12); $k++) {
                if ($arr[$k].Trim() -ne '') { $methodLine = $arr[$k].Trim(); break }
            }
            $methodName = $methodLine
            if ($methodLine -match 'public\s+(?:async\s+)?(?:[\w<>]+)\s+([\w_]+)\s*\(') { $methodName = $Matches[1] }
            $rows += [PSCustomObject]@{
                File = $file.FullName.Replace($projectRoot+'\\','')
                Line = $i+1
                Method = $methodName
                HasXml = $hasDoc
            }
        }
    }
}
# write markdown
"# Test Documentation Report`n`n" | Out-File -FilePath $target -Encoding utf8
"Generated: $(Get-Date -Format o)`n`n" | Out-File -FilePath $target -Encoding utf8 -Append
"| File | Line | Method | HasXmlSummary |`n|---|---:|---|---:|" | Out-File -FilePath $target -Encoding utf8 -Append
foreach ($r in $rows) {
    "| $($r.File) | $($r.Line) | $($r.Method) | $($r.HasXml) |" | Out-File -FilePath $target -Encoding utf8 -Append
}
Write-Host "Wrote report with $($rows.Count) test method entries to $target"