# Blog Controller Blog Post Tests - Debugging Session Handoff

**Date**: 2024
**Test**: `CompleteLifecycle_CreateEditDelete_BlogPost`
**File**: `Tests\Features\Blogs\BlogControllerBlogPostTests.cs`
**Status**: ?? In Progress - Test partially fixed but still failing

---

## Executive Summary

We were debugging a `NullReferenceException` in the blog post lifecycle integration test. The original issue was that the test didn't create an actual `IdentityUser` in the database, causing `UserManager.GetUserAsync()` to return null. We've fixed the original issue and several cascading problems, but the test still needs final corrections.

---

## Application Architecture Context

### SkyCMS Multi-Tenant Architecture

#### Key Components
1. **Tenant Resolution**
   - `IDynamicConfigurationProvider` (singleton) resolves tenants via headers
   - Priority: `x-origin-hostname` header > `Host` header
   - `DomainMiddleware` establishes tenant context early in the pipeline

2. **Per-Request Scoped Services**
   - Services inject `IDynamicConfigurationProvider` to get current tenant
   - Cookie isolation via `CookieDomain` claims in `Sky.Editor`

3. **Vertical Slice Architecture (CQRS Pattern)**
   - Commands and queries handled by dedicated handlers
   - Mediator pattern (`IMediator`) dispatches commands to handlers
   - Each feature has its own folder with commands, handlers, and results

#### Blog Architecture

**Blog Streams** (parent container):
- Represents a blog collection (e.g., "Tech Blog")
- Has a unique `BlogKey` (e.g., "tech-blog")
- Article type: `ArticleType.BlogStream`
- Managed via `GetBlogStreamQuery`, `UpdateBlogStreamCommand`, `DeleteBlogStreamCommand`

**Blog Posts** (children):
- Individual posts within a blog stream
- Have a `BlogKey` referencing parent stream
- Article type: `ArticleType.BlogPost`
- Managed via dedicated blog post commands:
  - `CreateBlogPostCommand` ? `CreateBlogPostCommandHandler`
  - `UpdateBlogPostCommand` ? `UpdateBlogPostCommandHandler`
  - `DeleteBlogPostCommand` ? `DeleteBlogPostCommandHandler`

#### Article Entity Key Fields
```csharp
public class Article
{
    public Guid Id { get; set; }                    // Primary key
    public int ArticleNumber { get; set; }          // Logical identifier (shared across versions)
    public int VersionNumber { get; set; }          // Version counter
    public string BlogKey { get; set; }             // Parent blog stream identifier
    public int ArticleType { get; set; }            // BlogStream, BlogPost, Page, etc.
    public int StatusCode { get; set; }             // Active, Deleted, etc.
    public string UserId { get; set; }              // Creator/editor
    // ... other fields
}
```

**Important**: 
- Primary key is `Guid Id`
- `ArticleNumber` is a logical identifier used for routing
- Queries by `ArticleNumber` need `.Where()` or `.FirstOrDefaultAsync()`, NOT `.FindAsync()`

---

## Original Problem

### Test Failure
```
Test method Sky.Tests.Features.Blogs.BlogControllerBlogPostTests.CompleteLifecycle_CreateEditDelete_BlogPost threw exception:
System.NullReferenceException: Object reference not set to an instance of an object.
```

### Stack Trace Analysis
```
at Sky.Cms.Controllers.BaseController.GetUserId() in Editor\Controllers\BaseController.cs:line 116
at Sky.Editor.Controllers.BlogController.CreateEntry(String blogKey, String title) in Editor\Controllers\BlogController.cs:line 413
```

### Root Cause
In `BaseController.GetUserId()`:
```csharp
protected async Task<string> GetUserId()
{
    var user = await baseUserManager.GetUserAsync(User);
    return user.Id;  // ? user was null, causing NullReferenceException
}
```

The test created a `ClaimsPrincipal` with a `NameIdentifier` claim but never created the corresponding `IdentityUser` in the database. When `UserManager.GetUserAsync(User)` looked up the user by the claim, it returned `null`.

---

## Fixes Applied

### ? Fix #1: Create Test User in Database

**File**: `Tests\Features\Blogs\BlogControllerBlogPostTests.cs`

**Changes**:
1. Added `using Microsoft.AspNetCore.Identity;`
2. Added `using Microsoft.AspNetCore.Routing;`
3. In `Setup()` method, added user creation:

