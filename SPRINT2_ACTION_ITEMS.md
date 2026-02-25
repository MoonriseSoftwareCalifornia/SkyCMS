# ? SPRINT 2 ACTION ITEMS - START NOW!

**Status**: Ready to Execute
**Sprint**: Sprint 2 (Weeks 7-8)
**Method**: PublishArticle Migration
**Effort**: 8-10 hours estimated
**Timeline**: 2 weeks

---

## ?? YOUR MISSION

Migrate `ArticleEditLogic.PublishArticle()` to CQRS pattern with full command/handler/validator implementation.

---

## ?? PHASE 1: CREATE PublishArticleCommand (30 min)

### File to Create
`Editor\Features\Articles\Publish\PublishArticleCommand.cs`

### Boilerplate Code

```csharp
// <copyright file="PublishArticleCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

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

? **Copy this, create file, commit**

---

## ?? PHASE 2: CREATE PublishResult DTO (15 min)

### File to Create
`Editor\Features\Articles\Publish\PublishResult.cs`

### Code

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

? **Copy this, create file, commit**

---

## ?? PHASE 3: CREATE PublishArticleValidator (30 min)

### File to Create
`Editor\Features\Articles\Publish\PublishArticleValidator.cs`

### Code

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

            // Basic validation
            RuleFor(x => x.ArticleId)
                .NotEqual(System.Guid.Empty)
                .WithMessage("Article ID is required");

            // If dbContext available, validate database state
            if (dbContext != null)
            {
                RuleFor(x => x)
                    .MustAsync(ValidateArticleExistsAsync)
                    .WithMessage("Article not found");

                RuleFor(x => x)
                    .MustAsync(ValidateArticleNotDeletedAsync)
                    .WithMessage("Cannot publish deleted article");
            }

            // Publish date validation
            RuleFor(x => x.PublishDate)
                .Must(d => !d.HasValue || d.Value <= System.DateTimeOffset.UtcNow)
                .WithMessage("Publish date cannot be in the future");
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

            // Check if not deleted (StatusCode != Deleted)
            return article.StatusCode != (int)StatusCodeEnum.Deleted;
        }
    }
}
```

? **Copy this, create file, adjust as needed, commit**

---

## ?? PHASE 4: CREATE PublishArticleHandler (1.5 hours)

### File to Create
`Editor\Features\Articles\Publish\PublishArticleHandler.cs`

### Template Code

```csharp
namespace Sky.Editor.Features.Articles.Publish
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.Catalog;
    using Sky.Editor.Services.Publishing;

    /// <summary>
    /// Handles article publication via CQRS pattern.
    /// Replaces deprecated ArticleEditLogic.PublishArticle() method.
    /// </summary>
    public class PublishArticleHandler : ICommandHandler<PublishArticleCommand, CommandResult<PublishResult>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IPublishingService publishingService;
        private readonly ICatalogService catalogService;
        private readonly IClock clock;
        private readonly ILogger<PublishArticleHandler> logger;
        private readonly PublishArticleValidator validator;

        public PublishArticleHandler(
            ApplicationDbContext dbContext,
            IPublishingService publishingService,
            ICatalogService catalogService,
            IClock clock,
            ILogger<PublishArticleHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.publishingService = publishingService ?? throw new ArgumentNullException(nameof(publishingService));
            this.catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.validator = new PublishArticleValidator(dbContext);
        }

        public async Task<CommandResult<PublishResult>> HandleAsync(
            PublishArticleCommand command,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate command
                var validationErrors = validator.Validate(command);
                if (validationErrors.Any())
                {
                    return CommandResult<PublishResult>.Failure(validationErrors);
                }

                // Get article
                var article = await dbContext.Articles
                    .FirstOrDefaultAsync(a => a.Id == command.ArticleId, cancellationToken);

                if (article == null)
                {
                    logger.LogWarning("Article {ArticleId} not found", command.ArticleId);
                    return CommandResult<PublishResult>.Failure("Article not found");
                }

                // Check if already published (unless ForceRepublish)
                if (!command.ForceRepublish && article.Published.HasValue)
                {
                    logger.LogInformation(
                        "Article {ArticleNumber} already published on {PublishedDate}. Use ForceRepublish=true to republish.",
                        article.ArticleNumber,
                        article.Published.Value);
                    
                    // Still return success with existing data
                    return CommandResult<PublishResult>.Success(new PublishResult
                    {
                        ArticleId = article.Id,
                        ArticleNumber = article.ArticleNumber,
                        PublishedDate = article.Published.Value,
                        CdnResults = new List<CdnResult>()
                    });
                }

                // Set publish timestamp
                article.Published = command.PublishDate ?? clock.UtcNow;

                // Save to database
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Publishing article {ArticleNumber} (ID: {ArticleId}) at {PublishDate}",
                    article.ArticleNumber,
                    article.Id,
                    article.Published.Value);

                // Publish via service (CDN, static files, etc)
                var cdnResults = await publishingService.PublishAsync(article);

                // Update catalog
                await catalogService.UpsertAsync(article, cancellationToken);

                logger.LogInformation(
                    "Successfully published article {ArticleNumber} (ID: {ArticleId})",
                    article.ArticleNumber,
                    article.Id);

                // Return success result
                return CommandResult<PublishResult>.Success(new PublishResult
                {
                    ArticleId = article.Id,
                    ArticleNumber = article.ArticleNumber,
                    PublishedDate = article.Published.Value,
                    CdnResults = cdnResults ?? new List<CdnResult>()
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error publishing article {ArticleId}", command.ArticleId);
                return CommandResult<PublishResult>.Failure("An error occurred while publishing the article.");
            }
        }
    }
}
```

