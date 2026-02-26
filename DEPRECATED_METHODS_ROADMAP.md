# ?? DEPRECATED METHODS ELIMINATION - ROADMAP

**Status**: CreateArticle ? DELETED | Build PASSING

---

## ?? DEPRECATED METHODS INVENTORY

### ? **ALREADY DELETED/COMMENTED OUT**:
1. ? **CreateArticle** - DELETED (just now)
2. ? **SaveArticle** - DELETED (earlier)
3. ? **GetLastPublishedDate** - Commented out
4. ? **GetArticleByArticleNumber** - Commented out
5. ? **GetArticleById** - Commented out
6. ? **GetArticleByUrl** (all 3 overloads) - Commented out
7. ? **GetArticleRedirects** - Commented out
8. ? **GetCatalogEntry** (both overloads) - Commented out

### ?? **STILL ACTIVE & MARKED [OBSOLETE]**:

#### 1. **NewVersion** (PRIORITY: HIGH)
- **Location**: Editor\Data\Logic\ArticleEditLogic.cs (lines ~480-500)
- **Replacement**: `CreateArticleVersionCommand` via IMediator
- **Status**: Still has implementation
- **Usage**: Tests likely use it for creating versions
- **Action**: Delete after tests updated

#### Other potentially deprecated methods not yet marked [Obsolete]:
- CreateHomePage
- DeleteArticle  
- RestoreArticle
- PublishArticle
- (These are core functions - may not be deprecated yet)

---

## ?? PHASE PLAN: REMAINING DEPRECATIONS

### **SPRINT 4: NewVersion Elimination** (Quick - 30 min)
1. **Step 1**: Search for test references to `NewVersion`
2. **Step 2**: Check if `CreateArticleVersionCommand` handler exists
3. **Step 3**: Create helper method in test base (if needed)
4. **Step 4**: Update all test calls
5. **Step 5**: Delete NewVersion method
6. **Step 6**: Verify build & tests

### **FUTURE SPRINTS**: Evaluate other methods
- DeleteArticle ? DeleteArticleCommand (exists! ?)
- RestoreArticle ? RestoreArticleCommand (need to check)
- PublishArticle ? PublishArticleCommand (exists! ?)
- CreateHomePage ? (custom command or refactor as controller method)

---

## ?? METHODS THAT LIKELY HAVE CQRS REPLACEMENTS ALREADY

Looking at your existing handler files open:
- ? **DeleteArticleCommand** exists (SPRINT3)
- ? **PublishArticleHandler** exists (SPRINT2)
- ? **RestoreArticleValidator** exists (SPRINT3)
- ? **CreateArticleVersionCommand** exists (SPRINT3 end)

**This means you likely have handlers for ALL deprecated methods!**

---

## ?? ESTIMATED EFFORT

| Method | Effort | Complexity | Status |
|--------|--------|-----------|--------|
| NewVersion | 20 min | Low | READY |
| CreateHomePage | 30 min | Medium | Scoped method? |
| DeleteArticle | 20 min | Low | Handler exists |
| RestoreArticle | 20 min | Low | Handler exists |
| PublishArticle | 20 min | Low | Handler exists |

---

## ?? NEXT IMMEDIATE STEP

**Shall we eliminate `NewVersion` now? (5 minute job)**

It's the quickest win:
1. ? Handler likely exists
2. ? Tests probably use it (easy to update)
3. ? Simple logic (can be deleted fast)
4. ? Small surface area

---

## ?? COMPLETION CHECKLIST

- [x] CreateArticle - DELETED ?
- [x] SaveArticle - DELETED ? (earlier)
- [ ] NewVersion - NEXT TARGET
- [ ] DeleteArticle - Follow-up
- [ ] RestoreArticle - Follow-up
- [ ] PublishArticle - Follow-up
- [ ] CreateHomePage - Follow-up

---

**Ready to tackle NewVersion? Or shall we assess all handlers first?** ??
