using Cosmos.DynamicConfig;
using Cosmos.MultiTenant.Administrator.Data;
using Cosmos.MultiTenant.Administrator.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.Graph;
using Microsoft.Kiota.Http.HttpClientLibrary;

var builder = WebApplication.CreateBuilder(args);

var initialScopes = builder.Configuration["DownstreamApi:Scopes"]?.Split(' ') ?? builder.Configuration["MicrosoftGraph:Scopes"]?.Split(' ');

// Add services to the container.
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
        .EnableTokenAcquisitionToCallDownstreamApi(initialScopes)
            .AddInMemoryTokenCaches();

// Register HttpClientFactory
builder.Services.AddHttpClient();

// Register GraphServiceClient with Kiota-based authentication
builder.Services.AddScoped<GraphServiceClient>(provider =>
{
    var tokenAcquisition = provider.GetRequiredService<ITokenAcquisition>();
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

    // Create a custom authentication provider using ITokenAcquisition
    var authProvider = new TokenAcquisitionAuthenticationProvider(tokenAcquisition, initialScopes);

    // Create HTTP client with custom handler
    var httpClient = httpClientFactory.CreateClient();

    // Create request adapter for Kiota
    var requestAdapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient);

    return new GraphServiceClient(requestAdapter);
});

var connectionString = builder.Configuration.GetConnectionString("ConfigDbConnectionString");

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'ConfigDbConnectionString' not found.");
}

builder.Services.AddDbContext<DynamicConfigDbContext>(options =>
    options.UseCosmos(connectionString: connectionString, databaseName: "configs"));

builder.Services.AddDbContext<StoryDeskDbContext>(options =>
    options.UseCosmos(connectionString: connectionString, databaseName: "configs"));

builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});
builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI();

// BEGIN
// When deploying to a Docker container, the OAuth redirect_url
// parameter may have http instead of https.
// Providers often do not allow http because it is not secure.
// So authentication will fail.
// Article below shows instructions for fixing this.
//
// NOTE: There is a companion secton below in the Configure method. Must have this
//
// https://seankilleen.com/2020/06/solved-net-core-azure-ad-in-docker-container-incorrectly-uses-an-non-https-redirect-uri/
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                               ForwardedHeaders.XForwardedProto;

    // Only loopback proxies are allowed by default.
    // Clear that restriction because forwarders are enabled by explicit
    // configuration.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// https://seankilleen.com/2020/06/solved-net-core-azure-ad-in-docker-container-incorrectly-uses-an-non-https-redirect-uri/
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<StoryDeskDbContext>();
    var task = dbContext.Database.EnsureCreatedAsync();
    task.Wait(); // Wait for the database to be created
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();
