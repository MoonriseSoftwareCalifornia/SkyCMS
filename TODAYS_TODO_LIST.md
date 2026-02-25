# ? TODAY'S TO-DO LIST - SPRINT 2 KICKOFF

**What**: Sprint 2 (PublishArticle) Immediate Start
**Timeline**: Today/Tomorrow
**Effort**: 4-6 hours
**Goal**: Create PublishArticleCommand + Start PublishArticleHandler

---

## ?? YOUR MISSION TODAY

Get PublishArticleCommand and PublishArticleHandler foundation in place so the team can continue tomorrow.

---

## ? QUICK CHECKLIST (30 minutes prep)

- [ ] Read this document
- [ ] Skim `SPRINT2_ACTION_ITEMS.md`
- [ ] Have `ArticleEditLogic.cs` open (lines 912-930)
- [ ] Have `CreateArticleHandler.cs` open (reference)
- [ ] Ready to code!

---

## ?? STEP 1: CREATE PublishArticleCommand (10 min)

**File**: `Editor\Features\Articles\Publish\PublishArticleCommand.cs`

**Copy This Code**:
```csharp
namespace Sky.Editor.Features.Articles.Publish
{
    using System;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;

    /// <summary>
    /// Command to publish an article.
    /// Replaces deprecated ArticleEditLogic.PublishArticle() method.
    /// </summary>
    public sealed class PublishArticleCommand : ICommand<CommandResult<PublishResult>>
    {
        /// <summary>
        /// Gets the article ID to publish.
        /// </summary>
        public Guid ArticleId { get; init; }

        /// <summary>
        /// Gets the optional publish timestamp (UTC).
        /// If null, current time is used.
        /// </summary>
        public DateTimeOffset? PublishDate { get; init; }

        /// <summary>
        /// Gets a value indicating whether to force republish.
        /// If true, updates catalog and CDN even if already published.
        /// </summary>
        public bool ForceRepublish { get; init; } = false;
    }
}
```

**Actions**:
1. Create folder: `Editor\Features\Articles\Publish\`
2. Create file: `PublishArticleCommand.cs`
3. Copy code above
4. Save

? **Done!**

---

## ?? STEP 2: CREATE PublishResult DTO (5 min)

**File**: `Editor\Features\Articles\Publish\PublishResult.cs`

**Copy This Code**:
```csharp
namespace Sky.Editor.Features.Articles.Publish
{
    using System;
    using System.Collections.Generic;
    using Cosmos.Common.Models;

    /// <summary>
    /// Result of article publication operation.
    /// </summary>
    public class PublishResult
    {
        /// <summary>
        /// Gets or sets the article ID that was published.
        /// </summary>
        public Guid ArticleId { get; set; }

        /// <summary>
        /// Gets or sets the article number.
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the publish timestamp.
        /// </summary>
        public DateTimeOffset PublishedDate { get; set; }

