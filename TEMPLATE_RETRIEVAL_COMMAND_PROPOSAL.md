# Template Retrieval Command - Design Proposal

## Overview
Based on analysis of the current codebase, I'm proposing a new `GetTemplateQuery` command following the CQRS pattern established in the SkyCMS project.

## Current Template Retrieval Patterns Found

### 1. **TemplatesController - Single Template Retrieval**
```csharp
// Edit method (line 381)
var template = await dbContext.Templates.FirstOrDefaultAsync(f => f.Id == id);

// EditCode method (line 429)
var entity = await dbContext.Templates.FirstOrDefaultAsync(f => f.Id == id);

// Designer method (line 508)
var template = await dbContext.Templates.FirstOrDefaultAsync(f => f.Id == id);
```

### 2. **EditorController - Template Info Retrieval**
```csharp
// GetTemplateInfo method (line 570)
var model = await dbContext.Templates.FirstOrDefaultAsync(f => f.Id == id.Value);
```

### 3. **BaseController - Template List for Current Layout**
```csharp
// GetTemplatesForCurrentLayoutAsync (line 204-217)
protected async Task<IQueryable<Template>> GetTemplatesForCurrentLayoutAsync()
{
    var layout = await GetCurrentLayoutAsync();
    return dbContext.Templates
        .Where(t => t.LayoutNumber == layout.LayoutNumber || 
                    (t.LayoutNumber == 0 && t.LayoutId == layout.Id));
}
```

## Pattern Analysis

### Existing Command Pattern (Create/Save)
- **Location**: `Editor/Features/Templates/Create|Save/`
- **Structure**: 
  - Command object (data carrier)
  - ICommand<CommandResult<T>> interface
  - Handler implementing ICommandHandler<TCommand, CommandResult<T>>
  - Validator class
- **Key Features**:
  - Strongly-typed CommandResult<T> return type
  - Built-in validation
  - Async execution with CancellationToken
  - Logging via ILogger
  - Dependency injection for services

### Query Pattern (Read-Only)
- **Location**: `Editor/Features/Shared/IQuery<TResult>`
- **Structure**: Marker interface for read-only operations
- **Used For**: Data retrieval without state modification

## Proposed Solution

### New Command: `GetTemplateQuery`

#### File Structure
```
Editor/Features/Templates/Get/
??? GetTemplateQuery.cs
??? GetTemplateQueryHandler.cs
??? GetTemplateQueryValidator.cs (optional)
??? GetTemplateQueryResult.cs (optional)
```

#### GetTemplateQuery.cs
```csharp
namespace Sky.Editor.Features.Templates.Get
{
    using System;
    using Sky.Editor.Features.Shared;

    /// <summary>
    /// Query to retrieve a template by ID.
    /// Supports optional inclusion of page design versions.
    /// </summary>
    public sealed class GetTemplateQuery : IQuery<CommandResult<GetTemplateQueryResult>>
    {
        /// <summary>
        /// Gets or sets the template ID to retrieve.
        /// </summary>
        public Guid TemplateId { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether to include page design versions.
        /// </summary>
        public bool IncludeVersions { get; init; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to include only the latest version.
        /// Only applicable when IncludeVersions is true.
        /// </summary>
        public bool LatestVersionOnly { get; init; } = false;
    }
}
```

#### GetTemplateQueryResult.cs
```csharp
namespace Sky.Editor.Features.Templates.Get
{
    using System;
    using System.Collections.Generic;
    using Cosmos.Common.Data;

    /// <summary>
    /// Result object for template queries.
    /// </summary>
    public class GetTemplateQueryResult
    {
        /// <summary>
        /// Gets or sets the template entity.
        /// </summary>
        public Template Template { get; set; }

        /// <summary>
        /// Gets or sets the page design versions (if requested).
        /// </summary>
        public IEnumerable<PageDesignVersion> Versions { get; set; } = new List<PageDesignVersion>();
    }
}
```

