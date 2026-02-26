# ?? NEXT PHASE: REMAINING DEPRECATED METHODS

**Status**: CreateArticle ? DELETED | Build ? PASSING
**Current Target**: NewVersion and remaining deprecated methods

---

## ?? DEPRECATED METHODS STILL IN ARTICLEEDITLOGIC

### Active [Obsolete] Methods Found:

1. **NewVersion** (Line ~490)
   - Status: ? Handler EXISTS (CreateArticleVersionHandler)
   - Tests: Minimal usage (appears in Versions.cshtml mostly)
   - Complexity: LOW
   - Effort: 20 minutes

---

## ?? ARCHITECTURE REVIEW

Based on your SPRINT files, these handlers ALREADY EXIST:
- ? **CreateArticleVersionHandler** - For creating versions
- ? **PublishArticleHandler** - For publishing (SPRINT2)
- ? **DeleteArticleHandler** - For deleting (SPRINT3)
- ? **RestoreArticleHandler** - For restoring (SPRINT3)

**This means all core article operations have CQRS handlers!**

---

## ?? RECOMMENDED NEXT STEP

### **Option 1: Quick Cleanup (Recommended)** ?
Delete the remaining obsolete method now:
1. ? NewVersion method (straightforward)
2. ? Verify tests still work
3. ? Done!

**Time**: 20 minutes | **Risk**: Low

### **Option 2: Comprehensive Audit** 
Review if other methods should be marked obsolete:
- CreateHomePage - Still active, no handler yet
- DeleteArticle - Has handler, might be obsolete
- RestoreArticle - Has handler, might be obsolete
- PublishArticle - Has handler, might be obsolete

**Time**: 1-2 hours | **Risk**: Medium

---

## ?? MY RECOMMENDATION

**Start with Option 1** (delete NewVersion), then assess if other methods need elimination.

The handlers already exist for most operations, which means:
- ? Code is already modern (vertical slice CQRS architecture)
- ? Just need to update references in ArticleEditLogic
- ? All the heavy lifting is done!

---

## ?? WHAT'S NEXT IF WE DELETE NEWVERSION

After NewVersion is gone, evaluate:

| Method | Handler | Status | Complexity |
|--------|---------|--------|-----------|
| DeleteArticle | ? Exists | Legacy method still used? | Medium |
| RestoreArticle | ? Exists | Legacy method still used? | Medium |
| PublishArticle | ? Exists | Legacy method still used? | Medium |
| CreateHomePage | ? None | Refactor as controller method? | High |

---

## ? READY TO PROCEED?

**Shall we:**
1. Delete NewVersion method now? (5 min)
2. Search for test usage of NewVersion? (2 min)
3. Create helper method if needed? (5 min)
4. Update tests? (5 min)
5. Verify build? (1 min)

**Total**: ~18 minutes for NewVersion elimination! ??

---

**Let me know if you want to:**
- Continue with NewVersion elimination
- Do a full audit of all methods first
- Focus on a specific deprecated method
