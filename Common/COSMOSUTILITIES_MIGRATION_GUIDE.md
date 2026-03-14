# CosmosUtilities to CQRS Migration Guide

## Overview
The `CosmosUtilities` static class is being phased out in favor of CQRS queries using the `IMediator` interface. This guide provides migration examples for all replaced methods.

---

## Why Migrate?

### Before (CosmosUtilities - Problems)
```csharp
// Static methods require passing dependencies explicitly
using Cosmos.Common;

var isAuthorized = await CosmosUtilities.AuthUser(dbContext, user, articleNumber);
var folderContents = await CosmosUtilities.GetArticleFolderContents(storageContext, articleNumber, path);
var articles = await CosmosUtilities.GetArticlesForUser(dbContext, user);
```

**Issues:**
- ❌ Cannot be mocked for unit testing
- ❌ Requires explicit `ApplicationDbContext` and `IStorageContext` references
- ❌ No dependency injection support
- ❌ Difficult to extend or add cross-cutting concerns (logging, caching, validation)

### After (CQRS - Benefits)
```csharp
// Clean, injectable, testable
using Cosmos.Common.Features.Articles.Queries;

var isAuthorized = await _mediator.QueryAsync(new AuthorizeUserForArticleQuery(user, articleNumber));
var folderContents = await _mediator.QueryAsync(new GetArticleFolderContentsQuery(articleNumber, path));
var articles = await _mediator.QueryAsync(new GetArticlesForUserQuery(user));
```

**Benefits:**
- ✅ Easy to mock `IMediator` in tests
- ✅ Supports cross-cutting concerns (logging, validation, authorization)
- ✅ Consistent with CQRS architecture
- ✅ Dependencies managed by DI container

---

## Migration Examples

### 1. AuthUser()

#### Before
```csharp
using Cosmos.Common;

// Check if user has permission to view article
var hasAccess = await CosmosUtilities.AuthUser(dbContext, User, articleNumber);
if (!hasAccess)
{
    return Forbid();
}
```

#### After
```csharp
using Cosmos.Common.Features.Articles.Queries;
using Cosmos.Common.Features.Shared;

// Inject IMediator via constructor
private readonly IMediator _mediator;

public SomeController(IMediator mediator)
{
    _mediator = mediator;
}

// Use query
var hasAccess = await _mediator.QueryAsync(new AuthorizeUserForArticleQuery(User, articleNumber));
if (!hasAccess)
{
    return Forbid();
}
```

**What It Does:**
- Checks article permissions (anonymous, authenticated, user-specific, role-based)
- Returns `true` if user has access, `false` otherwise

---

### 2. GetArticleFolderContents()

#### Before
```csharp
using Cosmos.Common;

var folderContents = await CosmosUtilities.GetArticleFolderContents(
    storageContext,
    articleNumber,
    path: "images");
```

#### After
```csharp
using Cosmos.Common.Features.Articles.Queries;

var folderContents = await _mediator.QueryAsync(
    new GetArticleFolderContentsQuery(
        ArticleNumber: articleNumber,
        Path: "images"));
```

**What It Does:**
- Retrieves file and folder metadata from article storage
- Path format: `/pub/articles/{articleNumber}/{path}`
- Returns `List<FileManagerEntry>` (from `Cosmos.BlobService`)

**Important:** This query does NOT authenticate the user. You must check permissions separately using `AuthorizeUserForArticleQuery`.

---

### 3. GetArticlesForUser()

#### Before
```csharp
using Cosmos.Common;

var userArticles = await CosmosUtilities.GetArticlesForUser(dbContext, User);
foreach (var article in userArticles)
{
    // Display article
}
```

#### After
```csharp
using Cosmos.Common.Features.Articles.Queries;

var userArticles = await _mediator.QueryAsync(new GetArticlesForUserQuery(User));
foreach (var article in userArticles)
{
    // Display article
}
```

