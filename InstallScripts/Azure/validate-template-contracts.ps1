<#
.SYNOPSIS
    Validates Azure template contracts against current SkyCMS runtime assumptions.

.DESCRIPTION
    Performs fast, source-based checks to detect drift between Azure IaC expectations
    and the current SkyCMS Editor runtime contracts.

.EXAMPLE
    .\validate-template-contracts.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

function Write-Header {
    param([string]$Text)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host " $Text" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Text)
    Write-Host "[PASS] $Text" -ForegroundColor Green
}

function Write-Info {
    param([string]$Text)
    Write-Host "[INFO] $Text" -ForegroundColor Blue
}

function Write-Failure {
    param([string]$Text)
    Write-Host "[FAIL] $Text" -ForegroundColor Red
}

function Assert-Pattern {
    param(
        [string]$FilePath,
        [string]$Pattern,
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $FilePath)) {
        Write-Failure "$Description (file not found: $FilePath)"
        return $false
    }

    $content = Get-Content -LiteralPath $FilePath -Raw
    if ($content -match $Pattern) {
        Write-Success $Description
        return $true
    }

    Write-Failure $Description
    return $false
}

Write-Header "SkyCMS Azure Template Contract Validation"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")

$editorDockerfile = Join-Path $repoRoot "Editor\Dockerfile"
$editorProgram = Join-Path $repoRoot "Editor\Program.cs"
$mainBicep = Join-Path $PSScriptRoot "bicep\main.bicep"
$webAppBicep = Join-Path $PSScriptRoot "bicep\modules\webApp.bicep"

Write-Info "Checking runtime and template alignment..."

$results = @()

# Runtime contract checks (current app)
$results += Assert-Pattern -FilePath $editorDockerfile -Pattern '(?m)^EXPOSE\s+8080\s*$' -Description "Editor Dockerfile exposes port 8080"
$results += Assert-Pattern -FilePath $editorProgram -Pattern 'MapGet\(\s*"/___healthz"' -Description "Editor defines /___healthz endpoint"
$results += Assert-Pattern -FilePath $editorProgram -Pattern 'CONNECTIONSTRING_APP_DB\s*=\s*"ApplicationDbContextConnection"' -Description "Editor expects ApplicationDbContextConnection"
$results += Assert-Pattern -FilePath $editorProgram -Pattern 'CONFIG_ALLOW_SETUP\s*=\s*"CosmosAllowSetup"' -Description "Editor expects CosmosAllowSetup"
$results += Assert-Pattern -FilePath $editorProgram -Pattern 'GetValue<string>\(\s*"CosmosPublisherUrl"\s*\)' -Description "Editor consumes CosmosPublisherUrl"

# IaC contract checks (expected by deployment templates)
$results += Assert-Pattern -FilePath $webAppBicep -Pattern "healthCheckPath\s*:\s*'/___healthz'" -Description "Web App template healthCheckPath matches /___healthz"
$results += Assert-Pattern -FilePath $webAppBicep -Pattern "name:\s*'WEBSITES_PORT'[\s\S]*?value:\s*'8080'" -Description "Web App template sets WEBSITES_PORT=8080"
$results += Assert-Pattern -FilePath $webAppBicep -Pattern "name:\s*'ApplicationDbContextConnection'" -Description "Web App template includes ApplicationDbContextConnection"
$results += Assert-Pattern -FilePath $webAppBicep -Pattern "name:\s*'StorageConnectionString'" -Description "Web App template includes StorageConnectionString"
$results += Assert-Pattern -FilePath $webAppBicep -Pattern "name:\s*'AzureCommunicationConnection'" -Description "Web App template includes AzureCommunicationConnection"

# Scenario toggles and outputs in main template
$results += Assert-Pattern -FilePath $mainBicep -Pattern "param\s+deployPublisher\s+bool" -Description "Main template supports deployPublisher toggle"
$results += Assert-Pattern -FilePath $mainBicep -Pattern "param\s+deployEmail\s+bool" -Description "Main template supports deployEmail toggle"
$results += Assert-Pattern -FilePath $mainBicep -Pattern "param\s+deployAppInsights\s+bool" -Description "Main template supports deployAppInsights toggle"
$results += Assert-Pattern -FilePath $mainBicep -Pattern "output\s+editorUrl\s+string" -Description "Main template outputs editorUrl"
$results += Assert-Pattern -FilePath $mainBicep -Pattern "output\s+editorFqdn\s+string" -Description "Main template outputs editorFqdn"
$results += Assert-Pattern -FilePath $mainBicep -Pattern "output\s+keyVaultName\s+string" -Description "Main template outputs keyVaultName"

Write-Header "Contract Validation Summary"

if ($results -contains $false) {
    Write-Failure "One or more contract checks failed. Review output above."
    exit 1
}

Write-Success "All template contract checks passed."
exit 0
