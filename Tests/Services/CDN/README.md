# CDN Integration Tests

This directory contains integration tests for CDN providers (Azure CDN, Cloudflare, and Sucuri).

## Configuration

These tests require actual CDN provider credentials to run. Without credentials configured, the tests will be marked as **Inconclusive** rather than failing.

### Setting Up User Secrets

The tests use .NET user secrets with the User Secrets ID: `c44b0fbc-a20c-4a15-8e5b-1a9eb09e6ac1`

To configure credentials, use the `dotnet user-secrets` command from the `Tests` project directory:

```bash
cd Tests

# For Azure CDN
dotnet user-secrets set "CdnIntegrationTests:Azure:SubscriptionId" "your-subscription-id"
dotnet user-secrets set "CdnIntegrationTests:Azure:ResourceGroup" "your-resource-group"
dotnet user-secrets set "CdnIntegrationTests:Azure:ProfileName" "your-profile-name"
dotnet user-secrets set "CdnIntegrationTests:Azure:EndpointName" "your-endpoint-name"

# For Cloudflare CDN
dotnet user-secrets set "CdnIntegrationTests:Cloudflare:ApiToken" "your-api-token"
dotnet user-secrets set "CdnIntegrationTests:Cloudflare:ZoneId" "your-zone-id"
dotnet user-secrets set "CdnIntegrationTests:Cloudflare:TestDomain" "your-test-domain.com"

# For Sucuri CDN
dotnet user-secrets set "CdnIntegrationTests:Sucuri:ApiKey" "your-api-key"
dotnet user-secrets set "CdnIntegrationTests:Sucuri:ApiSecret" "your-api-secret"
```

### Alternative: Manual secrets.json File

You can also manually create or edit the secrets file at:

**Windows**: `%APPDATA%\Microsoft\UserSecrets\c44b0fbc-a20c-4a15-8e5b-1a9eb09e6ac1\secrets.json`

**Linux/macOS**: `~/.microsoft/usersecrets/c44b0fbc-a20c-4a15-8e5b-1a9eb09e6ac1/secrets.json`

Example `secrets.json` content:

```json
{
  "CdnIntegrationTests": {
    "Azure": {
      "SubscriptionId": "your-subscription-id",
      "ResourceGroup": "your-resource-group",
      "ProfileName": "your-profile-name",
      "EndpointName": "your-endpoint-name"
    },
    "Cloudflare": {
      "ApiToken": "your-api-token",
      "ZoneId": "your-zone-id",
      "TestDomain": "example.com"
    },
    "Sucuri": {
      "ApiKey": "your-api-key",
      "ApiSecret": "your-api-secret"
    }
  }
}
```

## Running the Tests

Once configured, you can run the CDN integration tests:

```bash
# Run all CDN integration tests
dotnet test --filter "TestCategory=CDN"

# Run only Cloudflare tests
dotnet test --filter "FullyQualifiedName~Cloudflare"

# Run the diagnostic test to check configuration
dotnet test --filter "FullyQualifiedName~Diagnostic_ListConfiguredProviders"
```

## Notes

- You don't need to configure all providers - you only need at least one to run the diagnostic test
- Individual provider tests will be marked as **Inconclusive** if their specific credentials are not configured
- The tests are marked with `[DoNotParallelize]` to avoid conflicts when purging CDN caches
- Test credentials should have appropriate permissions for CDN cache purge operations
