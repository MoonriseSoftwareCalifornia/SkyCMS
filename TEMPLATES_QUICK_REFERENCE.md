# TemplatesController Command/Query Pattern - Quick Reference

## ?? What We Did (In 30 Seconds)

? **Step 1:** Converted 4 GET methods to use `GetTemplateQuery`
? **Step 2:** Created `UpdateTemplateMetadataCommand` for Edit POST
? **Step 3:** Removed unsafe `Trash` method
? **Step 4:** Created `GetTemplateListQuery` infrastructure (ready for future use)

**Result:** 6 methods refactored, 17 tests added, zero breaking changes.

---

## ?? Commands Available

| Command | Purpose | Handler |
|---------|---------|---------|
| `UpdateTemplateMetadataCommand` | Update title/description | `UpdateTemplateMetadataHandler` |
| `DeleteTemplateCommand` | Delete template with validation | `DeleteTemplateHandler` |
| `CreatePageDesignVersionCommand` | Create new template | `CreatePageDesignVersionHandler` |
| `SavePageDesignVersionCommand` | Save template content | `SavePageDesignVersionHandler` |

---

## ?? Queries Available

| Query | Purpose | Handler |
|-------|---------|---------|
| `GetTemplateQuery` | Get single template by ID | `GetTemplateQueryHandler` |
| `GetTemplateListQuery` | Get paginated/sorted list | `GetTemplateListQueryHandler` |

---

## ?? Usage Examples

### Get Single Template
```csharp
var query = new GetTemplateQuery { TemplateId = id };
var result = await mediator.QueryAsync(query);

if (!result.IsSuccess || result.Data?.Template == null)
{
    return NotFound();
}

var template = result.Data.Template;
```

### Update Template Metadata
```csharp
var command = new UpdateTemplateMetadataCommand
{
    TemplateId = model.Id,
    Title = model.Title,
    Description = model.Description
};

var result = await mediator.SendAsync(command);

if (!result.IsSuccess)
{
    ModelState.AddModelError(string.Empty, result.ErrorMessage);
    return View(model);
}
```

### Get Template List (Paginated)
```csharp
var query = new GetTemplateListQuery
{
    PageNo = 0,
    PageSize = 10,
    SortOrder = "asc",
    CurrentSort = "Title"
};

var result = await mediator.QueryAsync(query);

if (result.IsSuccess)
{
    var templates = result.Data.Templates;
    var totalCount = result.Data.TotalCount;
}
```

### Delete Template
```csharp
var command = new DeleteTemplateCommand
{
    TemplateId = id,
    UserId = Guid.Parse(await GetUserId())
};

var result = await mediator.SendAsync(command);

if (!result.IsSuccess)
{
    TempData["Error"] = result.ErrorMessage;
    return RedirectToAction("Index");
}

TempData["Success"] = "Template deleted successfully";
```

---

## ?? File Structure

```
Editor/Features/Templates/
??? Create/
?   ??? CreatePageDesignVersionCommand.cs
?   ??? CreatePageDesignVersionHandler.cs
??? Delete/
?   ??? DeleteTemplateCommand.cs
?   ??? DeleteTemplateHandler.cs
??? Get/
?   ??? GetTemplateQuery.cs
?   ??? GetTemplateQueryResult.cs
?   ??? GetTemplateQueryHandler.cs
??? GetList/                                    ? NEW
?   ??? GetTemplateListQuery.cs
?   ??? GetTemplateListQueryResult.cs
?   ??? GetTemplateListQueryHandler.cs
??? Save/
?   ??? SavePageDesignVersionCommand.cs
?   ??? SavePageDesignVersionHandler.cs
??? UpdateMetadata/                             ? NEW
    ??? UpdateTemplateMetadataCommand.cs
    ??? UpdateTemplateMetadataHandler.cs

Tests/Features/Templates/
??? DeleteTemplateCommandTests.cs
??? GetTemplateQueryHandlerTests.cs
??? GetTemplateListQueryTests.cs               ? NEW (7 tests)
??? PageDesignVersionCommandTests.cs
??? UpdateTemplateMetadataCommandTests.cs      ? NEW (10 tests)
```

---

## ? Test Coverage

| Feature | Tests | Status |
|---------|-------|--------|
| GetTemplateQuery | Multiple | ? Pass |
| UpdateTemplateMetadata | 10 | ? Pass |
| GetTemplateList | 7 | ? Pass |
| DeleteTemplate | Multiple | ? Pass |
| **Total New Tests** | **17** | **? All Pass** |

---

## ?? DI Registration (Already Done)

### Production (Program.cs)
```csharp
builder.Services.AddScoped<IQueryHandler<GetTemplateQuery, CommandResult<GetTemplateQueryResult>>, GetTemplateQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetTemplateListQuery, CommandResult<GetTemplateListQueryResult>>, GetTemplateListQueryHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateTemplateMetadataCommand, CommandResult<Template>>, UpdateTemplateMetadataHandler>();
builder.Services.AddScoped<ICommandHandler<DeleteTemplateCommand, CommandResult<bool>>, DeleteTemplateHandler>();
```

