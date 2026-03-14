# Phase 4 Breaking Changes - Migration Guide

## Overview

Phase 4 completes the CQRS migration by removing obsolete static helper classes and requiring `IMediator` dependency injection throughout the codebase. This is a **breaking change** for internal consumers.

**Impact Level:** 🔴 **MAJOR** - Requires code changes in consuming projects

---

## Deleted Classes

The following classes have been **permanently removed**:

### 1. ❌ `Cosmos.Common.Data.Logic.ArticleLogic`

**Removed Methods:**
- `PublishArticle(Article article, DateTimeOffset publishDate)`
- `GetArticleViewModel(Guid articleId)`
- `DeleteArticle(int articleNumber)`
- `RestoreArticle(int articleNumber)`

### 2. ❌ `Cosmos.Common.Data.Logic.LayoutHelper`

**Removed Methods:**
- `GetCurrentDefaultLayoutAsync(ApplicationDbContext dbContext)`
- `HasDefaultLayoutAsync(ApplicationDbContext dbContext)`

### 3. ❌ `Cosmos.Common.CosmosUtilities`

**Removed Methods:**
- `AuthUser(ClaimsPrincipal user, Article article, ApplicationDbContext dbContext)`
- `GetArticleFolderContents(IStorageContext storage, int articleNumber, string path)`

---

## Migration Guide

### Prerequisites

Ensure your project has access to:
- `Cosmos.Common.Features.Shared.IMediator` via dependency injection
- All CQRS query/command types (imported automatically from Cosmos.Common)

---

### ArticleLogic Migrations

#### 1. PublishArticle → PublishingService.PublishAsync

**Before:**
```csharp
using Cosmos.Common.Data.Logic;

// Static method call
await ArticleLogic.PublishArticle(article, DateTimeOffset.UtcNow);
```

**After:**
```csharp
using Sky.Editor.Services.Publishing;

public class YourService
{
    private readonly IPublishingService _publishingService;
    
    public YourService(IPublishingService publishingService)
    {
        _publishingService = publishingService;
    }
    
    public async Task YourMethod()
    {
        // Inject IPublishingService and call instance method
        await _publishingService.PublishAsync(article);
        // Note: PublishingService sets publish date to UtcNow automatically
    }
}
```

#### 2. GetArticleViewModel → GetArticleByIdQuery

**Before:**
```csharp
using Cosmos.Common.Data.Logic;

var viewModel = await ArticleLogic.GetArticleViewModel(articleId);
```

**After:**
```csharp
using Cosmos.Common.Features.Articles.EditorQueries;
using Cosmos.Common.Features.Shared;

public class YourService
{
    private readonly IMediator _mediator;
    
    public YourService(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    public async Task YourMethod()
    {
        var viewModel = await _mediator.QueryAsync(new GetArticleByIdQuery { Id = articleId });
    }
}
```

#### 3. DeleteArticle / RestoreArticle → Direct Database Operations

**Before:**
```csharp
using Cosmos.Common.Data.Logic;

await ArticleLogic.DeleteArticle(articleNumber);
await ArticleLogic.RestoreArticle(articleNumber);
```

**After (for test code only):**
```csharp
using Microsoft.EntityFrameworkCore;

// DeleteArticle
var article = await dbContext.Articles.FirstAsync(a => a.ArticleNumber == articleNumber);
article.StatusCode = (int)StatusCodeEnum.Deleted;
await dbContext.SaveChangesAsync();

// RestoreArticle
var article = await dbContext.Articles.FirstAsync(a => a.ArticleNumber == articleNumber);
article.StatusCode = (int)StatusCodeEnum.Active;
article.Published = null;
await dbContext.SaveChangesAsync();
```

**Note:** For production code, use proper command handlers instead of direct DB operations.

---

### LayoutHelper Migrations

#### 1. GetCurrentDefaultLayoutAsync → GetDefaultLayoutQuery

**Before:**
```csharp
using Cosmos.Common.Data.Logic;

var layout = await LayoutHelper.GetCurrentDefaultLayoutAsync(dbContext);
```

**After:**
```csharp
using Cosmos.Common.Features.Layouts.Queries;
using Cosmos.Common.Features.Shared;
using Microsoft.EntityFrameworkCore;

public class YourService
{
    private readonly IMediator _mediator;
    private readonly ApplicationDbContext _dbContext;
    
    public YourService(IMediator mediator, ApplicationDbContext dbContext)
    {
        _mediator = mediator;
        _dbContext = dbContext;
    }
    
    public async Task YourMethod()
    {
        // Query returns LayoutViewModel (DTO)
        var layoutViewModel = await _mediator.QueryAsync(new GetDefaultLayoutQuery());
        
        // If you need the full Layout entity:
        var layout = await _dbContext.Layouts.FirstAsync(l => l.Id == layoutViewModel.Id);
    }
}
```