```csharp
[TestInitialize]
public async Task Setup()
{
    InitializeTestContext(seedLayout: true);
    
    // ? Add a user to the in-memory database so UserManager can find it
    var testUser = new IdentityUser { Id = TestUserId.ToString(), UserName = "testuser" };
    Db.Users.Add(testUser);
    await Db.SaveChangesAsync();
    
    // Ensure templates exist
    await TemplateService.EnsureDefaultTemplatesExistAsync();
    // ... rest of setup
}
```

**Result**: Fixed the original `NullReferenceException` ?

---

### ? Fix #2: Register Blog Post Command Handlers

**Problem**: After fixing the NullReferenceException, test hit:
```
System.InvalidOperationException: No service for type 'ICommandHandler<CreateBlogPostCommand, CommandResult<CreateBlogPostCommandResult>>' has been registered.
```

**Root Cause**: The blog post command handlers were never registered in the test DI container.

**File**: `Tests\Infrastructure\SkyCmsTestBase.cs`

**Changes**: Added handler registrations to service collection (around line 406-430):

```csharp
serviceCollection
    .AddSingleton<IBlogStreamRenderingService>(BlogStreamRenderingService)
    .AddSingleton<IAuthorInfoService>(AuthorInfoService)
    .AddScoped<IViewRenderService>(sp => ViewRenderService)
    .AddSingleton<IReservedPaths>(ReservedPaths)
    .AddSingleton<IEditorSettings>(EditorSettings)
    .AddHttpClient()
    .AddSingleton<IMediator, Cosmos.Common.Features.Shared.Mediator>();

// ? Register blog post command handlers
serviceCollection.AddScoped<ICommandHandler<CreateBlogPostCommand, CommandResult<CreateBlogPostCommandResult>>>(sp =>
    new CreateBlogPostCommandHandler(
        Db,
        SlugService,
        new NullLogger<CreateBlogPostCommandHandler>()));

serviceCollection.AddScoped<ICommandHandler<UpdateBlogPostCommand, CommandResult<UpdateBlogPostCommandResult>>>(sp =>
    new UpdateBlogPostCommandHandler(
        Db,
        new NullLogger<UpdateBlogPostCommandHandler>()));

serviceCollection.AddScoped<ICommandHandler<DeleteBlogPostCommand, CommandResult<DeleteBlogPostCommandResult>>>(sp =>
    new DeleteBlogPostCommandHandler(
        Db,
        new NullLogger<DeleteBlogPostCommandHandler>()));

Services = serviceCollection.BuildServiceProvider();
```

**Result**: Blog post creation command now executes successfully ?

---

### ? Fix #3: Mock IUrlHelper for RedirectToAction

**Problem**: After command execution succeeded, test hit:
```
System.InvalidOperationException: No service for type 'Microsoft.AspNetCore.Mvc.Routing.IUrlHelperFactory' has been registered.
```

**Root Cause**: `BlogController.CreateEntry()` returns `RedirectToAction("Edit", "Editor", new { id = result.Data.ArticleNumber })`, which requires MVC routing infrastructure.

**File**: `Tests\Features\Blogs\BlogControllerBlogPostTests.cs`

**Changes**: Added URL helper mock in `Setup()`:

```csharp
controller.ControllerContext = new ControllerContext
{
    HttpContext = httpContext
};

// ? Mock IUrlHelper to support RedirectToAction
var mockUrlHelper = new Mock<Microsoft.AspNetCore.Mvc.IUrlHelper>();
mockUrlHelper.Setup(x => x.ActionContext).Returns(new Microsoft.AspNetCore.Mvc.ActionContext
{
    HttpContext = httpContext,
    RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
    ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()
});
mockUrlHelper.Setup(x => x.Action(It.IsAny<Microsoft.AspNetCore.Mvc.Routing.UrlActionContext>()))
    .Returns("/mock/url");
controller.Url = mockUrlHelper.Object;
```

**Result**: `RedirectToAction` now works without requiring full MVC infrastructure ?

---

## Current Status: ?? STILL FAILING

### Current Error
```
System.ArgumentException: The key value at position 0 of the call to 'DbSet<Article>.Find' was of type 'int', which does not match the property type of 'Guid'.
```

**Location**: `Tests\Features\Blogs\BlogControllerBlogPostTests.cs:475`

```csharp
var createdPost = await Db.Articles.FindAsync((int)articleNumber);  // ? Wrong
```

### Problem Analysis

The test is using `.FindAsync()` with an `int` (ArticleNumber), but:
- `Article.Id` (Guid) is the primary key
- `Article.ArticleNumber` (int) is a logical identifier, NOT the primary key
- `.FindAsync()` only works with primary keys

