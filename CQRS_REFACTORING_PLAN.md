# ArticleEditLogic - CQRS Refactoring Plan

## Current Status
ArticleEditLogic still contains mixed concerns. While read operations have been migrated to query handlers, several write operations remain as direct methods.

---

## Write Operations to Migrate to Commands

### ? ALREADY MIGRATED
1. **CreateArticleCommand** ? (Handler exists)
   - Location: `Editor/Features/Articles/Create/`
   - Status: DONE - marked `[Obsolete]`

2. **SaveArticleCommand** ? (Handler exists)
   - Location: `Editor/Features/Articles/Save/`
   - Status: DONE - marked `[Obsolete]`

3. **CloneArticleCommand** ? (Handler exists)
   - Location: `Editor/Features/Articles/Clone/`
   - Status: Implemented

---

## Methods Still Needing Migration to Commands

### ? PRIORITY 1 - Core Operations

#### 1. `PublishArticle(Guid articleId, DateTimeOffset? dateTime)`
- **Type:** Command (Write Operation)
- **Current Location:** Line 1166 in ArticleEditLogic
- **Suggested Command:** `PublishArticleCommand`
- **Handler Name:** `PublishArticleHandler`
- **Properties:**
  ```csharp
  public class PublishArticleCommand : ICommand
  {
      public Guid ArticleId { get; set; }
      public DateTimeOffset? PublishTime { get; set; }
  }
  ```
- **Returns:** `CommandResult<List<CdnResult>>`
- **Usage in Controller:** Lines 1356-1376 in EditorController.cs

#### 2. `DeleteArticle(int articleNumber)`
- **Type:** Command (Write Operation)
- **Current Location:** Line 608 in ArticleEditLogic
- **Suggested Command:** `DeleteArticleCommand`
- **Handler Name:** `DeleteArticleHandler`
- **Properties:**
  ```csharp
  public class DeleteArticleCommand : ICommand
  {
      public int ArticleNumber { get; set; }
  }
  ```
- **Returns:** `CommandResult<Unit>`
- **Usage in Controller:** Lines 2207-2223 in EditorController.cs (TrashArticle)

#### 3. `RestoreArticle(int articleNumber, string userId)`
- **Type:** Command (Write Operation)
- **Current Location:** Line 732 in ArticleEditLogic
- **Suggested Command:** `RestoreArticleCommand`
- **Handler Name:** `RestoreArticleHandler`
- **Properties:**
  ```csharp
  public class RestoreArticleCommand : ICommand
  {
      public int ArticleNumber { get; set; }
      public string UserId { get; set; }
  }
  ```
- **Returns:** `CommandResult<Unit>`
- **Usage in Controller:** Line 1083-1100 in EditorController.cs (Restore action)

#### 4. `CreateHomePage(NewHomeViewModel model)`
- **Type:** Command (Write Operation)
- **Current Location:** Line 578 in ArticleEditLogic
- **Suggested Command:** `CreateHomePageCommand`
- **Handler Name:** `CreateHomePageHandler`
- **Properties:**
  ```csharp
  public class CreateHomePageCommand : ICommand
  {
      public int ArticleNumber { get; set; }
      public string Title { get; set; }
      public Guid Id { get; set; }
      public string UrlPath { get; set; }
  }
  ```
- **Returns:** `CommandResult<Unit>`
- **Usage in Controller:** Line 918-925 in EditorController.cs (NewHome POST)

#### 5. `NewVersion(Article article)`
- **Type:** Command (Write Operation)
- **Current Location:** Line 695 in ArticleEditLogic
- **Suggested Command:** `CreateArticleVersionCommand`
- **Handler Name:** `CreateArticleVersionHandler`
- **Properties:**
  ```csharp
  public class CreateArticleVersionCommand : ICommand
  {
      public int ArticleNumber { get; set; }
      public Guid? SourceVersionId { get; set; } // Optional: base on specific version
  }
  ```
- **Returns:** `CommandResult<ArticleViewModel>`
- **Usage in Controller:** Line 842-851 in EditorController.cs (CreateVersion GET->POST)

---

### ? PRIORITY 2 - Helper Operations