**Important:** `GetDefaultLayoutQuery` returns a `LayoutViewModel` (lightweight DTO), not a full `Layout` entity. If you need `LayoutNumber` or `CommunityLayoutId`, fetch the entity from the database using the ID.

#### 2. HasDefaultLayoutAsync → CheckDefaultLayoutExistsQuery

**Before:**
```csharp
using Cosmos.Common.Data.Logic;

bool hasDefault = await LayoutHelper.HasDefaultLayoutAsync(dbContext);
```

**After:**
```csharp
using Cosmos.Common.Features.Layouts.Queries;
using Cosmos.Common.Features.Shared;

public class YourService
{
    private readonly IMediator _mediator;
    
    public YourService(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    public async Task YourMethod()
    {
        bool hasDefault = await _mediator.QueryAsync(new CheckDefaultLayoutExistsQuery());
    }
}
```

---

### CosmosUtilities Migrations

#### 1. AuthUser → AuthorizeUserForArticleQuery

**Before:**
```csharp
using Cosmos.Common;

bool isAuthorized = await CosmosUtilities.AuthUser(User, article, dbContext);
```

**After:**
```csharp
using Cosmos.Common.Features.Articles.Queries;
using Cosmos.Common.Features.Shared;

public class YourController : ControllerBase
{
    private readonly IMediator _mediator;
    
    public YourController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    public async Task<IActionResult> YourAction()
    {
        bool isAuthorized = await _mediator.QueryAsync(new AuthorizeUserForArticleQuery
        {
            User = User,
            ArticleId = article.Id
        });
    }
}
```

#### 2. GetArticleFolderContents → GetArticleFolderContentsQuery

**Before:**
```csharp
using Cosmos.Common;
using Cosmos.BlobService;

var files = await CosmosUtilities.GetArticleFolderContents(storageContext, articleNumber, path);
```

**After:**
```csharp
using Cosmos.Common.Features.Articles.Queries;
using Cosmos.Common.Features.Shared;

public class YourService
{
    private readonly IMediator _mediator;
    
    public YourService(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    public async Task YourMethod()
    {
        var files = await _mediator.QueryAsync(new GetArticleFolderContentsQuery(articleNumber, path));
    }
}
```

**Benefits:** No longer need to inject `IStorageContext` - query handler manages storage internally.

---

## Constructor Signature Changes

### Services Requiring IMediator Injection

The following services now require `IMediator` as a constructor parameter:

#### 1. TemplateService

**Before:**
```csharp
new TemplateService(
    webHostEnvironment,
    logger,
    dbContext,
    dynamicConfigProvider)
```

**After:**
```csharp
new TemplateService(
    webHostEnvironment,
    logger,
    dbContext,
    mediator,               // ✅ ADD THIS
    dynamicConfigProvider)
```

#### 2. PublishingService

**Before:**
```csharp
new PublishingService(
    dbContext,
    storage,
    settings,
    logger,
    httpContextAccessor,
    authorInfoService,
    clock,
    blogStreamRenderingService,    // IMediator should be HERE
    viewRenderService,
    serviceProvider,
    progressReporter,
    catalogQueryService)
```

**After:**
```csharp
new PublishingService(
    dbContext,
    storage,
    settings,
    logger,
    httpContextAccessor,
    authorInfoService,
    clock,
    mediator,                      // ✅ ADD THIS (before blogStreamRenderingService)
    blogStreamRenderingService,
    viewRenderService,
    serviceProvider,
    progressReporter,
    catalogQueryService)
```

#### 3. ArticleViewModelBuilder

**Before:**
```csharp
new ArticleViewModelBuilder(
    dbContext,
    memoryCache,
    publisherUrl,
    isEditor)
```

**After:**
```csharp
new ArticleViewModelBuilder(
    mediator,          // ✅ ADD THIS (FIRST parameter)
    dbContext,
    memoryCache,
    publisherUrl,
    isEditor)
```

#### 4. ImportLayoutHandler

**Before:**
```csharp
new ImportLayoutHandler(
    dbContext,
    layoutImportService,
    layoutVersioningService,
    logger)
```

**After:**
```csharp
new ImportLayoutHandler(
    dbContext,
    mediator,                  // ✅ ADD THIS (after dbContext)
    layoutImportService,
    layoutVersioningService,
    logger)
```

---

### Query Handlers Requiring IMediator Injection

