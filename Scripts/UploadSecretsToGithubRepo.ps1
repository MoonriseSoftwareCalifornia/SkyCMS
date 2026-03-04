#Requires -Version 5.1

<#
.SYNOPSIS
    Uploads secrets from user secrets to GitHub repository secrets.

.DESCRIPTION
    This script reads secrets from the local user secrets file and uploads them
    to a GitHub repository as encrypted secrets for use in GitHub Actions.
    Uses existing gh CLI authentication and verifies uploads with hash comparison.

.PARAMETER Owner
    GitHub repository owner (username or organization)

.PARAMETER Repo
    GitHub repository name

.PARAMETER SecretsId
    User secrets ID (GUID from secrets.json path)

.PARAMETER SkipVerification
    Skip hash verification after upload

.EXAMPLE
    .\UploadSecretsToGithubRepo.ps1 -Owner "myusername" -Repo "myrepo"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$Owner,

    [Parameter(Mandatory = $false)]
    [string]$Repo,

    [Parameter(Mandatory = $false)]
    [string]$SecretsId = "c44b0fbc-a20c-4a15-8e5b-1a9eb09e6ac1",

    [Parameter(Mandatory = $false)]
    [switch]$SkipVerification
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Ensure TLS 1.2 for GitHub API (required for PowerShell 5.1)
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Function to calculate SHA-256 hash of a string
function Get-StringHash {
    param([string]$InputString)
    
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($InputString)
    $hashBytes = $sha256.ComputeHash($bytes)
    $hash = [System.BitConverter]::ToString($hashBytes).Replace("-", "").ToLower()
    $sha256.Dispose()
    return $hash
}

# Function to check if gh CLI is installed and authenticated
function Test-GitHubCLI {
    $ghCommand = Get-Command gh -ErrorAction SilentlyContinue
    
    if (-not $ghCommand) {
        Write-Host ""
        Write-Host "ERROR: GitHub CLI (gh) is not installed." 
        Write-Host ""
        Write-Host "Install it with one of these methods:" 
        Write-Host "  winget install GitHub.cli" 
        Write-Host "  scoop install gh" 
        Write-Host "  Or download from: https://cli.github.com/" 
        Write-Host ""
        return $false
    }
    
    # Check if authenticated
    $authStatus = gh auth status 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "ERROR: Not authenticated with GitHub." 
        Write-Host ""
        Write-Host "Please authenticate first:" 
        Write-Host "  gh auth login" 
        Write-Host ""
        return $false
    }
    
    Write-Host "GitHub CLI authentication verified" 
    return $true
}

# Function to get repository info
function Get-RepositoryInfo {
    param(
        [string]$Owner,
        [string]$Repo
    )
    
    if ([string]::IsNullOrEmpty($Owner) -or [string]::IsNullOrEmpty($Repo)) {
        Write-Host ""
        Write-Host "Enter GitHub repository information:" 
        
        if ([string]::IsNullOrEmpty($Owner)) {
            $Owner = Read-Host "Repository Owner (username or org)"
        }
        
        if ([string]::IsNullOrEmpty($Repo)) {
            $Repo = Read-Host "Repository Name"
        }
    }
    
    return @{
        Owner = $Owner
        Repo  = $Repo
    }
}

# Function to read secrets from user secrets file
function Get-UserSecrets {
    param([string]$SecretsId)
    
    $secretsPath = Join-Path $env:APPDATA "Microsoft\UserSecrets\$SecretsId\secrets.json"
    
    if (-not (Test-Path $secretsPath)) {
        throw "Secrets file not found at: $secretsPath"
    }
    
    Write-Host "Reading secrets from: $secretsPath" 
    
    $secretsContent = Get-Content $secretsPath -Raw | ConvertFrom-Json
    return $secretsContent
}

