# WEEK 1 AUDIT INVENTORY - ArticleEditLogic.cs Analysis

**Generated**: Today
**Status**: ? COMPLETE
**Next**: Proceed to Week 2 Dependency Analysis

---

## ?? AUDIT FINDINGS SUMMARY

### Total [Obsolete] Methods Found: 6
### Total Public Methods to Migrate: 4
### Total Complexity: Medium (mostly)
### Estimated Total Hours: 55-65 hours
### Estimated Timeline: 7-8 weeks @ 1 FTE

---

## ?? DETAILED INVENTORY

### ARTICLE CREATION & MANAGEMENT

#### 1. ? CreateArticle (PRIORITY 1 - HIGH VALUE)
**Status**: [Obsolete]
**File**: `Editor\Data\Logic\ArticleEditLogic.cs`
**Lines**: 397-487
**Signature**: 
```csharp
public async Task<ArticleViewModel> CreateArticle(
    string title, 
    Guid userId, 
    Guid? templateId = null, 
    string blogKey = "", 
    ArticleType articleType = ArticleType.General)
```

**Deprecation Message**: 
"Use CreateArticleCommand via IMediator instead. This method will be removed in version 3.0."

**Complexity**: **MEDIUM** (10 hours)

**What It Does**:
- Determines if article is first (auto-publish)
- Gets template content if provided
- Calculates next article number
- Validates user belongs to tenant (security)
- Creates Article entity with defaults
- Generates URL path/slug
- Adds to database
- Auto-publishes if first article

**Services Used**:
- DbContext (entity operations)
- htmlService (content markers)
- titleChangeService (URL building)
- publishingService (publish if first)
- catalogService (implicit - via PublishArticle)
- templateService (template retrieval)

**Side Effects**:
- Creates Article entity
- Creates ArticleNumber entry
- Publishes article if first
- Creates catalog entry
- Triggers CDN operations

**Dependencies**:
- PublishArticle (called if first article)
- UpsertCatalogEntry (called via PublishArticle)

**Related Tests**:
- CreateArticleTests (currently use legacy method)
- BlogServiceTests (uses CreateArticle)
- ArticleLifecycleIntegrationTests (uses CreateArticle)

**Migration Path**:
```
CreateArticle() ? CreateArticleCommand + Handler
```

---

#### 2. ? SaveArticle (PRIORITY - ALREADY MIGRATED ?)
**Status**: [Obsolete] - **TESTS ALREADY CONVERTED**
**File**: `Editor\Data\Logic\ArticleEditLogic.cs`
**Lines**: 767-889
**Complexity**: MEDIUM (already done)

**Status**: ? This was our proof-of-concept
- All tests converted to CQRS pattern
- Handler implemented and working
- Controller updated
- Reference implementation for other methods

**Use as template for**:
- CreateArticle migration
- PublishArticle migration
- DeleteArticle migration

---

#### 3. ? PublishArticle (PRIORITY 2 - HIGH VALUE)
**Status**: Public method (not [Obsolete] but should migrate)
**File**: `Editor\Data\Logic\ArticleEditLogic.cs`
**Lines**: 912-930
**Signature**:
```csharp
public async Task<List<CdnResult>> PublishArticle(
    Guid articleId, 
    DateTimeOffset? dateTime)
```

**Complexity**: **MEDIUM** (10 hours)

**What It Does**:
- Finds article by ID
- Sets published timestamp (or uses current)
- Calls publishingService.PublishAsync
- Updates catalog entry
- Returns CDN results

**Services Used**:
- DbContext
- publishingService (core publishing)
- catalogService (via UpsertCatalogEntry)
- clock (optional timestamp)

**Side Effects**:
- Updates Article.Published field
- Triggers CDN operations
- Updates catalog entry
- Generates static artifacts

**Tests Needed**:
- PublishingTests (6 tests)
- CDN result validation
- Catalog update verification
- Timestamp handling

**Migration Path**:
```
PublishArticle() ? PublishArticleCommand + Handler
```

---

#### 4. ? DeleteArticle (PRIORITY 3 - HIGH VALUE)
**Status**: Public method
**File**: `Editor\Data\Logic\ArticleEditLogic.cs`
**Lines**: 941-968
**Signature**:
```csharp
public async Task DeleteArticle(int articleNumber)
```

