# ?? SAVEARTICLE ELIMINATION - STEP-BY-STEP ACTION PLAN

**Goal**: Completely eliminate the SaveArticle method
**Timeline**: 8-10 hours
**Result**: Clean, obsolete-free codebase
**Status**: Ready to execute

---

## ?? IMMEDIATE ACTION: Refactor SaveArticleHandler

**This is the critical first step** - SaveArticleHandler must not call the old method.

### Step 1: Check Current SaveArticleHandler Implementation

**File**: Editor\Features\Articles\Save\SaveArticleHandler.cs

**Current State**: 
- Handler calls `ArticleEditLogic.SaveArticle()` internally
- This is the blocker preventing deletion

**What to do**:
1. Open the handler
2. Find where it calls the old SaveArticle method
3. Extract that logic into the handler itself
4. Verify tests still pass

### Step 2: Extract Logic from SaveArticle Method

**Source**: ArticleEditLogic.SaveArticle() (lines ~801-889)

**Key Logic to Extract**:
```csharp
1. Load article by ArticleNumber
2. Store old title and URL path
3. Transform content (EnsureEditableMarkers, AngularBase)
4. Update article properties:
   - Content
   - Title  
   - Updated timestamp
   - HeaderJavaScript / FooterJavaScript
   - BannerImage
   - UserId
   - ArticleType
   - Category
   - Published
   - Introduction
5. Handle concurrency (retry on DbUpdateConcurrencyException)
6. Save changes
7. Handle title changes (via titleChangeService)
8. Update catalog (via catalogService)
9. Publish if needed (via publishingService)
10. Return ArticleUpdateResult with CDN results
```

### Step 3: Move Logic Into Handler

**Target**: SaveArticleHandler.HandleAsync()

**Pattern**:
```csharp
public async Task<CommandResult<ArticleUpdateResult>> HandleAsync(SaveArticleCommand command, CancellationToken cancellationToken)
{
    // 1. Validate command
    // 2. Load article
    // 3. Store old state
    // 4. Update article properties
    // 5. Save to database
    // 6. Handle side effects (title changes, catalog, publishing)
    // 7. Return result
}
```

---

## ?? DETAILED TEST FILE CLEANUP

### Tests to Delete (with rationale)

#### 1. SaveArticleBlogEdgeCaseTests.cs
**Scenarios Covered**:
- Blog post creation
- Blog post updates
- Blog-specific logic

**Action**: 
- Extract scenarios that test handler behavior
- Add to SaveArticleHandlerTests as blog-specific tests
- Delete file

#### 2. SaveArticleCatalogTests.cs
**Scenarios Covered**:
- Catalog entry creation
- Catalog updates
- Catalog state verification

**Action**:
- These are service-level tests (CatalogService responsibility)
- Add integration test for catalog update workflow
- Delete file

#### 3. SaveArticleConcurrencyTests.cs
**Scenarios Covered**:
- DbUpdateConcurrencyException handling
- Retry logic
- Concurrent updates

**Action**:
- Add concurrency scenarios to SaveArticleHandlerTests
- Mock DbContext to throw exception
- Verify retry logic works
- Delete file

#### 4. SaveArticleContentTests.cs
**Scenarios Covered**:
- Content sanitization
- EnsureEditableMarkers
- Content preservation

**Action**:
- Merge into SaveArticleHandlerTests as content-specific tests
- Delete file

#### 5. SaveArticleErrorHandlingTests.cs
**Scenarios Covered**:
- Article not found
- Database errors
- Validation failures

**Action**:
- Merge into SaveArticleHandlerTests as error tests
- Delete file

#### 6. SaveArticleJavaScriptBlockTests.cs
**Scenarios Covered**:
- Header JavaScript
- Footer JavaScript
- JavaScript validation

**Action**:
- Merge into SaveArticleHandlerTests
- Delete file

#### 7. SaveArticleRedirectCreationTests.cs
**Scenarios Covered**:
- Redirect creation on title change
- Old URL tracking

**Action**:
- These are TitleChangeService tests
- Keep only integration-level test in ArticleLifecycleIntegrationTests
- Delete file

#### 8. SaveArticleRootPageTests.cs
**Scenarios Covered**:
- Prevent root page deletion
- Root page special handling

**Action**:
- Add to SaveArticleHandlerTests as root-page tests
- Delete file

#### 9. SaveArticleSlugNormalizationTests.cs
**Scenarios Covered**:
- URL slug generation
- Slug normalization