All CQRS query handlers now require `IMediator` as the **first constructor parameter**:

#### Example: GetArticleByIdQueryHandler

**Before:**
```csharp
new GetArticleByIdQueryHandler(
    dbContext,
    memoryCache,
    configuration)
```

**After:**
```csharp
new GetArticleByIdQueryHandler(
    mediator,          // ✅ ADD THIS (FIRST parameter)
    dbContext,
    memoryCache,
    configuration)
```

**Affected Handlers:**
- `GetArticleByIdQueryHandler`
- `GetArticleByUrlQueryHandler`
- `GetArticleByArticleNumberQueryHandler`
- All other CQRS query handlers

---

## Dependency Injection Updates

### Program.cs / Startup.cs Registration

Ensure `IMediator` is registered in your DI container:

```csharp
// Example registration (adjust for your implementation)
services.AddScoped<IMediator, CommonMediator>();

// ArticleViewModelBuilder registration (updated)
services.AddScoped<ArticleViewModelBuilder>(sp => new ArticleViewModelBuilder(
    sp.GetRequiredService<IMediator>(),           // ✅ ADD THIS
    sp.GetRequiredService<ApplicationDbContext>(),
    sp.GetRequiredService<IMemoryCache>(),
    publisherUrl,
    isEditor: false));
```

---

## Test Code Updates

### For Unit Tests

Use `null!` for IMediator in test instantiations when the mediator is not actually used:

```csharp
// Handler instantiation in tests
var handler = new GetArticleByIdQueryHandler(
    null!,             // IMediator (not used in this test)
    dbContext,
    memoryCache,
    configuration);

// Service instantiation in tests
var templateService = new TemplateService(
    mockEnvironment.Object,
    mockLogger.Object,
    dbContext,
    null!,             // IMediator (not used in this test)
    mockConfigProvider.Object);
```

### For Integration Tests

Use the actual `IMediator` from your test base class:

```csharp
public class YourIntegrationTest : SkyCmsTestBase
{
    [TestMethod]
    public async Task YourTest()
    {
        // Mediator is available from SkyCmsTestBase
        var result = await Mediator.QueryAsync(new GetDefaultLayoutQuery());
        Assert.IsNotNull(result);
    }
}
```

---

## Common Migration Patterns

### Pattern 1: Static Helper → CQRS Query

```csharp
// Before: Static method call
var result = await StaticHelper.DoSomething(param1, param2);

// After: CQRS query via IMediator
var result = await _mediator.QueryAsync(new DoSomethingQuery 
{ 
    Param1 = param1, 
    Param2 = param2 
});
```

### Pattern 2: Direct Service → Service via DI

```csharp
// Before: Static method on service class
await ArticleLogic.PublishArticle(article, date);

// After: Inject service and call instance method
await _publishingService.PublishAsync(article);
```

### Pattern 3: ViewModel → Entity Conversion

When migrating from `GetArticleViewModel`, note that old code returned entities, but new queries return ViewModels:

```csharp
// Before: Returns Article entity
var article = await ArticleLogic.GetArticleViewModel(id);
await PublishingService.PublishAsync(article);  // Expects Article entity

// After: Convert ViewModel to Entity
var articleViewModel = await _mediator.QueryAsync(new GetArticleByIdQuery { Id = id });
var article = await _dbContext.Articles.FirstAsync(a => a.Id == articleViewModel.Id);
await _publishingService.PublishAsync(article);
```

---

## Checklist for Consumers

- [ ] Remove all `using Cosmos.Common.Data.Logic;` statements
- [ ] Remove all `using Cosmos.Common;` (if only for CosmosUtilities)
- [ ] Add `using Cosmos.Common.Features.Shared;` for IMediator
- [ ] Add `using Cosmos.Common.Features.Articles.Queries;` for article queries
- [ ] Add `using Cosmos.Common.Features.Layouts.Queries;` for layout queries
- [ ] Update all service/handler instantiations to include IMediator
- [ ] Replace all static helper calls with CQRS queries
- [ ] Update DI registrations in Program.cs/Startup.cs
- [ ] Run full test suite to verify migrations
- [ ] Update documentation referencing removed classes

---

## Support

If you encounter migration issues:

1. Check this guide for the specific class/method you're migrating
2. Review the CQRS query definitions in `Cosmos.Common.Features`
3. Examine the updated test files for examples
4. See `PHASE4_PROGRESS.md` for complete list of updated files

---

**Version:** Phase 4 Completion  
**Breaking Changes:** 3 classes removed, 20+ constructor signatures updated  
**Migration Complexity:** Medium (requires dependency injection updates)  
**Recommended Testing:** Full regression test suite after migration
