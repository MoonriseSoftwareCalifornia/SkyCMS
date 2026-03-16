# Cosmos.Common.Tests Infrastructure

This document describes the test infrastructure for the Cosmos.Common project, designed to achieve 90%+ code coverage with parallel test execution.

## Overview

The test infrastructure provides:
- **Thread-safe pooled database contexts** for parallel execution (6+ workers)
- **Test data builders** for creating unique test entities without conflicts
- **Base test class** with common utilities and helpers
- **Parallel execution enabled** via `MSTestSettings.cs`

## Architecture

### 1. Test Data Isolation Strategy

Each test creates **unique entities** using:
- `Guid.NewGuid()` for IDs
- Random numbers for article numbers
- Unique timestamps
- Unique strings for URLs/paths/names

This ensures parallel tests never conflict, even when using shared pooled contexts.

### 2. Context Management

#### Pooled Contexts (Recommended)
```csharp
var context = GetPooledContext();
```
- **Fast**: Pre-created during `[ClassInitialize]`
- **Isolated**: Each context has its own in-memory database
- **Thread-safe**: Round-robin distribution with locking
- **Use for**: 90% of your tests

#### Isolated Contexts (Special Cases)
```csharp
var context = GetIsolatedContext();
```
- **Slower**: Created on-demand
- **Fully isolated**: Brand new context and database
- **Use for**: Tests that modify context configuration or need guaranteed isolation

### 3. Key Components

#### `CommonTestsBase`
Abstract base class for all test classes. Provides:
- Context pool management
- Random number generation (thread-safe)
- Common test utilities

#### `TestDbContextPool`
Manages a pool of 10 pre-created `ApplicationDbContext` instances:
- Thread-safe access via locking
- Round-robin distribution
- Automatic cleanup via `IDisposable`

#### `TestDataBuilder`
Static factory methods for creating test entities:
- `CreateArticle()` - Unique articles
- `CreatePublishedPage()` - Unique published pages
- `CreateLayout()` - Unique layouts
- `CreateCatalogEntry()` - Unique catalog entries
- `CreateSetting()` - Unique settings
- `CreateContact()` - Unique contacts
- `CreateUser()` - Unique identity users
- `CreateTemplate()` - Unique templates
- `CreateArticleLog()` - Unique article logs
- `CreateAuthorInfo()` - Unique author info

## Usage Patterns

### Basic Test Class Setup

```csharp
[TestClass]
public class MyFeatureTests : CommonTestsBase
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        InitializeContextPool(context);
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        CleanupContextPool();
    }

    [TestMethod]
    public async Task MyTest_Scenario_ExpectedResult()
    {
        // Arrange
        var context = GetPooledContext();
        var article = TestDataBuilder.CreateArticle("My Test Article");
        context.Articles.Add(article);
        await context.SaveChangesAsync();

        // Act
        var result = await context.Articles.FindAsync(article.Id);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("My Test Article", result.Title);
    }
}
```

### Testing Query Handlers

```csharp
[TestMethod]
public async Task Handle_ValidId_ReturnsArticle()
{
    // Arrange
    var context = GetPooledContext();
    var article = TestDataBuilder.CreateArticle();
    context.Articles.Add(article);
    await context.SaveChangesAsync();

    var handler = new GetArticleByIdQueryHandler(context);
    var query = new GetArticleByIdQuery(article.Id);

    // Act
    var result = await handler.Handle(query, CancellationToken.None);

    // Assert
    Assert.IsNotNull(result);
    Assert.AreEqual(article.Id, result.Id);
}
```

### Testing Services with Mocks

```csharp
[TestMethod]
public async Task AddContact_ValidData_Succeeds()
{
    // Arrange
    var context = GetPooledContext();
    var mockEmailService = new Mock<IEmailConfigurationService>();
    var service = new ContactManagementService(context, mockEmailService.Object);
    
    var contact = TestDataBuilder.CreateContact();

    // Act
    await service.AddContactAsync(contact);

    // Assert
    var saved = await context.Contacts.FirstOrDefaultAsync(c => c.Id == contact.Id);
    Assert.IsNotNull(saved);
    Assert.AreEqual(contact.EmailAddress, saved.EmailAddress);
}
```

