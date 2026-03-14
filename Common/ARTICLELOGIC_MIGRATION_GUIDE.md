# ArticleLogic to CQRS Migration Guide

## Overview
The `ArticleLogic` class is being phased out in favor of a CQRS (Command Query Responsibility Segregation) pattern using the `IMediator` interface. This guide provides migration examples for all replaced methods.

---

## Why Migrate?

### Before (ArticleLogic - Problems)
```csharp
// Large, difficult-to-test service class
public class SomeController
{
    public async Task<IActionResult> Index()
    {
        // Must instantiate ArticleLogic with many dependencies
        var articleLogic = new ArticleLogic(dbContext, cache, publisherUrl, blobUrl, isEditor);
        
        // Tight coupling to implementation details
        var sitemap = await articleLogic.GetSiteMap();
        var layout = await articleLogic.GetDefaultLayout();
        var viewModel = await articleLogic.BuildArticleViewModelAsync(article, "en");
        
        return View(viewModel);
    }
}
```

**Issues:**
- ❌ Large constructor with many dependencies
- ❌ Difficult to mock for unit testing
- ❌ Violates Single Responsibility Principle
- ❌ Cannot extend without modifying class (Open/Closed Principle)

### After (CQRS - Benefits)
```csharp
// Clean, testable, focused
public class SomeController
{
    private readonly IMediator _mediator;

    public SomeController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IActionResult> Index()
    {
        // Clear, self-documenting query objects
        var sitemap = await _mediator.QueryAsync(new GetSitemapQuery());
        var layout = await _mediator.QueryAsync(new GetDefaultLayoutQuery());
        var viewModel = await _mediator.QueryAsync(new BuildArticleViewModelQuery(article, "en"));
        
        return View(viewModel);
    }
}
```

**Benefits:**
- ✅ Single dependency (`IMediator`)
- ✅ Easy to mock for testing
- ✅ Each query has single responsibility
- ✅ Add new features by adding handlers (no modifications)
- ✅ Query objects self-document intent

---

## Migration Examples

### 1. GetSiteMap()

#### Before
```csharp
using Cosmos.Common.Data.Logic;

var articleLogic = new ArticleLogic(dbContext, cache, publisherUrl, blobUrl, isEditor);
var sitemap = await articleLogic.GetSiteMap();
```

#### After
```csharp
using Cosmos.Common.Features.Sitemap.Queries;
using Cosmos.Common.Features.Shared;

// Inject IMediator via constructor
private readonly IMediator _mediator;

public SomeController(IMediator mediator)
{
    _mediator = mediator;
}

// Use query
var sitemap = await _mediator.QueryAsync(new GetSitemapQuery());
```

---

### 2. GetDefaultLayout()

#### Before
```csharp
var articleLogic = new ArticleLogic(dbContext, cache, publisherUrl, blobUrl, isEditor);

// Without caching
var layout = await articleLogic.GetDefaultLayout();

// With caching (10 minutes)
var layout = await articleLogic.GetDefaultLayout(TimeSpan.FromMinutes(10));
```

#### After
```csharp
using Cosmos.Common.Features.Layouts.Queries;

// Without caching
var layout = await _mediator.QueryAsync(new GetDefaultLayoutQuery());

// With caching (10 minutes)
var layout = await _mediator.QueryAsync(new GetDefaultLayoutQuery(TimeSpan.FromMinutes(10)));
```

---

### 3. BuildArticleViewModelAsync (from Article)

#### Before
```csharp
var articleLogic = new ArticleLogic(dbContext, cache, publisherUrl, blobUrl, isEditor);
var viewModel = await articleLogic.BuildArticleViewModelAsync(article, "en-US", includeLayout: true);
```

#### After
```csharp
using Cosmos.Common.Features.Articles.Queries;

var viewModel = await _mediator.QueryAsync(
    new BuildArticleViewModelQuery(
        Article: article,
        LanguageCode: "en-US",
        IncludeLayout: true));
```

---

### 4. BuildArticleViewModel (from PublishedPage)

#### Before
```csharp
var articleLogic = new ArticleLogic(dbContext, cache, publisherUrl, blobUrl, isEditor);

// Protected method - accessed via subclassing
var viewModel = await BuildArticleViewModel(
    publishedPage,
    "en-US",
    layoutCache: TimeSpan.FromMinutes(10),
    includeLayout: true);
```

#### After
```csharp
using Cosmos.Common.Features.Articles.Queries;

var viewModel = await _mediator.QueryAsync(
    new BuildPublishedPageViewModelQuery(
        PublishedPage: publishedPage,
        LanguageCode: "en-US",
        LayoutCacheDuration: TimeSpan.FromMinutes(10),
        IncludeLayout: true));
```

---

### 5. Static Utilities (Serialize/Deserialize/GetPublisherHealth)

#### Before
```csharp
using Cosmos.Common.Data.Logic;

var bytes = ArticleLogic.Serialize(myObject);
var obj = ArticleLogic.Deserialize<MyType>(bytes);
var isHealthy = ArticleLogic.GetPublisherHealth();
```

