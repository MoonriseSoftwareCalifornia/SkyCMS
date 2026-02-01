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

# Determine badge color based on results
$badgeColor = if ($failed -gt 0) {
    "red"  # Red when tests fail
} else {
    "green"  # Green when all tests pass
}

# Create comprehensive badge text showing Passing/Failing counts
$badgeText = "Passing: $passed Failing: $failed | Cov: $coverage%"

# Create badge SVG
$badgeSvg = @"
<svg xmlns="http://www.w3.org/2000/svg" width="320" height="20">
  <defs>
    <linearGradient id="b" x2="0" y2="100%">
      <stop offset="0" stop-color="#bbb" stop-opacity=".1"/>
      <stop offset="1" stop-opacity=".1"/>
    </linearGradient>
  </defs>
  <clipPath id="a">
    <rect width="280" height="20" rx="3" fill="#fff"/>
  </clipPath>
  <g clip-path="url(#a)">
    <path fill="#555" d="M0 0h90v20H0z"/>
    <path fill="$badgeColor" d="M90 0h230v20H90z"/>
    <path fill="url(#b)" d="M0 0h320v20H0z"/>
  </g>
  <g fill="#fff" text-anchor="middle" font-family="DejaVu Sans,Verdana,Geneva,sans-serif" font-size="11">
    <text x="46" y="15" fill="#010101" fill-opacity=".3">tests</text>
    <text x="45" y="14">tests</text>
    <text x="204" y="15" fill="#010101" fill-opacity=".3">$badgeText</text>
    <text x="203" y="14">$badgeText</text>
  </g>
</svg>
"@

# Save badge
$badgeSvg | Out-File -FilePath "./test-badge.svg" -Encoding UTF8
Write-Host "[OK] Badge generated successfully (color: $badgeColor)"

# Save metrics to GitHub environment
if ($env:GITHUB_ENV) {
    Add-Content -Path $env:GITHUB_ENV -Value "TEST_PASSED=$passed"
    Add-Content -Path $env:GITHUB_ENV -Value "TEST_FAILED=$failed"
    Add-Content -Path $env:GITHUB_ENV -Value "TEST_SKIPPED=$skipped"
    Add-Content -Path $env:GITHUB_ENV -Value "TEST_COVERAGE=$coverage"
}
