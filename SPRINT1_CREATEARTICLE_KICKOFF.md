# 🚀 SPRINT 1 KICKOFF - CreateArticle Migration (Weeks 5-6)

**Status**: Ready to begin
**Method**: CreateArticle
**Complexity**: Medium (10 hours estimated)
**Pattern**: Copy SaveArticle approach

---

## SPRINT 1 OVERVIEW

### Goal
Migrate `ArticleEditCreateArticleAsync()` from legacy method to CQRS `CreateArticleCommand` + `CreateArticleHandler` pattern.

### Timeline
- Week 5: Create command, handler, tests
- Week 6: Update controllers, verify, document

### Success Criteria
- [x] CreateArticleCommand created
- [x] CreateArticleHandler implemented
- [x] All tests passing
- [x] EditorController updated
- [x] Razor Pages updated
- [x] Documentation complete
- [x] Build successful
- [x] No regressions

---

## PHASE 1: ANALYZE CreateArticle (4 hours)

### Step 1: Review Current Implementation

**Read ArticleEditLogic.CreateArticle carefully**:
- Lines 397-487
- Understand all branches (first article vs subsequent)
- Note template handling
- Note security validation
- Note catalog/publish logic

**Key insights**:
```csharp
// If first article, auto-publish
var isFirstArticle = (await DbContext.Articles.CountAsync()) == 0;

// Get next article number
int nextArticleNumber = isFirstArticle ? 1 : (Max + 1);

// Security: Validate user in tenant
if (!string.IsNullOrEmpty(tenantDomain))
{
    // Check user exists
    // Check user email ends with tenant domain
}

// Create article with defaults
var article = new Article { ... };

// Auto-publish first article
if (isFirstArticle)
{
    await PublishArticle(article.Id, DateTimeOffset.UtcNow);
}
```

### Step 2: Reference SaveArticle Handler

**Compare to SaveArticle** (already migrated):
- Command structure
- Handler implementation
- Validation approach
- Error handling
- Result wrapping

**File**: `Editor\Features\Articles\Save\SaveArticleHandler.cs`

### Step 3: Map Parameters to Command

**CreateArticle parameters:**
```csharp
string title
Guid userId
Guid? templateId = null
string blogKey = ""
ArticleType articleType = ArticleType.General
```

**Command properties:**
```csharp
public class CreateArticleCommand
{
    public string Title { get; set; }
    public Guid UserId { get; set; }
    public Guid? TemplateId { get; set; }
    public string BlogKey { get; set; }
    public ArticleType ArticleType { get; set; }
    public DateTimeOffset? Published { get; set; } // Optional: auto-publish flag
    public string TemplateContent { get; set; } // Optional: pre-fetched content
}
```

### Step 4: Document Dependencies

**Services needed**:
- DbContext
- htmlService
- titleChangeService
- publishingService
- catalogService
- templateService
- clock
- slugService
- logger
- configurationProvider (tenant)

**Side effects**:
- Creates Article
- Creates ArticleNumber
- Creates Catalog entry
- Publishes if first
- Triggers CDN

---

## PHASE 2: CREATE Command & Handler (4 hours)

### Step 1: Create CreateArticleCommand

**File**: `Editor\Features\Articles\Create\CreateArticleCommand.cs`

**Template**:
```csharp
namespace Sky.Editor.Features.Articles.Create
{
    using Sky.Cms.Models;

    /// <summary>
    /// Command to create a new article.
    /// </summary>
    public class CreateArticleCommand
    {
        /// <summary>
        /// Article title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Author user ID.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Optional template ID to use as starting point.
        /// </summary>
        public Guid? TemplateId { get; set; }

        /// <summary>
        /// Blog key (default: "default").
        /// </summary>
        public string BlogKey { get; set; } = "default";

        /// <summary>
        /// Article type (General, BlogPost, etc).
        /// </summary>
        public ArticleType ArticleType { get; set; } = ArticleType.General;

        /// <summary>
        /// Optional: whether to publish immediately.
        /// First article is always published.
        /// </summary>
        public DateTimeOffset? Published { get; set; }
    }
}
```

