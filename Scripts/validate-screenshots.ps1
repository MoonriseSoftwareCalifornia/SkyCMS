<#
.SYNOPSIS
    Validates and updates screenshot index status automatically.

.DESCRIPTION
    Scans Docs/images/screenshots for .webp files and updates INDEX.md with status emoji.
    Validates filenames follow kebab-case convention.
    Reports missing screenshots and file size metrics.

.NOTES
    Run before committing: .\Scripts\validate-screenshots.ps1
    Runs automatically on GitHub Actions and pre-commit hooks.

.EXAMPLE
    .\Scripts\validate-screenshots.ps1
#>

param(
    [switch]$Verbose = $false,
    [switch]$FixBrokenLinks = $false
)

$ErrorActionPreference = "Stop"

# Colors for output
$Colors = @{
    Success = "Green"
    Warning = "Yellow"
    Error = "Red"
    Info = "Cyan"
}

function Write-Colored {
    param([string]$Message, [string]$Color = "White")
    Write-Host $Message -ForegroundColor $Color
}

function Get-RepositoryRoot {
    $current = Get-Location
    while ($current.Path -ne $current.Root) {
        if (Test-Path (Join-Path $current.Path ".git")) {
            return $current.Path
        }
        $current = $current.Parent
    }
    throw "Could not find repository root (.git directory)"
}

$repoRoot = Get-RepositoryRoot
$screenshotDir = Join-Path $repoRoot "Docs\images\screenshots"
$indexFile = Join-Path $screenshotDir "INDEX.md"
$docsDir = Join-Path $repoRoot "Docs"

# Expected screenshots (from INDEX.md table)
$expectedScreenshots = @(
    "setup-wizard-welcome.webp",
    "setup-wizard-storage.webp",
    "setup-wizard-admin.webp",
    "setup-wizard-publisher.webp",
    "setup-wizard-email.webp",
    "setup-wizard-cdn.webp",
    "setup-wizard-review.webp",
    "live-editor-dashboard.webp",
    "live-editor-editing-toolbar.webp",
    "live-editor-insert-image.webp",
    "publishing-modes-overview.webp",
    "publishing-mode-selector.webp",
    "publishing-staged-approval.webp",
    "page-scheduling-review-dialog.webp",
    "page-scheduling-calendar.webp",
    "page-scheduler-dashboard.webp",
    "storage-upload-test.webp",
    "settings-email-test.webp",
    "settings-cdn-test.webp",
    "multi-tenant-architecture.webp"
)

Write-Colored "=== SkyCMS Screenshot Validation ===" $Colors.Info
Write-Host ""

# Step 1: Scan for existing .webp files
Write-Colored "Step 1: Scanning for existing screenshots..." $Colors.Info
$existingFiles = @{}
$fileIssues = @()

if (Test-Path $screenshotDir) {
    Get-ChildItem -Path $screenshotDir -Filter "*.webp" | ForEach-Object {
        $filename = $_.Name
        $filepath = $_.FullName
        $sizeKB = [math]::Round($_.Length / 1KB, 2)
        
        $existingFiles[$filename] = @{
            Path = $filepath
            SizeKB = $sizeKB
            Valid = $true
        }
        
        # Check naming convention (kebab-case)
        if ($filename -notmatch '^[a-z0-9]+(-[a-z0-9]+)*\.webp$') {
            $fileIssues += "Invalid filename (not kebab-case): $filename"
            $existingFiles[$filename].Valid = $false
        }
        
        # Check file size (warn if > 200 KB)
        if ($sizeKB -gt 200) {
            $fileIssues += "File size warning ($($sizeKB) KB exceeds 150 KB target): $filename"
        }
        
        if ($Verbose) {
            Write-Host "  ✅ Found: $filename ($($sizeKB) KB)"
        }
    }
}

Write-Colored "  Found $($existingFiles.Count) screenshot file(s)" $Colors.Success

# Step 2: Compare against expected list
Write-Colored "Step 2: Checking for missing screenshots..." $Colors.Info
$missing = @()
$present = @()

foreach ($filename in $expectedScreenshots) {
    if ($existingFiles.ContainsKey($filename)) {
        $present += $filename
    } else {
        $missing += $filename
    }
}

Write-Colored "  Present: $($present.Count) / $($expectedScreenshots.Count)" $Colors.Success
Write-Colored "  Missing: $($missing.Count) / $($expectedScreenshots.Count)" $(if ($missing.Count -gt 0) { $Colors.Warning } else { $Colors.Success })