**Three places need fixing**:
1. Line 475: `var createdPost = await Db.Articles.FindAsync((int)articleNumber);`
2. Line 493: `var editedPost = await Db.Articles.FindAsync((int)articleNumber);`
3. Line 500: `var deletedPost = await Db.Articles.FindAsync((int)articleNumber);`

---

## Next Steps: REQUIRED FIXES

### ?? Fix #4: Correct Article Queries by ArticleNumber

**File**: `Tests\Features\Blogs\BlogControllerBlogPostTests.cs`

**Change all three `.FindAsync()` calls to use `.FirstOrDefaultAsync()` with a filter**:

#### Line 475 - After Create
```csharp
// ? WRONG (current)
var createdPost = await Db.Articles.FindAsync((int)articleNumber);

// ? CORRECT (change to)
var createdPost = await Db.Articles
    .Where(a => a.ArticleNumber == articleNumber)
    .OrderByDescending(a => a.VersionNumber)
    .FirstOrDefaultAsync();
```

#### Line 493 - After Edit
```csharp
// ? WRONG (current)
var editedPost = await Db.Articles.FindAsync((int)articleNumber);

// ? CORRECT (change to)
var editedPost = await Db.Articles
    .Where(a => a.ArticleNumber == articleNumber)
    .OrderByDescending(a => a.VersionNumber)
    .FirstOrDefaultAsync();
```

#### Line 500 - After Delete
```csharp
// ? WRONG (current)
var deletedPost = await Db.Articles.FindAsync((int)articleNumber);

// ? CORRECT (change to)
var deletedPost = await Db.Articles
    .Where(a => a.ArticleNumber == articleNumber)
    .OrderByDescending(a => a.VersionNumber)
    .FirstOrDefaultAsync();
```

**Why `.OrderByDescending(a => a.VersionNumber)`?**
- Blog posts create new versions on each edit
- We want the LATEST version of the article
- The latest version has the highest `VersionNumber`

---

## Additional Considerations

### ?? Potential Issue: Edit Operation

The `EditEntry` method uses `UpdateBlogPostCommand` which creates a NEW version of the article. After editing:
- `ArticleNumber` stays the same (e.g., 2)
- `VersionNumber` increments (1 ? 2)
- A new `Id` (Guid) is generated

**Implication**: When we query for the edited post, we need to get the HIGHEST version number, not just any version.

### ?? Potential Issue: Delete Operation

The `DeleteBlogPostCommand` marks ALL versions of an article as deleted:

```csharp
// From DeleteBlogPostCommandHandler.cs
var articleVersions = await dbContext.Articles
    .Where(a => a.ArticleNumber == command.ArticleNumber &&
                a.ArticleType == (int)ArticleType.BlogPost &&
                a.BlogKey == command.BlogKey &&
                a.StatusCode != (int)StatusCodeEnum.Deleted)
    .ToListAsync(cancellationToken);

foreach (var version in articleVersions)
{
    version.StatusCode = (int)StatusCodeEnum.Deleted;
    version.Updated = DateTimeOffset.UtcNow;
}
```

So after deletion, the test can still query for the article, but needs to verify `StatusCode == (int)StatusCodeEnum.Deleted`.

---

## Testing Strategy

### Test Flow
1. **Create** blog post ? Verify title, status = Active
2. **Edit** blog post ? Verify updated title (new version created)
3. **Delete** blog post ? Verify status = Deleted (all versions marked)

### Expected Article Versions After Test
- After Create: 1 version (ArticleNumber=2, VersionNumber=1, Title="Lifecycle Test")
- After Edit: 2 versions (ArticleNumber=2, VersionNumber=2, Title="Updated Lifecycle Test")
- After Delete: 2 versions (both marked StatusCode=Deleted)

---

## Production Code Impact

### ?? IMPORTANT: Handlers NOT Registered in Production

The blog post command handlers are **NOT REGISTERED** in `Editor\Program.cs`!

**File**: `Editor\Program.cs` (around line 565-575)

**Currently Registered** (blog STREAM handlers):
```csharp
builder.Services.AddScoped<IQueryHandler<GetBlogStreamQuery, CommandResult<GetBlogStreamQueryResult>>, GetBlogStreamQueryHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateBlogStreamCommand, CommandResult<Article>>, UpdateBlogStreamHandler>();
builder.Services.AddScoped<ICommandHandler<DeleteBlogStreamCommand, CommandResult<bool>>, DeleteBlogStreamHandler>();
```

