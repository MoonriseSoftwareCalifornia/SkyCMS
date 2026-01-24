---
title: UploadSecretsToGithubRepo Script
description: PowerShell script documentation for automating GitHub repository secrets management
keywords: PowerShell, GitHub, secrets, automation, CI-CD
audience: [developers, devops]
---

# UploadSecretsToGithubRepo.ps1 Documentation

## Overview

`UploadSecretsToGithubRepo.ps1` is a PowerShell script designed to automate the process of copying secrets from a local secrets file and uploading them as GitHub repository secrets. This is useful for synchronizing sensitive configuration values between your local development environment and your GitHub repository, ensuring that your CI/CD pipelines and GitHub Actions have access to the required secrets.

The script now includes built-in verification to confirm that all secrets were successfully uploaded to GitHub.

## How It Works

1. **Reads local secrets** from `secrets.json` (shared by all unit test projects via UserSecretsId)
2. **Flattens nested JSON** using double underscores (`__`) for ASP.NET Core configuration compatibility
3. **Uploads to GitHub** using the GitHub CLI
4. **Creates a local hash manifest** (`secrets-hashes.json`) for later verification
5. **Verifies upload success** by checking that all secrets are present in GitHub
6. **Reports results** with upload summary and verification status

## Prerequisites

- **PowerShell**: The script is written for PowerShell and should be run in a PowerShell terminal.
- **GitHub CLI (`gh`)**: You must have the GitHub CLI installed and authenticated. Download from [GitHub CLI](https://cli.github.com/).
- **Access to the target GitHub repository**: You need appropriate permissions to set secrets in the repository.
- **Local secrets file**: Secrets are read from `%APPDATA%\Microsoft\UserSecrets\c44b0fbc-a20c-4a15-8e5b-1a9eb09e6ac1\secrets.json` (shared by unit test projects).

## Usage

### Basic Usage (Auto-detect repository)

1. **Open PowerShell** in the root directory of the project.

2. **Authenticate with GitHub CLI (if not already authenticated)**

   ```powershell
   gh auth login
   ```

3. **Run the Script**

   ```powershell
   .\UploadSecretsToGithubRepo.ps1
   ```

   The script will:
   - Auto-detect the GitHub repository from `git remote origin`
   - Load secrets from the shared UserSecrets location
   - Upload all secrets to GitHub
   - Verify upload success
   - Create `secrets-hashes.json` for future verification

### Example Output

```powershell
Successfully parsed JSON file

Flattening JSON structure...
  DEBUG: Processing object with prefix '', found 5 properties
    DEBUG: Property 'AdminEmail', Value type: String
    DEBUG: Adding scalar value 'AdminEmail' = 'admin@example.com'
  ...

Found 12 secrets to upload.

Flattened secrets:
  - AdminEmail = admin@example.com
  - ConnectionStrings__CosmosDB = DefaultEndpointsProtocol=https;...
  ...

Setting secret: ADMINEMAIL [OK]
Setting secret: CONNECTIONSTRINGS__COSMOSDB [OK]
...

========================================
Upload Summary:
  Successfully set: 12 secrets
  Failed: 0 secrets
========================================

Verifying secrets in GitHub...

✓ All 12 secrets are present in GitHub

Verification: 12 secrets confirmed uploaded
```

## Secret Structure

Nested secrets are flattened using double underscores. For example:

```json
{
  "ConnectionStrings": {
    "CosmosDB": "value1",
    "SqlServer": "value2"
  },
  "CdnIntegrationTests": {
    "Cloudflare": {
      "ApiToken": "value3",
      "ZoneId": "value4"
    }
  }
}
```

Becomes:

- `CONNECTIONSTRINGS__COSMOSDB`
- `CONNECTIONSTRINGS__SQLSERVER`
- `CDNINTEGRATIONTESTS__CLOUDFLARE__APITOKEN`
- `CDNINTEGRATIONTESTS__CLOUDFLARE__ZONEID`

## Hash Manifest File

After running the upload script, a `secrets-hashes.json` file is created locally containing SHA256 hashes of all uploaded secrets. This file allows you to:

- **Verify that local secrets match what was uploaded** (without re-running upload)
- **Detect if a secret value has changed** since the last upload

The manifest should be added to `.gitignore` as it contains security-sensitive hash information.

## Verification Only (CheckGithubSecrets.ps1)

If you want to verify secrets after uploading without re-uploading:

```powershell
.\CheckGithubSecrets.ps1
```

This script:

- Checks if all expected secrets exist in GitHub
- Compares local secret values with the hash manifest from the last upload
- Reports any missing secrets or values that have changed
- Shows which secrets need to be re-uploaded

## Notes

- The script will **overwrite** existing secrets in the GitHub repository with the same names.
- Empty/null values are **skipped** and not uploaded to GitHub.
- Ensure you do not commit your local `secrets.json` file to version control.
- Add `secrets-hashes.json` to `.gitignore` to prevent committing the hash manifest.
- The script auto-detects the repository from `git remote origin`. Use standard git configuration to control which repository is targeted.

## Troubleshooting

**Issue**: "Error: Not authenticated with GitHub CLI"

- **Solution**: Run `gh auth login` and follow the authentication steps.

**Issue**: "Error: secrets.json not found"

- **Solution**: Ensure you have run `dotnet user-secrets` to initialize secrets, or manually create the secrets file in the UserSecrets directory.

**Issue**: "Missing in GitHub" warnings after upload

- **Solution**: Check that secrets were successfully set by running `gh secret list -R <owner/repo>`. Re-run the upload script if needed.

**Issue**: CheckGithubSecrets shows "VALUE CHANGED"

- **Solution**: Re-run `UploadSecretsToGithubRepo.ps1` to update the secrets in GitHub.

