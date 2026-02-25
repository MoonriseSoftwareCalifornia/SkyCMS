# DI Registration Configuration

Add this code to your `Program.cs` in the service configuration section to register all 5 new command handlers:

```csharp
// Register Article Command Handlers
// These are CQRS command handlers for article operations

// Publish Article Handler
services.AddTransient<
    ICommandHandler<PublishArticleCommand, CommandResult<PublishArticleCommandResult>>,
    PublishArticleHandler>();

// Delete Article Handler
services.AddTransient<
    ICommandHandler<DeleteArticleCommand, CommandResult<Unit>>,
    DeleteArticleHandler>();

// Restore Article Handler
services.AddTransient<
    ICommandHandler<RestoreArticleCommand, CommandResult<Unit>>,
    RestoreArticleHandler>();

// Create Article Version Handler
services.AddTransient<
    ICommandHandler<CreateArticleVersionCommand, CommandResult<CreateArticleVersionCommandResult>>,
    CreateArticleVersionHandler>();

// Create Home Page Handler
services.AddTransient<
    ICommandHandler<CreateHomePageCommand, CommandResult<Unit>>,
    CreateHomePageHandler>();
```

## Using Statements to Add

Ensure these using statements are present in `Program.cs`:

```csharp
using Sky.Editor.Features.Articles.Publish;
using Sky.Editor.Features.Articles.Delete;
using Sky.Editor.Features.Articles.Restore;
using Sky.Editor.Features.Articles.CreateVersion;
using Sky.Editor.Features.Articles.CreateHomePage;
using Cosmos.Common.Features.Shared;
```

## Recommended Placement

Place these registrations alongside other command handler registrations, or if none exist, add them after query handler registrations but before other service registrations:

```csharp
// Query Handlers (existing)
services.AddTransient<IQueryHandler<...>, ...>();

// Command Handlers (NEW - add here)
services.AddTransient<ICommandHandler<PublishArticleCommand, ...>, PublishArticleHandler>();
// ... other command handlers ...

// Other Services
services.AddScoped<IStorageContext, StorageContext>();
// ... etc ...
```

## Verification

After adding registrations, you can verify they compile by:
1. Building the solution: `dotnet build`
2. Looking for no build errors related to the handlers
3. Running tests: `dotnet test Tests/Sky.Tests.csproj --filter "PublishArticle or DeleteArticle or RestoreArticle or CreateVersion or CreateHomePage"`