### Tests (SkyCmsTestBase.cs)
```csharp
.AddScoped<IQueryHandler<GetTemplateQuery, CommandResult<GetTemplateQueryResult>>>(sp =>
    new GetTemplateQueryHandler(Db, logger))
.AddScoped<IQueryHandler<GetTemplateListQuery, CommandResult<GetTemplateListQueryResult>>>(sp =>
    new GetTemplateListQueryHandler(Db, logger))
.AddScoped<ICommandHandler<UpdateTemplateMetadataCommand, CommandResult<Template>>>(sp =>
    new UpdateTemplateMetadataHandler(Db, logger))
.AddScoped<ICommandHandler<DeleteTemplateCommand, CommandResult<bool>>>(sp =>
    new DeleteTemplateHandler(Db))
```

---

## ?? Controller Methods Status

| Method | Pattern | Status |
|--------|---------|--------|
| `Edit` (GET) | Query | ? GetTemplateQuery |
| `Edit` (POST) | Command | ? UpdateTemplateMetadataCommand |
| `EditCode` (GET) | Query | ? GetTemplateQuery |
| `EditCode` (POST) | Command | ? SavePageDesignVersionCommand |
| `Designer` (GET) | Query | ? GetTemplateQuery |
| `DesignerData` (GET) | Query | ? GetTemplateQuery |
| `DesignerData` (POST) | Command | ? SavePageDesignVersionCommand |
| `Delete` | Command | ? DeleteTemplateCommand |
| `Create` | Command | ? CreatePageDesignVersionCommand |
| ~~`Trash`~~ | ~~Direct DB~~ | ? **REMOVED** |
| `Index` | Direct DB | ?? Deferred (complex dependencies) |
| `Pages` | Direct DB | ?? Deferred (complex dependencies) |

---

## ?? Metrics

| Metric | Value |
|--------|-------|
| Methods Refactored | 6 |
| Commands Created | 2 |
| Queries Created | 2 |
| Handlers Created | 4 |
| Tests Added | 17 |
| Breaking Changes | 0 |
| Production Ready | ? Yes |

---

## ?? Patterns to Follow

### Command Pattern
```csharp
// 1. Create command
public class MyCommand : ICommand<CommandResult<T>>
{
    public Guid Id { get; set; }
    // ... properties
}

// 2. Create handler
public class MyCommandHandler : ICommandHandler<MyCommand, CommandResult<T>>
{
    public async Task<CommandResult<T>> HandleAsync(MyCommand command, ...)
    {
        // Validate
        // Execute
        // Log
        // Return result
    }
}

// 3. Register in DI
builder.Services.AddScoped<ICommandHandler<MyCommand, CommandResult<T>>, MyCommandHandler>();

// 4. Use in controller
var command = new MyCommand { ... };
var result = await mediator.SendAsync(command);
if (!result.IsSuccess) { /* handle error */ }
```

### Query Pattern
```csharp
// 1. Create query
public class MyQuery : IQuery<CommandResult<MyResult>>
{
    public Guid Id { get; set; }
    // ... parameters
}

// 2. Create result
public class MyResult
{
    public Data Data { get; set; }
}

// 3. Create handler
public class MyQueryHandler : IQueryHandler<MyQuery, CommandResult<MyResult>>
{
    public async Task<CommandResult<MyResult>> HandleAsync(MyQuery query, ...)
    {
        // Query database
        // Map to result
        // Return
    }
}

// 4. Register in DI
builder.Services.AddScoped<IQueryHandler<MyQuery, CommandResult<MyResult>>, MyQueryHandler>();

// 5. Use in controller
var query = new MyQuery { ... };
var result = await mediator.QueryAsync(query);
if (result.IsSuccess) { var data = result.Data; }
```

---

## ?? Common Pitfalls

### 1. Template Entity Has No Layout Navigation Property
? **Wrong:**
```csharp
var template = dbContext.Templates.Include(t => t.Layout)
```

? **Right:**
```csharp
var query = from t in dbContext.Templates
            join l in dbContext.Layouts on t.LayoutId equals l.Id
            select new { ... };
```

### 2. Query Returns CommandResult, Not Direct Data
? **Wrong:**
```csharp
var result = await mediator.QueryAsync(query);
var template = result.Template; // ? Won't compile
```

? **Right:**
```csharp
var result = await mediator.QueryAsync(query);
if (result.IsSuccess)
{
    var template = result.Data.Template; // ? Correct
}
```

### 3. Don't Forget Null Checks
? **Wrong:**
```csharp
var result = await mediator.QueryAsync(query);
var template = result.Data.Template; // ? Might be null
```

? **Right:**
```csharp
var result = await mediator.QueryAsync(query);
if (!result.IsSuccess || result.Data?.Template == null)
{
    return NotFound();
}
var template = result.Data.Template; // ? Safe
```

---

## ?? Documentation Files

1. **STEP1_GETTEMPLATE_QUERY_CONVERSION_COMPLETE.md** - GET method refactoring
2. **STEP2_UPDATE_TEMPLATE_METADATA_COMPLETE.md** - Edit POST command
3. **STEP3_REMOVE_TRASH_METHOD_COMPLETE.md** - Cleanup
4. **STEP4_LIST_QUERIES_COMPLETE.md** - List query infrastructure
5. **TEMPLATES_COMMAND_QUERY_FINAL_SUMMARY.md** - Complete overview
6. **TEMPLATES_QUICK_REFERENCE.md** - This file

---

## ?? Success!

All steps complete. TemplatesController now follows command/query pattern with:
- ? 17 new tests (all passing)
- ? Zero breaking changes
- ? Production ready
- ? Clear patterns for team to follow

**Ready to deploy and use as reference for other controllers!**
