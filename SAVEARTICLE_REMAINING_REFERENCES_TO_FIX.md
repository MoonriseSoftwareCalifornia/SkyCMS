# ?? SAVEARTICLE ELIMINATION - REMAINING REFERENCES FIX

**Status**: SaveArticle method deleted ?
**Build Status**: 48 compilation errors (all from SaveArticle calls)
**Task**: Fix 48 remaining references to use SaveArticleCommand/Handler

---

## ?? REFERENCE BREAKDOWN

### Production Code (1 reference)
- [x] FileManagerController.cs (line 976) - 1 call

### Test Code (47 references)
- [ ] EditorControllerTests.cs - 31 calls
- [ ] SaveArticlePublishingTests.cs - 2 calls
- [ ] ArticleLifecycleIntegrationTests.cs - 2 calls
- [ ] PerformanceAndConcurrencyTests.cs - 1 call
- [ ] BlogServiceTests.cs - 1 call

---

## ?? MIGRATION PATTERN

### OLD CODE (Direct call to SaveArticle)
```csharp
await Logic.SaveArticle(article, TestUserId);
```

### NEW CODE (Using SaveArticleCommand via Mediator)
```csharp
var command = new SaveArticleCommand
{
    ArticleNumber = article.ArticleNumber,
    Title = article.Title,
    Content = article.Content,
    HeadJavaScript = article.HeaderJavaScript,
    FooterJavaScript = article.FooterJavaScript,
    BannerImage = article.BannerImage,
    UserId = TestUserId,
    ArticleType = (ArticleType)article.ArticleType,
    Category = article.Category,
    Introduction = article.Introduction,
    Published = article.Published
};
var result = await SaveArticleHandler.HandleAsync(command);
// OR: var result = await Mediator.SendAsync(command);
```

---

## ? FIXES IN PROGRESS

**Next**: Update all 48 references to use the command pattern.

Would you like me to:
A) Update all files automatically (save lots of typing)
B) Show you one example and you apply the pattern

Recommend: A (it's straightforward find/replace with proper mapping)
