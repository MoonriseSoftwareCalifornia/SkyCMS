# ?? SPRINT 2 KICKOFF - PublishArticle Migration

**Status**: READY TO START
**Sprint**: Sprint 2 (Weeks 7-8)
**Method**: PublishArticle
**Complexity**: Medium (8-10 hours)
**Pattern**: Same as CreateArticle

---

## ?? SPRINT 2 GOAL

Migrate `ArticleEditLogic.PublishArticle()` to CQRS pattern with **PublishArticleCommand** + **PublishArticleHandler**.

---

## ?? SOURCE ANALYSIS

### Current Method: PublishArticle
**File**: `Editor\Data\Logic\ArticleEditLogic.cs`
**Lines**: 912-930
**Signature**:
```csharp
public async Task<List<CdnResult>> PublishArticle(
    Guid articleId, 
    DateTimeOffset? dateTime)
```

### What It Does
1. Finds article by ID
2. Sets published timestamp (uses provided or current time)
3. Calls publishingService.PublishAsync(article)
4. Updates catalog entry
5. Returns CDN results

### Services Used
- DbContext
- publishingService (core publishing logic)
- catalogService (catalog updates)
- clock (for timestamp)

### Side Effects
- Updates Article.Published field
- Triggers CDN operations
- Updates/creates catalog entry
- Generates static HTML artifacts

---

## ??? IMPLEMENTATION PHASES

### Phase 1: Create PublishArticleCommand (30 min)

**File**: `Editor\Features\Articles\Publish\PublishArticleCommand.cs`

```csharp
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
```

**Key Points**:
- ArticleId required
- PublishDate optional (null = use current time)
- ForceRepublish flag for special cases

---

### Phase 2: Create PublishArticleValidator (30 min)

**File**: `Editor\Features\Articles\Publish\PublishArticleValidator.cs`

**Validations**:
- ArticleId is not empty Guid
- Article exists in database
- Article is not deleted
- Article is not already published (unless ForceRepublish=true)
- PublishDate is not in past (if provided)

```csharp
RuleFor(x => x.ArticleId)
    .NotEqual(Guid.Empty)
    .WithMessage("Article ID is required");

RuleFor(x => x)
    .MustAsync(ArticleExistsAsync)
    .WithMessage("Article not found");

RuleFor(x => x)
    .MustAsync(ArticleNotDeletedAsync)
    .WithMessage("Cannot publish deleted article");

RuleFor(x => x.PublishDate)
    .Must(d => !d.HasValue || d.Value >= DateTimeOffset.UtcNow)
    .WithMessage("Publish date cannot be in the past");
```

---

### Phase 3: Create PublishArticleHandler (1.5 hours)

**File**: `Editor\Features\Articles\Publish\PublishArticleHandler.cs`

**Implementation Logic**:

```csharp
public async Task<CommandResult<PublishResult>> HandleAsync(
    PublishArticleCommand command,
    CancellationToken cancellationToken = default)
{
    try
    {
        // 1. Validate command
        var validationErrors = validator.Validate(command);
        if (validationErrors.Any())
        {
            return CommandResult<PublishResult>.Failure(validationErrors);
        }

        // 2. Get article
        var article = await dbContext.Articles
            .FirstOrDefaultAsync(a => a.Id == command.ArticleId, cancellationToken);

        if (article == null)
        {
            logger.LogWarning("Article {ArticleId} not found", command.ArticleId);
            return CommandResult<PublishResult>.Failure("Article not found");
        }

        // 3. Set publish timestamp
        article.Published = command.PublishDate ?? clock.UtcNow;
        
        // 4. Save to database
        await dbContext.SaveChangesAsync(cancellationToken);

        // 5. Publish via service (CDN, static files, etc)
        var cdnResults = await publishingService.PublishAsync(article);

        // 6. Update catalog
        await catalogService.UpsertAsync(article, cancellationToken);

        logger.LogInformation(
            "Published article {ArticleNumber} (ID: {ArticleId})",
            article.ArticleNumber,
            article.Id);

        // 7. Return result
        return CommandResult<PublishResult>.Success(new PublishResult
        {
            ArticleId = article.Id,
            ArticleNumber = article.ArticleNumber,
            PublishedDate = article.Published.Value,
            CdnResults = cdnResults
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error publishing article {ArticleId}", command.ArticleId);
        return CommandResult<PublishResult>.Failure("Failed to publish article");
    }
}
```