### Step 2: Create CreateArticleValidator

**File**: `Editor\Features\Articles\Create\CreateArticleValidator.cs`

**Validations to include**:
- Title is required and not empty
- Title length reasonable (max 254 chars based on SaveArticle)
- UserId is valid (not empty)
- Template exists if TemplateId provided
- User belongs to current tenant
- Article title doesn't conflict with reserved paths (if needed)

**Template**:
```csharp
namespace Sky.Editor.Features.Articles.Create
{
    using FluentValidation;
    using Microsoft.EntityFrameworkCore;
    using Cosmos.Common.Data;

    public class CreateArticleValidator : AbstractValidator<CreateArticleCommand>
    {
        private readonly ApplicationDbContext dbContext;

        public CreateArticleValidator(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;

            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required")
                .MaximumLength(254).WithMessage("Title must be 254 characters or less");

            RuleFor(x => x.UserId)
                .NotEqual(Guid.Empty).WithMessage("User ID is required");

            RuleFor(x => x)
                .MustAsync(ValidateUserAsync).WithMessage("User not found or not authorized");

            RuleFor(x => x.TemplateId)
                .MustAsync(ValidateTemplateAsync)
                .When(x => x.TemplateId.HasValue)
                .WithMessage("Template not found");
        }

        private async Task<bool> ValidateUserAsync(CreateArticleCommand command, CancellationToken ct)
        {
            var user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == command.UserId.ToString(), ct);
            
            return user != null;
        }

        private async Task<bool> ValidateTemplateAsync(Guid? templateId, CancellationToken ct)
        {
            if (!templateId.HasValue) return true;

            var template = await dbContext.Templates
                .FirstOrDefaultAsync(t => t.Id == templateId.Value, ct);
            
            return template != null;
        }
    }
}
```

### Step 3: Create CreateArticleHandler

**File**: `Editor\Features\Articles\Create\CreateArticleHandler.cs`

**Template structure**:
```csharp
namespace Sky.Editor.Features.Articles.Create
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Models;
    using Sky.Cms.Models;
    using Sky.Editor.Services.Html;
    using Sky.Editor.Services.Titles;
    using Sky.Editor.Services.Templates;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using MediatR;

    /// <summary>
    /// Handles article creation with CQRS pattern.
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

        public async Task<CommandResult<ArticleViewModel>> Handle(
            CreateArticleCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                // Implementation here
                // Copy logic from ArticleEditLogic.CreateArticle
                
                return CommandResult<ArticleViewModel>.Success(viewModel);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating article");
                return CommandResult<ArticleViewModel>.Failure("Failed to create article");
            }
        }
    }
}
```

---

## PHASE 3: MIGRATE TESTS (3 hours)

### Step 1: Create CreateArticleHandlerTests

**File**: `Tests\Features\Articles\Create\CreateArticleHandlerTests.cs`

**Test cases to include** (copy pattern from SaveArticleHandlerTests):
- CreateArticle_ValidCommand_CreatesArticle
- CreateArticle_FirstArticle_AutoPublishes
- CreateArticle_WithTemplate_UsesTemplateContent
- CreateArticle_InvalidTitle_ReturnsError
- CreateArticle_InvalidUser_ReturnsError
- CreateArticle_InvalidTemplate_ReturnsError
- CreateArticle_GeneratesUniqueArticleNumber
- CreateArticle_GeneratesUrlPath

### Step 2: Update Existing Tests

**Files to update**:
- `Tests\Services\BlogServiceTests.cs` - Uses CreateArticle
- `Tests\Integration\ArticleLifecycleIntegrationTests.cs` - Uses CreateArticle
- Any other test files calling CreateArticle

**Pattern**:
```csharp
// OLD
var article = await CreateArticleAsync(title, userId, templateId);

// NEW
var command = new CreateArticleCommand
{
    Title = title,
    UserId = userId,
    TemplateId = templateId
};
var result = await mediator.SendAsync<CommandResult<ArticleViewModel>>(command);
var article = result.Data;
```

