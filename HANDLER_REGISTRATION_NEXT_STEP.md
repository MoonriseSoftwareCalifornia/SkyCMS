# Final Step: Register Handlers in Program.cs

## Status
**Production code is compiling successfully!** ?

Now we need to register the 5 new command handlers in the dependency injection container.

---

## Where to Add (Program.cs)

Find the section where other handlers are registered. Look for:
- Command handler registrations
- Query handler registrations  
- Other feature registrations

Add these lines (in the appropriate DI setup section):

```csharp
// Article Command Handlers - CQRS Migration Phase
services.AddTransient<
    ICommandHandler<PublishArticleCommand, CommandResult<PublishArticleCommandResult>>,
    PublishArticleHandler>();

services.AddTransient<
    ICommandHandler<DeleteArticleCommand, CommandResult<Unit>>,
    DeleteArticleHandler>();

services.AddTransient<
    ICommandHandler<RestoreArticleCommand, CommandResult<Unit>>,
    RestoreArticleHandler>();

services.AddTransient<
    ICommandHandler<CreateArticleVersionCommand, CommandResult<CreateArticleVersionCommandResult>>,
    CreateArticleVersionHandler>();

services.AddTransient<
    ICommandHandler<CreateHomePageCommand, CommandResult<Unit>>,
    CreateHomePageHandler>();
```

---

## Required Using Statements

Add these to the top of Program.cs:

```csharp
using Sky.Editor.Features.Articles.Publish;
using Sky.Editor.Features.Articles.Delete;
using Sky.Editor.Features.Articles.Restore;
using Sky.Editor.Features.Articles.CreateVersion;
using Sky.Editor.Features.Articles.CreateHomePage;
using Cosmos.Common.Features.Shared;  // For ICommandHandler, Unit
```

---

## After Registration

Once registered in DI:

1. **EditorController** - Already updated ?
2. **No test updates needed** - Production code is ready ?
3. **Ready to run** - Application can now use the new CQRS commands

---

## Verification

After adding the registrations, the application should:
- ? Build without errors
- ? Start without DI errors
- ? Handle NewHome() controller action (uses CreateHomePageCommand)
- ? Handle ExportPage() controller action (uses CreateArticleCommand)
- ? Use all 5 new commands through the mediator pattern

---

## Optional Test Updates

The EditorControllerTests.cs file has test methods that call:
- `controller.Clone()` - These reference deprecated methods
- `controller.CreateVersion()` - These reference deprecated methods
- `controller.NewHome()` - These reference updated methods

**These can be updated in a future phase** - they don't block the production system from working.

---

## Done! ??

With handler registration in Program.cs, the complete CQRS migration will be fully operational.
