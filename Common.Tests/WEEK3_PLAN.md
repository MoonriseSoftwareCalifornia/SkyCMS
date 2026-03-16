# Week 3: Query Handlers - Planning Document

## Selected Handlers for Week 3 (10 handlers, ~70-90 tests)

### Priority 1: Editor Article Queries (High Business Value) - 4 handlers, ~30 tests

#### 1. GetArticleByArticleNumberQueryHandler ⭐
**Complexity**: Medium  
**Lines of Code**: ~40  
**Dependencies**: ArticleViewModelBuilder, IMediator, IMemoryCache, IConfiguration  
**Estimated Tests**: 10

**Key Scenarios**:
- ✅ Retrieve by article number only (latest version)
- ✅ Retrieve specific version by article number + version
- ✅ Non-existent article number
- ✅ Deleted articles excluded
- ✅ Version ordering (highest version)
- ✅ Multiple versions handling
- ✅ Configuration usage
- ✅ ArticleViewModelBuilder integration

**Pattern**: Similar to GetArticleByIdQueryHandler - uses ArticleViewModelBuilder with IMediator mocking

---

#### 2. GetArticleCatalogEntryQueryHandler ⭐
**Complexity**: Low-Medium  
**Lines of Code**: ~60  
**Dependencies**: ApplicationDbContext, IMemoryCache (optional)  
**Estimated Tests**: 8

**Key Scenarios**:
- ✅ Retrieve catalog entry by article number
- ✅ Non-existent article number returns null
- ✅ With caching enabled
- ✅ Without caching
- ✅ Cache hit scenario
- ✅ Cache miss scenario
- ✅ Null cache duration (no caching)
- ✅ Include ArticlePermissions

**Pattern**: Simple EF query with optional caching (similar to GetLayoutByIdQueryHandler)

---

#### 3. GetLastPublishedDateQueryHandler
**Complexity**: Low  
**Lines of Code**: ~30  
**Dependencies**: ApplicationDbContext  
**Estimated Tests**: 6

**Key Scenarios**:
- ✅ Get last published date for article with published versions
- ✅ Article with no published versions
- ✅ Non-existent article number
- ✅ Multiple versions (returns latest)
- ✅ Future published dates excluded
- ✅ Deleted articles excluded

**Pattern**: Simple EF query with date filtering

---

#### 4. GetArticleRedirectsQueryHandler
**Complexity**: Low  
**Lines of Code**: ~35  
**Dependencies**: ApplicationDbContext  
**Estimated Tests**: 6

**Key Scenarios**:
- ✅ Get all redirects for article number
- ✅ Article with no redirects
- ✅ Article with multiple redirects
- ✅ Non-existent article number
- ✅ Redirect ordering
- ✅ Active vs deleted redirects

**Pattern**: Simple EF collection query

---

### Priority 2: Search & Filter (Medium Business Value) - 3 handlers, ~20 tests

#### 5. SearchPublishedArticlesQueryHandler ⭐⭐
**Complexity**: Low (Service Delegation)  
**Lines of Code**: ~25  
**Dependencies**: IArticleCatalogQueryService  
**Estimated Tests**: 6

**Key Scenarios**:
- ✅ Search with text query
- ✅ Empty search text
- ✅ Null search text
- ✅ Service delegation verification
- ✅ CancellationToken handling
- ✅ Results returned correctly

**Pattern**: Service delegation (like GetPublishedPageByUrlQueryHandler)

---

#### 6. GetArticleFolderContentsQueryHandler
**Complexity**: Low-Medium  
**Lines of Code**: ~30  
**Dependencies**: IStorageContext (blob storage)  
**Estimated Tests**: 8

**Key Scenarios**:
- ✅ Get folder contents for article
- ✅ Empty folder
- ✅ Folder with files only
- ✅ Folder with directories only
- ✅ Mixed files and directories
- ✅ Path normalization (leading slash)
- ✅ Null query throws ArgumentNullException
- ✅ Storage context integration

**Pattern**: External service integration with path manipulation

---

#### 7. GetArticlesForUserQueryHandler
**Complexity**: Medium  
**Lines of Code**: ~50  
**Dependencies**: ApplicationDbContext, ClaimsPrincipal  
**Estimated Tests**: 8

**Key Scenarios**:
- ✅ Articles for specific user (by userId)
- ✅ Articles for anonymous user
- ✅ User with no articles
- ✅ Multiple articles per user
- ✅ Exclude deleted articles
- ✅ Ordering by date/title
- ✅ Claims principal integration
- ✅ User identity extraction

**Pattern**: EF query with user filtering (similar to AuthorizeUserForArticleQueryHandler)

---

### Priority 3: Published Pages (High Value) - 2 handlers, ~15 tests

#### 8. GetPublishedPageHeaderByUrlQueryHandler ⭐
**Complexity**: Medium  
**Lines of Code**: ~40  
**Dependencies**: ApplicationDbContext  
**Estimated Tests**: 8

**Key Scenarios**:
- ✅ Get page header by URL
- ✅ Non-existent URL returns null
- ✅ URL normalization
- ✅ Language parameter handling
- ✅ Published date filtering
- ✅ Latest version selection
- ✅ Root path handling
- ✅ Future published dates excluded

**Pattern**: EF query with URL normalization and date filtering

---

#### 9. BuildPublishedPageViewModelQueryHandler
**Complexity**: Low (Service Delegation)  
**Lines of Code**: ~30  
**Dependencies**: IPublishedPageQueryService  
**Estimated Tests**: 7

**Key Scenarios**:
- ✅ Build view model from published page
- ✅ With layout caching enabled
- ✅ Without layout caching
- ✅ Include layout option
- ✅ Exclude layout option
- ✅ Service delegation verification
- ✅ Parameter passing validation

