# EditorController Endpoint Migration Guide

## Overview
The EditorController has been refactored to use a **unified Edit endpoint** that handles all save operations through a `Command` property. Legacy direct methods are now marked as `[Obsolete]` and will be removed in a future version.

---

## Legacy Methods (Deprecated)

### ❌ `EditSaveRegion(EditorRegionViewModel)` 
**Status:** Obsolete  
**Replacement:** Use `Edit(EditPostViewModel)` with `Command = "SaveRegion"`

### ❌ `EditSaveBody(EditorRegionViewModel)`
**Status:** Obsolete  
**Replacement:** Use `Edit(EditPostViewModel)` with `Command = "SaveBody"`

---

## Migration Examples

### Before: EditSaveRegion (Legacy)
```javascript
// OLD WAY - Direct call to EditSaveRegion
fetch('/Editor/EditSaveRegion', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
        ArticleNumber: 123,
        EditorId: 'region-1',
        Data: encryptedContent
    })
});
```

### After: Unified Edit Endpoint ✅
```javascript
// NEW WAY - Use unified Edit endpoint with Command
fetch('/Editor/Edit', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
        ArticleNumber: 123,
        Command: 'SaveRegion',
        EditorId: 'region-1',
        Data: encryptedContent,
        Title: articleTitle,
        VersionNumber: currentVersion
    })
});
```

---

### Before: EditSaveBody (Legacy)
```javascript
// OLD WAY - Direct call to EditSaveBody
fetch('/Editor/EditSaveBody', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
        ArticleNumber: 123,
        Data: encryptedBodyContent
    })
});
```

### After: Unified Edit Endpoint ✅
```javascript
// NEW WAY - Use unified Edit endpoint with Command
fetch('/Editor/Edit', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
        ArticleNumber: 123,
        Command: 'SaveBody',
        Data: encryptedBodyContent,
        Title: articleTitle,
        VersionNumber: currentVersion
    })
});
```

---

## Available Commands

The unified `Edit` endpoint supports the following commands:

| Command | Purpose | Required Fields |
|---------|---------|----------------|
| `SaveBody` | Replace entire article content | `Data`, `Title` |
| `SaveRegion` | Update specific editable region | `EditorId`, `Data`, `Title` |
| `SaveCode` | Update content and scripts from Code Editor | `Content`, `HeadJavaScript`, `FooterJavaScript`, `Title` |
| `SavePageProperties` | Update metadata only (title, banner, etc.) | `Title` |
| `SaveDesigner` | Save from GrapesJS designer | `HtmlContent`, `CssContent`, `Title` |
| _(empty/null)_ | Metadata-only update | `Title` |

---

## Benefits of Unified Endpoint

1. **Single entry point** - All save operations go through one endpoint
2. **Consistent response format** - All responses use `ServerSideSuccess`, `Model`, `CdnResults` structure
3. **Better validation** - Centralized validation logic with proper error responses
4. **Improved testing** - Single comprehensive test suite (`EditorUnifiedEndpointTests.cs`)
5. **CQRS integration** - Uses `SaveArticleCommand` for all operations

---

## Response Format

All save operations return JSON with this structure:

### Success Response
```json
{
  "ServerSideSuccess": true,
  "Model": {
    "ArticleNumber": 123,
    "VersionNumber": 2,
    "Title": "Updated Title",
    "Updated": "2024-01-15T10:30:00Z",
    ...
  },
  "CdnResults": [...]
}
```

### Error Response
```json
{
  "ServerSideSuccess": false,
  "errors": {
    "Title": ["Title cannot be null or empty."],
    "Content": ["Nested editable regions are not allowed."]
  }
}
```

---

## Migration Checklist

- [ ] Search frontend code for `/Editor/EditSaveRegion` calls
- [ ] Replace with `/Editor/Edit` + `Command: 'SaveRegion'`
- [ ] Search frontend code for `/Editor/EditSaveBody` calls
- [ ] Replace with `/Editor/Edit` + `Command: 'SaveBody'`
- [ ] Update JavaScript/TypeScript to include `Command` property
- [ ] Ensure `Title` is always included (now required field)
- [ ] Update response handling to use `ServerSideSuccess` property
- [ ] Test all editor workflows (Live Editor, Code Editor, Designer)

---

## Timeline

- **Current:** Legacy methods marked `[Obsolete]` with migration warning
- **Next Release:** Frontend migration should be completed
- **Future Release:** Legacy methods will be removed from controller

---

## Support

If you encounter issues during migration:
1. Check `EditorUnifiedEndpointTests.cs` for working examples
2. Review `EditPostViewModel` properties in `Editor\Models\EditPostViewModel.cs`
3. Ensure encryption/decryption still works with new endpoint

---

## See Also

- `Tests\Controllers\EditorUnifiedEndpointTests.cs` - Comprehensive test examples
- `Tests\Controllers\TEST_CLEANUP_SUMMARY.md` - Test suite cleanup documentation
- `Editor\Controllers\EditorController.cs` (line 1242) - Unified Edit method implementation
