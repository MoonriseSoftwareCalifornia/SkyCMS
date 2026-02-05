# DynamicConfigurationProvider Integration Tests

This directory contains **integration tests** for `DynamicConfigurationProvider` using **real** provider instances with SQLite databases.

## Files in This Directory

### ?? DynamicConfigurationProviderTests.cs
- **Type**: Integration Tests
- **Categories**: `IntegrationTest`, `MultiTenantConfiguration`
- **Purpose**: End-to-end testing with real database
- **Tests**:
  - Connection string retrieval with seeded data
  - Multi-domain tenant scenarios
  - Cache behavior verification
  - Database query correctness

### ?? DynamicConfigurationProviderTenantResolutionTests.cs
- **Type**: Security & Integration Tests
- **Categories**: `SecurityTest`, `IntegrationTest`, `MultiTenantConfiguration`
- **Purpose**: Security-focused tenant resolution testing
- **Tests**:
  - Trusted proxy IP validation (IPv4, IPv6, CIDR ranges)
  - X-Origin-Hostname header priority and validation
  - Malformed hostname injection attack prevention
  - Domain normalization edge cases
  - Header injection security

## Comparison with Unit Tests

| Aspect | Integration Tests (Here) | Unit Tests (Configuration\) |
|--------|--------------------------|----------------------------|
| **Provider** | Real `DynamicConfigurationProvider` | Mocked via `SkyCmsTestBase` |
| **Database** | SQLite with seeded data | No database (mocked) |
| **Speed** | Slower (~100-500ms/test) | Fast (~1-10ms/test) |
| **Purpose** | Verify real behavior | Contract validation |
| **When** | Pre-merge, CI gates | TDD, quick feedback |

## Running Tests

### Run all integration tests:
```bash
dotnet test --filter "TestCategory=IntegrationTest"
```

### Run only security tests:
```bash
dotnet test --filter "TestCategory=SecurityTest"
```

### Run multi-tenant integration tests:
```bash
dotnet test --filter "TestCategory=IntegrationTest&TestCategory=MultiTenantConfiguration"
```

### Run specific test file:
```bash
dotnet test --filter "FullyQualifiedName~DynamicConfigurationProviderTenantResolutionTests"
```

## Test Data Setup

These tests create temporary SQLite databases in `Path.GetTempPath()`:
- `skycms-config-{guid}.db` - Configuration database
- `tenant-{name}.db` - Per-tenant databases

Files are automatically cleaned up after each test.

## Related Tests

For **fast unit tests** with mocks, see: `Tests\Configuration\DynamicConfigurationProviderTests.cs`
