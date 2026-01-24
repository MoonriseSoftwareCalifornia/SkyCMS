# LayoutHelper Usage Guide

## Overview

The `LayoutHelper` static class provides standardized methods for retrieving layouts from the database throughout the SkyCMS solution. This ensures consistent logic for getting the current default layout with proper version and publish date filtering.

## Key Methods

### 1. GetCurrentDefaultLayoutAsync

Gets the current active default layout (latest published version).

```csharp
var layout = await LayoutHelper.GetCurrentDefaultLayoutAsync(dbContext);
if (layout != null)
{
    // Use the layout
}
```

**What it does:**
- Filters for `IsDefault = true`
- Filters for `Published <= now` (active/published layouts)
- Orders by `Version`
- Returns the latest (highest version) layout

### 2. HasDefaultLayoutAsync

Checks if any default layout exists (useful for setup/initialization).

```csharp
if (!await LayoutHelper.HasDefaultLayoutAsync(dbContext))
{
    // No default layout exists - create one
    layout.Version = 1;
    layout.IsDefault = true;
}
else
{
    // Default layout exists - create as non-default
    layout.IsDefault = false;
}
```

### 3. GetLayoutByIdAsync

Gets a specific layout by its unique identifier.

```csharp
var layout = await LayoutHelper.GetLayoutByIdAsync(dbContext, layoutId);
```

## Migration from Old Patterns

### ? Old (Problematic) Pattern:
```csharp
var defaultLayout = await dbContext.Layouts.FirstOrDefaultAsync(l => l.IsDefault);
```

**Problems:**
- Doesn't check if layout is published
- Doesn't get the latest version
- Returns arbitrary match if multiple defaults exist

### ? New (Correct) Pattern:
```csharp
var defaultLayout = await LayoutHelper.GetCurrentDefaultLayoutAsync(dbContext);
```

**Benefits:**
- Ensures published and active layouts only
- Gets the latest version
- Standardized across the solution
- Single source of truth for layout retrieval logic

## Usage Examples

### Example 1: In a Controller
```csharp
public class MyController : BaseController
{
    private readonly ApplicationDbContext _dbContext;
    
    public async Task<IActionResult> Index()
    {
        var layout = await LayoutHelper.GetCurrentDefaultLayoutAsync(_dbContext);
        // ... use layout
    }
}
```

### Example 2: In a Service
```csharp
public class MyService
{
    private readonly ApplicationDbContext _db;
    
    public async Task DoSomething()
    {
        var layout = await LayoutHelper.GetCurrentDefaultLayoutAsync(_db);
        if (layout != null)
        {
            var templates = await _db.Templates
                .Where(t => t.LayoutId == layout.Id)
                .ToListAsync();
        }
    }
}
```

### Example 3: Setup/Initialization Scenario
```csharp
public async Task ImportLayout()
{
    var layout = await layoutImportService.GetCommunityLayoutAsync(id, true);
    
    // Check if we need to set as default
    if (!await LayoutHelper.HasDefaultLayoutAsync(dbContext))
    {
        layout.Version = 1;
        layout.IsDefault = true;
    }
    else
    {
        layout.Version = await dbContext.Layouts.CountAsync() + 1;
        layout.IsDefault = false;
    }
    
    dbContext.Layouts.Add(layout);
    await dbContext.SaveChangesAsync();
}
```

## Files Updated

The following files have been updated to use the new `LayoutHelper` methods:

1. `Editor/Controllers/BaseController.cs` - `FetchCurrentLayoutAsync()`
2. `Editor/Controllers/LayoutsController.cs` - `Import()` method
3. `Editor/Controllers/BlogController.cs` - 2 occurrences
4. `Common/Data/Logic/ArticleLogic.cs` - `FetchCurrentLayoutAsync()`
5. `Editor/Services/Publishing/PublishingService.cs` - `GetDefaultLayoutAsync()`
6. `Editor/Services/Setup/MultiTenantSetupService.cs` - layout import
7. `Editor/Services/Setup/SetupService.cs` - layout setup
8. `Editor/Services/Templates/TemplateService.cs` - template creation

## Benefits

1. **Consistency**: All layout queries use the same logic
2. **Maintainability**: Single place to update layout retrieval logic
3. **Correctness**: Ensures proper version and publish date filtering
4. **Testability**: Easy to mock for unit tests
5. **Multi-tenant Safe**: Works correctly with tenant-scoped queries
