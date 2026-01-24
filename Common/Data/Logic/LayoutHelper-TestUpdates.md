# LayoutHelper Test Updates Summary

## Overview
This document summarizes the updates made to test files to use the new `LayoutHelper` static methods for consistent layout retrieval.

## Files Updated

### 1. ? Tests/Infrastructure/SkyCmsTestBase.cs
**Lines Updated:** 98, 119

**Changes:**
- `EnsureBlogStreamTemplateExistsAsync()` - Updated to use `LayoutHelper.GetCurrentDefaultLayoutAsync()`
- `EnsureBlogPostTemplateExistsAsync()` - Updated to use `LayoutHelper.GetCurrentDefaultLayoutAsync()`

**Before:**
```csharp
var defaultLayout = await Db.Layouts.FirstOrDefaultAsync(f => f.IsDefault);
```

**After:**
```csharp
var defaultLayout = await Cosmos.Common.Data.Logic.LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
```

**Reason:** These methods set up test templates and need the current published default layout.

---

### 2. ? Tests/Areas/Setup/DatabaseInitializationTests.cs
**Line Updated:** 164

**Changes:**
- `CompleteSetup_CreatesDefaultLayout()` test - Updated verification to use `LayoutHelper.GetCurrentDefaultLayoutAsync()`

**Before:**
```csharp
var defaultLayout = await verifyContext.Layouts.FirstOrDefaultAsync(l => l.IsDefault);
```

**After:**
```csharp
var defaultLayout = await Cosmos.Common.Data.Logic.LayoutHelper.GetCurrentDefaultLayoutAsync(verifyContext);
```

**Reason:** Test verification should use the same logic as production code to ensure it tests the right behavior.

---

### 3. ?? Tests/Data/MigrationHelperTests.cs
**Line 457: NO CHANGE NEEDED**

**Code:**
```csharp
var savedLayout = await context.Layouts.FirstOrDefaultAsync(l => l.LayoutName == "Test Layout");
```

**Reason:** This query is searching by `LayoutName` (not `IsDefault`), which is a valid test assertion to verify a specific layout was saved. This is not a "get default layout" query, so no change is needed.

---

### 4. ? Tests/LayoutsControllerTests.cs
**Lines Updated:** 315, 429, 628

**Changes:**

#### Test: `Delete_RejectsDefaultLayoutDeletion()` (Line 315)
**Before:**
```csharp
var defaultLayout = await Db.Layouts.FirstAsync(l => l.IsDefault);
```

**After:**
```csharp
var defaultLayout = await Cosmos.Common.Data.Logic.LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
Assert.IsNotNull(defaultLayout, "Default layout should exist for this test");
```

#### Test: `EditCode_CreatesNewVersion_ForDefaultLayout()` (Line 429)
**Before:**
```csharp
var defaultLayout = await Db.Layouts.FirstAsync(l => l.IsDefault);
```

**After:**
```csharp
var defaultLayout = await Cosmos.Common.Data.Logic.LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
Assert.IsNotNull(defaultLayout, "Default layout should exist for this test");
```

#### Test: `Publish_UnsetsOtherDefaultLayouts()` (Line 628)
**Before:**
```csharp
var oldDefault = await Db.Layouts.FirstAsync(l => l.IsDefault);
```

**After:**
```csharp
var oldDefault = await Cosmos.Common.Data.Logic.LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
Assert.IsNotNull(oldDefault, "Default layout should exist for this test");
```

**Reason:** These tests need the current default layout to test controller behavior. Using the helper ensures consistency with production code.

---

### 5. ?? Tests/LayoutsControllerTests.cs - Non-Default Layout Queries
**Lines NOT Changed:** 398, 447, 495, 523, 564, 836, 864, 889

**Pattern:**
```csharp
var layout = await Db.Layouts.FirstOrDefaultAsync(l => !l.IsDefault);
```

**Reason:** These queries intentionally look for **non-default** layouts for test setup. They have fallback logic to create a layout if none exists. These are appropriate for test scenarios and don't need the helper method since they're specifically testing with non-default layouts.

---

## Summary Statistics

| File | Queries Updated | Queries Unchanged | Reason for Unchanged |
|------|----------------|-------------------|---------------------|
| **SkyCmsTestBase.cs** | 2 | 0 | - |
| **DatabaseInitializationTests.cs** | 1 | 0 | - |
| **MigrationHelperTests.cs** | 0 | 1 | Query by LayoutName (test assertion) |
| **LayoutsControllerTests.cs** | 3 | 7 | Non-default layout queries for test setup |
| **TOTAL** | **6** | **8** | - |

## Benefits of These Changes

1. **Consistency**: Tests now use the same layout retrieval logic as production code
2. **Correctness**: Tests verify behavior against the actual published/active default layout
3. **Maintainability**: If layout query logic changes, tests automatically use the updated logic
4. **Documentation**: Tests demonstrate the correct way to retrieve layouts
5. **Better Assertions**: Added null checks in tests to make failures more informative

## Test Patterns That DON'T Need the Helper

The following patterns are legitimate in tests and should **NOT** use the helper:

### 1. Querying by Specific Properties (Non-Default)
```csharp
// ? OK - Testing with non-default layouts
var layout = await Db.Layouts.FirstOrDefaultAsync(l => !l.IsDefault);
```

### 2. Querying by Name/ID for Verification
```csharp
// ? OK - Verifying a specific layout exists
var savedLayout = await context.Layouts.FirstOrDefaultAsync(l => l.LayoutName == "Test Layout");
```

### 3. Getting Any Layout for Generic Testing
```csharp
// ? OK - Just need any layout, not the default
var layout = await Db.Layouts.FirstAsync();
```

## When TO Use the Helper in Tests

Use `LayoutHelper.GetCurrentDefaultLayoutAsync()` when:

- ? Testing behavior that depends on the "current" default layout
- ? Setting up test data that needs the active default layout
- ? Verifying that the correct default layout was created/used
- ? Mocking production scenarios where code would use `GetCurrentLayoutAsync()`

## Best Practices for Test Code

1. **Use the helper for consistency**: When testing code that uses the helper in production
2. **Add null assertions**: Always verify the layout exists before using it
3. **Be explicit**: Add comments explaining why you're using the helper
4. **Test edge cases**: Consider adding tests for scenarios where no default layout exists

## Next Steps

All critical test files have been updated. The remaining non-default layout queries are intentional and appropriate for their test scenarios.
