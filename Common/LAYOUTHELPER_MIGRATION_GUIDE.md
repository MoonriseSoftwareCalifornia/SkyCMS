# LayoutHelper to CQRS Migration Guide

## Overview
The `LayoutHelper` static class is being phased out in favor of CQRS queries using the `IMediator` interface. This guide provides migration examples for all replaced methods.

---

## Why Migrate?

### Before (LayoutHelper - Problems)
```csharp
// Static methods require passing DbContext explicitly
using Cosmos.Common.Data.Logic;

var layout = await LayoutHelper.GetCurrentDefaultLayoutAsync(dbContext);
var exists = await LayoutHelper.HasDefaultLayoutAsync(dbContext);
var layoutById = await LayoutHelper.GetLayoutByIdAsync(dbContext, layoutId);
```

**Issues:**
- ❌ Cannot be mocked for unit testing
- ❌ Requires explicit `ApplicationDbContext` reference (not `IApplicationDbContext`)
- ❌ No dependency injection support
- ❌ Difficult to extend or add cross-cutting concerns

### After (CQRS - Benefits)
```csharp
// Clean, injectable, testable
using Cosmos.Common.Features.Layouts.Queries;

var layout = await _mediator.QueryAsync(new GetDefaultLayoutQuery());
var exists = await _mediator.QueryAsync(new CheckDefaultLayoutExistsQuery());
var layoutById = await _mediator.QueryAsync(new GetLayoutByIdQuery(layoutId));
```

**Benefits:**
- ✅ Easy to mock `IMediator` in tests
- ✅ Uses `IApplicationDbContext` interface (better abstraction)
- ✅ Supports cross-cutting concerns (logging, validation, caching)
- ✅ Consistent with CQRS architecture

---

## Migration Examples

### 1. GetCurrentDefaultLayoutAsync()

#### Before
```csharp
using Cosmos.Common.Data.Logic;

var layout = await LayoutHelper.GetCurrentDefaultLayoutAsync(dbContext);
if (layout != null)
{
    // Use layout entity
}
```

#### After
```csharp
using Cosmos.Common.Features.Layouts.Queries;
using Cosmos.Common.Features.Shared;

// Inject IMediator via constructor
private readonly IMediator _mediator;

public SomeController(IMediator mediator)
{
    _mediator = mediator;
}

// Use query - returns LayoutViewModel instead of Layout entity
var layoutViewModel = await _mediator.QueryAsync(new GetDefaultLayoutQuery());
if (layoutViewModel != null)
{
    // Use layout view model
}
```

**Note:** `GetDefaultLayoutQuery` returns `LayoutViewModel` (for presentation), not the raw `Layout` entity. If you need the entity, you can still use the obsolete method temporarily or create a new query that returns the entity.

---

### 2. GetCurrentDefaultLayoutAsync() with Caching

#### Before
```csharp
// No built-in caching support
var layout = await LayoutHelper.GetCurrentDefaultLayoutAsync(dbContext);

// Manual caching required
if (cache.TryGetValue("layout", out Layout cached))
{
    layout = cached;
}
else
{
    layout = await LayoutHelper.GetCurrentDefaultLayoutAsync(dbContext);
    cache.Set("layout", layout, TimeSpan.FromMinutes(10));
}
```

#### After
```csharp
// Built-in caching support
var layout = await _mediator.QueryAsync(
    new GetDefaultLayoutQuery(CacheDuration: TimeSpan.FromMinutes(10)));
```

**Note:** Caching is handled by the query handler when `CacheDuration` is provided.

---

### 3. HasDefaultLayoutAsync()

#### Before
```csharp
using Cosmos.Common.Data.Logic;

var hasDefault = await LayoutHelper.HasDefaultLayoutAsync(dbContext);
if (!hasDefault)
{
    // Create default layout
}
```

#### After
```csharp
using Cosmos.Common.Features.Layouts.Queries;

var hasDefault = await _mediator.QueryAsync(new CheckDefaultLayoutExistsQuery());
if (!hasDefault)
{
    // Create default layout
}
```

---

### 4. GetLayoutByIdAsync()

#### Before
```csharp
using Cosmos.Common.Data.Logic;

var layout = await LayoutHelper.GetLayoutByIdAsync(dbContext, layoutId);
if (layout == null)
{
    // Handle not found
}
```

#### After
```csharp
using Cosmos.Common.Features.Layouts.Queries;

var layout = await _mediator.QueryAsync(new GetLayoutByIdQuery(layoutId));
if (layout == null)
{
    // Handle not found
}
```

**Note:** Returns `null` automatically if `layoutId` is `Guid.Empty`.

---

## Testing Benefits

### Before (LayoutHelper)
```csharp
// Difficult to test - requires mocking DbContext and DbSets
[Fact]
public async Task TestLayoutRetrieval()
{
    // Complex mock setup
    var mockContext = new Mock<ApplicationDbContext>();
    var mockLayouts = new Mock<DbSet<Layout>>();
    // ... extensive setup
    
    mockContext.Setup(x => x.Layouts).Returns(mockLayouts.Object);
    
    var layout = await LayoutHelper.GetCurrentDefaultLayoutAsync(mockContext.Object);
    
    Assert.NotNull(layout);
}
```

### After (CQRS)
```csharp
// Simple mock of IMediator
[Fact]
public async Task TestLayoutRetrieval()
{
    // Single mock - much cleaner!
    var mockMediator = new Mock<IMediator>();
    mockMediator
        .Setup(x => x.QueryAsync(It.IsAny<GetDefaultLayoutQuery>(), default))
        .ReturnsAsync(new LayoutViewModel { Title = "Test Layout" });
    
    var controller = new SomeController(mockMediator.Object);
    var result = await controller.Index();
    
    Assert.NotNull(result);
    mockMediator.Verify(x => x.QueryAsync(It.IsAny<GetDefaultLayoutQuery>(), default), Times.Once);
}
```

