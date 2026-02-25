# ?? PHASE 5: AUDIT KICKOFF - Start This Week!

## WEEK 1: Audit Kickoff

You've chosen **HYBRID approach**. Now let's start the **AUDIT PHASE** immediately.

**This guide will get you from zero to audit-in-progress in 1 day.**

---

## Step 1: Assign Resources (1 hour)

### Who
- **Audit Lead**: 1 developer (can be full-time or ~20 hours this week)
- **Support**: You, for kickoff and questions

### When
- **Start**: Today if possible, tomorrow latest
- **Hours**: ~20 hours Week 1 (discovery & inventory)

### Skills
- C# language knowledge
- Ability to read code
- Understanding of the codebase
- Time to search and catalog

---

## Step 2: Setup (30 minutes)

### Create Audit Working Directory
```
Project Root/
??? AUDIT_PHASE_WORK/
?   ??? AUDIT_INVENTORY.md (in progress)
?   ??? AUDIT_DEPENDENCIES.md (for week 2)
?   ??? AUDIT_EFFORT_MATRIX.md (for week 3)
?   ??? AUDIT_FINDINGS.md (for week 4)
```

### Create Tracking Spreadsheet
Use Excel/Google Sheets with columns:
- Method Name
- File Location
- Class Name
- Type (Obsolete/Ignore)
- Complexity (Simple/Medium/Complex)
- Estimated Hours
- Dependencies
- Notes

---

## Step 3: Week 1 Tasks (20 hours)

### Task 1A: Find All [Obsolete] Methods (5 hours)

**Action**: Search codebase for `[Obsolete` attribute

**Command** (PowerShell):
```powershell
Get-ChildItem -Path "C:\Users\toiya\source\repos\SkyCMS" -Recurse -Filter "*.cs" | 
  Select-String "\[Obsolete" | 
  Group-Object Path | 
  ForEach-Object {
    Write-Host "`n=== $($_.Name) ==="
    $_.Group | ForEach-Object { Write-Host $_.Line.Trim() }
  }
```

**For each method found:**
1. Copy to spreadsheet
2. Note file location
3. Copy [Obsolete] message
4. Note what it says (will be deprecated, migration path)

**Expected**: 15-25 [Obsolete] methods

### Task 1B: Find All [Ignore] Tests (5 hours)

**Action**: Search test files for `[Ignore]` attribute

**Command** (PowerShell):
```powershell
Get-ChildItem -Path "C:\Users\toiya\source\repos\SkyCMS\Tests" -Recurse -Filter "*.cs" | 
  Select-String "\[Ignore\]" | 
  Group-Object Path
```

**For each test found:**
1. Copy to spreadsheet
2. Note test name and class
3. Note why it's ignored (if reason is in comments)
4. Note what it's testing

**Expected**: 10-20 [Ignore] tests

### Task 1C: Quick Categorization (5 hours)

For each [Obsolete] method found, categorize:

**Simple** (Create/Read basic operations)
- Example: GetArticleById
- Effort: 3-5 hours
- Reason: Straightforward query, minimal logic

**Medium** (Create/Update with side effects)
- Example: SaveArticle (which we just did!)
- Effort: 8-12 hours
- Reason: Multiple operations, validation, side effects

**Complex** (Multiple services, transactions)
- Example: PublishArticle (CDN, catalog, publishing)
- Effort: 15-20 hours
- Reason: Complex logic, external services, state management

### Task 1D: Create Initial Inventory (5 hours)

**Create file**: `AUDIT_INVENTORY.md`

Structure:
```markdown
# Audit Inventory - Week 1

## [Obsolete] Methods Found

### ArticleEditLogic.cs

