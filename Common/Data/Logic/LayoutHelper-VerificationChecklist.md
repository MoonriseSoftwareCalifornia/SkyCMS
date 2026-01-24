# Manual Verification Checklist - LayoutHelper Implementation

## Quick Verification Steps

Use this checklist to manually verify that all layout queries have been properly updated.

### Step 1: Run Visual Studio Find in Files

**Find what:** `.Layouts.FirstOrDefaultAsync`  
**Look in:** Entire Solution  
**Options:** Enable additional file exclusions

**Expected Results:**
- All results should be either:
  - ? ID-based lookups: `f.Id == id` or `f.Id == itemId`
  - ? Non-default test queries: `!l.IsDefault` (in test files only)
  - ? Name-based test assertions: `l.LayoutName ==` (in test files only)
  - ? **NO RESULTS** with just `l.IsDefault` or `a.IsDefault` in production code

### Step 2: Run Additional Find Patterns

#### Pattern 2A: FirstAsync with IsDefault
**Find what:** `.Layouts.FirstAsync`  
**Expected:** Only in tests with `!l.IsDefault` or no filter (getting any layout)

#### Pattern 2B: Where IsDefault
**Find what:** `Layouts.Where` + `IsDefault`  
**Expected:** Only in helper methods or with proper Published checks

### Step 3: Verify Helper Usage

**Find what:** `LayoutHelper.GetCurrentDefaultLayoutAsync`  
**Look in:** Entire Solution

**Expected Files:**
1. ? Editor\Controllers\BlogController.cs (2 occurrences)
2. ? Editor\Controllers\BaseController.cs (1 occurrence)
3. ? Common\Data\Logic\ArticleLogic.cs (1 occurrence)
4. ? Editor\Services\Publishing\PublishingService.cs (1 occurrence)
5. ? Editor\Services\Templates\TemplateService.cs (1 occurrence)
6. ? Tests\Infrastructure\SkyCmsTestBase.cs (2 occurrences)
7. ? Tests\Areas\Setup\DatabaseInitializationTests.cs (1 occurrence)
8. ? Tests\LayoutsControllerTests.cs (3 occurrences)

**Total Expected:** 12 occurrences

#### Pattern 3B: HasDefaultLayoutAsync
**Find what:** `LayoutHelper.HasDefaultLayoutAsync`

**Expected Files:**
1. ? Editor\Controllers\LayoutsController.cs (1 occurrence)
2. ? Editor\Services\Setup\MultiTenantSetupService.cs (1 occurrence)
3. ? Editor\Services\Setup\SetupService.cs (1 occurrence)

**Total Expected:** 3 occurrences

### Step 4: Build Solution

**Action:** Build the entire solution in Visual Studio

**Expected:** 
- ? No compilation errors
- ? All projects build successfully

### Step 5: Run All Tests

**Action:** Test > Run All Tests

**Expected:**
- ? All tests pass
- ? No test failures related to layout queries
- ? Specific tests to verify:
  - `LayoutsControllerTests.Delete_RejectsDefaultLayoutDeletion`
  - `LayoutsControllerTests.EditCode_CreatesNewVersion_ForDefaultLayout`
  - `LayoutsControllerTests.Publish_UnsetsOtherDefaultLayouts`
  - `DatabaseInitializationTests.CompleteSetup_CreatesDefaultLayout`

### Step 6: Spot Check Key Files

Open and visually inspect these files:

#### 6.1 BlogController.cs
- [ ] Line ~136: Uses `LayoutHelper.GetCurrentDefaultLayoutAsync(db)`
- [ ] Line ~383: Uses `LayoutHelper.GetCurrentDefaultLayoutAsync(db)`

#### 6.2 LayoutsController.cs
- [ ] Line ~882: Uses `LayoutHelper.HasDefaultLayoutAsync(dbContext)`

#### 6.3 PublishingService.cs
- [ ] Line ~504: Uses `LayoutHelper.GetCurrentDefaultLayoutAsync(_db)`

#### 6.4 TemplateService.cs
- [ ] Line ~104: Uses `LayoutHelper.GetCurrentDefaultLayoutAsync(dbContext)`

#### 6.5 BaseController.cs
- [ ] `FetchCurrentLayoutAsync()` method calls `LayoutHelper.GetCurrentDefaultLayoutAsync()`

#### 6.6 ArticleLogic.cs
- [ ] `FetchCurrentLayoutAsync()` method calls `LayoutHelper.GetCurrentDefaultLayoutAsync()`

### Step 7: Verify Helper Implementation

**File:** `Common\Data\Logic\LayoutHelper.cs`

Verify it contains:
- [ ] `GetCurrentDefaultLayoutAsync` method
- [ ] `HasDefaultLayoutAsync` method  
- [ ] `GetLayoutByIdAsync` method
- [ ] Proper XML documentation
- [ ] Filters: `IsDefault && Published <= now`
- [ ] Orders by: `Version`
- [ ] Uses: `LastOrDefaultAsync()`

### Step 8: Check Documentation

Verify these files exist and are complete:
- [ ] `Common\Data\Logic\LayoutHelper.md`
- [ ] `Common\Data\Logic\LayoutHelper-TestUpdates.md`
- [ ] `Common\Data\Logic\LayoutHelper-DeepSearchVerification.md`

## Red Flags to Watch For

? **PROBLEMATIC** - Report immediately if found:
```csharp
// In production code:
await dbContext.Layouts.FirstOrDefaultAsync(l => l.IsDefault)
await db.Layouts.FirstAsync(f => f.IsDefault)
await context.Layouts.FirstOrDefaultAsync(a => a.IsDefault == true)
```

? **OK** - These are fine:
```csharp
// ID-based lookups:
await dbContext.Layouts.FirstOrDefaultAsync(f => f.Id == itemId)

// In tests only - non-default layouts:
await Db.Layouts.FirstOrDefaultAsync(l => !l.IsDefault)

// Helper usage:
await LayoutHelper.GetCurrentDefaultLayoutAsync(dbContext)
await LayoutHelper.HasDefaultLayoutAsync(dbContext)
```

## Final Sign-Off

Once all steps are complete, verify:

- [ ] All Find patterns show expected results
- [ ] Solution builds without errors
- [ ] All unit tests pass
- [ ] Spot checks confirm correct implementation
- [ ] Documentation is complete
- [ ] No red flags found

**Verification Date:** _______________  
**Verified By:** _______________  
**Status:** ? Passed | ? Issues Found

## If Issues Are Found

Document any findings here:

**Issue #1:**
- Location: _______________
- Problem: _______________
- Action Taken: _______________

**Issue #2:**
- Location: _______________
- Problem: _______________
- Action Taken: _______________

---

## Quick Reference: What to Use When

| Scenario | Method to Use |
|----------|---------------|
| Get current published default layout | `LayoutHelper.GetCurrentDefaultLayoutAsync(dbContext)` |
| Check if any default layout exists | `LayoutHelper.HasDefaultLayoutAsync(dbContext)` |
| Get layout by specific ID | `LayoutHelper.GetLayoutByIdAsync(dbContext, id)` or `FindAsync(id)` |
| Get layout by name (tests) | `dbContext.Layouts.FirstOrDefaultAsync(l => l.LayoutName == name)` |
| Get non-default layout (tests) | `dbContext.Layouts.FirstOrDefaultAsync(l => !l.IsDefault)` |

---

**End of Verification Checklist**