**Complexity**: **MEDIUM** (10 hours)

**What It Does**:
- Gets all versions of article
- Validates article exists
- Prevents deletion of root page
- Soft-deletes (marks as deleted)
- Removes published pages
- Deletes catalog entry
- Deletes static artifacts
- Updates TOC

**Services Used**:
- DbContext
- catalogService (delete)
- publishingService (TOC)
- storageContext (static files)
- slugService (referenced)

**Side Effects**:
- Soft-deletes article (StatusCode = Deleted)
- Removes Pages
- Removes catalog entry
- Deletes static HTML files
- Updates table of contents

**Edge Cases**:
- Root page cannot be deleted (throws NotSupportedException)
- Static file deletion protected from /pub

**Migration Path**:
```
DeleteArticle() ? DeleteArticleCommand + Handler
```

---

#### 5. ? RestoreArticle (PRIORITY 4 - MEDIUM VALUE)
**Status**: Public method
**File**: `Editor\Data\Logic\ArticleEditLogic.cs`
**Lines**: 1005-1038
**Signature**:
```csharp
public async Task RestoreArticle(
    int articleNumber, 
    string userId)
```

**Complexity**: **MEDIUM** (10 hours)

**What It Does**:
- Gets article by number
- Checks for title conflicts
- If conflict, renames with counter
- Normalizes URL path
- Updates all versions to Active
- Clears published timestamp
- Recreates catalog entry

**Services Used**:
- DbContext
- slugService (normalize)
- catalogService (implicit - adds catalog entry)

**Side Effects**:
- Updates StatusCode to Active
- May rename article title
- Updates UrlPath
- Clears Published timestamp
- Creates new catalog entry

**Edge Cases**:
- Title conflict resolution (append number)
- Only affects Deleted articles

**Migration Path**:
```
RestoreArticle() ? RestoreArticleCommand + Handler
```

---

#### 6. ? NewVersion (PRIORITY 5 - LOW VALUE)
**Status**: [Obsolete]
**File**: `Editor\Data\Logic\ArticleEditLogic.cs`
**Lines**: 927-950
**Signature**:
```csharp
public async Task<Article> NewVersion(Article article)
```

**Complexity**: **SIMPLE** (5 hours)

**What It Does**:
- Counts existing versions
- Creates new Article entity
- Sets version number to count + 1
- Copies all properties
- Generates new ID
- Sets Published to null
- Adds to database

**Services Used**:
- DbContext (only)

**Side Effects**:
- Creates new Article record
- Does NOT publish
- Does NOT update catalog
- Does NOT create static files

**Migration Path**:
```
NewVersion() ? CreateArticleVersionCommand + Handler
```

---

## ?? PRIORITY RANKING FOR FAST TRACK

### Sprint 1 (Weeks 5-6): CreateArticle
- **Reason**: Most used, high value, proven pattern
- **Effort**: 10 hours
- **Dependencies**: None (or minimal)
- **Tests**: Extensive

### Sprint 2 (Weeks 7-8): PublishArticle
- **Reason**: High value, complex side effects, CDN integration
- **Effort**: 10 hours
- **Dependencies**: CreateArticle (builds on it)
- **Tests**: Extensive

### Sprint 3 (Weeks 9-10): DeleteArticle + RestoreArticle
- **Reason**: Pair them (opposite operations)
- **Effort**: 10 hours (both)
- **Dependencies**: PublishArticle (needs publishing logic)
- **Tests**: Edge cases important

### Sprint 4 (Weeks 11-12): NewVersion + Cleanup
- **Reason**: Simple, finalize, document
- **Effort**: 5 hours
- **Dependencies**: CreateArticle (related)
- **Tests**: Basic

---

## ?? COMPLEXITY BREAKDOWN

| Complexity | Methods | Hours | Notes |
|-----------|---------|-------|-------|
| **Simple** | NewVersion | 5 | Only uses DbContext |
| **Medium** | CreateArticle, PublishArticle, DeleteArticle, RestoreArticle | 40 | Multiple services, side effects |
| **Complex** | None | 0 | All are manageable |