---

## Common Migration Patterns

### Pattern 1: Controller with LayoutHelper → Controller with IMediator

#### Before
```csharp
public class SomeController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public SomeController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index()
    {
        var layout = await LayoutHelper.GetCurrentDefaultLayoutAsync(_dbContext);
        return View(layout);
    }
}
```

#### After
```csharp
public class SomeController : Controller
{
    private readonly IMediator _mediator;

    public SomeController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IActionResult> Index()
    {
        var layout = await _mediator.QueryAsync(new GetDefaultLayoutQuery());
        return View(layout);
    }
}
```

---

### Pattern 2: Service with LayoutHelper → Service with IMediator

#### Before
```csharp
public class SomeService
{
    private readonly ApplicationDbContext _dbContext;

    public SomeService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> InitializeAsync()
    {
        var hasLayout = await LayoutHelper.HasDefaultLayoutAsync(_dbContext);
        if (!hasLayout)
        {
            // Create default layout
        }
        return true;
    }
}
```

#### After
```csharp
public class SomeService
{
    private readonly IMediator _mediator;

    public SomeService(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<bool> InitializeAsync()
    {
        var hasLayout = await _mediator.QueryAsync(new CheckDefaultLayoutExistsQuery());
        if (!hasLayout)
        {
            // Create default layout (use command via mediator)
        }
        return true;
    }
}
```

---

### Pattern 3: ArticleViewModelBuilder Using LayoutHelper

**Note:** `ArticleViewModelBuilder` currently uses `LayoutHelper.GetCurrentDefaultLayoutAsync`. This will be migrated as part of Phase 2.

#### Current State
```csharp
// In ArticleViewModelBuilder.cs
private async Task<LayoutViewModel> GetDefaultLayoutAsync(TimeSpan? layoutCache = null)
{
    if (memoryCache == null || layoutCache == null)
    {
        var entity = await LayoutHelper.GetCurrentDefaultLayoutAsync(dbContext);
        return new LayoutViewModel(entity);
    }
    // ... caching logic
}
```

#### After Migration
```csharp
// ArticleViewModelBuilder will inject IMediator and use GetDefaultLayoutQuery
private readonly IMediator _mediator;

private async Task<LayoutViewModel> GetDefaultLayoutAsync(TimeSpan? layoutCache = null)
{
    return await _mediator.QueryAsync(new GetDefaultLayoutQuery(layoutCache));
}
```

---

## Migration Checklist

When migrating from `LayoutHelper` to CQRS:

1. ✅ **Add `IMediator` dependency** to your controller/service constructor
2. ✅ **Replace `LayoutHelper.GetCurrentDefaultLayoutAsync()` calls** with `GetDefaultLayoutQuery`
3. ✅ **Replace `LayoutHelper.HasDefaultLayoutAsync()` calls** with `CheckDefaultLayoutExistsQuery`
4. ✅ **Replace `LayoutHelper.GetLayoutByIdAsync()` calls** with `GetLayoutByIdQuery`
5. ✅ **Update using statements** to import query namespaces:
   - `Cosmos.Common.Features.Layouts.Queries`
   - `Cosmos.Common.Features.Shared`
6. ✅ **Remove `ApplicationDbContext` dependency** if only used for LayoutHelper
7. ✅ **Update tests** to mock `IMediator` instead of DbContext
8. ✅ **Handle LayoutViewModel vs Layout entity differences** (query returns ViewModel)

---

## Query Reference

### Available Queries

| Old Method | New Query | Returns | Namespace |
|------------|-----------|---------|-----------|
| `GetCurrentDefaultLayoutAsync(dbContext)` | `GetDefaultLayoutQuery(cacheDuration?)` | `LayoutViewModel` | `Cosmos.Common.Features.Layouts.Queries` |
| `HasDefaultLayoutAsync(dbContext)` | `CheckDefaultLayoutExistsQuery()` | `bool` | `Cosmos.Common.Features.Layouts.Queries` |
| `GetLayoutByIdAsync(dbContext, layoutId)` | `GetLayoutByIdQuery(layoutId)` | `Layout?` | `Cosmos.Common.Features.Layouts.Queries` |

---

## Important Differences

### 1. GetDefaultLayoutQuery Returns LayoutViewModel
The CQRS query returns `LayoutViewModel` (for presentation), while `LayoutHelper` returned the raw `Layout` entity.

**If you need the entity:**
- Option A: Use the obsolete method temporarily during migration
- Option B: Create a new query `GetDefaultLayoutEntityQuery` that returns `Layout`
- Option C: Convert your code to use `LayoutViewModel` instead

### 2. Built-in Caching
`GetDefaultLayoutQuery` has built-in caching when you provide `CacheDuration`:
```csharp
// With caching
var layout = await _mediator.QueryAsync(new GetDefaultLayoutQuery(TimeSpan.FromMinutes(10)));

// Without caching
var layout = await _mediator.QueryAsync(new GetDefaultLayoutQuery());
```

---

## Timeline

- **Now**: All methods marked `[Obsolete]` - warnings generated at compile time
- **Phase 2 (current)**: Gradual migration of call sites, both patterns supported
- **Phase 3**: Update tests to use CQRS pattern
- **Phase 4 (future)**: `LayoutHelper` class removed (breaking change, major version bump)

---

## Questions?

See `MODERNIZATION_RECOMMENDATIONS.md` for architectural rationale and implementation phases.

**Document Version:** 1.0  
**Last Updated:** 2025-01-11  
**Status:** Active Migration Guide (Phase 2)