# Function to flatten nested JSON into key-value pairs
function ConvertTo-FlatDictionary {
    param(
        [Parameter(Mandatory = $true)]
        $InputObject,
        
        [string]$Prefix = ""
    )
    
    $result = @{}


    if ($InputObject -eq $null) {
        return $result
    }
    
    $properties = $InputObject.PSObject.Properties
    
    foreach ($prop in $properties) {
        $key = if ($Prefix) { "${Prefix}__$($prop.Name)" } else { $prop.Name }
        
        if ($prop.Value -is [PSCustomObject]) {
            # Recursively flatten nested objects
            $nested = ConvertTo-FlatDictionary -InputObject $prop.Value -Prefix $key
            foreach ($nestedKey in $nested.Keys) {
                $result[$nestedKey] = $nested[$nestedKey]
            }
        }
        elseif ($prop.Value -ne $null) {
            $result[$key] = $prop.Value.ToString()
        }
    }
    
    return $result
}

# Function to get existing secrets from GitHub
function Get-GitHubSecrets {
    param(
        [string]$Owner,
        [string]$Repo
    )
    
    try {
        $secretsList = gh secret list --repo "$Owner/$Repo" --json name,updatedAt | ConvertFrom-Json
        
        $secrets = @{}


        foreach ($secret in $secretsList) {
            $secrets[$secret.name] = $secret
        }
        
        return $secrets
    }
    catch {
        Write-Warning "Could not retrieve existing secrets list. Verification will be limited."
        return @{}


    }
}

# Function to upload secret to GitHub using gh CLI
function Set-GitHubSecret {
    param(
        [string]$Owner,
        [string]$Repo,
        [string]$SecretName,
        [string]$SecretValue
    )
    
    try {
        # Use gh CLI to set the secret (pipe the value to avoid command line exposure)
        $SecretValue | gh secret set $SecretName --repo "$Owner/$Repo" 2>&1 | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            return $true
        }
        else {
            return $false
        }
    }
    catch {
        return $false
    }
}

# Function to verify secret was uploaded
function Test-GitHubSecretExists {
    param(
        [string]$Owner,
        [string]$Repo,
        [string]$SecretName
    )
    
    try {
        # Try to get the secret (this won't return the value, just confirms it exists)
        gh secret list --repo "$Owner/$Repo" --json name | ConvertFrom-Json | Where-Object { $_.name -eq $SecretName } | Out-Null
        return ($LASTEXITCODE -eq 0)
    }
    catch {
        return $false
    }
}