#### After
```csharp
using Cosmos.Common.Utilities;

var bytes = ArticleLogicUtilities.Serialize(myObject);
var obj = ArticleLogicUtilities.Deserialize<MyType>(bytes);
var isHealthy = ArticleLogicUtilities.GetPublisherHealth();
```

---

## Testing Benefits

### Before (ArticleLogic)
```csharp
// Complex mock setup required
[Fact]
public async Task TestArticleViewModelBuilding()
{
    // Must mock DbContext, IMemoryCache, and configure many DbSets
    var mockDbContext = new Mock<ApplicationDbContext>();
    var mockAuthorInfos = new Mock<DbSet<AuthorInfo>>();
    var mockLayouts = new Mock<DbSet<Layout>>();
    // ... many more mocks
    
    mockDbContext.Setup(x => x.AuthorInfos).Returns(mockAuthorInfos.Object);
    mockDbContext.Setup(x => x.Layouts).Returns(mockLayouts.Object);
    // ... many more setups
    
    var articleLogic = new ArticleLogic(
        mockDbContext.Object,
        null, // cache
        "https://example.com",
        "https://cdn.example.com",
        isEditor: true);
    
    var result = await articleLogic.BuildArticleViewModelAsync(article, "en");
    
    Assert.NotNull(result);
}
```

### After (CQRS)
```csharp
// Simple mock of IMediator
[Fact]
public async Task TestArticleViewModelBuilding()
{
    // Single mock - much cleaner!
    var mockMediator = new Mock<IMediator>();
    mockMediator
        .Setup(x => x.QueryAsync(It.IsAny<BuildArticleViewModelQuery>(), default))
        .ReturnsAsync(new ArticleViewModel { Title = "Test" });
    
    var controller = new SomeController(mockMediator.Object);
    var result = await controller.Index();
    
    Assert.NotNull(result);
    mockMediator.Verify(x => x.QueryAsync(It.IsAny<BuildArticleViewModelQuery>(), default), Times.Once);
}
```

---

## Registration in DI Container

The CQRS infrastructure is already registered in your application. You only need to inject `IMediator`:

```csharp
// In your controller or service
public class MyController : Controller
{
    private readonly IMediator _mediator;

    public MyController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public async Task<IActionResult> SomeAction()
    {
        // Use queries via mediator
        var sitemap = await _mediator.QueryAsync(new GetSitemapQuery());
        return Ok(sitemap);
    }
}
```

---

## Migration Checklist

When migrating from `ArticleLogic` to CQRS:

1. ✅ **Add `IMediator` dependency** to your controller/service constructor
2. ✅ **Replace `new ArticleLogic(...)` calls** with appropriate query objects
3. ✅ **Update using statements** to import query namespaces:
   - `Cosmos.Common.Features.Sitemap.Queries`
   - `Cosmos.Common.Features.Layouts.Queries`
   - `Cosmos.Common.Features.Articles.Queries`
4. ✅ **Update static utility calls** to use `ArticleLogicUtilities`
5. ✅ **Update tests** to mock `IMediator` instead of complex DbContext setups
6. ✅ **Remove `ArticleLogic` instantiation** from your code

---

## Query Reference

### Available Queries

| Old Method | New Query | Namespace |
|------------|-----------|-----------|
| `GetSiteMap()` | `GetSitemapQuery` | `Cosmos.Common.Features.Sitemap.Queries` |
| `GetDefaultLayout(TimeSpan?)` | `GetDefaultLayoutQuery(TimeSpan?)` | `Cosmos.Common.Features.Layouts.Queries` |
| `BuildArticleViewModelAsync(Article, string, bool)` | `BuildArticleViewModelQuery(Article, string, bool)` | `Cosmos.Common.Features.Articles.Queries` |
| `BuildArticleViewModel(PublishedPage, string, TimeSpan?, bool)` | `BuildPublishedPageViewModelQuery(PublishedPage, string, TimeSpan?, bool)` | `Cosmos.Common.Features.Articles.Queries` |
| `Serialize(object)` | `ArticleLogicUtilities.Serialize(object)` | `Cosmos.Common.Utilities` |
| `Deserialize<T>(byte[])` | `ArticleLogicUtilities.Deserialize<T>(byte[])` | `Cosmos.Common.Utilities` |
| `GetPublisherHealth()` | `ArticleLogicUtilities.GetPublisherHealth()` | `Cosmos.Common.Utilities` |

---

## Timeline

- **Now**: All methods marked `[Obsolete]` - warnings generated at compile time
- **Phase 1-3 (current)**: Gradual migration of call sites, both patterns supported
- **Phase 4 (future)**: `ArticleLogic` class removed (breaking change, major version bump)

---

## Questions?

See `MODERNIZATION_RECOMMENDATIONS.md` for architectural rationale and implementation phases.

**Document Version:** 1.0  
**Last Updated:** 2025-01-11  
**Status:** Active Migration Guide
