Param(
    [string]$Repo = $null,
    [string]$JsonPath = "$env:APPDATA\Microsoft\UserSecrets\c44b0fbc-a20c-4a15-8e5b-1a9eb09e6ac1\secrets.json"
)

Write-Host "=== GitHub Secrets Validation ===" -ForegroundColor Cyan

# Auto-detect repository from git remote if not provided
if (-not $Repo) {
    try {
        $gitRemote = git remote get-url origin 2>$null
        if ($gitRemote -match 'github\.com[:/](.+?)(?:\.git)?$') {
            $Repo = $matches[1]
            Write-Host "Detected repository: $Repo" -ForegroundColor DarkGray
        } else {
            throw "Could not parse GitHub repository from git remote"
        }
    } catch {
        Write-Host "Error: Could not detect GitHub repository. Use -Repo <owner>/<repo>." -ForegroundColor Red
        exit 1
    }
}

if (-not (Test-Path $JsonPath)) {
    Write-Host "Error: secrets.json not found at $JsonPath" -ForegroundColor Red
    exit 1
}

# Ensure gh CLI exists and is authenticated
try {
    $null = gh --version 2>&1
} catch {
    Write-Host "Error: GitHub CLI (gh) not found. Install with winget: winget install --id GitHub.cli" -ForegroundColor Red
    exit 1
}

try {
    $null = gh auth status 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Not authenticated" }
} catch {
    Write-Host "Error: Not authenticated with GitHub CLI. Run: gh auth login" -ForegroundColor Red
    exit 1
}

# Strip comments and load JSON
try {
    $jsonContent = Get-Content $JsonPath -Raw
    $jsonContent = $jsonContent -replace '(?m)^\s*//.*$', ''
    $jsonContent = $jsonContent -replace '//.*$', ''
    $json = $jsonContent | ConvertFrom-Json
    Write-Host "Loaded secrets.json" -ForegroundColor DarkGray
} catch {
    Write-Host "Error parsing secrets.json: $_" -ForegroundColor Red
    exit 1
}

function Flatten-Json {
    param(
        [Parameter(Mandatory=$true)]
        $InputObject,
        [string]$Prefix = ""
    )
    $result = @{}

    $properties = $InputObject.PSObject.Properties

    foreach ($property in $properties) {
        $propertyName = $property.Name
        $value = $property.Value

        $key = if ($Prefix) { "${Prefix}__${propertyName}" } else { $propertyName }

        # Match uploader behavior: recurse objects, skip arrays, add scalars
        if ($null -ne $value -and $value -is [PSCustomObject]) {
            $nested = Flatten-Json -InputObject $value -Prefix $key
            foreach ($nestedKey in $nested.Keys) {
                $result[$nestedKey] = $nested[$nestedKey]
            }
        }
        elseif ($null -ne $value -and $value -isnot [System.Array]) {
            $result[$key] = $value.ToString()
        }
    }
    return $result
}

$expected = Flatten-Json -InputObject $json

# Filter out empty values (matching upload script behavior)
$filteredExpected = @{}
foreach ($key in $expected.Keys) {
    $value = $expected[$key]
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        $filteredExpected[$key] = $value
    }
}

$expectedKeys = $filteredExpected.Keys | ForEach-Object { $_.ToUpper() } | Sort-Object

Write-Host "Expected secrets (from $JsonPath): $($expectedKeys.Count) keys" -ForegroundColor Cyan
$expectedKeys | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }

# Fetch current repo secrets
try {
    $remoteSecrets = gh secret list -R $Repo | ForEach-Object { ($_ -split '\s+')[0] }
} catch {
    Write-Host "Error retrieving secrets from GitHub: $_" -ForegroundColor Red
    exit 1
}
$remoteSet = $remoteSecrets | Sort-Object

Write-Host "Remote secrets present: $($remoteSet.Count) keys" -ForegroundColor Cyan
$remoteSet | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }

# Compare
$missing = $expectedKeys | Where-Object { $_ -notin $remoteSet }
$unexpected = $remoteSet | Where-Object { $_ -notin $expectedKeys }

Write-Host ""; Write-Host "Results:" -ForegroundColor Cyan
if ($missing.Count -gt 0) {
    Write-Host "  Missing in GitHub:" -ForegroundColor Yellow
    $missing | ForEach-Object { Write-Host "    - $_" -ForegroundColor Yellow }
} else {
    Write-Host "  All expected keys are present." -ForegroundColor Green
}

if ($unexpected.Count -gt 0) {
    Write-Host "  Extra keys in GitHub (not in secrets.json):" -ForegroundColor Yellow
    $unexpected | ForEach-Object { Write-Host "    - $_" -ForegroundColor Yellow }
}

# Verify checksums using local manifest
$manifestPath = Join-Path $PSScriptRoot "secrets-hashes.json"
if (Test-Path $manifestPath) {
    Write-Host ""; Write-Host "Verifying secret values via local hash manifest..." -ForegroundColor Cyan
    try {
        $savedHashes = Get-Content $manifestPath -Raw | ConvertFrom-Json
        $mismatchCount = 0
        $verifiedCount = 0
        
        foreach ($key in $expectedKeys) {
            if ($key -in $remoteSet) {
                # Compute current local hash
                $localValue = ($filteredExpected.GetEnumerator() | Where-Object { $_.Key.ToUpper() -eq $key }).Value
                
                $sha256 = [System.Security.Cryptography.SHA256]::Create()
                $hashBytes = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($localValue))
                $currentHash = [System.BitConverter]::ToString($hashBytes).Replace("-","")
                $sha256.Dispose()
                
                # Compare with saved hash from last upload
                $savedHash = $savedHashes.PSObject.Properties | Where-Object { $_.Name -eq $key } | Select-Object -ExpandProperty Value
                
                if ($savedHash -eq $currentHash) {
                    Write-Host "  ✓ $key - matches last upload" -ForegroundColor Green
                    $verifiedCount++
                } else {
                    Write-Host "  ✗ $key - VALUE CHANGED since last upload!" -ForegroundColor Red
                    $mismatchCount++
                }
            }
        }
        
        Write-Host ""
        Write-Host "Hash verification: $verifiedCount verified, $mismatchCount changed" -ForegroundColor $(if ($mismatchCount -gt 0) { "Yellow" } else { "Green" })
        if ($mismatchCount -gt 0) {
            Write-Host "  Re-run UploadSecretsToGithubRepo.ps1 to sync changed values" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  Error reading hash manifest: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host ""; Write-Host "Note: No hash manifest found. Run UploadSecretsToGithubRepo.ps1 to create one." -ForegroundColor Yellow
}

Write-Host ""; Write-Host "Validation complete." -ForegroundColor Cyan