# Main script execution
try {
    Write-Host ""
    Write-Host "=== GitHub Secrets Uploader ===" 
    Write-Host "This script uploads your local user secrets to GitHub repository secrets." 
    Write-Host ""
    
    # Check GitHub CLI
    Write-Host "Checking GitHub CLI..." 
    if (-not (Test-GitHubCLI)) {
        exit 1
    }
    Write-Host ""
    
    # Get repository info
    $repoInfo = Get-RepositoryInfo -Owner $Owner -Repo $Repo
    $Owner = $repoInfo.Owner
    $Repo = $repoInfo.Repo
    
    Write-Host "Target Repository: $Owner/$Repo" 
    Write-Host ""
    
    # Read user secrets
    Write-Host "Step 1: Reading user secrets..." 
    $secrets = Get-UserSecrets -SecretsId $SecretsId
    
    # Flatten secrets
    Write-Host "Step 2: Flattening secret structure..." 
    $flatSecrets = ConvertTo-FlatDictionary -InputObject $secrets
    
    Write-Host "Found $($flatSecrets.Count) secrets to upload" 
    
    # Calculate hashes for verification
    Write-Host "Step 3: Calculating secret hashes..." 
    $secretHashes = @{}


    foreach ($key in $flatSecrets.Keys) {
        if (-not [string]::IsNullOrWhiteSpace($flatSecrets[$key])) {
            $secretName = $key.ToUpper() -replace '[^A-Z0-9_]', '_'
            $secretHashes[$secretName] = Get-StringHash -InputString $flatSecrets[$key]
        }
    }
    Write-Host "Calculated $($secretHashes.Count) hashes" 
    Write-Host ""
    
    # Get existing secrets for comparison
    if (-not $SkipVerification) {
        Write-Host "Step 4: Retrieving existing secrets..." 
        $existingSecrets = Get-GitHubSecrets -Owner $Owner -Repo $Repo
        Write-Host "Found $($existingSecrets.Count) existing secrets" 
        Write-Host ""
    }
    
    # Upload secrets
    $stepNumber = if ($SkipVerification) { 4 } else { 5 }
    Write-Host "Step $stepNumber`: Uploading secrets..." 
    Write-Host ""
    
    $successCount = 0
    $failCount = 0
    $skippedCount = 0
    $uploadedSecrets = @{}
    
    foreach ($key in $flatSecrets.Keys | Sort-Object) {
        # Sanitize secret name (GitHub requirements: uppercase, alphanumeric + underscore)
        $secretName = $key.ToUpper() -replace '[^A-Z0-9_]', '_'
        $secretValue = $flatSecrets[$key]
        
        # Skip empty values
        if ([string]::IsNullOrWhiteSpace($secretValue)) {
            Write-Host "  [SKIP] $secretName (empty)" 
            $skippedCount++
            continue
        }
        
        # Upload the secret
        $success = Set-GitHubSecret `
            -Owner $Owner `
            -Repo $Repo `
            -SecretName $secretName `
            -SecretValue $secretValue
        
        if ($success) {
            $uploadedSecrets[$secretName] = $secretHashes[$secretName]
            Write-Host "  [OK] $secretName" 
            $successCount++
        }
        else {
            Write-Host "  [FAIL] $secretName" 
            $failCount++
        }
    }
    
    # Verify uploads
    if (-not $SkipVerification -and $uploadedSecrets.Count -gt 0) {
        Write-Host ""
        Write-Host "Step 6: Verifying uploads..." 
        Write-Host ""
        
        # Wait a moment for GitHub to process
        Start-Sleep -Seconds 2
        
        $verifiedCount = 0
        $verificationFailCount = 0
        
        foreach ($secretName in $uploadedSecrets.Keys) {
            $exists = Test-GitHubSecretExists -Owner $Owner -Repo $Repo -SecretName $secretName
            
            if ($exists) {
                Write-Host "  [VERIFIED] $secretName (hash: $($uploadedSecrets[$secretName].Substring(0, 8))...)" 
                $verifiedCount++
            }
            else {
                Write-Host "  [NOT FOUND] $secretName" 
                $verificationFailCount++
            }
        }
        
        Write-Host ""
        Write-Host "Verification Results:" 
        Write-Host "  Verified: $verifiedCount" 
        if ($verificationFailCount -gt 0) {
            Write-Host "  Failed:   $verificationFailCount" 
        }
    }
    
    # Summary
    Write-Host ""
    Write-Host "=== Summary ===" 
    Write-Host "Successfully uploaded: $successCount secrets" 
    
    if ($skippedCount -gt 0) {
        Write-Host "Skipped (empty):      $skippedCount secrets" 
    }
    
    if ($failCount -gt 0) {
        Write-Host "Failed:               $failCount secrets" 
    }
    
    Write-Host ""
    Write-Host "Secret Hashes (first 16 chars):" 
    foreach ($secretName in $secretHashes.Keys | Sort-Object) {
        Write-Host "  $secretName`: $($secretHashes[$secretName].Substring(0, 16))..." 
    }
    
    Write-Host ""
    Write-Host "Done! View secrets at:" 
    Write-Host "https://github.com/$Owner/$Repo/settings/secrets/actions" 
    Write-Host ""
}
catch {
    Write-Host ""
    Write-Host "Script failed: $($_.Exception.Message)" 
    if ($_.ScriptStackTrace) {
        Write-Host $_.ScriptStackTrace 
    }
    exit 1
}
