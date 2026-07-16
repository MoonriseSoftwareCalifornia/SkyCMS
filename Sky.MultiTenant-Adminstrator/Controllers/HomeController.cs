using Cosmos.DynamicConfig;
using Cosmos.MultiTenant.Administrator.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using Microsoft.Identity.Web;
using System.Diagnostics;

namespace Cosmos.MultiTenant.Administrator.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly GraphServiceClient _graphServiceClient;
        private readonly ILogger<HomeController> _logger;
        private readonly DynamicConfigDbContext _context;

        public HomeController(ILogger<HomeController> logger, GraphServiceClient graphServiceClient, DynamicConfigDbContext context)
        {
            _logger = logger;
            _graphServiceClient = graphServiceClient;
            _context = context;
        }
                
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var user = await _graphServiceClient.Me.GetAsync();

                // Test to see if the database is built and accessible
                _ = await _context.Database.EnsureCreatedAsync();

                ViewData["GraphApiResult"] = user?.DisplayName;
            }
            catch
            {
                ViewData["GraphApiResult"] = "Error retrieving user information.";
            }
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
