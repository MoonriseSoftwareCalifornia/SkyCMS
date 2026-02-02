# Parse TRX test results and generate a comprehensive badge with coverage
param()

# Find the TRX file
$trxFile = Get-ChildItem -Path "./TestResults" -Filter "*.trx" -Recurse | Select-Object -First 1

# Default values
$passed = 0
$failed = 0
$skipped = 0
$coverage = 0

# Parse test results
if ($trxFile) {
    [xml]$xml = Get-Content $trxFile.FullName
    if ($xml.TestRun.ResultSummary.Counters.passed) { $passed = [int]$xml.TestRun.ResultSummary.Counters.passed }
    if ($xml.TestRun.ResultSummary.Counters.failed) { $failed = [int]$xml.TestRun.ResultSummary.Counters.failed }
    if ($xml.TestRun.ResultSummary.Counters.skipped) { $skipped = [int]$xml.TestRun.ResultSummary.Counters.skipped }
    Write-Host "Test Results: $passed passed, $failed failed, $skipped skipped"
} else {
    Write-Host "No TRX file found"
}

# Parse coverage from JSON summary
$coverageJson = Get-ChildItem -Path "./TestResults/CoverageReport" -Filter "Summary.json" -ErrorAction SilentlyContinue
if ($coverageJson) {
  $summary = Get-Content $coverageJson.FullName | ConvertFrom-Json
  if ($summary -and $summary.summary -and $summary.summary.linecoverage) {
    $coverage = [math]::Round($summary.summary.linecoverage, 1)
    Write-Host "Code Coverage: $coverage%"
  } else {
    Write-Host "Coverage summary not found in Summary.json"
  }
} else {
    Write-Host "No coverage report found"
}

# Build a two-part badge: left shows total tests, right shows percent passing with color buckets
# Compute totals and percent passing
$total = $passed + $failed + $skipped
$total = [int]$total
$percent = 0
if ($total -gt 0) {
    $percent = [math]::Round((($passed / $total) * 100), 1)
}

# Choose color thresholds (typical):
#  - Green: >= 95%
#  - Yellow: >= 80% and < 95%
#  - Red: < 80%
$percentColor = if ($percent -ge 95) { '#4c1' } elseif ($percent -ge 80) { '#dfb317' } else { '#e05d44' }

$leftText = "${total} tests"
$rightText = "${percent}% passing"

# SVG dimensions (left/right widths tuned for typical lengths)
$leftWidth = 140
$rightWidth = 140
$totalWidth = $leftWidth + $rightWidth

$badgeSvg = @"
<svg xmlns="http://www.w3.org/2000/svg" width="$totalWidth" height="20">
  <defs>
    <linearGradient id="b" x2="0" y2="100%">
      <stop offset="0" stop-color="#bbb" stop-opacity=".1"/>
      <stop offset="1" stop-opacity=".1"/>
    </linearGradient>
  </defs>
  <clipPath id="a">
    <rect width="$totalWidth" height="20" rx="3" fill="#fff"/>
  </clipPath>
  <g clip-path="url(#a)">
    <path fill="#555" d="M0 0h${leftWidth}v20H0z"/>
    <path fill="$percentColor" d="M${leftWidth} 0h${rightWidth}v20H${leftWidth}z"/>
    <path fill="url(#b)" d="M0 0h${totalWidth}v20H0z"/>
  </g>
  <g fill="#fff" text-anchor="middle" font-family="DejaVu Sans,Verdana,Geneva,sans-serif" font-size="11">
    <text x="${([int]($leftWidth/2))}" y="14" fill="#010101" fill-opacity=".3">${leftText}</text>
    <text x="${([int]($leftWidth/2))}" y="13">${leftText}</text>
    <text x="${([int]($leftWidth + $rightWidth/2))}" y="14" fill="#010101" fill-opacity=".3">${rightText}</text>
    <text x="${([int]($leftWidth + $rightWidth/2))}" y="13">${rightText}</text>
  </g>
</svg>
"@

# Save main test badge
$badgeSvg | Out-File -FilePath "./test-badge.svg" -Encoding UTF8
Write-Host "[OK] Test badge generated: $leftText | $rightText (color: $percentColor)"

# Save metrics to GitHub environment
if ($env:GITHUB_ENV) {
    Add-Content -Path $env:GITHUB_ENV -Value "TEST_PASSED=$passed"
    Add-Content -Path $env:GITHUB_ENV -Value "TEST_FAILED=$failed"
    Add-Content -Path $env:GITHUB_ENV -Value "TEST_SKIPPED=$skipped"
    Add-Content -Path $env:GITHUB_ENV -Value "TEST_COVERAGE=$coverage"
}

# --- Additional outputs: coverage-only badge (SVG) and Shields-compatible JSON ---

# Determine coverage badge color by thresholds
$coverageColor = if ($coverage -ge 90) { "green" } elseif ($coverage -ge 75) { "yellow" } else { "red" }
$coverageMessage = "${coverage}%"

# Create coverage badge SVG
$coverageBadgeSvg = @"
<svg xmlns="http://www.w3.org/2000/svg" width="120" height="20">
  <defs>
    <linearGradient id="b" x2="0" y2="100%">
      <stop offset="0" stop-color="#bbb" stop-opacity=".1"/>
      <stop offset="1" stop-opacity=".1"/>
    </linearGradient>
  </defs>
  <clipPath id="a">
    <rect width="120" height="20" rx="3" fill="#fff"/>
  </clipPath>
  <g clip-path="url(#a)">
    <path fill="#555" d="M0 0h60v20H0z"/>
    <path fill="$coverageColor" d="M60 0h60v20H60z"/>
    <path fill="url(#b)" d="M0 0h120v20H0z"/>
  </g>
  <g fill="#fff" text-anchor="middle" font-family="DejaVu Sans,Verdana,Geneva,sans-serif" font-size="11">
    <text x="30" y="14" fill="#010101" fill-opacity=".3">coverage</text>
    <text x="30" y="13">coverage</text>
    <text x="90" y="14" fill="#010101" fill-opacity=".3">$coverageMessage</text>
    <text x="90" y="13">$coverageMessage</text>
  </g>
</svg>
"@

$coverageBadgeSvg | Out-File -FilePath "./coverage-badge.svg" -Encoding UTF8
Write-Host "[OK] Coverage badge generated (coverage: $coverage%, color: $coverageColor)"

# Create a Shields-compatible JSON endpoint so the README can reference it via img.shields.io/endpoint
$shieldsObj = @{ schemaVersion = 1; label = 'coverage'; message = "$coverage%"; color = $coverageColor }
$shieldsJson = $shieldsObj | ConvertTo-Json -Compress
$shieldsJson | Out-File -FilePath "./coverage-results.json" -Encoding UTF8
Write-Host "[OK] Coverage JSON written to ./coverage-results.json"