#### GetTemplateQueryHandler.cs
```csharp
namespace Sky.Editor.Features.Templates.Get
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Features.Shared;

    /// <summary>
    /// Handles template retrieval queries.
    /// </summary>
    public class GetTemplateQueryHandler : IQueryHandler<GetTemplateQuery, CommandResult<GetTemplateQueryResult>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger<GetTemplateQueryHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetTemplateQueryHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="logger">Logger service.</param>
        public GetTemplateQueryHandler(
            ApplicationDbContext dbContext,
            ILogger<GetTemplateQueryHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles the get template query.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Query result containing template and optional versions.</returns>
        public async Task<CommandResult<GetTemplateQueryResult>> HandleAsync(
            GetTemplateQuery query,
            CancellationToken cancellationToken = default)
        {
            if (query.TemplateId == Guid.Empty)
            {
                logger.LogWarning("GetTemplateQuery: TemplateId is empty");
                return CommandResult<GetTemplateQueryResult>.Failure("Template ID cannot be empty");
            }

            try
            {
                var template = await dbContext.Templates
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == query.TemplateId, cancellationToken);

                if (template == null)
                {
                    logger.LogWarning("GetTemplateQuery: Template {TemplateId} not found", query.TemplateId);
                    return CommandResult<GetTemplateQueryResult>.Failure($"Template {query.TemplateId} not found");
                }

                var result = new GetTemplateQueryResult { Template = template };

                // Load versions if requested
                if (query.IncludeVersions)
                {
                    var versionsQuery = dbContext.PageDesignVersions
                        .Where(v => v.TemplateId == query.TemplateId)
                        .AsNoTracking();

                    if (query.LatestVersionOnly)
                    {
                        var latestVersion = await versionsQuery
                            .OrderByDescending(v => v.Version)
                            .FirstOrDefaultAsync(cancellationToken);

                        result.Versions = latestVersion != null ? new[] { latestVersion } : Array.Empty<PageDesignVersion>();
                    }
                    else
                    {
                        result.Versions = await versionsQuery
                            .OrderByDescending(v => v.Version)
                            .ToListAsync(cancellationToken);
                    }
                }

                logger.LogInformation(
                    "Successfully retrieved template {TemplateId} with {VersionCount} versions",
                    query.TemplateId,
                    result.Versions.Count());

                return CommandResult<GetTemplateQueryResult>.Success(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving template {TemplateId}", query.TemplateId);
                return CommandResult<GetTemplateQueryResult>.Failure($"Error retrieving template: {ex.Message}");
            }
        }
    }
}
```

#### GetTemplateQueryValidator.cs (Optional)
```csharp
namespace Sky.Editor.Features.Templates.Get
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Validator for GetTemplateQuery.
    /// </summary>
    public class GetTemplateQueryValidator
    {
        /// <summary>
        /// Validates the query.
        /// </summary>
        /// <param name="query">The query to validate.</param>
        /// <returns>List of validation errors, empty if valid.</returns>
        public List<string> Validate(GetTemplateQuery query)
        {
            var errors = new List<string>();

            if (query.TemplateId == Guid.Empty)
            {
                errors.Add("TemplateId cannot be empty");
            }

            return errors;
        }
    }
}
```

## Usage Examples

### Basic Template Retrieval (Controller Usage)
```csharp
public async Task<IActionResult> Edit(Guid id)
{
    var query = new GetTemplateQuery { TemplateId = id };
    var result = await mediator.SendAsync(query);

    if (!result.IsSuccess)
    {
        return NotFound();
    }

    var template = result.Data.Template;
    // Use template...
}
```

### With Versions
```csharp
var query = new GetTemplateQuery 
{ 
    TemplateId = id,
    IncludeVersions = true,
    LatestVersionOnly = true
};
var result = await mediator.SendAsync(query);

if (result.IsSuccess)
{
    var template = result.Data.Template;
    var latestVersion = result.Data.Versions.FirstOrDefault();
}
```

## Advantages

1. **CQRS Compliance**: Follows the same pattern as CreatePageDesignVersionCommand
2. **Separation of Concerns**: Query logic is isolated in a dedicated handler
3. **Testability**: Easy to unit test with mocked DbContext
4. **Logging**: Built-in diagnostic logging
5. **Error Handling**: Consistent CommandResult<T> error handling
6. **Flexibility**: Optional version loading with configurable depth
7. **Performance**: Uses AsNoTracking() for read-only operations
8. **Composability**: Can be used consistently across all controllers and services

## Migration Path

Replace existing inline queries:
```csharp
// Old
var template = await dbContext.Templates.FirstOrDefaultAsync(f => f.Id == id);

// New
var query = new GetTemplateQuery { TemplateId = id };
var result = await mediator.SendAsync(query);
var template = result.IsSuccess ? result.Data.Template : null;
```

## Implementation Priority

**Phase 1 (Essential)**: GetTemplateQuery + Handler
**Phase 2 (Optional)**: Validator + Result class
**Phase 3 (Future)**: Variants (GetTemplatesByLayoutQuery, GetTemplateListQuery, etc.)