### Using Random Values

```csharp
[TestMethod]
public void MyTest_WithRandomData()
{
    // Get random values (thread-safe)
    var randomNumber = GetRandomInt(100, 1000);
    var randomBool = GetRandomBool();
    var randomDate = GetRandomPastDateTime();
    
    // Use in test...
}
```

## Best Practices

### ✅ DO

1. **Inherit from `CommonTestsBase`** for all test classes
2. **Call `InitializeContextPool()` in `[ClassInitialize]`**
3. **Call `CleanupContextPool()` in `[ClassCleanup]`**
4. **Use `TestDataBuilder`** to create test entities
5. **Use `GetPooledContext()`** for most tests
6. **Use unique identifiers** (GUIDs, random numbers) for all test data
7. **Follow naming convention**: `MethodName_Scenario_ExpectedResult`
8. **Write focused tests**: One behavior per test method
9. **Use Arrange-Act-Assert** pattern

### ❌ DON'T

1. **Don't share test data** between test methods
2. **Don't use hard-coded IDs** (use GUIDs instead)
3. **Don't dispose pooled contexts** (they're managed by the pool)
4. **Don't test multiple behaviors** in one test method
5. **Don't create overlapping tests** (check coverage to identify)
6. **Don't forget to save changes** (`await context.SaveChangesAsync()`)

## Parallel Execution

Tests run in parallel at the **method level** via:

```csharp
// MSTestSettings.cs
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
```

**Thread Safety Guarantees:**
- ✅ Each test gets a different context from the pool (round-robin)
- ✅ Each context has its own in-memory database
- ✅ Each test creates unique entities (no ID conflicts)
- ✅ Random number generator is thread-local
- ✅ Context pool access is locked

**Expected Performance:**
- With 10 pooled contexts and 6+ workers, tests execute efficiently in parallel
- Context creation overhead is minimized (happens once per class)

## Coverage Goals

**Target: 90%+ code coverage**

### Priority Tiers

**Tier 1 - Critical (Start Here):**
- Query/Command Handlers
- Services (business logic)
- Utilities
- Extensions

**Tier 2 - Important:**
- Models with logic
- Data entities with custom methods
- Mediator implementation

**Tier 3 - Lower Priority:**
- Simple POCOs
- Configuration classes
- Constants (typically excluded)

### Measuring Coverage

```powershell
# Run tests with coverage
dotnet test ./Common.Tests/Cosmos.Common.Tests.csproj --collect:"XPlat Code Coverage"

# Generate HTML report
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html

# Open report
start coveragereport/index.html
```

## Examples

See the following test classes for complete examples:
- `ExtensionsTests.cs` - Simple unit tests without database
- `ArticleExtensionsTests.cs` - Database tests using pooled contexts and builders

## Troubleshooting

### "Context pool not initialized" Error
**Cause**: Forgot to call `InitializeContextPool()` in `[ClassInitialize]`

**Fix**:
```csharp
[ClassInitialize]
public static void ClassInitialize(TestContext context)
{
    InitializeContextPool(context);
}
```

### Tests Failing Intermittently in Parallel
**Cause**: Test data conflicts (same IDs, numbers, or paths)

**Fix**: Use `TestDataBuilder` and ensure all test data is unique:
```csharp
// ❌ BAD - hard-coded ID
var article = new Article { Id = "test123" };

// ✅ GOOD - unique ID
var article = TestDataBuilder.CreateArticle();
```

### Slow Test Execution
**Cause**: Creating too many isolated contexts instead of using pooled ones

**Fix**: Use `GetPooledContext()` instead of `GetIsolatedContext()` for most tests

## Contributing

When adding new tests:
1. Follow the existing patterns in sample test files
2. Ensure tests are parallel-safe (unique data)
3. Add helper methods to `TestDataBuilder` for new entity types
4. Document any special test scenarios
5. Run coverage reports to verify no overlapping tests
