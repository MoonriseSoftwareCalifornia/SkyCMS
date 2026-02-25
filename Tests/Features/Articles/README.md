# Phase 3: Handler Tests - README

## Overview
This directory contains comprehensive unit tests for the CQRS command handlers created during the migration from ArticleEditLogic to CQRS pattern.

## Test Files

### 1. CreateArticleHandlerTests.cs
Tests the `CreateArticleHandler` and `CreateArticleCommand`
- Creating new articles with various configurations
- Auto-publishing of root article
- Template application
- URL path generation
- Catalog entry creation
- Metadata handling

### 2. SaveArticleHandlerTests.cs  
Tests the `SaveArticleHandler` and `SaveArticleCommand`
- Content updates
- Title changes with article number preservation
- Timestamp updates
- Article type preservation
- JavaScript block updates (head and footer)
- Metadata updates
- Banner image updates

### 3. PublishArticleHandlerTests.cs
Tests the `PublishArticleHandler` and `PublishArticleCommand`
- Setting published timestamps
- Creating page entries from articles
- Updating existing page entries on republish
- Custom publish dates
- Page status codes
- URL path preservation

### 4. DeleteArticleHandlerTests.cs
Tests the `DeleteArticleHandler` and `DeleteArticleCommand`
- Soft deletion (marks as deleted, doesn't remove)
- Removal from catalog
- Page deletion with article
- Root page protection
- Multiple article isolation
- Data retention

### 5. RestoreArticleHandlerTests.cs
Tests the `RestoreArticleHandler` and `RestoreArticleCommand`
- Restoring deleted articles to Active status
- Catalog re-addition
- Data preservation
- Multiple article isolation
- Already-active articles

## Test Structure

All tests inherit from `SkyCmsTestBase` which provides:
- Test database context
- Mediator instance
- Standard test user ID
- Layout seeding
- Database cleanup

## Running Tests

```bash
dotnet test SkyCMS.sln --filter "Category=Handler"
```

Or run individual test files:
```bash
dotnet test Tests/Features/Articles/CreateArticleHandlerTests.cs
```

## Test Patterns

### Standard Test Arrangement
```csharp
// Arrange - Setup test conditions
var command = new CreateArticleCommand { ... };

// Act - Execute command
var result = await Mediator.SendAsync<CommandResult<ArticleViewModel>>(command);

// Assert - Verify results
Assert.IsTrue(result.IsSuccess);
```

### Data Validation
Tests verify:
- Command execution success/failure
- Data persistence in database
- State transitions
- Related entity updates (catalog, pages)

## Migration Notes

These tests replace the legacy `ArticleEditLogicTests` which tested the monolithic `ArticleEditLogic` class.

**Migration Pattern:**
- **Old:** Direct calls to `Logic.CreateArticle()`, `Logic.SaveArticle()`, etc.
- **New:** CQRS commands via mediator: `Mediator.SendAsync<CommandResult<T>>(command)`

## Related Documentation

- See `CQRS_MIGRATION_COMPLETE.md` for handler implementation details
- See `ArticleEditLogic.cs` for deprecated methods (marked [Obsolete])
- See `.github/copilot-instructions.md` for architecture patterns

## TODO (Future Enhancements)

- [ ] Add integration tests with database transactions
- [ ] Add performance benchmarks for command handlers
- [ ] Add concurrency/race condition tests
- [ ] Add edge case tests for boundary conditions
- [ ] Add tests for CDN integration during publish
- [ ] Add tests for antiforgery token validation
