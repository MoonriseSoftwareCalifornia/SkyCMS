# 🎯 ArticleEditLogic Final Migration - Action Plan

## **Current Status**

You have **2 methods still actively being called** from `ArticleEditLogic` in the controller:

1. **Line 1013:** `await articleLogic.CreateHomePage(model);` → EditorController.NewHome()
2. **Line 1965:** `article = await articleCreateArticleAsync("Blank Page", userId);` → EditorController.ExportPage()

All other methods have already been migrated to commands or are ready to migrate.

---

## **🚀 Immediate Actions Required**

### **1. Replace EditorController.NewHome() - Line 1013**

**Current Code (Line 1000-1016):**
```csharp
public async Task<IActionResult> NewHome(NewHomeViewModel model)
{
    if (model == null)
    {
        return NotFound();
    }

    if (!ModelState.IsValid)
    {
        return View(model);
    }

    var user = await userManager.GetUserAsync(User);
    await articleLogic.CreateHomePage(model);  // ← REPLACE THIS

    return RedirectToAction("Index");
}
```

**Migration (Use CreateHomePageCommand):**
```csharp
public async Task<IActionResult> NewHome(NewHomeViewModel model)
{
    if (model == null)
    {
        return NotFound();
    }

    if (!ModelState.IsValid)
    {
        return View(model);
    }

    var command = new CreateHomePageCommand
    {
        ArticleNumber = model.ArticleNumber,
        Title = model.Title
    };

    var result = await mediator.SendAsync(command);
    
    if (!result.IsSuccess)
    {
        ModelState.AddModelError(string.Empty, result.ErrorMessage);
        return View(model);
    }

    return RedirectToAction("Index");
}
```

---

### **2. Replace EditorController.ExportPage() - Line 1965**

**Current Code (Line 1946-1977):**
```csharp
public async Task<IActionResult> ExportPage(Guid? id)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(ModelState);
    }

    ArticleViewModel article;
    var userId = Guid.Parse(await GetUserId());
    if (id.HasValue)
    {
        article = await mediator.QueryAsync(new GetArticleByIdQuery
        {
            Id = id.Value
        });
    }
    else
    {
        // Get the user's ID for logging.
        article = await articleCreateArticleAsync("Blank Page", userId);  // ← REPLACE THIS
    }

    var html = await articleLogic.ExportArticle(article, viewRenderService);
    // ... rest of method
}
```

**Migration (Use CreateArticleCommand + ExportArticleQuery):**
```csharp
public async Task<IActionResult> ExportPage(Guid? id)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(ModelState);
    }

    ArticleViewModel article;
    var userId = Guid.Parse(await GetUserId());
    
    if (id.HasValue)
    {
        article = await mediator.QueryAsync(new GetArticleByIdQuery
        {
            Id = id.Value
        });
    }
    else
    {
        // Create temporary blank page for export
        var command = new CreateArticleCommand
        {
            Title = "Blank Page",
            UserId = userId,
            ArticleType = ArticleType.General,
            BlogKey = string.Empty,
            TemplateId = null
        };

        var result = await mediator.SendAsync<CommandResult<ArticleViewModel>>(command);
        
        if (!result.IsSuccess)
        {
            return BadRequest(result.ErrorMessage);
        }

        article = result.Data;
    }

    var html = await articleLogic.ExportArticle(article, viewRenderService);
    // ... rest of method
}
```

---

## **📋 ArticleEditLogic Methods - Complete Migration Checklist**

### **Methods to Mark as `[Obsolete]`**

- [x] **CreateArticle()** - Has CreateArticleCommand/Handler
  - Mark: `[Obsolete("Use CreateArticleCommand via mediator.", error: false)]`
  - Keep until ExportPage is migrated

- [x] **SaveArticle()** - Has SaveArticleCommand/Handler
  - Mark: `[Obsolete("Use SaveArticleCommand via mediator.", error: false)]`
  - Safe to deprecate now (not directly called in controller)

- [x] **PublishArticle()** - Has PublishArticleCommand/Handler
  - Mark: `[Obsolete("Use PublishArticleCommand via mediator.", error: false)]`
  - Check if called from CreateHomePage handler

