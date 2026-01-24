---
title: SaveArticle() Migration Guide
layout: default
---

# SaveArticle() Migration Guide

## Overview

The legacy `ArticleEditLogic.SaveArticle()` method has been deprecated in favor of the new vertical slice architecture using `SaveArticleHandler` via the mediator pattern.

## Status

**Production Code**: Fully migrated  
**EditorController**: Migrated  
**BlogController**: Migrated  
**Test Files**: Legacy tests remain for backward compatibility

## Migration Path

### Old Approach (Deprecated)


## Benefits of New Approach

| Aspect | Old Method | New Handler |
|--------|-----------|-------------|
| **Validation** | Mixed with logic | Separate `SaveArticleValidator` |
| **Testing** | Requires full DI setup | Mock mediator easily |
| **Error Handling** | Custom per caller | Consistent `CommandResult<T>` |
| **Logging** | Inconsistent | Built-in structured logging |
| **Separation of Concerns** | Mixed | Clean CQRS pattern |

## Timeline

- **Step 1** (Current): Both methods available, obsolete warnings on old method
- **Step 2** (Next minor): Continue supporting both for backward compatibility
- **Step 3** (Future major): Remove `SaveArticle()` method entirely

## Test Coverage

Legacy integration tests remain in place to ensure backward compatibility:
- `ArticleEditLogicAdditionalTests.cs`
- `ArticleEditLogicExtendedTests.cs`
- `SaveArticleCatalogTests.cs`
- `SaveArticleContentTests.cs`
- `SaveArticleConcurrencyTests.cs`
- `SaveArticleErrorHandlingTests.cs`
- `SaveArticlePublishingTests.cs`
- `EditorControllerSaveTests.cs`

These tests validate the legacy method still works correctly while the new handler is being adopted.

## Questions?

Contact the architecture team or open an issue on GitHub.