**Pattern**: Service delegation

---

### Priority 4: Layout Utilities (Low Complexity) - 1 handler, ~5 tests

#### 10. CheckDefaultLayoutExistsQueryHandler
**Complexity**: Very Low  
**Lines of Code**: ~25  
**Dependencies**: ApplicationDbContext  
**Estimated Tests**: 5

**Key Scenarios**:
- ✅ Default layout exists
- ✅ No default layout exists
- ✅ Multiple default layouts (returns true)
- ✅ Unpublished default layout
- ✅ Published date filtering

**Pattern**: Simple boolean EF query

---

## Estimated Totals for Week 3

| Category | Handlers | Tests | Complexity |
|----------|----------|-------|------------|
| **Editor Article Queries** | 4 | 30 | Medium |
| **Search & Filter** | 3 | 22 | Low-Medium |
| **Published Pages** | 2 | 15 | Medium |
| **Layout Utilities** | 1 | 5 | Very Low |
| **TOTAL** | **10** | **~72** | **Mixed** |

---

## Test Patterns to Use (from Week 2)

### 1. ArticleViewModelBuilder Pattern
For handlers using ArticleViewModelBuilder:
```csharp
private static Mock<IMediator> CreateMockMediatorWithLayout()
{
    var mockMediator = new Mock<IMediator>();
    var mockLayout = new LayoutViewModel { /* properties */ };
    mockMediator.Setup(m => m.QueryAsync(It.IsAny<GetDefaultLayoutQuery>(), default))
        .ReturnsAsync(mockLayout);
    return mockMediator;
}
```

### 2. Service Delegation Pattern
For handlers that delegate to services:
```csharp
var mockService = new Mock<IService>();
mockService.Setup(s => s.MethodAsync(params)).ReturnsAsync(result);
var handler = new Handler(mockService.Object);
await handler.HandleAsync(query);
mockService.Verify(s => s.MethodAsync(params), Times.Once);
```

### 3. Simple EF Query Pattern
For handlers with direct EF queries:
```csharp
using var context = TestDbContextPool.CreateIsolatedContext();
var entity = TestDataBuilder.CreateEntity();
context.Entities.Add(entity);
await context.SaveChangesAsync();
var handler = new Handler(context);
var result = await handler.HandleAsync(query);
Assert.IsNotNull(result);
```

### 4. Caching Pattern
For handlers with optional caching:
```csharp
// Test with cache
var memoryCache = new MemoryCache(new MemoryCacheOptions());
var query = new Query { CacheDuration = TimeSpan.FromMinutes(5) };
// First call - cache miss
var result1 = await handler.HandleAsync(query);
// Second call - cache hit
var result2 = await handler.HandleAsync(query);
Assert.AreSame(result1, result2); // Verify same instance from cache
```

---

## External Dependencies to Mock

### New Dependencies for Week 3
1. **IArticleCatalogQueryService** - Article search service
2. **IStorageContext** - Blob storage operations
3. **IPublishedPageQueryService** - Published page operations (already used in Week 2)

All other dependencies (IMediator, IMemoryCache, IConfiguration, ApplicationDbContext) already have established patterns from Weeks 1-2.

---

## Execution Strategy

### Phase 1: Editor Queries (High Priority)
Create tests for 4 editor query handlers (~30 tests):
1. GetArticleByArticleNumberQueryHandler
2. GetArticleCatalogEntryQueryHandler
3. GetLastPublishedDateQueryHandler
4. GetArticleRedirectsQueryHandler

**Estimated Time**: 2-3 hours  
**Target**: 100% coverage for all 4 handlers

---

### Phase 2: Search & Content
Create tests for 3 search/filter handlers (~22 tests):
5. SearchPublishedArticlesQueryHandler
6. GetArticleFolderContentsQueryHandler
7. GetArticlesForUserQueryHandler

**Estimated Time**: 2 hours  
**Target**: 100% coverage for all 3 handlers

---

### Phase 3: Published Pages & Layout
Create tests for remaining 3 handlers (~20 tests):
8. GetPublishedPageHeaderByUrlQueryHandler
9. BuildPublishedPageViewModelQueryHandler
10. CheckDefaultLayoutExistsQueryHandler

**Estimated Time**: 1.5 hours  
**Target**: 100% coverage for all 3 handlers

---

## Success Criteria

✅ All 10 handlers with 100% code coverage  
✅ ~72 comprehensive tests created  
✅ All tests passing (0 failures)  
✅ Build successful with zero errors  
✅ Execution time < 2 seconds for all Week 3 tests  
✅ 28 parallel workers validated  
✅ Patterns documented for future use  

---

## Cumulative Progress After Week 3 (Projected)

| Metric | Week 1 | Week 2 | Week 3 (Projected) | Total |
|--------|--------|--------|--------------------|-------|
| **Test Files** | 4 | 10 | 10 | 24 |
| **Tests** | 116 | 239 | ~72 | ~427 |
| **Handlers Tested** | 0 | 10 | 10 | 20 |
| **Pass Rate** | 100% | 100% | 100% (goal) | 100% |
| **Code Coverage** | 98.7% | 100% | 100% (goal) | 99.5%+ |

---

## Ready to Begin?

This plan provides a clear roadmap for Week 3 with:
- ✅ 10 carefully selected query handlers
- ✅ Balanced complexity (mix of simple and medium)
- ✅ Business value prioritized
- ✅ Established patterns from Weeks 1-2
- ✅ Clear success criteria
- ✅ ~72 comprehensive tests

**Recommendation**: Proceed with autonomous execution (like Week 2) - create all 10 test files without stopping for confirmation.

**Alternative**: Create tests in phases (1-3 above) with validation checkpoints between each phase.

Which approach would you prefer?
