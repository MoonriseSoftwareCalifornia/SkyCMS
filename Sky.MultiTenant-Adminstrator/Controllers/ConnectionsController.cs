using Azure.Storage.Blobs;
using Cosmos.Common.Data;
using Cosmos.DynamicConfig;
using Cosmos.MultiTenant.Administrator.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;

namespace Cosmos.MultiTenant.Administrator.Controllers
{
    [Authorize]
    public class ConnectionsController : Controller
    {
        private readonly DynamicConfigDbContext _context;
        public ConnectionsController(DynamicConfigDbContext context)
        {
            _context = context;
        }

        // GET: Connections
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> Index()
        {
            await _context.Database.EnsureCreatedAsync();

            try
            {
                var connections = await _context.Connections.ToListAsync();
                if (connections == null || !connections.Any())
                {
                    return RedirectToAction("Create");
                }

                var entities = await _context.Connections.ToListAsync();
                var model = new List<ConnectionsIndexViewModel>();

                foreach (var item in entities)
                {
                    var result = await this.TestConnections(item, false);
                    model.Add(new ConnectionsIndexViewModel(item)
                    {
                        DatabaseStatus = result.IsDatabaseConnected,
                        StorageStatus = result.IsStorageConnected
                    });
                }

                return View(model);
            }
            catch (Exception ex)
            {
                // Handle exceptions as needed
                ModelState.AddModelError(string.Empty, "An error occurred while retrieving connections: " + ex.Message);
            }

            return View(new List<Connection>());
        }

        // GET: Connections/Details/5

        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var connection = await _context.Connections
                .FirstOrDefaultAsync(m => m.Id == id);
            if (connection == null)
            {
                return NotFound();
            }

            return View(new ConnectionViewModel(connection));
        }

        // GET: Connections/Create

        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public IActionResult Create()
        {
            return View(new ConnectionViewModel
            {
                Id = Guid.NewGuid(),
                DomainNames = string.Empty,
                DbConn = string.Empty,
                StorageConn = string.Empty,
                Customer = string.Empty,
                WebsiteUrl = string.Empty,
            });
        }

        // POST: Connections/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,DomainNames,DbConn,DbName,StorageConn,Customer,WebsiteUrl,ResourceGroup,PublisherMode,OwnerEmail")] ConnectionViewModel model)
        {
            if (ModelState.IsValid)
            {
                model.Id = Guid.NewGuid();
                var connection = model.ToConnection();
                var result = await TestConnections(connection);
                if (!string.IsNullOrWhiteSpace(connection.DbConn) && !result.IsDatabaseConnected)
                {
                    ModelState.AddModelError(string.Empty, "Database connection failed: " + result.ErrorMessage);
                    return View(model);
                }
                if (!result.IsStorageConnected)
                {
                    ModelState.AddModelError(string.Empty, "Storage connection failed: " + result.ErrorMessage);
                    return View(model);
                }
                _context.Add(connection);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // GET: Connections/Edit/5
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var connection = await _context.Connections.FindAsync(id);
            if (connection == null)
            {
                return NotFound();
            }
            return View(new ConnectionViewModel(connection));
        }

        // POST: Connections/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,DomainNames,DbConn,DbName,StorageConn,Customer,WebsiteUrl,ResourceGroup,PublisherMode,OwnerEmail")] ConnectionViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var connection = model.ToConnection();
                    var result = await TestConnections(connection);
                    if (!string.IsNullOrWhiteSpace(connection.DbConn) && !result.IsDatabaseConnected)
                    {
                        ModelState.AddModelError(string.Empty, "Database connection failed: " + result.ErrorMessage);
                        return View(model);
                    }
                    if (!result.IsStorageConnected)
                    {
                        ModelState.AddModelError(string.Empty, "Storage connection failed: " + result.ErrorMessage);
                        return View(model);
                    }
                    var entity = await _context.Connections.FindAsync(id);
                    if (entity == null)
                    {
                        return NotFound();
                    }

                    entity.PublisherMode = model.PublisherMode;
                    entity.Customer = model.Customer;
                    entity.DomainNames = model.DomainNames.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(d => d.Trim()).ToArray();
                    entity.DbConn = model.DbConn;
                    entity.StorageConn = model.StorageConn;
                    entity.WebsiteUrl = model.WebsiteUrl;
                    entity.ResourceGroup = model.ResourceGroup;
                    entity.OwnerEmail = model.OwnerEmail;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ConnectionExists(model.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // GET: Connections/Delete/5
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var connection = await _context.Connections
                .FirstOrDefaultAsync(m => m.Id == id);
            if (connection == null)
            {
                return NotFound();
            }

            return View(new ConnectionViewModel(connection));
        }

        // POST: Connections/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var connection = await _context.Connections.FindAsync(id);
            if (connection != null)
            {
                _context.Connections.Remove(connection);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ConnectionExists(Guid id)
        {
            return _context.Connections.Any(e => e.Id == id);
        }

        private async Task<TestResult> TestConnections(Connection connection, bool setup = true)
        {
            var result = new TestResult();
            try
            {
                if (!string.IsNullOrWhiteSpace(connection.DbConn))
                {
                    // Test the database connection
                    var dbContext = new ApplicationDbContext(connection.DbConn);
                    var databaseName = GetDatabaseName(connection.DbConn);
                    var testResult = ApplicationDbContext.EnsureDatabaseExists(dbContext, setup, databaseName);
                    result.IsDatabaseConnected = testResult == DbStatus.ExistsWithUsers || testResult == DbStatus.ExistsWithNoUsers;
                }

                // Test the storage connection
                var blobClient = new BlobServiceClient(connection.StorageConn);
                var containerClient = blobClient.GetBlobContainerClient("$web");
                var result1 = await containerClient.CreateIfNotExistsAsync();

                var result2 = await blobClient.GetPropertiesAsync();

                result.IsStorageConnected = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
            }
            return result;
        }

        private static string GetDatabaseName(string connectionString)
        {
            return connectionString
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(s => s.StartsWith("Database=", StringComparison.InvariantCultureIgnoreCase))
                ?.Split('=', 2)[1]
                ?? "cosmoscms";
        }
    }
}
