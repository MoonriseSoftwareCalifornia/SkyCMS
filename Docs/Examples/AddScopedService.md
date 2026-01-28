# Example: Register a scoped service and add a unit test

This example shows a minimal pattern to add a new service and register it in DI.

1) Create service interface and implementation (example path: `Common/Services/MyFeatureService.cs`)

```csharp
public interface IMyFeatureService
{
    Task<string> GetValueAsync();
}

public class MyFeatureService : IMyFeatureService
{
    public Task<string> GetValueAsync() => Task.FromResult("ok");
}
```

2) Register the service in the project `Program.cs` where DI is configured:

```csharp
builder.Services.AddScoped<IMyFeatureService, MyFeatureService>();
```

3) Add a basic unit test (project: `Tests` or `Editor.Tests`) verifying behavior.

This scaffold follows repository patterns: small services, scoped lifetime, and tests alongside existing test projects.
