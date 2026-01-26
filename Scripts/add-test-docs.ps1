# Adds concise XML <summary> comments above undocumented MSTest [TestMethod] attributes
# Backup created as <file>.bak

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projectRoot = Resolve-Path "$root\.." | Select-Object -ExpandProperty Path
$searchRoot = "$projectRoot"
Write-Host "Scanning test files under: $searchRoot"

$files = Get-ChildItem -Path $searchRoot -Recurse -Include *Tests*.cs -File | Sort-Object FullName
$modified = 0
$changedFiles = @()

foreach ($file in $files) {
    $text = Get-Content -LiteralPath $file.FullName -Encoding UTF8 -Raw
    $lines = $text -split "\r?\n"
    $newLines = [System.Collections.Generic.List[string]]::new()
    $i = 0
    $fileChanged = $false
    while ($i -lt $lines.Length) {
        $line = $lines[$i]
        if ($line.TrimStart().StartsWith('[TestMethod]')) {
            # Check up to 5 lines above for XML doc
            $hasDoc = $false
            for ($j = 1; $j -le 5; $j++) {
                if ($i - $j -ge 0) {
                    if ($lines[$i - $j].TrimStart().StartsWith('///')) { $hasDoc = $true; break }
                    # stop scanning if we hit a non-empty non-comment and non-attribute line
                    if (-not ($lines[$i - $j].TrimStart().StartsWith('[') -or $lines[$i - $j].TrimStart().StartsWith('///') -or [string]::IsNullOrWhiteSpace($lines[$i - $j]))) { break }
                }
            }
            if (-not $hasDoc) {
                # find method signature line (look ahead up to 5 lines)
                $methodLine = ''
                for ($k = $i + 1; $k -lt [Math]::Min($lines.Length, $i + 8); $k++) {
                    if ($lines[$k].Trim() -ne '') {
                        $methodLine = $lines[$k].Trim()
                        break
                    }
                }
                $methodName = 'TestMethod'
                if ($methodLine -ne '') {
                    $m = [regex]::Match($methodLine, 'public\s+(?:async\s+)?(?:[\w<>]+)\s+([\w_]+)\s*\(')
                    if ($m.Success) { $methodName = $m.Groups[1].Value }
                }

                # Determine indentation from attribute line
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
        # backup
        Copy-Item -LiteralPath $file.FullName -Destination ($file.FullName + '.bak') -Force
        $newText = [string]::Join("`r`n", $newLines)
        Set-Content -LiteralPath $file.FullName -Value $newText -Encoding UTF8
        $modified++
        $changedFiles += $file.FullName
        Write-Host "Patched: $($file.FullName)"
    }
}

Write-Host "Done. Files modified: $modified"
if ($modified -gt 0) { Write-Host "Backups created with .bak suffix." }

# Output summary for CI
$newText = "FilesModified:`n" + ($changedFiles -join "`n")
Set-Content -LiteralPath "$projectRoot\scripts\add-test-docs-output.txt" -Value $newText -Encoding UTF8
