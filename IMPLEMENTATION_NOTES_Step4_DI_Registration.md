// <copyright file="PublishedBlogServiceRegistration.md" company="Moonrise Software, LLC">
// Step 4: DI Registration Instructions for IPublishedBlogService
// </copyright>

# Step 4: Register IPublishedBlogService in Publisher\Boot\DynamicPublisherWebsite.cs

## Location in DynamicPublisherWebsite.Boot() method

Add the following registration **after** the `ApplicationDbContext` registration and **before** the MVC/authentication configuration.

A good place is around line 100-110, after the storage and data protection setup but before `builder.Services.AddMvc()`.

## Code to Add

```csharp
// Add Published Blog Service for public-facing blog rendering
builder.Services.AddScoped<IPublishedBlogService, PublishedBlogService>();
```

## Full Context (where to insert)

```csharp
// Around line 100 in DynamicPublisherWebsite.Boot():

// Add shared data protection here
builder.Services.AddCosmosCmsDataProtection(builder.Configuration, defaultAzureCredential);

// ? ADD THIS LINE:
builder.Services.AddScoped<Cosmos.Common.Services.PublishedBlog.IPublishedBlogService, 
                            Cosmos.Common.Services.PublishedBlog.PublishedBlogService>();

builder.Services.AddMvc()
                .AddNewtonsoftJson(options =>
                    options.SerializerSettings.ContractResolver =
                        new DefaultContractResolver());
```

## Required Using Statements

Add these to the top of `DynamicPublisherWebsite.cs` if not already present:

```csharp
using Cosmos.Common.Services.PublishedBlog;
```

## Why Scoped?

- **Scoped** lifetime is correct because each HTTP request should get a fresh instance
- Allows proper dependency injection of `ApplicationDbContext` (which is also scoped)
- Ensures thread-safe database queries per request

## Verification

After registration, you can inject `IPublishedBlogService` into any Razor Page handler or controller in the Publisher project:

```csharp
public class YourPageModel : PageModel
{
    private readonly IPublishedBlogService _blogService;

    public YourPageModel(IPublishedBlogService blogService)
    {
        _blogService = blogService;
    }

    public async Task OnGet(string blogKey, string entryUrl)
    {
        var entry = await _blogService.GetPublishedBlogEntryAsync(entryUrl);
        // ... use entry ...
    }
}
```