---

## ?? EFFORT ESTIMATE DETAIL

| Method | Create | Test | Controller | Docs | Total |
|--------|--------|------|------------|------|-------|
| CreateArticle | 4 hrs | 3 hrs | 1 hr | 2 hrs | **10 hrs** |
| PublishArticle | 3 hrs | 3 hrs | 1 hr | 1 hr | **8 hrs** |
| DeleteArticle | 3 hrs | 3 hrs | 1 hr | 1 hr | **8 hrs** |
| RestoreArticle | 3 hrs | 3 hrs | 1 hr | 1 hr | **8 hrs** |
| NewVersion | 2 hrs | 1 hr | 0 hrs | 1 hr | **4 hrs** |
| **TOTAL** | **15 hrs** | **13 hrs** | **4 hrs** | **6 hrs** | **38 hrs** |

**Plus:**
- Refactoring existing tests: ~10-15 hours
- Integration testing: ~5-10 hours
- Documentation: ~5 hours
- Buffer/contingency: ~5-10 hours

**Grand Total**: ~60-75 hours (~8-9 weeks @ 1 FTE)

---

## ?? DEPENDENCIES & RELATIONSHIPS

```
CreateArticle
    ? (calls if first article)
PublishArticle
    ? (uses catalog update)
UpsertCatalogEntry (private)

DeleteArticle
    ? (calls)
DeleteCatalogEntry (private)
    ? (uses)
DeleteStaticWebpage (private)

RestoreArticle
    ? (calls)
UpsertCatalogEntry (private)

NewVersion
    ? (independent)
(no dependencies)
```

---

## ?? PRODUCTION CODE REFERENCES

These methods are called from:
1. **EditorController.cs** - Direct calls in action methods
2. **Razor Pages** - Via PageModel base classes
3. **Tests** - Via test logic (already partially refactored)
4. **Other services** - Possible indirect calls

**Action Items**:
- [ ] Search EditorController for CreateArticle calls
- [ ] Search EditorController for PublishArticle calls
- [ ] Search EditorController for DeleteArticle calls
- [ ] Search EditorController for RestoreArticle calls
- [ ] Search Razor Pages for direct calls
- [ ] Search services for calls

---

## ? WEEK 1 COMPLETION CHECKLIST

- [x] All [Obsolete] methods identified
- [x] All public methods documented
- [x] Complexity categorized
- [x] Effort estimated
- [x] Priority ranking created
- [x] Dependencies mapped
- [x] Service usage documented
- [x] Side effects listed
- [x] Test requirements identified
- [x] Production code references noted

---

## ?? NEXT STEPS (Week 2)

### Week 2: Dependency Analysis
**Goals**:
- [ ] Map method call chains
- [ ] Identify service dependencies
- [ ] Document integration points
- [ ] Plan handler structure
- [ ] Estimate data flow

### Week 3: Risk & Effort Assessment
**Goals**:
- [ ] Verify effort estimates
- [ ] Identify technical risks
- [ ] Plan mitigation
- [ ] Create detailed risk matrix

### Week 4: Final Roadmap
**Goals**:
- [ ] Create sprint breakdown
- [ ] Schedule each method
- [ ] Resource planning
- [ ] Executive presentation

---

## ?? SUPPORTING DOCUMENTATION

Reference these for implementation:
- SaveArticle: Completed CQRS migration (use as template)
- SaveArticleHandler: Handler implementation pattern
- SaveArticleCommand: Command structure
- EditorController: Integration pattern
- SaveArticleTests: Test pattern

---

## ?? READY FOR WEEK 2

**Status**: ? **WEEK 1 AUDIT COMPLETE**

**Deliverables**:
- ? Complete inventory (this document)
- ? Complexity analysis
- ? Effort estimates
- ? Priority ranking
- ? Dependency mapping

**Next Meeting**: Week 2 Kickoff (Dependency Analysis)

**Questions for Team**:
1. Do these complexity estimates align with your experience?
2. Any other methods we missed?
3. Are there additional constraints we should know?
4. Ready to proceed with Week 2 dependency analysis?

---

**Week 1 Status: ? COMPLETE - Ready to advance to Week 2**
