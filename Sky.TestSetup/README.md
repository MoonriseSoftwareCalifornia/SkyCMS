# Sky.TestSetup — Developer README

Purpose
- Provides pre-flight connectivity tests for external dependencies used by the wider test suite.
- Helps verify environment readiness (local/dev/CI) before running larger test projects.

What this project contains
- `ConnectivityTests.cs`: MSTest tests that validate connectivity to:
  - Databases: Cosmos DB, SQL Server, MySQL, SQLite
  - Storage: Azure Blob Storage, Amazon S3, Cloudflare R2
- `MSTestSettings.cs`: enables method-level parallelization for this test assembly.

How configuration is resolved
`ConnectivityTests` builds configuration in this order:
1. `appsettings.json` (optional)
2. User Secrets (optional)
3. Environment variables (highest priority)

This allows CI variables to override local settings cleanly.

Connection string names used by tests
- `CosmosDB`
- `SqlServer`
- `MySQL`
- `SQLite`
- `AzureBlobStorageConnectionString` (or fallback: `StorageConnectionString`)
- `AmazonS3ConnectionString`
- `CloudflareR2ConnectionString`

How to run
Run only connectivity checks:
```powershell
dotnet test Sky.TestSetup\Sky.TestSetup.csproj
```

Run from solution but filter to connectivity category:
```powershell
dotnet test SkyCMS.sln --filter "TestCategory=Connectivity"
```

Run only storage connectivity tests:
```powershell
dotnet test Sky.TestSetup\Sky.TestSetup.csproj --filter "TestCategory=Storage"
```

Run only database connectivity tests:
```powershell
dotnet test Sky.TestSetup\Sky.TestSetup.csproj --filter "TestCategory=Database"
```

Expected behavior
- If a required connection string is missing, tests return `Inconclusive` (skipped) rather than failing.
- If a dependency is configured but unreachable/invalid, tests fail with provider-specific diagnostics.
- Some tests create/check minimal resources as part of connectivity validation (for example, ensuring databases exist).

When to use this project
- Before running full test suites when changing environment/secrets.
- In CI as a fast dependency smoke-check stage.
- During troubleshooting to isolate infra/config issues from functional test failures.
