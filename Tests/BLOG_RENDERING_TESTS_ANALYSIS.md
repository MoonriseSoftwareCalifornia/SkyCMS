# Blog Rendering Service Tests - Status Report

## Issue Summary

We have **two sets of blog rendering tests** in the namespace `Sky.Tests.Services.BlogPublishing`:

### 1. **BlogRenderingServiceTests.cs** (EXISTING - OLD SERVICE)
- **Purpose**: Tests the OLD **template-based** `IBlogRenderingService` 
- **Status**: Tests are well-structured with helpers
- **Location**: `Tests/BlogRenderingServiceTests.cs`
- **Test Count**: ~13 test methods

### 2. **BlogStreamRenderingServiceTests.cs** (NEW - NEW SERVICE)
- **Purpose**: Tests the NEW **JSON + client-side** `IBlogStreamRenderingService`
- **Status**: Tests created and should be passing
- **Location**: `Tests/Services/BlogPublishing/BlogStreamRenderingServiceTests.cs`
- **Test Count**: 14 test methods

## Potential Issues

### In BlogRenderingServiceTests.cs:
1. **Assert.Contains() usage** - This is checking if a substring exists in HTML output
   - This works for simple cases but can be fragile with whitespace/formatting
   - Tests may fail if HTML formatting changes slightly

2. **Template seeding** - Tests manually seed templates with minimal HTML
   - Templates must exist and have correct CSS selectors
   - If selectors don't match, HtmlAgilityPack won't find elements

3. **Database state** - Tests use `SkyCmsTestBase` which provides in-memory DB
   - Should be isolated per test
   - Helper methods properly call `await Db.SaveChangesAsync()`

## Recommended Actions

### Option A: Debug Current Tests
```powershell
cd D:\source\SkyCMS
dotnet test Tests/Sky.Tests.csproj --filter "BlogRenderingServiceTests" -v detailed
```

This will show:
- Which specific test is failing
- The actual vs expected output
- More detailed error messages

### Option B: Verify New Tests
```powershell
dotnet test Tests/Sky.Tests.csproj --filter "BlogStreamRenderingServiceTests" -v detailed
```

## Architecture Clarity

**Keep Both Services:**
- ? OLD `IBlogRenderingService` - Template-based (uses HtmlAgilityPack)
  - Used by: Legacy code, tests
  - Location: `Sky.Editor.Services.BlogPublishing`
  - Status: Backward compatible

- ? NEW `IBlogStreamRenderingService` - JSON + Client-side (no external deps)
  - Used by: New `PublishingService`
  - Location: `Cosmos.Common.Services.BlogPublishing`
  - Status: Modern, testable

## Next Steps

1. **Run the failing tests** to see specific errors
2. **Fix assertions** if they're too strict (whitespace issues)
3. **Verify templates** are seeded with correct structure
4. **Confirm new service tests** are passing

Would you like me to:
1. Run the tests with verbose output to see the failures?
2. Update the test assertions to be more flexible?
3. Add logging/debugging to understand why templates aren't matching?
