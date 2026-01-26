Param(
    [int]$BatchSize = 20,
    [int]$BatchNumber = 1
)

# Batch runner that applies `add-test-docs` logic to a subset of test files.
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projectRoot = Resolve-Path "$scriptRoot\.." | Select-Object -ExpandProperty Path
$searchRoot = "$projectRoot"
Write-Host "Batch runner: scanning test files under: $searchRoot"

$allFiles = Get-ChildItem -Path $searchRoot -Recurse -Include *Tests*.cs -File | Sort-Object FullName
$total = $allFiles.Count
if ($total -eq 0) { Write-Host 'No test files found.'; exit 0 }

$totalBatches = [math]::Ceiling($total / $BatchSize)
if ($BatchNumber -lt 1 -or $BatchNumber -gt $totalBatches) {
    Write-Host "BatchNumber must be between 1 and $totalBatches"; exit 1
}

$start = ($BatchNumber - 1) * $BatchSize
$end = [math]::Min($start + $BatchSize - 1, $total - 1)
$selected = $allFiles[$start..$end]

Write-Host ("Processing batch {0} of {1}: files {2}-{3} of {4}" -f $BatchNumber, $totalBatches, ($start + 1), ($end + 1), $total)

$modified = 0
$changedFiles = @()

foreach ($file in $selected) {
    $text = Get-Content -LiteralPath $file.FullName -Encoding UTF8 -Raw
    $lines = $text -split "\r?\n"
    $newLines = [System.Collections.Generic.List[string]]::new()
    $i = 0
    $fileChanged = $false
    while ($i -lt $lines.Length) {
        $line = $lines[$i]
        if ($line.TrimStart().StartsWith('[TestMethod]')) {
            $hasDoc = $false
            for ($j = 1; $j -le 5; $j++) {
                if ($i - $j -ge 0) {
                    if ($lines[$i - $j].TrimStart().StartsWith('///')) { $hasDoc = $true; break }
                    if (-not ($lines[$i - $j].TrimStart().StartsWith('[') -or $lines[$i - $j].TrimStart().StartsWith('///') -or [string]::IsNullOrWhiteSpace($lines[$i - $j]))) { break }
                }
            }
            if (-not $hasDoc) {
                $methodLine = ''
                for ($k = $i + 1; $k -lt [Math]::Min($lines.Length, $i + 8); $k++) {
                    if ($lines[$k].Trim() -ne '') { $methodLine = $lines[$k].Trim(); break }
                }
                $methodName = 'TestMethod'
                if ($methodLine -ne '') {
                    $m = [regex]::Match($methodLine, 'public\s+(?:async\s+)?(?:[\w<>]+)\s+([\w_]+)\s*\(')
                    if ($m.Success) { $methodName = $m.Groups[1].Value }
                }

                $indentMatch = [regex]::Match($line, '^(\s*)')
                $indent = $indentMatch.Groups[1].Value

                $summaryLines = @(
                    "$indent/// <summary>",
                    "$indent/// Tests that $methodName.",
                    "$indent/// </summary>"
                )

                foreach ($s in $summaryLines) { $newLines.Add($s) }
                $fileChanged = $true
            }
        }
        $newLines.Add($line)
        $i++
    }

    if ($fileChanged) {
        Copy-Item -LiteralPath $file.FullName -Destination ($file.FullName + '.bak') -Force
        $newText = [string]::Join("`r`n", $newLines)
        Set-Content -LiteralPath $file.FullName -Value $newText -Encoding UTF8
        $modified++
        $changedFiles += $file.FullName
        Write-Host "Patched: $($file.FullName)"
    }
}

Write-Host "Batch complete. Files modified in this batch: $modified"
if ($modified -gt 0) { Write-Host "Backups created with .bak suffix." }

$out = "BatchNumber: $BatchNumber`nBatchSize: $BatchSize`nFilesInBatch: $($selected.Count)`nFilesModified: $modified`n" + ($changedFiles -join "`n")
Set-Content -LiteralPath "$projectRoot\scripts\batch-add-test-docs-output-batch$BatchNumber.txt" -Value $out -Encoding UTF8

exit 0