? **Copy this, adjust logic if needed, commit**

---

## ?? PHASE 5: BUILD & TEST (30 min)

### Step 1: Build
```bash
dotnet build SkyCMS.sln
```

**Expected**: Success (0 errors, 0 warnings)

### Step 2: Quick Test
```bash
dotnet test Tests\Features\Articles\Publish\
```

**Expected**: Tests pass (or ready to run)

---

## ?? PHASE 6: CREATE TESTS (1 hour)

### File to Create
`Tests\Features\Articles\Publish\PublishArticleHandlerTests.cs`

### Test Template

```csharp
namespace Sky.Tests.Features.Articles.Publish
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Cosmos.Common.Data;
    using Sky.Editor.Features.Articles.Publish;
    using System;

    [TestClass]
    public class PublishArticleHandlerTests
    {
        private ApplicationDbContext dbContext;
        private PublishArticleHandler handler;

        [TestInitialize]
        public void Setup()
        {
            // Setup test context
            // Create test data
            // Initialize handler
        }

        [TestMethod]
        public async Task PublishArticle_ValidArticle_SuccessfullyPublishes()
        {
            // Arrange
            var articleId = Guid.NewGuid();
            var command = new PublishArticleCommand { ArticleId = articleId };

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual(articleId, result.Data.ArticleId);
        }

        [TestMethod]
        public async Task PublishArticle_UsesProvidedTimestamp()
        {
            // Test that provided timestamp is used
        }

        [TestMethod]
        public async Task PublishArticle_UsesCurrentTimeIfNotProvided()
        {
            // Test that current time is used if null
        }

        [TestMethod]
        public async Task PublishArticle_InvalidArticleId_ReturnsError()
        {
            // Test invalid article ID handling
        }

        [TestMethod]
        public async Task PublishArticle_DeletedArticle_ReturnsError()
        {
            // Test deleted article handling
        }

        [TestMethod]
        public async Task PublishArticle_TriggersCDN()
        {
            // Test CDN operations
        }

        [TestMethod]
        public async Task PublishArticle_UpdatesCatalog()
        {
            // Test catalog updates
        }
    }
}
```

? **Create this file, fill in tests**

---

## ?? PHASE 7: UPDATE CONTROLLERS (1 hour)

### Search in EditorController.cs
```bash
Search for: "PublishArticle"
```

### Replace Pattern
```csharp
// OLD
var cdnResults = await Logic.PublishArticle(articleId, null);

// NEW
var command = new PublishArticleCommand
{
    ArticleId = articleId,
    PublishDate = null // Uses current time
};

var result = await mediator.SendAsync(command);

if (result.IsSuccess)
{
    var cdnResults = result.Data.CdnResults;
}
```

? **Find calls, replace pattern**

---

## ?? FINAL VERIFICATION (30 min)

### Build
```bash
dotnet build SkyCMS.sln
```

### Test
```bash
dotnet test
```

### Verify
- [ ] No compilation errors
- [ ] No compiler warnings
- [ ] All tests passing
- [ ] No legacy PublishArticle calls
- [ ] Ready to commit

---

## ?? CHECKLIST

### Files to Create
- [ ] PublishArticleCommand.cs
- [ ] PublishResult.cs
- [ ] PublishArticleValidator.cs
- [ ] PublishArticleHandler.cs
- [ ] PublishArticleHandlerTests.cs

### Code Changes
- [ ] EditorController.cs - Replace PublishArticle calls
- [ ] Other controllers - Replace calls if needed
- [ ] DI registration - Wire up PublishArticleHandler

### Verification
- [ ] Build passes
- [ ] Tests pass
- [ ] No warnings
- [ ] No errors
- [ ] Controllers compile
- [ ] Controllers integrate

---

## ?? ESTIMATED TIME

| Phase | Time |
|-------|------|
| 1. Command | 30 min |
| 2. DTO | 15 min |
| 3. Validator | 30 min |
| 4. Handler | 90 min |
| 5. Build/Test | 30 min |
| 6. Tests | 60 min |
| 7. Controllers | 60 min |
| **TOTAL** | **315 min** = **~5.25 hours** |

**Buffer**: 2-3 hours
**Total Sprint 2**: 7-8 hours ?

---

## ?? REFERENCE

**Use as Template**:
- CreateArticleCommand (structure)
- CreateArticleHandler (pattern)
- CreateArticleValidator (style)

**Reference Implementation**:
- SaveArticleHandler (publishing reference)
- ArticleEditLogic.PublishArticle (source logic)

---

## ?? SUCCESS CRITERIA

? PublishArticleCommand created
? PublishArticleHandler implemented
? PublishArticleValidator working
? Tests created & passing
? Controllers updated
? Build successful
? No legacy PublishArticle calls
? CDN operations working
? Catalog updates working

---

**Ready to start Sprint 2?**

**Create PublishArticleCommand now!** ??
