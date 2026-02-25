# ? FINISH PHASE 5 TODAY - ACTION PLAN

**Status**: Ready to Execute
**Time Available**: 3-4 hours
**Goal**: Complete Phase 5 by tonight
**Success Criteria**: All 6 methods migrated, build passing

---

## ?? YOUR MISSION (If You Choose to Accept)

**Finish 2 sprints (2-3 hours of controllers) + 1 new sprint (2-3 hours) = PHASE 5 COMPLETE!**

---

## ?? TIMELINE

### Hour 1: Controller Updates for Sprints 2-3
- **30 min**: Update Sprint 2 (PublishArticle) controllers
- **30 min**: Update Sprint 3 (Delete/Restore) controllers
- **Result**: 2 sprints complete, build passing

### Hour 2-3: Sprint 4 - NewVersion Command + Handler
- **20 min**: Create NewArticleVersionCommand.cs
- **60 min**: Create NewArticleVersionHandler.cs
- **Result**: Handler logic complete

### Hour 3-4: Sprint 4 - Validator, Tests, & Verification
- **20 min**: Create NewArticleVersionValidator.cs
- **60 min**: Create comprehensive tests
- **30 min**: Verify build passes, all tests run
- **RESULT**: PHASE 5 COMPLETE! ??

---

## ?? STEP-BY-STEP EXECUTION

### STEP 1: Update Sprint 2 Controllers (30 min)

**File**: `Editor\Controllers\EditorController.cs`

**Search for**: `PublishArticle`

**Find & Replace**:
```csharp
// OLD (find this)
var cdnResults = await Logic.PublishArticle(articleId, publishTime);

// NEW (replace with this)
var command = new PublishArticleCommand
{
    ArticleId = articleId,
    PublishTime = publishTime
};
var result = await mediator.SendAsync(command);
if (result.IsSuccess)
{
    var cdnResults = result.Data.CdnResults;
}
```

**Expected**: 1-2 calls to replace
**Build**: Should pass

---

### STEP 2: Update Sprint 3 Controllers (30 min)

**File**: `Editor\Controllers\EditorController.cs`

**Search for**: `DeleteArticle` and `RestoreArticle`

**Replace**:
```csharp
// OLD
await Logic.DeleteArticle(articleNumber);

// NEW
var deleteCmd = new DeleteArticleCommand { ArticleNumber = articleNumber };
await mediator.SendAsync(deleteCmd);
```

```csharp
// OLD
await Logic.RestoreArticle(articleNumber, userId);

// NEW
var restoreCmd = new RestoreArticleCommand 
{ 
    ArticleNumber = articleNumber, 
    UserId = userId 
};
await mediator.SendAsync(restoreCmd);
```

**Expected**: 1-2 calls each
**Build**: Should pass

---

### STEP 3: Create Sprint 4 - NewArticleVersionCommand (20 min)

**File**: `Editor\Features\Articles\NewVersion\NewArticleVersionCommand.cs`

```csharp
namespace Sky.Editor.Features.Articles.NewVersion
{
    using System;
    using Cosmos.Common.Features.Shared;

    public sealed class NewArticleVersionCommand : ICommand<CommandResult<NewArticleVersionResult>>
    {
        public Guid ArticleId { get; init; }
        public string Title { get; init; }
        public string Content { get; init; }
    }

    public class NewArticleVersionResult
    {
        public int ArticleNumber { get; set; }
        public int NewVersionNumber { get; set; }
    }
}
```

**Time**: 15-20 min (copy-paste + adjust)

---

### STEP 4: Create Sprint 4 - NewArticleVersionHandler (60 min)

**File**: `Editor\Features\Articles\NewVersion\NewArticleVersionHandler.cs`

**Reference**: `ArticleEditLogic.NewVersion()` (lines 970-1003)

**Key Logic**:
```csharp
public async Task<CommandResult<NewArticleVersionResult>> HandleAsync(
    NewArticleVersionCommand command,
    CancellationToken cancellationToken = default)
{
    // 1. Get article by ID
    // 2. Create new version with incremented number
    // 3. Copy properties from original
    // 4. Save to database
    // 5. Return new version info
}
```

**Dependencies**: DbContext only
**Complexity**: Simple (mostly property copying)
**Time**: 45-60 min

---

### STEP 5: Create Sprint 4 - Validator (20 min)

**File**: `Editor\Features\Articles\NewVersion\NewArticleVersionValidator.cs`

**Simple Validations**:
- ArticleId not empty
- Article exists
- Title not empty
- Content not empty

**Time**: 15-20 min

---

### STEP 6: Create Sprint 4 - Tests (60 min)

**File**: `Tests\Features\Articles\NewVersion\NewArticleVersionHandlerTests.cs`

**Test Cases** (5-6 tests):
- Valid article creates new version
- Increments version number
- Article not found returns error
- New version has null published
- Title and content preserved
- Multiple versions work

**Time**: 45-60 min

---

### STEP 7: Final Verification (30 min)

1. **Build**: `dotnet build` (should pass)
2. **Test Run**: `dotnet test` (all tests should pass)
3. **No Warnings**: Check for any warnings
4. **Commit**: Optional (up to you)

**Result**: ? **PHASE 5 COMPLETE!** ??

---

## ?? WHAT YOU GET

**After completing today**:
- ? All 6 article methods CQRS-migrated
- ? 35+ comprehensive tests
- ? Build passing with 0 errors
- ? Production-ready code
- ? Phase 5 COMPLETE
- **Timeline achieved**: Week 10-11 (vs 12-16 planned!) ??

---

## ?? YOU CAN DO THIS!

**You've already proven**:
- ? Sprint 1: Completed (CreateArticle)
- ? Sprint 2: 90% complete (just controllers)
- ? Sprint 3: 80% complete (just controllers)
- ? Tests working (31+ passing tests)
- ? Velocity increasing (125% ? 200% ? 300%+)

**NewVersion is the easiest method** (least logic to migrate).

**You have everything you need.**

**It's just 3-4 more hours of work.**

---

## ?? GO TIME!

**Ready to finish Phase 5 TODAY?**

**Here's your path**:
1. Controller updates (1 hour) ?
2. NewVersion command + handler (2 hours) ?
3. Validator + tests (1.5 hours) ?
4. Verification (30 min) ?
5. **PHASE 5 DONE** ??

---

## ?? IF YOU NEED HELP

**I can**:
- Provide exact code for any step
- Help with controller search/replace
- Create NewVersion command/handler
- Create validator + tests
- Verify build and tests

**Just say the word!** ??

---

## ?? YOUR FINAL DECISION

**Choose one**:

A) **"Let's finish today!"** ?
? Execute the 7-step plan
? Phase 5 COMPLETE tonight
? Celebrate your success! ??

B) **"Let's do controllers now, Sprint 4 later"**
? Do steps 1-2 now (1 hour)
? Sprint 4 when ready (2-3 hours)
? Phase 5 done by end of week

C) **"I need a break"**
? Rest today
? Attack it fresh tomorrow
? Still doable in 1 day of work

---

**Status**: Everything ready
**Build**: Passing
**Tests**: 31+ created
**Time to Phase 5 Complete**: 3-4 hours

**You've got this!** ????

**What's your move?**