#### 6. `ExportArticle(ArticleViewModel article, IViewRenderService renderer)`
- **Type:** Query (Read Operation - but currently in Logic)
- **Suggested Approach:** Create `ExportArticleQuery`
- **Handler Name:** `ExportArticleQueryHandler`
- **Alternative:** Could be a service method if not frequently queried
- **Usage in Controller:** Line 1538 in EditorController.cs (ExportPage)

---

## Private Helper Methods to Consider

### Keep Private (No Change Needed)
- `GetAuthorInfoForUserId()` - Already private, used internally
- `DeleteStaticWebpage()` - Already private, utility for DeleteArticle
- `UpsertCatalogEntry()` - Already private, utility for PublishArticle
- `DeleteCatalogEntry()` - Already private, utility for DeleteArticle

### Potential Refactoring
- These could be extracted to **specialized services** rather than commands:
  - `ICatalogMaintenanceService` for `UpsertCatalogEntry`/`DeleteCatalogEntry`
  - `IStaticWebPageService` for `DeleteStaticWebpage`
  - `IAuthorInfoService` for `GetAuthorInfoForUserId`

---

## Migration Path (Recommended Order)

### Phase 1 (Critical) - Core Workflow
1. ? CreateArticleCommand (DONE)
2. ? SaveArticleCommand (DONE)
3. ? **PublishArticleCommand** - Create next
4. ? **DeleteArticleCommand** - Create next
5. ? **RestoreArticleCommand** - Create next

### Phase 2 (Important) - Secondary Workflows
6. ? **CreateArticleVersionCommand** - Create
7. ? **CreateHomePageCommand** - Create

### Phase 3 (Optional) - Export/Utility
8. ? **ExportArticleQuery** - May not be necessary

---

## After Refactoring Complete

### ArticleEditLogic Should ONLY Contain:
- `GetLastPublishedDate()` - Read operation (marked obsolete, points to query)
- `GetArticleByUrl()` - Read operation (marked obsolete, points to query)
- `GetCatalogEntry()` - Read operation (marked obsolete, points to query)
- `ExportArticle()` - Read operation (could be query or service)

### Even Better: Remove ArticleEditLogic Entirely
Once all write operations are in commands and read operations are in queries, `ArticleEditLogic` becomes unnecessary. It can be completely removed, with only the inherited `ArticleLogic` base class remaining for shared read logic.

---

## Benefits of This Refactoring

? **True CQRS Architecture** - Clear separation between reads and writes
? **Better Testability** - Each operation has its own handler with isolated tests
? **Improved Traceability** - Command handlers can log all state-changing operations
? **Event Sourcing Ready** - Commands naturally fit event sourcing patterns
? **Simplified ArticleEditLogic** - Becomes thin or obsolete
? **Easier Auditing** - All mutations go through commands
? **Better DI Management** - Handlers can inject only what they need

---

## Files to Create

```
Editor/Features/Articles/Publish/
  ??? PublishArticleCommand.cs
  ??? PublishArticleHandler.cs

Editor/Features/Articles/Delete/
  ??? DeleteArticleCommand.cs
  ??? DeleteArticleHandler.cs

Editor/Features/Articles/Restore/
  ??? RestoreArticleCommand.cs
  ??? RestoreArticleHandler.cs

Editor/Features/Articles/CreateVersion/
  ??? CreateArticleVersionCommand.cs
  ??? CreateArticleVersionHandler.cs

Editor/Features/Articles/CreateHomePage/
  ??? CreateHomePageCommand.cs
  ??? CreateHomePageHandler.cs

Tests/Features/Articles/Publish/
  ??? PublishArticleHandlerTests.cs

Tests/Features/Articles/Delete/
  ??? DeleteArticleHandlerTests.cs

(etc. for each command)
```

---

## Implementation Checklist

- [ ] Create PublishArticleCommand & Handler
- [ ] Create DeleteArticleCommand & Handler  
- [ ] Create RestoreArticleCommand & Handler
- [ ] Create CreateArticleVersionCommand & Handler
- [ ] Create CreateHomePageCommand & Handler
- [ ] Update EditorController to use commands
- [ ] Write handler tests
- [ ] Mark ArticleEditLogic methods as `[Obsolete]`
- [ ] Remove ArticleEditLogic usage
- [ ] Consider removing ArticleEditLogic entirely