        /// <summary>
        /// Gets or sets the CDN purge results.
        /// </summary>
        public List<CdnResult> CdnResults { get; set; } = new();
    }
}
```

**Actions**:
1. Create file: `Editor\Features\Articles\Publish\PublishResult.cs`
2. Copy code above
3. Save

? **Done!**

---

## ?? STEP 3: CREATE PublishArticleValidator (15 min)

**File**: `Editor\Features\Articles\Publish\PublishArticleValidator.cs`

**Copy This Code**:
```csharp
namespace Sky.Editor.Features.Articles.Publish
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using FluentValidation;
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// Validates PublishArticleCommand requests.
    /// </summary>
    public class PublishArticleValidator : AbstractValidator<PublishArticleCommand>
    {
        private readonly ApplicationDbContext dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublishArticleValidator"/> class.
        /// </summary>
        public PublishArticleValidator(ApplicationDbContext dbContext = null)
        {
            this.dbContext = dbContext;

            RuleFor(x => x.ArticleId)
                .NotEqual(System.Guid.Empty)
                .WithMessage("Article ID is required");

            if (dbContext != null)
            {
                RuleFor(x => x)
                    .MustAsync(ValidateArticleExistsAsync)
                    .WithMessage("Article not found");

                RuleFor(x => x)
                    .MustAsync(ValidateArticleNotDeletedAsync)
                    .WithMessage("Cannot publish deleted article");
            }
        }

        private async Task<bool> ValidateArticleExistsAsync(
            PublishArticleCommand command,
            CancellationToken ct)
        {
            if (dbContext == null) return true;

            var exists = await dbContext.Articles
                .AnyAsync(a => a.Id == command.ArticleId, ct);

            return exists;
        }

        private async Task<bool> ValidateArticleNotDeletedAsync(
            PublishArticleCommand command,
            CancellationToken ct)
        {
            if (dbContext == null) return true;

            var article = await dbContext.Articles
                .FirstOrDefaultAsync(a => a.Id == command.ArticleId, ct);

            if (article == null) return false;

            return article.StatusCode != (int)StatusCodeEnum.Deleted;
        }
    }
}
```

**Actions**:
1. Create file: `Editor\Features\Articles\Publish\PublishArticleValidator.cs`
2. Copy code above
3. Save

? **Done!**

---

## ?? STEP 4: BUILD & VERIFY (10 min)

**Run Build**:
```bash
dotnet build SkyCMS.sln
```

**Expected**: Success (0 errors, 0 warnings)

**If errors occur**:
1. Check file names match exactly
2. Check namespaces are correct
3. Check usings are present
4. Check syntax is correct
5. Ask for help if stuck

? **Build should pass!**

---

## ?? REMAINING WORK (For Tomorrow)

### Tomorrow: Create PublishArticleHandler
- Time: 1-2 hours
- File: `PublishArticleHandler.cs`
- Reference: `CreateArticleHandler.cs` pattern
- Source: `ArticleEditLogic.PublishArticle()` logic

### After Handler: Tests & Controller Updates
- Create tests
- Update controllers
- Verify build
- Complete Sprint 2

---

## ?? QUICK SUCCESS CHECKLIST

- [ ] PublishArticleCommand created
- [ ] PublishResult created
- [ ] PublishArticleValidator created
- [ ] Build passes with 0 errors
- [ ] Build passes with 0 warnings
- [ ] Ready to create handler tomorrow

---

## ?? REFERENCE DOCUMENTS

**For Detailed Instructions**:
- `SPRINT2_ACTION_ITEMS.md` - Complete task list
- `SPRINT2_PUBLISHARTICLE_KICKOFF.md` - Full planning

**For Code Reference**:
- `CreateArticleCommand.cs` - Command structure
- `CreateArticleHandler.cs` - Handler pattern
- `ArticleEditLogic.cs` lines 912-930 - Source logic

**For Support**:
- AI Copilot is here to help
- Ask any questions
- Reference docs as needed

---

## ?? WHAT YOU'RE LEARNING

By creating PublishArticleCommand, you're:
? Reinforcing CQRS pattern
? Building consistency across methods
? Developing command/handler skills
? Getting faster at the process
? Building toward completeness

---

## ?? TIME ESTIMATE

| Task | Time | Status |
|------|------|--------|
| Setup/Read | 10 min | ? |
| Command | 10 min | ? |
| DTO | 5 min | ? |
| Validator | 15 min | ? |
| Build/Verify | 10 min | ? |
| **TOTAL** | **50 min** | ? |

**Buffer**: 30 min
**Total Time**: ~1-1.5 hours

---

## ?? LET'S GO!

**Your mission**: 
Create PublishArticleCommand, PublishResult, and PublishArticleValidator, then verify build passes.

**Your timeline**: 
1-1.5 hours starting now

**Your reference**:
- Code templates provided above
- Build check included
- Tomorrow: Create handler

**Your support**:
- All documentation available
- Code templates ready
- AI support standing by

---

## ?? WHEN YOU'RE DONE

Once build passes with 0 errors:
1. ? Commit your work
2. ? Take a break
3. ? Tomorrow: Create PublishArticleHandler
4. ? Continue momentum

---

**You've got this! Let's create PublishArticleCommand!** ??

**Start now. Build passes in 1-1.5 hours. Done!**

---

*Next: SPRINT2_PUBLISHARTICLE_KICKOFF.md for detailed plan*
*Reference: CreateArticleHandler.cs for patterns*
*Support: AI available for questions*

**GO! ??**