**What It Does:**
- Retrieves all articles accessible to the user based on roles and permissions
- Includes articles with no permissions (public) or matching user/role permissions
- Returns `List<TableOfContentsItem>` with article metadata (title, URL, published date, etc.)

---

## Testing Benefits

### Before (CosmosUtilities)
```csharp
// Complex mock setup required
[Fact]
public async Task TestArticleAuthorization()
{
    // Must mock DbContext, DbSets, and configure complex query chains
    var mockDbContext = new Mock<ApplicationDbContext>();
    var mockArticles = new Mock<DbSet<CatalogEntry>>();
    var mockRoles = new Mock<DbSet<IdentityRole>>();
    var mockUserRoles = new Mock<DbSet<IdentityUserRole<string>>>();
    // ... many more mocks
    
    mockDbContext.Setup(x => x.ArticleCatalog).Returns(mockArticles.Object);
    mockDbContext.Setup(x => x.Roles).Returns(mockRoles.Object);
    // ... many more setups
    
    var hasAccess = await CosmosUtilities.AuthUser(mockDbContext.Object, user, articleNumber);
    
    Assert.True(hasAccess);
}
```

### After (CQRS)
```csharp
// Simple mock of IMediator
[Fact]
public async Task TestArticleAuthorization()
{
    // Single mock - much cleaner!
    var mockMediator = new Mock<IMediator>();
    mockMediator
        .Setup(x => x.QueryAsync(It.IsAny<AuthorizeUserForArticleQuery>(), default))
        .ReturnsAsync(true);
    
    var controller = new SomeController(mockMediator.Object);
    var result = await controller.ViewArticle(articleNumber);
    
    Assert.IsType<ViewResult>(result);
    mockMediator.Verify(x => x.QueryAsync(It.IsAny<AuthorizeUserForArticleQuery>(), default), Times.Once);
}
```

---

## Common Migration Patterns

### Pattern 1: Publisher Controller with Authorization Check

#### Before
```csharp
public class HomeController : PubControllerBase
{
    public async Task<IActionResult> Index(string path)
    {
        var article = await GetArticleByPath(path);
        
        // Check authorization
        var hasAccess = await CosmosUtilities.AuthUser(DbContext, User, article.ArticleNumber);
        if (!hasAccess)
        {
            return Forbid();
        }
        
        return View(article);
    }
}
```

#### After
```csharp
public class HomeController : PubControllerBase
{
    private readonly IMediator _mediator;

    public HomeController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IActionResult> Index(string path)
    {
        var article = await GetArticleByPath(path);
        
        // Check authorization via CQRS
        var hasAccess = await _mediator.QueryAsync(
            new AuthorizeUserForArticleQuery(User, article.ArticleNumber));
        
        if (!hasAccess)
        {
            return Forbid();
        }
        
        return View(article);
    }
}
```

---

### Pattern 2: Editor Controller with File Management

#### Before
```csharp
public class FileController : Controller
{
    private readonly IStorageContext _storageContext;

    public async Task<IActionResult> GetFolderContents(int articleNumber, string path = "")
    {
        var contents = await CosmosUtilities.GetArticleFolderContents(
            _storageContext,
            articleNumber,
            path);
        
        return Json(contents);
    }
}
```

#### After
```csharp
public class FileController : Controller
{
    private readonly IMediator _mediator;

    public FileController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IActionResult> GetFolderContents(int articleNumber, string path = "")
    {
        var contents = await _mediator.QueryAsync(
            new GetArticleFolderContentsQuery(articleNumber, path));
        
        return Json(contents);
    }
}
```

---

### Pattern 3: Getting User-Accessible Articles

#### Before
```csharp
public async Task<List<TableOfContentsItem>> GetMyArticles()
{
    var articles = await CosmosUtilities.GetArticlesForUser(dbContext, User);
    return articles;
}
```

#### After
```csharp
public async Task<List<TableOfContentsItem>> GetMyArticles()
{
    var articles = await _mediator.QueryAsync(new GetArticlesForUserQuery(User));
    return articles;
}
```

---

## Migration Checklist

