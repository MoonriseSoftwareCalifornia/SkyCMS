# ⚡ IMMEDIATE ACTION ITEMS - Start Now!

**Status**: READY TO CODE
**Timeline**: Next 2 hours to get started
**First Task**: Create CreateArticleCommand

---

## WHAT TO DO RIGHT NOW

### Step 1: Create the Command Class (10 minutes)

**File to create**: `Editor\Features\Articles\Create\CreateArticleCommand.cs`

Use this code:

```csharp
namespace Sky.Editor.Features.Articles.Create
{
    using Sky.Cms.Models;

    /// <summary>
    /// Command to create a new article.
    /// Replaces deprecated CreateArticle() method.
    /// </summary>
    public class CreateArticleCommand
    {
        /// <summary>
        /// Article title (required).
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Author user ID (required).
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Optional template ID to use as content template.
        /// </summary>
        public Guid? TemplateId { get; set; }

        /// <summary>
        /// Blog key for categorization (default: "default").
        /// </summary>
        public string BlogKey { get; set; } = "default";

        /// <summary>
        /// Article type: General, BlogPost, etc (default: General).
        /// </summary>
        public ArticleType ArticleType { get; set; } = ArticleType.General;

        /// <summary>
        /// Optional: explicit publish timestamp.
        /// If null and this is first article, will auto-publish with current time.
        /// </summary>
        public DateTimeOffset? Published { get; set; }
    }
}
```

✅ **Done with Step 1!**

---

### Step 2: Create the Validator (15 minutes)

**File to create**: `Editor\Features\Articles\Create\CreateArticleValidator.cs`

```csharp
namespace Sky.Editor.Features.Articles.Create
{
    using FluentValidation;
    using Microsoft.EntityFrameworkCore;
    using Cosmos.Common.Data;

    /// <summary>
    /// Validates CreateArticleCommand requests.
    /// </summary>
    public class CreateArticleValidator : AbstractValidator<CreateArticleCommand>
    {
        private readonly ApplicationDbContext dbContext;

        public CreateArticleValidator(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;

            // Title validation
            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Article title is required");

            RuleFor(x => x.Title)
                .MaximumLength(254)
                .WithMessage("Title must be 254 characters or less");

            // User validation
            RuleFor(x => x.UserId)
                .NotEqual(Guid.Empty)
                .WithMessage("User ID is required");

            // Template validation (if provided)
            RuleFor(x => x.TemplateId)
                .MustAsync(ValidateTemplateAsync)
                .When(x => x.TemplateId.HasValue)
                .WithMessage("Template not found");
        }

        /// <summary>
        /// Validates that template exists if provided.
        /// </summary>
        private async Task<bool> ValidateTemplateAsync(Guid? templateId, CancellationToken ct)
        {
            if (!templateId.HasValue)
                return true;

            var exists = await dbContext.Templates
                .AnyAsync(t => t.Id == templateId.Value, ct);

            return exists;
        }
    }
}
```

✅ **Done with Step 2!**

---

### Step 3: Create Handler Skeleton (20 minutes)

**File to create**: `Editor\Features\Articles\Create\CreateArticleHandler.cs`

**First version** (copy structure from SaveArticle, fill in logic next):

```csharp
namespace Sky.Editor.Features.Articles.Create
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Models;
    using Sky.Cms.Models;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Services.Html;
    using Sky.Editor.Services.Titles;
    using Sky.Editor.Services.Templates;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using MediatR;

    /// <summary>
    /// Handles article creation via CQRS pattern.
    /// Replaces deprecated ArticleEditCreateArticleAsync() method.
    /// </summary>
    public class CreateArticleHandler : IRequestHandler<CreateArticleCommand, CommandResult<ArticleViewModel>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IArticleHtmlService htmlService;
        private readonly ITitleChangeService titleChangeService;
        private readonly ITemplateService templateService;
        private readonly ILogger<CreateArticleHandler> logger;

        public CreateArticleHandler(
            ApplicationDbContext dbContext,
            IArticleHtmlService htmlService,
            ITitleChangeService titleChangeService,
            ITemplateService templateService,
            ILogger<CreateArticleHandler> logger)
        {
            this.dbContext = dbContext;
            this.htmlService = htmlService;
            this.titleChangeService = titleChangeService;
            this.templateService = templateService;
            this.logger = logger;
        }

        /// <summary>
        /// Handles article creation command.
        /// </summary>
        public async Task<CommandResult<ArticleViewModel>> Handle(
            CreateArticleCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                // Validation happens via validator
                // TODO: Implement creation logic here
                // Copy from ArticleEditLogic.CreateArticle (lines 397-487)

                throw new NotImplementedException("Handler implementation in progress");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error creating article: {Title}", request.Title);
                return CommandResult<ArticleViewModel>.Failure(
                    "Failed to create article",
                    new Dictionary<string, string[]>
                    {
                        { "CreateArticle", new[] { ex.Message } }
                    });
            }
        }
    }
}
```

