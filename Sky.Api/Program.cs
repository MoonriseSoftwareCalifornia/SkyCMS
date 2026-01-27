using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Cosmos.Common.Data;
using Cosmos.Common.Data.Logic;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Services.Configurations;
using Cosmos.DynamicConfig;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Add required services
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// Add OpenAPI/Swagger
builder.Services.AddOpenApi();

// Add database context
builder.Services.AddDbContext<ApplicationDbContext>();

// Add multi-tenant configuration provider
builder.Services.AddSingleton<IDynamicConfigurationProvider, DynamicConfigurationProvider>();

// Add article logic
builder.Services.AddScoped<ArticleLogic>(provider =>
{
    var dbContext = provider.GetRequiredService<ApplicationDbContext>();
    var memoryCache = provider.GetRequiredService<IMemoryCache>();
    var configuration = provider.GetRequiredService<IConfiguration>();
    
    // Get URLs from configuration or use defaults
    var publisherUrl = configuration["PublisherUrl"] ?? "http://localhost:5001";
    var blobPublicUrl = configuration["BlobPublicUrl"] ?? "";
    
    return new ArticleLogic(dbContext, memoryCache, publisherUrl, blobPublicUrl, false);
});

// Add mediator
builder.Services.AddScoped<IMediator, Mediator>();

// Add search query handlers
builder.Services.AddScoped<IQueryHandler<Sky.Cms.Api.Shared.Features.Search.Query.SearchQuery, Sky.Cms.Api.Shared.Models.Search.SearchApiResponse>, Sky.Cms.Api.Shared.Features.Search.Query.SearchQueryHandler>();
builder.Services.AddScoped<IQueryHandler<Sky.Cms.Api.Shared.Features.Search.Query.SearchHealthQuery, Sky.Cms.Api.Shared.Models.Search.SearchHealthApiResponse>, Sky.Cms.Api.Shared.Features.Search.Query.SearchHealthQueryHandler>();
builder.Services.AddScoped<IQueryHandler<Sky.Cms.Api.Shared.Features.Search.Suggest.SearchSuggestionsQuery, Sky.Cms.Api.Shared.Models.Search.SearchSuggestionsApiResponse>, Sky.Cms.Api.Shared.Features.Search.Suggest.SearchSuggestionsQueryHandler>();

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    // Search rate limiting policy
    options.AddFixedWindowLimiter("search-policy", configureOptions =>
    {
        configureOptions.PermitLimit = builder.Environment.IsDevelopment() ? 100 : 30; // More generous limits for search
        configureOptions.Window = TimeSpan.FromMinutes(1);
        configureOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        configureOptions.QueueLimit = 10;
    });

    // Default rate limiting policy
    options.AddFixedWindowLimiter("default", configureOptions =>
    {
        configureOptions.PermitLimit = builder.Environment.IsDevelopment() ? 200 : 100;
        configureOptions.Window = TimeSpan.FromMinutes(1);
        configureOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        configureOptions.QueueLimit = 20;
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Use rate limiting
app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

app.Run();