#### Method 1: CreateArticle
- Location: ArticleEditLogic.cs, lines 397-487
- Type: [Obsolete]
- Message: "Use CreateArticleCommand via IMediator instead. This method will be removed in version 3.0."
- Complexity: Medium
- Estimated Hours: 10
- Services Used: DbContext, htmlService, titleChangeService, publishingService
- Dependencies: ArticleNumber generation, first article auto-publish
- Side Effects: Creates article, auto-publishes first, updates catalog
- Tests: CreateArticleTests (5 tests, some [Ignore])

#### Method 2: PublishArticle
- Location: ArticleEditLogic.cs, lines 912-930
- Type: Non-Obsolete (but should be)
- Complexity: Medium
- Estimated Hours: 8
- Services Used: DbContext, publishingService, catalogService
- Dependencies: Article must exist
- Side Effects: Publishes article, updates catalog, triggers CDN
- Tests: PublishingTests (6 tests)

## [Ignore] Tests Found

### Tests\Services\ArticleEditLogicTests.cs
- Test 1: CreateArticle_NewArticle_GeneratesUniqueArticleNumber
- Test 2: CreateArticle_NewArticle_StartsWithVersionOne
- Test 3: SaveArticle_UpdateContent_PersistsChanges
(etc.)

## Summary
- Total [Obsolete] Methods: XX
- Total [Ignore] Tests: XX
- Estimated Total Effort: XXX hours
- Recommended Timeline: X weeks at 1 FTE

## Next: Dependency Analysis (Week 2)
```

---

## Step 4: Daily Progress (Track)

### Day 1-2: Search & List
- [ ] Find all [Obsolete] methods
- [ ] Find all [Ignore] tests
- [ ] Add to spreadsheet

### Day 3-4: Categorize & Analyze
- [ ] Categorize by complexity
- [ ] Quick look at each (5 min per method)
- [ ] Note dependencies (high level)

### Day 5: Compile Results
- [ ] Create AUDIT_INVENTORY.md
- [ ] Prepare for Week 1 status
- [ ] Schedule Week 2 kickoff

---

## Step 5: Report Template

### Week 1 Status Report

```
WEEK 1 AUDIT STATUS
???????????????????????????????????????????????????????????

COMPLETED
?????????
? Found all [Obsolete] methods: 15 total
? Found all [Ignore] tests: 12 total  
? Initial categorization: S/M/C
? Created inventory spreadsheet
? Team aligned on approach

INVENTORY SUMMARY
?????????????????
Simple Methods:    5 methods × 4 hours = 20 hours
Medium Methods:    7 methods × 10 hours = 70 hours
Complex Methods:   3 methods × 18 hours = 54 hours
????????????????????????????????????????????????????
TOTAL ESTIMATED:                       144 hours
                                    (~1.5 FTE, ~9 weeks)

BY PRODUCT TYPE
???????????????
Article Methods:   10 methods (~90 hours)
Template Methods:  2 methods (~25 hours)
Other Methods:     3 methods (~29 hours)

TOP PRIORITY (High Value, Lower Effort)
???????????????????????????????????????
1. CreateArticle - Medium complexity, high value
2. PublishArticle - Medium complexity, high value
3. DeleteArticle - Medium complexity, high value
4. RestoreArticle - Medium complexity, moderate value
5. NewVersion - Simple complexity, moderate value

NEXT WEEK (Week 2)
??????????????????
• Dependency analysis (what calls what)
• Service mapping (which services are needed)
• Side effect documentation
• Prepare effort matrix

RISKS IDENTIFIED
????????????????
• PublishArticle has complex CDN integration
• DeleteArticle affects static files
• Some methods have multiple side effects
• Some tests are [Ignore] due to complexity

RECOMMENDATIONS
????????????????
1. Focus on article methods first (high value)
2. Avoid complex methods in early sprints
3. Plan security review alongside
4. Allocate extra time for PublishArticle