✅ **Done with Step 3!**

---

## NEXT: Register in DI (5 minutes)

**Find**: `Program.cs` (main application setup)

**Add** (in the mediator registration section):
```csharp
// Add to MediatR registration
services.AddScoped<IRequestHandler<CreateArticleCommand, CommandResult<ArticleViewModel>>, CreateArticleHandler>();
services.AddScoped<IValidator<CreateArticleCommand>, CreateArticleValidator>();
```

✅ **Done with DI registration!**

---

## VERIFY IT BUILDS

**Run**:
```bash
dotnet build
```

**Expected**: No errors (NotImplementedException is fine for now)

---

## QUICK CHECKPOINT

You now have:
✅ CreateArticleCommand class (request)
✅ CreateArticleValidator (input validation)
✅ CreateArticleHandler skeleton (request handler)
✅ DI registration ready

**Next**: Fill in the handler implementation (copy from ArticleEditLogic.CreateArticle)

---

## THEN: Create Unit Test

**File**: `Tests\Features\Articles\Create\CreateArticleHandlerTests.cs`

**Template**:
```csharp
namespace Sky.Tests.Features.Articles.Create
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Features.Articles.Create;
    using Cosmos.Common.Data;
    using Sky.Cms.Models;

    [TestClass]
    public class CreateArticleHandlerTests
    {
        // Copy test pattern from SaveArticleHandlerTests
        // Tests to create:
        // - CreateArticle_WithValidCommand_SuccessfullyCreatesArticle
        // - CreateArticle_FirstArticle_AutoPublishes
        // - CreateArticle_InvalidTitle_ReturnsError
        // - CreateArticle_InvalidUser_ReturnsError
    }
}
```

---

## TIMELINE

**Today (Next 2 hours):**
- [ ] Create CreateArticleCommand
- [ ] Create CreateArticleValidator
- [ ] Create CreateArticleHandler skeleton
- [ ] Register in DI
- [ ] Verify build

**Tomorrow:**
- [ ] Implement handler logic (copy from CreateArticle method)
- [ ] Create unit tests
- [ ] Verify all tests pass

**This Week:**
- [ ] Update controller calls
- [ ] Migrate integration tests
- [ ] Documentation
- [ ] Sprint 1 complete

---

## CURRENT STATE

You have `ArticleEditLogic.cs` open with CreateArticle method visible.

**Copy from lines 397-487** into CreateArticleHandler.Handle() method.

**Key logic to copy**:
1. Check if first article
2. Get template content if provided
3. Calculate next article number
4. Validate user tenant
5. Create Article entity
6. Add to database
7. Auto-publish if first

---

## REFERENCE CODE LOCATION

**Source**: `ArticleEditLogic.cs` lines 397-487
**Template**: Look at `SaveArticleHandler.cs` for structure
**Test Template**: Look at `SaveArticleHandlerTests.cs` for pattern

---

## YOU'RE READY!

**Command**: Create the three files above in `Editor\Features\Articles\Create\` folder

**Questions?** Refer to:
- `SPRINT1_CREATEARTICLE_KICKOFF.md` (full guide)
- `WEEK1_AUDIT_INVENTORY_COMPLETE.md` (what to build)
- SaveArticle implementation (pattern reference)

---

**GO TIME! 🚀 Start creating CreateArticleCommand now!**

Once you're done with those 3 files, I'll help you:
1. Implement the handler logic
2. Create the tests
3. Update controllers
4. Verify build

**Let me know when files are created and ready for review!**
