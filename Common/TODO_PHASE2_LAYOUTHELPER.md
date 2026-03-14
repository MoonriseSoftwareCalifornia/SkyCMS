# Phase 2 Optional Tasks - TODO

**Status:** Optional / As-Needed  
**Priority:** Low-Medium (can be done incrementally as code is touched)

---

## 📋 **LayoutHelper Call Site Migration**

### Context
30+ files still use `LayoutHelper` static methods. Migration can be done incrementally - obsolete warnings guide developers.

---

### 1. High Priority - Production Code (7 files)

#### ArticleViewModelBuilder (2 usages)
**File:** `Common/Features/Articles/Shared/ArticleViewModelBuilder.cs`

**Current:**
```csharp
var entity = await LayoutHelper.GetCurrentDefaultLayoutAsync(dbContext);
return new LayoutViewModel(entity);
```

**Migrate To:**
```csharp
// Inject IMediator, use GetDefaultLayoutQuery
var layoutViewModel = await _mediator.QueryAsync(new GetDefaultLayoutQuery(layoutCache));
return layoutViewModel;
```

**Tasks:**
- [ ] Add `IMediator` to `ArticleViewModelBuilder` constructor
- [ ] Replace line 128 with `GetDefaultLayoutQuery`
- [ ] Replace line 134 with `GetDefaultLayoutQuery`
- [ ] Update tests for `ArticleViewModelBuilder`

---

#### PublishingService (1 usage)
**File:** `Editor/Services/Publishing/PublishingService.cs`  
**Line:** 579

**Tasks:**
- [ ] Inject `IMediator` into `PublishingService`
- [ ] Replace `LayoutHelper.GetCurrentDefaultLayoutAsync` with `GetDefaultLayoutQuery`

---

#### TemplateService (1 usage)
**File:** `Editor/Services/Templates/TemplateService.cs`  
**Line:** 105

**Tasks:**
- [ ] Inject `IMediator` into `TemplateService`
- [ ] Replace `LayoutHelper.GetCurrentDefaultLayoutAsync` with `GetDefaultLayoutQuery`

---

#### BaseController (1 usage)
**File:** `Editor/Controllers/BaseController.cs`  
**Line:** 200

**Tasks:**
- [ ] `BaseController` likely already has `IMediator` injected
- [ ] Replace `LayoutHelper.GetCurrentDefaultLayoutAsync` with `GetDefaultLayoutQuery`

---

#### HomeController (1 usage)
**File:** `Editor/Controllers/HomeController.cs`  
**Line:** 401

**Tasks:**
- [ ] Verify `IMediator` is available (likely via `BaseController`)
- [ ] Replace `LayoutHelper.GetCurrentDefaultLayoutAsync` with `GetDefaultLayoutQuery`

---

#### BlogController (2 usages)
**File:** `Editor/Controllers/BlogController.cs`  
**Lines:** 136, 405

**Tasks:**
- [ ] Verify `IMediator` is available (likely via `BaseController`)
- [ ] Replace both `LayoutHelper.GetCurrentDefaultLayoutAsync` calls with `GetDefaultLayoutQuery`

---

### 2. Medium Priority - Test Files (18 files)

#### Test Files Using LayoutHelper
- `Tests/Services/LayoutManagementTests.cs` (6 usages)
- `Tests/Areas/Setup/DatabaseInitializationTests.cs` (1 usage)
- `Tests/Services/TemplateServiceTests.cs` (8 usages)
- `Tests/Infrastructure/SkyCmsTestBase.cs` (2 usages)
- `Tests/Controllers/BaseControllerTests.cs` (1 usage)

**Migration Strategy:**
- Update incrementally as tests are modified
- Consider creating test helper that uses `GetDefaultLayoutQuery`
- Mock `IMediator` in unit tests instead of mocking DbContext

**Tasks:**
- [ ] Create test helper for layout queries
- [ ] Update `LayoutManagementTests` (6 usages)
- [ ] Update `TemplateServiceTests` (8 usages)
- [ ] Update `SkyCmsTestBase` setup (2 usages)
- [ ] Update `DatabaseInitializationTests` (1 usage)
- [ ] Update `BaseControllerTests` (1 usage)

---

### 3. Low Priority - Already Obsolete Code

#### ArticleLogic (2 usages)
**File:** `Common/Data/Logic/ArticleLogic.cs`  
**Lines:** 389, 397

**Note:** ArticleLogic itself is marked obsolete. These will be removed in Phase 4 when ArticleLogic is deleted.

**Tasks:**
- [ ] No action needed - will be removed with ArticleLogic in Phase 4

---

## 📊 **Progress Tracking**

**Total Call Sites:** 30  
**High Priority (Production):** 7  
**Medium Priority (Tests):** 18  
**Low Priority (Obsolete Code):** 2  

**Completed:** 0  
**In Progress:** 0  
**Not Started:** 30

---

## 🎯 **Migration Guidelines**

### Pattern: Inject IMediator
```csharp
// Add to constructor
private readonly IMediator _mediator;

public SomeService(IMediator mediator, /* other dependencies */)
{
    _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
}
```

### Pattern: Replace GetCurrentDefaultLayoutAsync
```csharp
// Before
var layout = await LayoutHelper.GetCurrentDefaultLayoutAsync(dbContext);

// After
var layout = await _mediator.QueryAsync(new GetDefaultLayoutQuery());
```

### Pattern: Replace with Caching
```csharp
// Before (manual caching)
if (!cache.TryGetValue("layout", out Layout layout))
{
    layout = await LayoutHelper.GetCurrentDefaultLayoutAsync(dbContext);
    cache.Set("layout", layout, TimeSpan.FromMinutes(10));
}

// After (built-in caching)
var layout = await _mediator.QueryAsync(new GetDefaultLayoutQuery(TimeSpan.FromMinutes(10)));
```

### Pattern: Replace HasDefaultLayoutAsync
```csharp
// Before
var exists = await LayoutHelper.HasDefaultLayoutAsync(dbContext);

// After
var exists = await _mediator.QueryAsync(new CheckDefaultLayoutExistsQuery());
```

### Pattern: Replace GetLayoutByIdAsync
```csharp
// Before
var layout = await LayoutHelper.GetLayoutByIdAsync(dbContext, layoutId);

// After
var layout = await _mediator.QueryAsync(new GetLayoutByIdQuery(layoutId));
```

---

## ⚠️ **Important Notes**

1. **GetDefaultLayoutQuery Returns LayoutViewModel**
   - `LayoutHelper` returns `Layout` entity
   - `GetDefaultLayoutQuery` returns `LayoutViewModel`
   - If you need the entity, consider creating `GetDefaultLayoutEntityQuery`

2. **IMediator Might Already Be Available**
   - Many controllers inherit from base classes that inject `IMediator`
   - Check constructor chain before adding duplicate dependency

3. **Tests Should Mock IMediator**
   - Cleaner than mocking `DbContext` and `DbSet<Layout>`
   - Faster test execution
   - See `LAYOUTHELPER_MIGRATION_GUIDE.md` for examples

---

## 🔄 **When to Complete These**

These tasks should be completed:
- **High Priority Production Code:** Within next 2-4 weeks (Phase 2 completion)
- **Test Files:** Incrementally as tests are modified
- **ArticleLogic Usages:** No action - removed in Phase 4

---

**Document Version:** 1.0  
**Created:** 2025-01-11  
**Status:** Active TODO List (Phase 2)