- [x] **DeleteArticle()** - Has DeleteArticleCommand/Handler
  - Mark: `[Obsolete("Use DeleteArticleCommand via mediator.", error: false)]`
  - Safe to deprecate now

- [x] **RestoreArticle()** - Has RestoreArticleCommand/Handler
  - Mark: `[Obsolete("Use RestoreArticleCommand via mediator.", error: false)]`
  - Safe to deprecate now

- [x] **NewVersion()** - Has CreateArticleVersionCommand/Handler
  - Mark: `[Obsolete("Use CreateArticleVersionCommand via mediator.", error: false)]`
  - Safe to deprecate now

- [x] **CreateHomePage()** - Has CreateHomePageCommand/Handler
  - Mark: `[Obsolete("Use CreateHomePageCommand via mediator.", error: false)]`
  - After EditorController.NewHome() is updated

### **Methods That Can Stay (Read Operations)**

- ✅ **ExportArticle()** - Can stay as-is (read operation, not write)
  - Consider: Move to `ExportArticleService` or create `ExportArticleQuery`
  - For now: Can keep in ArticleEditLogic

---

## **🔧 Handler Internal Calls to Fix**

In `CreateHomePageHandler`, check if it calls:
- `PublishArticle()` - Should use PublishArticleCommand instead
- `UpsertCatalogEntry()` - This is private, keep as-is

---

## **📝 Step-by-Step Instructions**

### **Step 1: Update EditorController.NewHome()**
Replace line 1013 with the CreateHomePageCommand usage above.

### **Step 2: Update EditorController.ExportPage()**
Replace line 1965 with the CreateArticleCommand usage above.

### **Step 3: Mark ArticleEditLogic Methods as Obsolete**
Add `[Obsolete(...)]` attributes to:
- CreateArticle()
- SaveArticle()
- PublishArticle()
- DeleteArticle()
- RestoreArticle()
- NewVersion()
- CreateHomePage()

### **Step 4: Check Handler Usage**
Verify that:
- `PublishArticleCommand` handler doesn't call `PublishArticle()` from logic
- `DeleteArticleCommand` handler doesn't call `DeleteArticle()` from logic
- `RestoreArticleCommand` handler doesn't call `RestoreArticle()` from logic
- `CreateArticleVersionCommand` handler doesn't call `NewVersion()` from logic
- `CreateHomePageCommand` handler doesn't call `CreateHomePage()` from logic

### **Step 5: Build & Test**
- Run `dotnet build` to verify no compile errors
- Run tests to verify behavior unchanged
- Search for remaining `articleLogic.` calls to catch any misses

---

## **📊 Final State After Migration**

### **ArticleEditLogic will contain:**
✅ Private helpers:
- `GetAuthorInfoForUserId()` - Private
- `DeleteStaticWebpage()` - Private
- `UpsertCatalogEntry()` - Private
- `DeleteCatalogEntry()` - Private

✅ Read operations (can deprecate):
- `ExportArticle()` - Should become Query or Service

❌ Obsolete write operations (deprecated):
- CreateArticle()
- SaveArticle()
- PublishArticle()
- DeleteArticle()
- RestoreArticle()
- NewVersion()
- CreateHomePage()

### **EditorController will use:**
✅ Mediator for all commands:
- CreateArticleCommand
- SaveArticleCommand
- PublishArticleCommand
- DeleteArticleCommand
- RestoreArticleCommand
- CreateArticleVersionCommand
- CreateHomePageCommand

✅ Mediator for queries:
- GetArticleByIdQuery
- GetArticleByArticleNumberQuery
- GetArticleByUrlQuery
- etc.

---

## **🎯 Quick Summary**

| File | Change | Command | Status |
|------|--------|---------|--------|
| EditorController.NewHome() | Replace articleLogic call | CreateHomePageCommand | Ready |
| EditorController.ExportPage() | Replace articleLogic call | CreateArticleCommand | Ready |
| ArticleEditLogic | Mark methods obsolete | All 7 methods | Ready |
| Handlers | Verify no direct calls | All handlers | Verify |

**Total Impact:** 2 controller methods to update, 7 logic methods to deprecate, all handlers already implemented! ✅