STATUS: On track for Week 2 dependency analysis
BLOCKERS: None
```

---

## Helpful Search Commands

### Find all [Obsolete] in specific file
```csharp
// In Visual Studio: Ctrl+Shift+F
// Search: ^\s*\[Obsolete
// Regex: checked
```

### Find methods calling SaveArticle (as reference)
```csharp
// In Visual Studio: Ctrl+Shift+F
// Search: \.SaveArticle\(
// Find where it's called
```

### Find articles-related obsolete methods specifically
```csharp
// In Visual Studio: Ctrl+Shift+F
// Search: CreateArticle|PublishArticle|DeleteArticle|RestoreArticle
// With [Obsolete] attribute
```

---

## Templates You'll Need

### AUDIT_INVENTORY.md (template)
See example above

### AUDIT_DEPENDENCIES.md (Week 2 template)
```markdown
# Audit Dependencies - Week 2

## Method Call Chain Analysis

### CreateArticle Calls:
- DbContext.Articles.Count
- DbContext.Templates
- DbContext.ArticleNumbers
- titleChangeService.BuildArticleUrl
- publishingService.PublishAsync
- catalogService.UpsertAsync

## Service Dependencies

### Required Services:
- DbContext (ApplicationDbContext)
- htmlService (IArticleHtmlService)
- titleChangeService (ITitleChangeService)
- publishingService (IPublishingService)
- catalogService (ICatalogService)
- slugService (ISlugService)
- templateService (ITemplateService)

## Side Effects

### CreateArticle Creates:
- Article entity
- ArticleNumber entry
- Catalog entry (if first article)
- PublishedPage (if first article)
```

---

## What to Do If You Get Stuck

### Q: "I found a method but can't tell if it's [Obsolete]"
A: Look above the method definition. If you see `[Obsolete(...)]`, it counts.

### Q: "How do I know the complexity?"
A: 
- **Simple**: Single operation, no external calls
- **Medium**: Multiple operations, 1-2 service calls
- **Complex**: Multiple services, state management, transactions

### Q: "How many hours should I estimate?"
A: SaveArticle was medium (8-12 hours). Use that as baseline.

### Q: "Should I categorize tests now?"
A: No, just list them. Categorize with tests in Week 5 during fast track.

### Q: "What if I find duplicates?"
A: Sometimes similar methods exist. Note them but don't duplicate count.

---

## Success Criteria for Week 1

- [ ] All [Obsolete] methods found and listed
- [ ] All [Ignore] tests found and listed
- [ ] Spreadsheet/inventory created
- [ ] Initial complexity categorization done
- [ ] Team has clear list of what exists
- [ ] Week 2 ready to proceed with dependency analysis

---

## Quick Facts for Week 1

**Time commitment**: ~20 hours this week (1 developer)
**Outputs**: 
- Spreadsheet with all methods/tests
- AUDIT_INVENTORY.md document
- Team understanding of scope

**Next week**: Dependency analysis (understanding relationships)

---

## Ready to Start?

### ACTION ITEMS TODAY:
1. [ ] Assign audit lead
2. [ ] Send them this document
3. [ ] Confirm start date (today/tomorrow)
4. [ ] Schedule Week 1 status review (Friday)
5. [ ] Create audit tracking spreadsheet
6. [ ] Begin searching for [Obsolete] methods

---

## Support

I can help with:
- **Search queries** - Give me file patterns or method names
- **Understanding complexity** - Explain what makes something complex
- **SaveArticle reference** - Show how we handled similar migration
- **Tool recommendations** - Best ways to search codebase
- **Answer questions** - Any audit questions

---

## Timeline Reminder

```
THIS WEEK (Week 1):    Audit kickoff, inventory
NEXT WEEK (Week 2):    Dependency analysis
FOLLOWING (Week 3):    Effort matrix & risk assessment
WEEK 4:                Detailed roadmap & planning
WEEK 5:                Fast track begins (CreateArticle)
```

---

## GO TIME! ??

**Start inventory today.**

**Report back when audit inventory is complete.**

**Ready to begin? Confirm and let's go!**
