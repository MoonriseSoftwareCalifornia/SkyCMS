# PHASE 4: Production Code Migration - SAVEARTICLE

## Overview

Now that all **test code** has been successfully migrated from the obsolete `Logic.SaveArticle()` method to the modern CQRS `SaveArticleCommand`/`SaveArticleHandler` pattern, we need to migrate the **production code**.

---

## Current Status

### ? PHASE 1-3: Test Migration - COMPLETE
- All 27 SaveArticle test references migrated
- 5 test files refactored
- 1 obsolete test file deleted
- Build: ? Successful

### ? PHASE 4: Production Code Migration - READY TO START
Identified controller/page code still using obsolete method

---

## Scope: Production Code Using SaveArticle

### Files Requiring Migration

#### 1. **EditorController.cs** (2 references)
- **Location**: `Editor\Controllers\EditorController.cs`
- **References**: Lines 295-312, 1586-1601
- **Pattern**: Currently calling `Logic.SaveArticle()`
- **Action**: Migrate to `SaveArticleHandler`

#### 2. **EditorControllerSaveTests.cs** (Integration Tests)
- **Location**: `Tests\Controllers\EditorControllerSaveTests.cs`
- **References**: Multiple test methods
- **Pattern**: Testing legacy SaveArticle flow through controller
- **Action**: Update to test new CQRS pattern

---

## Phase 4 Plan

### Step 1: Analyze EditorController Usage
- [ ] Read EditorController.cs to understand SaveArticle context
- [ ] Identify HTTP endpoints calling SaveArticle
- [ ] Map request models to SaveArticleCommand properties
- [ ] Identify any special handling/validation

### Step 2: Migrate EditorController
- [ ] Replace `Logic.SaveArticle()` with `SaveArticleHandler.HandleAsync()`
- [ ] Convert ArticleViewModel to SaveArticleCommand
- [ ] Update result handling (ArticleUpdateResult ? CommandResult)
- [ ] Update HTTP responses
- [ ] Test compilation

### Step 3: Update Integration Tests
- [ ] Update EditorControllerSaveTests.cs test setup
- [ ] Update test assertions for new response format
- [ ] Verify test execution still works
- [ ] Add new tests for error scenarios

### Step 4: Verify & Document
- [ ] Build solution successfully
- [ ] Run all tests (unit + integration)
- [ ] Create completion documentation
- [ ] Update architectural guidance

---

## Key Differences: Controller Integration

### Current Pattern (Legacy)
```csharp
// In EditorController
var result = await Logic.SaveArticle(article, userId);
return Ok(new { 
    success = result.ServerSideSuccess,
    model = result.Model,
    cdnResults = result.CdnResults
});
```

### New Pattern (CQRS)
```csharp
// In EditorController
var command = new SaveArticleCommand
{
    ArticleNumber = article.ArticleNumber,
    Title = article.Title,
    Content = article.Content,
    // ... other properties
    UserId = userId
};

var result = await SaveArticleHandler.HandleAsync(command);
return result.IsSuccess 
    ? Ok(new { 
        success = true,
        model = result.Data?.Model,
        cdnResults = result.Data?.CdnResults
    })
    : BadRequest(new { 
        errors = result.Errors,
        message = result.ErrorMessage
    });
```

---

## Benefits of Migration

### For Controllers
? **Cleaner separation of concerns**
- Validation in handler, not controller
- Business logic isolated

? **Better error handling**
- CommandResult provides structured errors
- Consistent response format

? **Easier testing**
- Mock handler independently
- Test request ? command mapping
- Test response ? HTTP mapping

? **Audit & logging**
- Commands are audit-friendly
- Clear operation intent

---

## Expected Challenges & Solutions

| Challenge | Solution |
|-----------|----------|
| Response format change | Map CommandResult to HTTP response |
| Error handling | Use Errors dictionary and ErrorMessage |
| Validation | Handler validates, controller returns errors |
| CDN results | Available in CommandResult.Data.CdnResults |

---

## Testing Strategy

### Unit Tests (EditorController)
- Mock SaveArticleHandler
- Test request ? command mapping
- Test error responses
- Test success responses

### Integration Tests (EditorControllerSaveTests)
- Test full HTTP flow
- Test with real handler
- Test validation errors
- Test CDN purge scenarios

---

## Success Criteria

- [ ] EditorController compiles without warnings
- [ ] All references to Legacy SaveArticle removed
- [ ] EditorControllerSaveTests updated and passing
- [ ] Build successful (zero errors)
- [ ] Integration tests pass
- [ ] No breaking API changes for HTTP clients

---

## Estimated Effort

| Task | Complexity | Time |
|------|-----------|------|
| Analyze controller | Low | 15 min |
| Migrate controller | Medium | 30 min |
| Update tests | Medium | 45 min |
| Verify & document | Low | 30 min |
| **Total** | | **2 hours** |

---

## Next Steps

1. **Review this plan** with team
2. **Start Step 1**: Analyze EditorController usage
3. **Execute Steps 2-4**: Systematic migration
4. **Verify**: Build and test
5. **Document**: Completion and lessons learned

---

## Document References

- **Test Migration**: `SAVEARTICLE_TEST_REFACTORING_COMPLETE.md`
- **CQRS Pattern**: `SAVEARTICLE_BEFORE_AFTER_COMPARISON.md`
- **Handler Implementation**: Handler code in `Editor/Features/Articles/Save/`

---

## Go/No-Go Checklist

- [x] Tests migrated successfully
- [x] CQRS pattern established
- [x] Handler implementation complete
- [x] Architecture clear
- [x] Team alignment on approach
- [ ] **? READY TO PROCEED WITH PHASE 4**

---

**Ready to start Phase 4: Production Code Migration?**

Answer: YES / Let me review the controller code first