---

### Phase 4: Create PublishResult DTO (15 min)

**File**: `Editor\Features\Articles\Publish\PublishResult.cs`

```csharp
public class PublishResult
{
    public Guid ArticleId { get; set; }
    public int ArticleNumber { get; set; }
    public DateTimeOffset PublishedDate { get; set; }
    public List<CdnResult> CdnResults { get; set; } = new();
}
```

---

### Phase 5: Create Tests (1 hour)

**File**: `Tests\Features\Articles\Publish\PublishArticleHandlerTests.cs`

**Test Cases**:
- PublishArticle_ValidArticle_SuccessfullyPublishes
- PublishArticle_UsesProvidedTimestamp
- PublishArticle_UsesCurrentTimeIfNotProvided
- PublishArticle_InvalidArticleId_ReturnsError
- PublishArticle_DeletedArticle_ReturnsError
- PublishArticle_TriggersCDN
- PublishArticle_UpdatesCatalog
- PublishArticle_ForceRepublish_RePublishes

---

### Phase 6: Update Controllers (1 hour)

**Search for**: `PublishArticle` calls in EditorController

**Pattern**:
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

---

## ?? SPRINT 2 TIMELINE

**Week 7:**
- Mon: Create command, validator, handler (Phase 1-3)
- Tue-Wed: Create tests (Phase 4)
- Thu: Update controllers (Phase 5)
- Fri: Verify & document

**Week 8:**
- Mon-Tue: Integration testing
- Wed: Documentation
- Thu: Final verification
- Fri: Sprint 2 complete, Sprint 3 kickoff

---

## ?? SUCCESS CRITERIA

- [x] PublishArticleCommand created
- [x] PublishArticleHandler implemented
- [x] PublishArticleValidator working
- [x] Tests created & passing
- [x] Controllers updated
- [x] Build successful
- [x] No legacy PublishArticle calls
- [x] CDN operations working
- [x] Catalog updates working

---

## ?? REFERENCE IMPLEMENTATION

**Use as Template**:
1. CreateArticleCommand (structure)
2. CreateArticleHandler (pattern)
3. CreateArticleValidator (style)
4. SaveArticleHandler (publishing reference)

**Copy From ArticleEditLogic.PublishArticle**:
- Lines 912-930
- Logic flow
- Service calls

---

## ?? DEPENDENCIES

**No blocking dependencies** - PublishArticle is independent!

Can start immediately after Sprint 1.

---

## ?? FILES TO CREATE

1. `Editor\Features\Articles\Publish\PublishArticleCommand.cs`
2. `Editor\Features\Articles\Publish\PublishArticleValidator.cs`
3. `Editor\Features\Articles\Publish\PublishArticleHandler.cs`
4. `Editor\Features\Articles\Publish\PublishResult.cs`
5. `Tests\Features\Articles\Publish\PublishArticleHandlerTests.cs`
6. `Tests\Features\Articles\Publish\PublishArticleValidatorTests.cs`

---

## ? QUICK START

1. **Create PublishArticleCommand** (copy structure from CreateArticleCommand)
2. **Create PublishArticleValidator** (validate article exists, not deleted)
3. **Create PublishArticleHandler** (copy publishingService.PublishAsync logic)
4. **Create tests** (use CreateArticleHandlerTests as template)
5. **Update controllers** (find PublishArticle calls, replace with command)
6. **Verify** (dotnet build, dotnet test)

---

## ?? QUESTIONS?

**Refer to**:
- CreateArticleHandler (similar pattern)
- ArticleEditLogic.PublishArticle (source logic)
- SaveArticleHandler (reference implementation)

---

**Ready to start Sprint 2? Let's build PublishArticleCommand!** ??