# Step 3: Update INDEX.md with status emoji
Write-Colored "Step 3: Updating INDEX.md..." $Colors.Info

$indexContent = Get-Content $indexFile -Raw
$originalContent = $indexContent

foreach ($filename in $expectedScreenshots) {
    if ($existingFiles.ContainsKey($filename)) {
        # File exists → Update to ✅
        $pattern = "^\| \`$($filename)\` \| ⬜ \|"
        $replacement = "| \`$filename\` | ✅ |"
        $indexContent = $indexContent -replace $pattern, $replacement
    } else {
        # File missing → Ensure ⬜
        $pattern = "^\| \`$($filename)\` \|.*\|"
        $replacement = "| \`$filename\` | ⬜ |"
        # Only update if not already ⬜
        if ($indexContent -match "^\| \`$($filename)\` \| ✅ \|") {
            $indexContent = $indexContent -replace ("^\| \`$($filename)\` \| ✅ \|"), "| \`$filename\` | ⬜ |"
        }
    }
}

# Also update last validated timestamp
$timestamp = (Get-Date -Format "MMM dd, yyyy HH:mm:ss UTC")
$indexContent = $indexContent -replace "Last Validated:.*", "Last Validated:** $timestamp"

if ($indexContent -ne $originalContent) {
    Set-Content $indexFile -Value $indexContent -Encoding UTF8
    Write-Colored "  ✅ INDEX.md updated" $Colors.Success
} else {
    Write-Host "  INDEX.md unchanged"
}

# Step 4: Report status by Phase
Write-Colored "Step 4: Phase breakdown..." $Colors.Info

$phases = @{
    "Setup Wizard" = @("setup-wizard-welcome.webp", "setup-wizard-storage.webp", "setup-wizard-admin.webp", "setup-wizard-publisher.webp", "setup-wizard-email.webp", "setup-wizard-cdn.webp", "setup-wizard-review.webp")
    "Editor & Publishing" = @("live-editor-dashboard.webp", "live-editor-editing-toolbar.webp", "live-editor-insert-image.webp", "publishing-modes-overview.webp", "publishing-mode-selector.webp", "publishing-staged-approval.webp")
    "Remaining" = @("page-scheduling-review-dialog.webp", "page-scheduling-calendar.webp", "page-scheduler-dashboard.webp", "storage-upload-test.webp", "settings-email-test.webp", "settings-cdn-test.webp", "multi-tenant-architecture.webp")
}

foreach ($phaseName in $phases.Keys) {
    $phaseFiles = $phases[$phaseName]
    $phasePresent = $phaseFiles | Where-Object { $existingFiles.ContainsKey($_) }
    $phaseCount = $phasePresent.Count
    $phaseTotal = $phaseFiles.Count
    $percent = [math]::Round(($phaseCount / $phaseTotal) * 100, 0)
    
    $statusColor = if ($phaseCount -eq $phaseTotal) { $Colors.Success } elseif ($phaseCount -gt 0) { $Colors.Warning } else { $Colors.Error }
    Write-Colored "  [$phaseName] $phaseCount/$phaseTotal ($percent%)" $statusColor
}

# Step 5: Summary
Write-Host ""
Write-Colored "=== Summary ===" $Colors.Info

if ($missing.Count -eq 0) {
    Write-Colored "✅ All screenshots present! Phase 2 complete." $Colors.Success
} else {
    Write-Colored "⬜ $($missing.Count) screenshot(s) still needed:" $Colors.Warning
    $missing | ForEach-Object {
        Write-Host "   - $_"
    }
}

if ($fileIssues.Count -gt 0) {
    Write-Host ""
    Write-Colored "⚠️  File issues detected:" $Colors.Warning
    $fileIssues | ForEach-Object {
        Write-Host "   - $_"
    }
}

Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Developers: Follow QUICK_REFERENCE.md to capture missing screenshots"
Write-Host "  2. After capturing: Save to Docs/images/screenshots/{filename}.webp"
Write-Host "  3. Run this script again: .\Scripts\validate-screenshots.ps1"
Write-Host "  4. Commit when validation passes"
Write-Host ""

# Exit code
if ($missing.Count -eq 0 -and $fileIssues.Count -eq 0) {
    Write-Colored "✅ Validation PASSED" $Colors.Success
    exit 0
} elseif ($fileIssues.Count -eq 0) {
    Write-Colored "⏳ Validation OK (awaiting $($missing.Count) screenshot(s))" $Colors.Warning
    exit 0
} else {
    Write-Colored "❌ Validation FAILED (fix issues above)" $Colors.Error
    exit 1
}
