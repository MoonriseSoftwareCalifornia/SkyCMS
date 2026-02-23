# Template Save Operations - Quick Reference

## ?? 5 Locations Found Where Templates Are Saved Without Handler

### Summary Table

| # | File | Method | Lines | Type | Issue |
|---|------|--------|-------|------|-------|
| 1 | TemplatesController | Create() | 363-365 | Template + Version | Direct save, no handler |
| 2 | TemplatesController | Edit() | 409-412 | Template (metadata) | Direct save, no handler |
| 3 | TemplatesController | EditCode() | 481-487 | Template (content) | Direct save, no handler |
| 4 | TemplatesController | DesignerData() | 575-593 | Template (content) | Direct save, no handler |
| 5 | BlogController | Edit() | 253-254 | Article (blog stream) | Direct save, no handler |

---

## ?? Issue #1: TemplatesController.Create()

**File**: `Editor/Controllers/TemplatesController.cs`  
**Lines**: 363-365  
**Severity**: ?? HIGH

```csharp
// Line 363-365: Direct save without SavePageDesignVersionHandler
dbContext.Templates.Add(entity);
dbContext.PageDesignVersions.Add(version);
await dbContext.SaveChangesAsync();
```

**Problem**:
- Initial PageDesignVersion is created without handler validation
- No logging of initial version creation
- Should use SavePageDesignVersionHandler

---

## ?? Issue #2: TemplatesController.Edit()

**File**: `Editor/Controllers/TemplatesController.cs`  
**Lines**: 409-412  
**Severity**: ?? MEDIUM

```csharp
// Line 409-412: Direct save of template metadata
var template = await dbContext.Templates.FirstOrDefaultAsync(f => f.Id == model.Id);
template.Title = model.Title;
template.Description = model.Description;
await dbContext.SaveChangesAsync();
```

**Problem**:
- Updates Template directly without creating new PageDesignVersion
- No version history for metadata changes
- Inconsistent with other template operations

**Note**: Metadata-only, less critical than content changes

---

## ?? Issue #3: TemplatesController.EditCode()

**File**: `Editor/Controllers/TemplatesController.cs`  
**Lines**: 481-487  
**Severity**: ?? HIGH

```csharp
// Line 481-487: Direct save without handler
var entity = await dbContext.Templates.FirstOrDefaultAsync(f => f.Id == model.Id);
entity.Title = model.Title;
entity.Content = htmlService.EnsureEditableMarkers(model.Content);
await dbContext.SaveChangesAsync();  // ? WRONG
```

**Problem**:
- Calls EnsureEditableMarkers but saves directly to Template
- No PageDesignVersion created
- Should use SavePageDesignVersionHandler
- Content changes need version tracking

---

## ?? Issue #4: TemplatesController.DesignerData()

**File**: `Editor/Controllers/TemplatesController.cs`  
**Lines**: 575-593  
**Severity**: ?? HIGH

```csharp
// Line 575-593: Direct save without handler
var entity = await dbContext.Templates.FirstOrDefaultAsync(f => f.Id == model.Id);
model.HtmlContent = htmlService.EnsureEditableMarkers(model.HtmlContent);
// ... processing ...
entity.Content = designerUtils.AssembleDesignerOutput(model);
await dbContext.SaveChangesAsync();  // ? WRONG
```

**Problem**:
- GrapeJS designer output saved directly
- No PageDesignVersion created
- Designer assembly happens outside handler
- Should use SavePageDesignVersionHandler

---

## ?? Issue #5: BlogController.Edit()

**File**: `Editor/Controllers/BlogController.cs`  
**Lines**: 253-254  
**Severity**: ?? HIGH

```csharp
// Line 253-254: Direct save of blog stream HTML
article.Content = await blogRenderingService.GenerateBlogStreamHtml(article);
await db.SaveChangesAsync();  // ? WRONG
```

**Problem**:
- Blog stream HTML saved directly
- No editable marker validation
- Saves to Article, not PageDesignVersion
- No logging or audit trail
- Should use SavePageDesignVersionHandler

---

## ? What Should Happen

All template saves should:

1. ? Call `SavePageDesignVersionHandler` via mediator
2. ? Create a new `SavePageDesignVersionCommand`
3. ? Ensure `EnsureEditableMarkers()` is called
4. ? Create version history (PageDesignVersion)
5. ? Log the operation
6. ? Validate content

**Pattern**:
```csharp
var command = new SavePageDesignVersionCommand
{
    Id = versionId,
    Title = model.Title,
    Content = model.Content,
    // ... other properties ...
};

var result = await mediator.SendAsync(command);

if (!result.IsSuccess)
    return BadRequest(result.ErrorMessage);
```

---

## ?? Severity Breakdown

- **?? HIGH** (Must Fix): 4 issues
  - Create(), EditCode(), DesignerData(), BlogController.Edit()
  - Involve content changes needing version tracking

- **?? MEDIUM** (Should Fix): 1 issue
  - Edit() - metadata only
  - But should be consistent

---

## ?? Impact

Without fixing:
- ? Editable markers may be incomplete
- ? No version history for some changes
- ? Inconsistent behavior
- ? No audit trail for some operations
- ? Harder to debug/recover

With fixing:
- ? All content guaranteed to have markers
- ? Complete version history
- ? Consistent behavior everywhere
- ? Full audit trail
- ? Easier debugging

---

## ?? How to Find These in Code

### TemplatesController.cs
- **Create()**: Around line 363-365
  - Look for: `dbContext.PageDesignVersions.Add(version)`
  
- **Edit()**: Around line 409-412
  - Look for: `template.Title = model.Title;`
  
- **EditCode()**: Around line 481-487
  - Look for: `entity.Content = htmlService.EnsureEditableMarkers`
  
- **DesignerData()**: Around line 575-593
  - Look for: `entity.Content = designerUtils.AssembleDesignerOutput`

### BlogController.cs
- **Edit()**: Around line 253-254
  - Look for: `article.Content = await blogRenderingService.GenerateBlogStreamHtml`

---

## ?? Next Steps

1. ? **Review**: Read TEMPLATE_SAVE_OPERATIONS_AUDIT.md for details
2. ? **Plan**: Create refactoring plan for each method
3. ? **Implement**: Refactor to use SavePageDesignVersionHandler
4. ? **Test**: Add tests for each refactored method
5. ? **Verify**: Ensure all markers are properly added

---

**Status**: ?? **ISSUES FOUND - ACTION REQUIRED**

All 5 locations need to be refactored to use SavePageDesignVersionHandler consistently.
