# Phase 1 Optional Tasks - TODO

**Status:** Optional / As-Needed  
**Priority:** Low (can be done incrementally)

---

## 📋 **Remaining Optional Tasks**

### 1. Update Call Sites to Use CQRS Queries
**Scope:** Find and update existing `ArticleLogic` instantiations to use new CQRS queries

**Impact:** Low priority - obsolete warnings already guide developers

**Tasks:**
- [ ] Search for `new ArticleLogic(` in Editor project
- [ ] Search for `new ArticleLogic(` in Publisher project
- [ ] Replace with `IMediator` injection and appropriate queries
- [ ] Update tests that instantiate `ArticleLogic` directly

**Example Migration:**
```csharp
// Before
var articleLogic = new ArticleLogic(dbContext, cache, publisherUrl, blobUrl, isEditor);
var sitemap = await articleLogic.GetSiteMap();

// After
var sitemap = await _mediator.QueryAsync(new GetSitemapQuery());
```

**Estimated Effort:** 2-4 hours (depends on number of call sites)

---

### 2. Write Unit Tests for Query Handlers
**Scope:** Create unit tests for the 4 new query handlers

**Tasks:**
- [ ] Create test class for `GetSitemapQueryHandler`
  - Test with various article states (published, future, expired)
  - Test home page (root) handling
  - Test banner image URL generation
- [ ] Create test class for `GetDefaultLayoutQueryHandler`
  - Test caching behavior (cache hit/miss)
  - Test with no cache
  - Test Published date filtering
- [ ] Create test class for `BuildArticleViewModelQueryHandler`
  - Test with article that has author
  - Test with article without author
  - Test includeLayout flag
- [ ] Create test class for `BuildPublishedPageViewModelQueryHandler`
  - Test OG metadata generation
  - Test banner image URL handling (relative vs absolute)
  - Test layout caching

**Test Strategy:**
- Mock `IArticleViewModelBuilder` for ViewModel building tests
- Mock `IApplicationDbContext` with in-memory DbSets for data access tests
- Mock `IMemoryCache` for caching tests
- Use MOQ or NSubstitute for mocking

**Estimated Effort:** 4-6 hours

---

### 3. Add Example Usage to README
**Scope:** Document CQRS pattern usage in main README

**Tasks:**
- [ ] Add "Architecture" section to README
- [ ] Include CQRS pattern overview
- [ ] Link to `ARTICLELOGIC_MIGRATION_GUIDE.md`
- [ ] Add before/after code examples
- [ ] Document mediator registration (if not already documented)

**Estimated Effort:** 1 hour

---

## 📊 **Progress Tracking**

**Total Optional Tasks:** 3  
**Completed:** 0  
**In Progress:** 0  
**Not Started:** 3

---

## 🎯 **When to Complete These**

These tasks should be completed:
- **Update Call Sites:** When touching code that uses `ArticleLogic` (incremental)
- **Unit Tests:** Before Phase 4 (when `ArticleLogic` is removed)
- **README Updates:** When onboarding new developers or releasing major version

---

**Document Version:** 1.0  
**Created:** 2025-01-11  
**Status:** Active TODO List
