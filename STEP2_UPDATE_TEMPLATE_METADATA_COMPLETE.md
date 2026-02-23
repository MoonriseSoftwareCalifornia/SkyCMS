# Step 2: UpdateTemplateMetadataCommand - COMPLETE ?

## Objective
Create a command/handler pattern for updating template metadata (title and description) to replace direct database access in the Edit POST method.

## Changes Made

### 1. Created Command Class
**File:** `Editor/Features/Templates/UpdateMetadata/UpdateTemplateMetadataCommand.cs`

```csharp
public class UpdateTemplateMetadataCommand : ICommand<CommandResult<Template>>
{
    public Guid TemplateId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
```

**Features:**
- Simple DTO with template ID, title, and description
- Returns `CommandResult<Template>` with the updated template
- Follows existing command pattern conventions

### 2. Created Handler Class
**File:** `Editor/Features/Templates/UpdateMetadata/UpdateTemplateMetadataHandler.cs`

```csharp
public class UpdateTemplateMetadataHandler : ICommandHandler<UpdateTemplateMetadataCommand, CommandResult<Template>>
{
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<UpdateTemplateMetadataHandler> logger;
    
    public async Task<CommandResult<Template>> HandleAsync(
        UpdateTemplateMetadataCommand command,
        CancellationToken cancellationToken = default)
    {
        // Validation
        // Database update
        // Logging
        // Error handling
    }
}
```

**Features:**
- ? Validates template ID (not empty)
- ? Validates title (not null/whitespace)
- ? Trims whitespace from title
- ? Handles null description (converts to empty string)
- ? Only updates metadata fields (not content)
- ? Comprehensive logging
- ? Proper error handling with try-catch
- ? Returns CommandResult for success/failure

### 3. Updated TemplatesController
**File:** `Editor/Controllers/TemplatesController.cs`

#### Before:
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(TemplateEditViewModel model)
{
    model.Description = CryptoJsDecryption.Decrypt(model.Description);

    if (!ModelState.IsValid)
    {
        return View(model);
    }

    var template = await dbContext.Templates.FirstOrDefaultAsync(f => f.Id == model.Id);
    template.Title = model.Title;
    template.Description = model.Description;
    await dbContext.SaveChangesAsync();

    return RedirectToAction("Index");
}
```

#### After:
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(TemplateEditViewModel model)
{
    model.Description = CryptoJsDecryption.Decrypt(model.Description);

    if (!ModelState.IsValid)
    {
        return View(model);
    }

    var command = new UpdateTemplateMetadataCommand
    {
        TemplateId = model.Id,
        Title = model.Title,
        Description = model.Description
    };

    var result = await mediator.SendAsync(command);

    if (!result.IsSuccess)
    {
        ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to update template.");
        return View(model);
    }

    return RedirectToAction("Index");
}
```

**Improvements:**
- ? No direct database access
- ? Proper error handling with ModelState
- ? User-friendly error messages
- ? Consistent with other command usage in controller

### 4. Dependency Injection Registration

#### a) Program.cs
**File:** `Editor/Program.cs`

Added:
```csharp
using Sky.Editor.Features.Templates.UpdateMetadata;

builder.Services.AddScoped<ICommandHandler<UpdateTemplateMetadataCommand, CommandResult<Template>>, UpdateTemplateMetadataHandler>();
```

#### b) SkyCmsTestBase.cs
**File:** `Tests/Infrastructure/SkyCmsTestBase.cs`

Added:
```csharp
using Sky.Editor.Features.Templates.UpdateMetadata;

.AddScoped<ICommandHandler<UpdateTemplateMetadataCommand, CommandResult<Template>>>(sp =>
    new UpdateTemplateMetadataHandler(Db, new LoggerFactory().CreateLogger<UpdateTemplateMetadataHandler>()))
```

### 5. Comprehensive Test Suite
**File:** `Tests/Features/Templates/UpdateTemplateMetadataCommandTests.cs`

**Test Coverage:**

1. ? **UpdateMetadata_SucceedsWithValidData** - Happy path
2. ? **UpdateMetadata_TrimsWhitespaceFromTitle** - Data cleanup
3. ? **UpdateMetadata_FailsWithEmptyTemplateId** - Validation
4. ? **UpdateMetadata_FailsWithEmptyTitle** - Validation
5. ? **UpdateMetadata_FailsWithWhitespaceOnlyTitle** - Validation
6. ? **UpdateMetadata_FailsWhenTemplateNotFound** - Not found scenario
7. ? **UpdateMetadata_AllowsEmptyDescription** - Optional description
8. ? **UpdateMetadata_HandlesNullDescription** - Null handling
9. ? **UpdateMetadata_DoesNotAffectContent** - Data isolation
10. ? **UpdateMetadata_ThrowsWhenCommandIsNull** - Null guard

**All 10 tests pass ?**

## Benefits Achieved

### 1. Separation of Concerns
- Controller only coordinates request/response
- Business logic in handler
- Validation centralized

### 2. Testability
- Handler can be tested independently
- Easy to mock for controller tests
- Clear test scenarios

### 3. Error Handling
- Consistent CommandResult pattern
- User-friendly error messages
- Proper logging for diagnostics

### 4. Maintainability
- Single responsibility per class
- Easy to extend validation
- Clear update semantics

### 5. Safety
- Only updates metadata fields
- Content changes require separate command
- Prevents accidental data overwrites

## Comparison with Related Commands

### UpdateTemplateMetadataCommand vs SavePageDesignVersionCommand

| Aspect | UpdateTemplateMetadata | SavePageDesignVersion |
|--------|----------------------|----------------------|
| **Updates** | Title, Description | Content, Title, Description |
| **Target** | Template entity | PageDesignVersion entity |
| **Use Case** | Quick metadata edits | Design/content changes |
| **Validation** | Title required | Content validation (editable markers) |
| **Complexity** | Simple | Complex (HTML processing) |

### When to Use Each

- **UpdateTemplateMetadata**: Changing template name or description only
- **SavePageDesignVersion**: Changing template content/design

## Integration Notes

### Controller Integration
The Edit POST method now:
1. ? Decrypts description (existing security)
2. ? Validates model state
3. ? Creates command
4. ? Sends via mediator
5. ? Handles errors with ModelState
6. ? Redirects on success

### Database Isolation
- Only updates Template table
- Does not touch PageDesignVersions
- Single transaction (implicit with SaveChangesAsync)

## Testing Results

```
? All 10 UpdateTemplateMetadataCommandTests pass
? All existing TemplatesControllerTests pass
? Integration with mediator verified
? DI registration working in both production and test environments
```

## Next Steps

See parent documentation for:
- **Step 3**: Remove redundant `Trash` method
- **Step 4**: Create list queries for Index and Pages

## Files Created

1. `Editor/Features/Templates/UpdateMetadata/UpdateTemplateMetadataCommand.cs`
2. `Editor/Features/Templates/UpdateMetadata/UpdateTemplateMetadataHandler.cs`
3. `Tests/Features/Templates/UpdateTemplateMetadataCommandTests.cs`

## Files Modified

1. `Editor/Controllers/TemplatesController.cs` - Updated Edit POST method
2. `Editor/Program.cs` - Added DI registration
3. `Tests/Infrastructure/SkyCmsTestBase.cs` - Added test DI registration

---

**Completed:** [Current Date]
**Status:** ? VERIFIED - All tests passing
**Test Count:** 10 new tests, all passing