---

## PHASE 4: UPDATE CONTROLLERS (1 hour)

### Step 1: Find CreateArticle Calls

**Search in EditorController.cs**:
```csharp
Logic.CreateArticle
```

### Step 2: Replace with Command

**Pattern**:
```csharp
// OLD
var article = await CreateArticleAsync(title, userId, templateId);

// NEW
var command = new CreateArticleCommand
{
    Title = title,
    UserId = userId,
    TemplateId = templateId
};
var result = await mediator.SendAsync<CommandResult<ArticleViewModel>>(command);

if (!result.IsSuccess)
{
    return BadRequest(new { errors = result.Errors });
}

var article = result.Data;
```

### Step 3: Update Razor Pages

Search in Razor Pages for direct calls to CreateArticle and update similarly.

---

## PHASE 5: VERIFY & DOCUMENT (2 hours)

### Step 1: Build & Test

```bash
dotnet build
dotnet test
```

### Step 2: Run Integration Tests

- [ ] CreateArticle tests pass
- [ ] BlogServiceTests pass
- [ ] ArticleLifecycleIntegrationTests pass
- [ ] No regressions in other tests

### Step 3: Manual Testing (if applicable)

- [ ] Create new article via UI
- [ ] Create first article (should auto-publish)
- [ ] Create article with template
- [ ] Verify catalog entry created
- [ ] Verify CDN triggered

### Step 4: Document

**Create**: `SPRINT1_CREATEARTICLE_COMPLETE.md`
- What was done
- Tests migrated
- Controllers updated
- Known issues (if any)
- Next sprint prep

---

## 📋 SPRINT 1 CHECKLIST

### Code Changes
- [ ] CreateArticleCommand created
- [ ] CreateArticleValidator created
- [ ] CreateArticleHandler created
- [ ] Registered in DI container
- [ ] Tests created/updated
- [ ] Controllers updated
- [ ] Razor Pages updated
- [ ] Documentation updated

### Testing
- [ ] CreateArticle tests pass
- [ ] Integration tests pass
- [ ] No compiler errors
- [ ] No runtime errors
- [ ] No new warnings

### Documentation
- [ ] Code comments added
- [ ] XML docs complete
- [ ] Migration guide created
- [ ] Examples provided

### Verification
- [ ] Build successful
- [ ] All tests passing
- [ ] No regressions
- [ ] Ready for sprint review

---

## 🎯 SUCCESS CRITERIA

By end of Sprint 1:
✅ CreateArticle fully migrated to CQRS
✅ All tests passing
✅ Controllers/Pages updated
✅ No legacy CreateArticle calls in production code
✅ Ready for Sprint 2 (PublishArticle)

---

## 📚 REFERENCE IMPLEMENTATION

**Use SaveArticle as template**:
- `Editor\Features\Articles\Save\SaveArticleCommand.cs`
- `Editor\Features\Articles\Save\SaveArticleHandler.cs`
- `Editor\Features\Articles\Save\SaveArticleValidator.cs`
- `Tests\Features\Articles\Save\SaveArticleHandlerTests.cs`

Copy structure, adapt logic.

---

## READY FOR SPRINT 1?

**Confirm:**
1. [ ] Reviewed CreateArticle implementation
2. [ ] Understood dependencies
3. [ ] Reviewed SaveArticle as template
4. [ ] Ready to create command/handler
5. [ ] Ready to migrate tests
6. [ ] Ready to update controllers

**Then**: Start with Phase 2 (Create Command & Handler)

---

## TIMELINE

**Week 5:**
- Mon-Tue: Analyze (Phase 1)
- Wed-Thu: Create command/handler (Phase 2)
- Fri: Migrate tests (Phase 3)

**Week 6:**
- Mon: Update controllers (Phase 4)
- Tue-Wed: Verify & test (Phase 5)
- Thu-Fri: Documentation & cleanup

**Friday EOW**: Sprint 1 Complete, Sprint 2 Kickoff

---

**Ready to begin Sprint 1? Let's create the CreateArticleCommand!** 🚀
