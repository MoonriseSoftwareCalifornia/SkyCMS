# CQRS Implementation Patterns Used

This document summarizes the exact patterns and conventions used in the newly implemented commands and handlers.

---

## Command Definition Pattern

All commands inherit from `ICommand<CommandResult<TResult>>`:

```csharp
public class XxxCommand : ICommand<CommandResult<XxxResult>>
{
    public PropertyType Property { get; set; }
}

public class XxxResult
{
    public SomeType Data { get; set; }
}
```

**For void-like commands** (no data returned), use `Unit`:

```csharp
public class DeleteArticleCommand : ICommand<CommandResult<Unit>>
{
    public int ArticleNumber { get; set; }
}
```

---

## Handler Implementation Pattern

All handlers implement `ICommandHandler<TCommand, CommandResult<TResult>>`:

```csharp
public class XxxHandler : ICommandHandler<XxxCommand, CommandResult<XxxResult>>
{
    private readonly IDependency1 dependency1;
    private readonly IDependency2 dependency2;
    private readonly ILogger<XxxHandler> logger;

    public XxxHandler(
        IDependency1 dependency1,
        IDependency2 dependency2,
        ILogger<XxxHandler> logger)
    {
        this.dependency1 = dependency1 ?? throw new ArgumentNullException(nameof(dependency1));
        this.dependency2 = dependency2 ?? throw new ArgumentNullException(nameof(dependency2));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CommandResult<XxxResult>> HandleAsync(
        XxxCommand command, 
        CancellationToken cancellationToken = default)
    {
        // Null check
        if (command == null)
        {
            return new CommandResult<XxxResult> 
            { 
                IsSuccess = false, 
                ErrorMessage = "Command cannot be null" 
            };
        }

        try
        {
            // Business logic here
            var result = await DoSomethingAsync(command, cancellationToken);
            
            logger.LogInformation("Operation completed successfully");
            
            return new CommandResult<XxxResult> 
            { 
                IsSuccess = true, 
                Data = result 
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during operation");
            return new CommandResult<XxxResult> 
            { 
                IsSuccess = false, 
                ErrorMessage = $"Error: {ex.Message}" 
            };
        }
    }
}
```

---

## CommandResult Pattern

The `CommandResult<T>` is used with object initializer syntax:

```csharp
// Success case
return new CommandResult<T> 
{ 
    IsSuccess = true, 
    Data = resultData 
};

// Failure case
return new CommandResult<T> 
{ 
    IsSuccess = false, 
    ErrorMessage = "Description of error" 
};
```

**No** `.Failed()` or `.Success()` factory methods - use object initializer syntax.

---

## Error Handling Pattern

All error cases follow this pattern:

```csharp
if (validation_fails)
{
    logger.LogWarning("Specific validation failure details");
    return new CommandResult<T> 
    { 
        IsSuccess = false, 
        ErrorMessage = "User-friendly error message" 
    };
}
```

For exceptions:

```csharp
catch (Exception ex)
{
    logger.LogError(ex, "Context about what failed with {Variable}", variable);
    return new CommandResult<T> 
    { 
        IsSuccess = false, 
        ErrorMessage = $"Error: {ex.Message}" 
    };
}
```

---

## Using Directives Pattern

Commands use:

```csharp
using System;
using Cosmos.Cms.Common;              // For ArticleType, enums
using Cosmos.Common.Data.Logic;       // For StatusCodeEnum
using Cosmos.Common.Features.Shared;  // For Unit, ICommand
using Cosmos.Common.Models;           // For ArticleViewModel
using Sky.Cms.Models;                 // For ArticleViewModel
```

Handlers use:

```csharp
using System;
using System.Linq;
using System.Threading;               // For CancellationToken
using System.Threading.Tasks;         // For Task
using Cosmos.BlobService;             // For IStorageContext if needed
using Cosmos.Cms.Common;              // For StatusCodeEnum, ArticleType
using Cosmos.Common.Data;             // For ApplicationDbContext
using Cosmos.Common.Data.Logic;       // For StatusCodeEnum, ArticleLogic
using Cosmos.Common.Features.Shared;  // For Unit, CommandResult
using Cosmos.Common.Models;           // For ArticleViewModel
using Microsoft.EntityFrameworkCore;  // For async DB operations
using Microsoft.Extensions.Logging;   // For ILogger
using Sky.Cms.Models;                 // For ArticleViewModel
using Sky.Editor.Infrastructure.Time; // For IClock
using Sky.Editor.Services.*;          // For specific services
```

---

## Database Operations Pattern

All database calls use `cancellationToken`:

```csharp
// Single item
var item = await dbContext.Articles
    .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

// Collections
var items = await dbContext.Articles
    .Where(a => a.ArticleNumber == number)
    .ToListAsync(cancellationToken);

// Saves
await dbContext.SaveChangesAsync(cancellationToken);
```

---

## Logging Pattern

Structured logging with context:

```csharp
// Warnings for validation failures
logger.LogWarning("Item {ItemId} not found", itemId);

// Information for successful operations
logger.LogInformation(
    "Article {ArticleNumber} version {VersionNumber} published at {PublishedTime}",
    article.ArticleNumber,
    article.VersionNumber,
    publishTime);

// Errors for exceptions
logger.LogError(ex, "Error during operation for {ItemId}", itemId);
```

---

## DI Registration Pattern

In Program.cs:

```csharp
services.AddTransient<
    ICommandHandler<XxxCommand, CommandResult<XxxResult>>,
    XxxHandler>();
```

For Unit-returning commands:

```csharp
services.AddTransient<
    ICommandHandler<XxxCommand, CommandResult<Unit>>,
    XxxHandler>();
```

---

## Null Safety Pattern

All handlers start with:

```csharp
if (command == null)
{
    return new CommandResult<T> { IsSuccess = false, ErrorMessage = "Command cannot be null" };
}
```

Constructor parameter validation:

```csharp
this.parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
```

---

## Async Pattern

All database operations are async:

```csharp
public async Task<CommandResult<T>> HandleAsync(TCommand command, CancellationToken cancellationToken)
{
    // Never use .Result or .Wait()
    var entity = await dbContext.Set<Entity>()
        .FirstOrDefaultAsync(predicate, cancellationToken);
    
    // Always pass cancellationToken
    await dbContext.SaveChangesAsync(cancellationToken);
}
```

---

## Example: Complete Handler

```csharp
namespace Sky.Editor.Features.Articles.Publish
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.Catalog;
    using Sky.Editor.Services.Publishing;

    public class PublishArticleHandler : ICommandHandler<PublishArticleCommand, CommandResult<PublishArticleCommandResult>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IClock clock;
        private readonly IPublishingService publishingService;
        private readonly ICatalogService catalogService;
        private readonly ILogger<PublishArticleHandler> logger;

        public PublishArticleHandler(
            ApplicationDbContext dbContext,
            IClock clock,
            IPublishingService publishingService,
            ICatalogService catalogService,
            ILogger<PublishArticleHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.publishingService = publishingService ?? throw new ArgumentNullException(nameof(publishingService));
            this.catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CommandResult<PublishArticleCommandResult>> HandleAsync(
            PublishArticleCommand command, 
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                return new CommandResult<PublishArticleCommandResult> 
                { 
                    IsSuccess = false, 
                    ErrorMessage = "Command cannot be null" 
                };
            }

            try
            {
                var article = await dbContext.Articles
                    .FirstOrDefaultAsync(a => a.Id == command.ArticleId, cancellationToken);
                
                if (article == null)
                {
                    logger.LogWarning("Article with ID {ArticleId} not found for publishing", command.ArticleId);
                    return new CommandResult<PublishArticleCommandResult> 
                    { 
                        IsSuccess = false, 
                        ErrorMessage = $"Article with ID {command.ArticleId} not found" 
                    };
                }

                var publishTime = command.PublishTime ?? clock.UtcNow;
                article.Published = publishTime;
                
                await dbContext.SaveChangesAsync(cancellationToken);
                
                logger.LogInformation(
                    "Article {ArticleNumber} version {VersionNumber} published",
                    article.ArticleNumber,
                    article.VersionNumber);

                var cdnResults = await publishingService.PublishAsync(article);
                await catalogService.UpsertAsync(article);

                return new CommandResult<PublishArticleCommandResult>
                {
                    IsSuccess = true,
                    Data = new PublishArticleCommandResult
                    {
                        CdnResults = cdnResults ?? new List<Sky.Editor.Services.CDN.CdnResult>()
                    }
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error publishing article {ArticleId}", command.ArticleId);
                return new CommandResult<PublishArticleCommandResult> 
                { 
                    IsSuccess = false, 
                    ErrorMessage = $"Error publishing article: {ex.Message}" 
                };
            }
        }
    }
}
```

---

## Summary

These patterns ensure:
- ? Consistency across all handlers
- ? Proper error handling and logging
- ? Full async/await support with cancellation
- ? Clear, predictable command results
- ? Type-safe CQRS implementation
- ? Easy unit testing
- ? Maintainable, readable code