**Action**:
- These are SlugService tests
- Add to SaveArticleHandlerTests for integration
- Delete file

#### 10. SaveArticleTitleChangeTests.cs
**Scenarios Covered**:
- Title change workflow
- URL path updates
- Redirect creation

**Action**:
- These are TitleChangeService tests
- Add integration test
- Delete file

#### 11. SaveArticleTransactionTests.cs
**Scenarios Covered**:
- Transaction handling
- Rollback on error

**Action**:
- EF Core handles transactions automatically
- Integration tests cover this
- Delete file

#### 12. SaveArticleVersionIntegrityTests.cs
**Scenarios Covered**:
- Version number preservation
- Version history

**Action**:
- Integration test level
- Delete file

---

## ? CONSOLIDATED TEST STRUCTURE (AFTER CLEANUP)

### Unit Tests
```
SaveArticleHandlerTests.cs
??? Basic save scenarios
??? Content handling
??? Blog post edge cases
??? Concurrency handling
??? Error handling
??? Root page special cases
??? JavaScript block tests

SaveArticleValidatorTests.cs
??? Command validation
??? Required field validation
??? Business rule validation
```

### Feature Tests
```
SaveArticlePublishingTests.cs
??? Publishing workflow
??? CDN purge triggers
??? Catalog updates
```

### Integration Tests
```
ArticleLifecycleIntegrationTests.cs
??? Full save + publish workflow
??? Title change + redirect workflow
??? Catalog consistency
??? Multi-version integrity
```

---

## ?? EXECUTION CHECKLIST

### Phase 1: Handler Refactoring (Critical Path)
- [ ] Open SaveArticleHandler.cs
- [ ] Review current implementation
- [ ] Open ArticleEditLogic.SaveArticle() for reference
- [ ] Move logic into handler
  - [ ] Article loading
  - [ ] Content transformation
  - [ ] Property updates
  - [ ] Database save with concurrency handling
  - [ ] Title change handling
  - [ ] Catalog update
  - [ ] Publishing
- [ ] Remove call to old SaveArticle method
- [ ] Build project
- [ ] Run SaveArticleHandlerTests
- [ ] All tests pass? ? Continue to Phase 2
- [ ] Tests fail? ? Fix issues, retry

### Phase 2: Test File Cleanup
- [ ] List all test files in Tests\Features\Articles\Save\
- [ ] For each old test file:
  - [ ] Review scenarios
  - [ ] Identify what to merge
  - [ ] Add merged tests to SaveArticleHandlerTests
  - [ ] Delete old file
  - [ ] Delete .bak file
- [ ] Run full test suite
- [ ] All tests pass? ? Continue to Phase 3
- [ ] Tests fail? ? Update tests, retry

### Phase 3: Code Deletion
- [ ] Open ArticleEditLogic.cs
- [ ] Find SaveArticle method (~line 801-889)
- [ ] Delete method body
- [ ] Build project (should have 0 errors)
- [ ] Search for any references to SaveArticle
- [ ] Verify no remaining calls
- [ ] Run all tests
- [ ] All tests pass? ? DONE!

---

## ?? What You'll Achieve

**After Completion**:
? No more [Obsolete] SaveArticle method
? SaveArticleHandler is self-contained
? No redundant test files
? Consolidated test pyramid
? Clean, professional codebase
? 0 compilation errors
? 100% test pass rate

**Code Reduction**:
- 88 lines removed (SaveArticle method)
- ~1,500 lines removed (test files)
- **Total: ~1,600 lines eliminated** ??

---

## ?? CRITICAL SUCCESS FACTORS

1. **SaveArticleHandler must NOT call old SaveArticle method**
   - This is the blocker
   - Once removed, SaveArticle method becomes unreferenced

2. **Handler tests must pass after refactoring**
   - Validates logic is preserved
   - Ensures no functionality is lost

3. **Test consolidation must maintain coverage**
   - Don't lose test scenarios
   - Merge into handler tests

4. **Build must pass with zero errors**
   - Final validation
   - Confirms no orphaned references

---

## ?? QUICK DECISION NEEDED

**Do you want to:**

### Option A: I help with Handler Refactoring First
? Provide guidance on extracting SaveArticle logic into handler
? 2-3 hours of focused work
? Then you handle test cleanup

### Option B: I do the Full Elimination
? Handler refactoring
? Test consolidation  
? Code deletion
? ~8-10 hours total

### Option C: Step-by-Step Together
? We do it together
? I guide each phase
? You execute and learn

---

**Ready to proceed? Pick A, B, or C!** ??
