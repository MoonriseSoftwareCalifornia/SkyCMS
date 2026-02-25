# SaveArticle Test Audit & Refactoring Analysis

## Summary
Found **27 references** to the obsolete `ArticleEditLogic.SaveArticle()` method across 6 test files.

## Test Files Using SaveArticle

### 1. **Tests\Services\ArticleEditLogicTests.cs** (1 test)
- **Status**: All tests marked `[Ignore]`
- **Recommendation**: DELETE entirely (legacy logic class tests, obsolete comments state to use CQRS)
- **Test Count**: 1
  - `SaveArticle_UpdateContent_PersistsChanges()` - Basic content update

### 2. **Tests\Features\Articles\Save\SaveArticleErrorHandlingTests.cs** (4 references)
- **Status**: Using legacy `Logic.SaveArticle()` mixed with new `SaveArticleHandler.HandleAsync()`
- **Recommendation**: REFACTOR - Convert remaining logic calls to handler
- **Tests Count**: 4 unique tests
  - `SaveArticle_NonExistentArticle_ReturnsNotFound()`
  - `SaveArticle_WithInvalidUserId_ReturnsValidationError()`
  - `SaveArticle_TitleExceeds254Chars_ReturnsValidationError()`
  - `SaveArticle_IntroductionExceeds512Chars_ReturnsValidationError()` (+ CategoryExceeds64Chars variant)

### 3. **Tests\Features\Articles\Save\SaveArticlePublishingTests.cs** (6 references)
- **Status**: Partially migrated - uses both legacy and new patterns
- **Recommendation**: REFACTOR - Replace legacy calls with command handler
- **Test Count**: ~3 unique tests
  - `SaveArticle_PublishedArticle_TriggersCdnPurge()`
  - `SaveArticle_UnpublishedThenPublished_UpdatesCatalog()`
  - `SaveArticle_ChangesWhilePublished_MaintainsPublishedState()`

### 4. **Tests\Integration\ArticleLifecycleIntegrationTests.cs** (5 references)
- **Status**: Full workflow tests, uses both legacy methods
- **Recommendation**: REFACTOR OR KEEP - These are comprehensive integration tests
- **Test Count**: ~2-3 integration scenarios
  - `ArticleLifecycle_CompleteWorkflow_Success()`
  - `MultipleArticles_PublishInDifferentOrders_AllWorkCorrectly()`
  - `EditAndRepublish_MaintainsCorrectState()`

### 5. **Tests\Performance\PerformanceAndConcurrencyTests.cs** (2 references)
- **Status**: Performance/stress testing
- **Recommendation**: KEEP BUT REFACTOR - Important for regression testing

### 6. **Tests\Services\BlogServiceTests.cs** (7 references)
- **Status**: Blog-specific functionality tests
- **Recommendation**: KEEP BUT REFACTOR - Domain-specific tests
- **Test Count**: ~5 tests
  - Various blog post creation, update, and ordering tests

---

## Duplicate Analysis

### Potential Duplicates Identified:

1. **Content Update Tests**: 
   - `ArticleEditLogicTests.SaveArticle_UpdateContent_PersistsChanges()` 
   - **Vs** Basic assertions in handler tests
   - **Action**: DELETE ArticleEditLogicTests version

2. **Publishing Integration**:
   - Multiple variations of "save published article" tests exist
   - **Action**: Consolidate to one reference test in SaveArticleHandlerTests

3. **Error/Validation Tests**:
   - SaveArticleErrorHandlingTests covers comprehensive validation
   - **Action**: KEEP - these are thorough and non-duplicative

4. **Integration Tests**:
   - ArticleLifecycleIntegrationTests covers full workflows
   - **Action**: KEEP - valuable for regression testing

---

## Migration Strategy

### Phase 1: Delete Obsolete Tests
```
- Delete: Tests\Services\ArticleEditLogicTests.cs
  Reason: Entire class marked [Obsolete], all tests marked [Ignore]
  Impact: 1 test removed
```

### Phase 2: Refactor to CQRS Pattern
Update all remaining `Logic.SaveArticle()` calls to use `SaveArticleCommand`/`SaveArticleHandler`:

**Old Pattern**:
```csharp
var result = await Logic.SaveArticle(article, TestUserId);
```

**New Pattern**:
```csharp
var command = new SaveArticleCommand
{
    ArticleNumber = article.ArticleNumber,
    Title = article.Title,
    Content = article.Content,
    UserId = TestUserId,
    ArticleType = article.ArticleType,
    // ... other properties
};
var result = await SaveArticleHandler.HandleAsync(command);
```

### Files to Refactor:
1. `SaveArticleErrorHandlingTests.cs` - 4 instances
2. `SaveArticlePublishingTests.cs` - 6 instances  
3. `ArticleLifecycleIntegrationTests.cs` - 5 instances
4. `BlogServiceTests.cs` - 7 instances
5. `PerformanceAndConcurrencyTests.cs` - 2 instances

---

## Test Consolidation Recommendations

| Test Category | Current Count | Recommended | Reasoning |
|---|---|---|---|
| Content Updates | 3+ variations | 1 canonical | Covered by SaveArticleHandlerTests |
| Publishing Workflow | 6+ variations | 2-3 canonical | Keep CDN + Catalog + State tests |
| Error Handling | 4-5 | 4-5 | All non-duplicate, comprehensive |
| Integration | 2-3 | 2-3 | Valuable regression tests |
| Blog-specific | 7 | 5-7 | Domain-specific, keep most |
| **Total** | **~27** | **~20-23** | **Reduction of 4-7 tests** |

---

## Implementation Checklist

- [ ] Step 1: Delete `Tests\Services\ArticleEditLogicTests.cs`
- [ ] Step 2: Refactor `SaveArticleErrorHandlingTests.cs` 
- [ ] Step 3: Refactor `SaveArticlePublishingTests.cs`
- [ ] Step 4: Refactor `ArticleLifecycleIntegrationTests.cs`
- [ ] Step 5: Refactor `BlogServiceTests.cs`
- [ ] Step 6: Refactor `PerformanceAndConcurrencyTests.cs`
- [ ] Step 7: Run full test suite to verify no regressions
- [ ] Step 8: Verify all SaveArticleHandler tests pass

---

## Key Files Reference
- `SaveArticleCommand.cs` - Command definition
- `SaveArticleHandler.cs` - Handler implementation  
- `SaveArticleHandlerTests.cs` - Reference test patterns (CQRS)
- `ArticleTestBase.cs` - Base class for article tests with seeding helpers