**MISSING** (blog POST handlers):
```csharp
// ? NOT REGISTERED - NEED TO ADD:
builder.Services.AddScoped<ICommandHandler<CreateBlogPostCommand, CommandResult<CreateBlogPostCommandResult>>, CreateBlogPostCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateBlogPostCommand, CommandResult<UpdateBlogPostCommandResult>>, UpdateBlogPostCommandHandler>();
builder.Services.AddScoped<ICommandHandler<DeleteBlogPostCommand, CommandResult<DeleteBlogPostCommandResult>>, DeleteBlogPostCommandHandler>();
```

**Action Required**: After tests pass, add these registrations to `Program.cs` so the production app can use the blog post handlers!

---

## Files Modified

### Test Files
1. ? `Tests\Features\Blogs\BlogControllerBlogPostTests.cs`
   - Added test user creation
   - Added URL helper mock
   - **TODO**: Fix `.FindAsync()` calls

2. ? `Tests\Infrastructure\SkyCmsTestBase.cs`
   - Registered blog post command handlers

### Production Files (Pending)
3. ?? `Editor\Program.cs`
   - **TODO**: Register blog post command handlers

---

## Quick Start for Next Session

### Option 1: Quick Fix (10 minutes)
```bash
# 1. Open the test file
code Tests\Features\Blogs\BlogControllerBlogPostTests.cs

# 2. Replace the 3 FindAsync calls (lines 475, 493, 500) with the correct queries shown above

# 3. Run the test
dotnet test --filter "FullyQualifiedName~CompleteLifecycle_CreateEditDelete_BlogPost"

# 4. If test passes, register handlers in Program.cs
code Editor\Program.cs
# Add the 3 handler registrations shown above (around line 575)
```

### Option 2: Use Debugger (20 minutes)
```bash
# Set breakpoints and validate data
# Line 475 - Check createdPost
# Line 493 - Check editedPost (verify new version created)
# Line 500 - Check deletedPost (verify StatusCode = Deleted)
```

---

## Reference: Command Handler Dependencies

### CreateBlogPostCommandHandler
- `ApplicationDbContext` - Database access
- `ISlugService` - URL slug generation
- `ILogger<CreateBlogPostCommandHandler>` - Logging

### UpdateBlogPostCommandHandler
- `ApplicationDbContext` - Database access
- `ILogger<UpdateBlogPostCommandHandler>` - Logging

### DeleteBlogPostCommandHandler
- `ApplicationDbContext` - Database access
- `ILogger<DeleteBlogPostCommandHandler>` - Logging

---

## Architecture Notes

### Why Vertical Slice (CQRS)?
- **Separation of concerns**: Blog posts have different rules than generic articles
- **Testability**: Each handler can be tested in isolation
- **Maintainability**: Changes to blog posts don't affect other article types
- **Clarity**: Intent is explicit (CreateBlogPost vs CreateArticle)

### Pattern Used
```
Feature Request
    ?
Controller Action
    ?
Create Command Object
    ?
Mediator.SendAsync(command)
    ?
Resolve Handler from DI
    ?
Handler.HandleAsync(command)
    ?
Return CommandResult<TData>
    ?
Controller processes result
```

---

## Key Learnings

1. **Test User Creation**: Integration tests using `UserManager` need actual users in the database, not just claims.

2. **MVC Routing in Tests**: `RedirectToAction` requires `IUrlHelper` - mock it rather than registering full MVC infrastructure.

3. **Primary Key vs Logical Key**: 
   - `.FindAsync()` = primary key only
   - `.Where().FirstOrDefaultAsync()` = any field

4. **Versioning**: Blog post edits create NEW versions - always query with `.OrderByDescending(a => a.VersionNumber)` to get latest.

5. **DI Registration**: Both test and production code need handler registrations!

---

## Success Criteria

? Test passes without exceptions  
? Created blog post exists in database  
? Edited blog post has new version with updated title  
? Deleted blog post has StatusCode = Deleted  
?? Blog post handlers registered in Program.cs (production)  

---

## Contact Info / Handoff Notes

**Date**: 2024  
**Test Status**: ?? 3 line changes needed to complete fix  
**Estimated Time to Complete**: 10-15 minutes  
**Confidence Level**: High - clear path forward  

**Last Known State**: Test runs through creation successfully, blog post is created with ArticleNumber=2, but query fails due to incorrect use of `.FindAsync()`.

Good luck! ??