When migrating from `CosmosUtilities` to CQRS:

1. ✅ **Add `IMediator` dependency** to your controller/service constructor
2. ✅ **Replace `CosmosUtilities.AuthUser()` calls** with `AuthorizeUserForArticleQuery`
3. ✅ **Replace `CosmosUtilities.GetArticleFolderContents()` calls** with `GetArticleFolderContentsQuery`
4. ✅ **Replace `CosmosUtilities.GetArticlesForUser()` calls** with `GetArticlesForUserQuery`
5. ✅ **Update using statements** to import query namespaces:
   - `Cosmos.Common.Features.Articles.Queries`
   - `Cosmos.Common.Features.Shared`
6. ✅ **Remove `ApplicationDbContext` or `IStorageContext` dependencies** if only used for CosmosUtilities
7. ✅ **Update tests** to mock `IMediator` instead of DbContext/StorageContext
8. ✅ **Verify authorization logic** - ensure permissions are checked before accessing article content

---

## Query Reference

### Available Queries

| Old Method | New Query | Parameters | Returns | Namespace |
|------------|-----------|------------|---------|-----------|
| `AuthUser(dbContext, user, articleNumber)` | `AuthorizeUserForArticleQuery` | `User`, `ArticleNumber` | `bool` | `Cosmos.Common.Features.Articles.Queries` |
| `GetArticleFolderContents(storageContext, articleNumber, path)` | `GetArticleFolderContentsQuery` | `ArticleNumber`, `Path` | `List<FileManagerEntry>` | `Cosmos.Common.Features.Articles.Queries` |
| `GetArticlesForUser(dbContext, user)` | `GetArticlesForUserQuery` | `User` | `List<TableOfContentsItem>` | `Cosmos.Common.Features.Articles.Queries` |

---

## Important Implementation Notes

### 1. AuthorizeUserForArticleQuery Uses ApplicationDbContext
This handler requires `ApplicationDbContext` (not `IApplicationDbContext`) because it needs access to:
- `UserRoles` DbSet (not exposed in interface)
- `Roles` DbSet (not exposed in interface)

**Implication:** The query handler cannot be easily unit tested without a real database or in-memory provider.

**Alternative:** Mock `IMediator` in consuming code instead of mocking the handler directly.

---

### 2. GetArticleFolderContentsQuery Does NOT Authorize
This query retrieves storage contents but does NOT check user permissions.

**Security:** Always call `AuthorizeUserForArticleQuery` before `GetArticleFolderContentsQuery` in public-facing code.

**Example:**
```csharp
// ✅ Correct - check permissions first
var hasAccess = await _mediator.QueryAsync(new AuthorizeUserForArticleQuery(User, articleNumber));
if (!hasAccess)
{
    return Forbid();
}

var contents = await _mediator.QueryAsync(new GetArticleFolderContentsQuery(articleNumber, path));
return Json(contents);
```

```csharp
// ❌ Incorrect - security vulnerability
var contents = await _mediator.QueryAsync(new GetArticleFolderContentsQuery(articleNumber, path));
return Json(contents); // Anyone can access!
```

---

### 3. GetArticlesForUserQuery Filters by Permissions
This query automatically filters articles based on user's roles and permissions.

**What's Included:**
- Articles with no permissions (public/anonymous access)
- Articles where user is explicitly granted access
- Articles where user's roles are granted access

**What's Excluded:**
- Articles with permissions that don't match the user's roles or identity

---

## Timeline

- **Now**: All methods marked `[Obsolete]` - warnings generated at compile time
- **Phase 2 (current)**: Gradual migration of call sites, both patterns supported
- **Phase 3**: Update tests to use CQRS pattern
- **Phase 4 (future)**: `CosmosUtilities` class removed (breaking change, major version bump)

---

## Questions?

See `MODERNIZATION_RECOMMENDATIONS.md` for architectural rationale and implementation phases.

**Document Version:** 1.0  
**Last Updated:** 2025-01-11  
**Status:** Active Migration Guide (Phase 2)
