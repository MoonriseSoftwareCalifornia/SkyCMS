using Cosmos.DynamicConfig;
using Cosmos.MultiTenant.Administrator.Data;
using Cosmos.MultiTenant.Administrator.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;

namespace Cosmos.MultiTenant.Administrator.Controllers
{
    public class WebsiteAuthorsController : Controller
    {
        private readonly StoryDeskDbContext dbContext;
        private readonly List<ConnectionWebSiteViewModel> websites;
        private readonly DynamicConfigDbContext configContext;

        public WebsiteAuthorsController(StoryDeskDbContext context, DynamicConfigDbContext configContext)
        {
            this.dbContext = context;
            this.configContext = configContext;
            this.websites = configContext.Connections.Select(s => new ConnectionWebSiteViewModel() { ConnectionId = s.Id, WebsiteUrl = s.WebsiteUrl }).ToListAsync().Result;
        }

        // GET: StoryAuthorsAndWebsites
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> Index()
        {
            var data = await dbContext.WebsiteAuthors.ToListAsync();

            return View(data.Select(s => new WebsiteAuthorsViewModel(s)).ToList());
        }

        // GET: StoryAuthorsAndWebsites/Details/5
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            ViewData["Connections"] = websites;

            var entity = await dbContext.WebsiteAuthors
                .FirstOrDefaultAsync(m => m.Id == id);
            if (entity == null)
            {
                return NotFound();
            }

            return View(entity);
        }

        // GET: StoryAuthorsAndWebsites/Create
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public IActionResult Create()
        {
            ViewData["Connections"] = websites;
            return View(new WebsiteAuthorsViewModel());
        }

        // POST: StoryAuthorsAndWebsites/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> Create([Bind("Id,ConnectionId,WebsiteUrl,TemplateId,TemplateName,EmailAddress,Path")] WebsiteAuthorsViewModel model)
        {
            if (ModelState.IsValid)
            {
                ViewData["Connections"] = websites;

                var entity = new WebsiteAuthor()
                {
                    ConnectionId = model.ConnectionId,
                    EmailAddress = model.EmailAddress,
                    Path = model.Path,
                    TemplateId = model.TemplateId,
                    TemplateName = model.TemplateName,
                    WebsiteUrl = model.WebsiteUrl
                };

                dbContext.Add(entity);
                await dbContext.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // GET: StoryAuthorsAndWebsites/Edit/5
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var entity = await dbContext.WebsiteAuthors.FindAsync(id);
            if (entity == null)
            {
                return NotFound();
            }

            ViewData["Connections"] = websites;

            return View(new WebsiteAuthorsViewModel(entity));
        }

        // POST: StoryAuthorsAndWebsites/Edit/5
        // To protect from over-posting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,ConnectionId,WebsiteUrl,TemplateId,TemplateName,EmailAddress,Path")] WebsiteAuthorsViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            var entity = await dbContext.WebsiteAuthors.FindAsync(id);

            if (entity == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                ViewData["Connections"] = websites;

                entity.ConnectionId = model.ConnectionId;
                entity.EmailAddress = model.EmailAddress;
                entity.Path = model.Path;
                entity.TemplateId = model.TemplateId;
                entity.TemplateName = model.TemplateName;
                entity.WebsiteUrl = model.WebsiteUrl;

                await dbContext.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewData["Connections"] = websites;

            return View(model);
        }

        // GET: StoryAuthorsAndWebsites/Delete/5
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var storyAuthorsAndWebsites = await dbContext.WebsiteAuthors
                .FirstOrDefaultAsync(m => m.Id == id);
            if (storyAuthorsAndWebsites == null)
            {
                return NotFound();
            }

            ViewData["Connections"] = websites;

            return View(storyAuthorsAndWebsites);
        }

        // POST: StoryAuthorsAndWebsites/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var storyAuthorsAndWebsites = await dbContext.WebsiteAuthors.FindAsync(id);
            if (storyAuthorsAndWebsites != null)
            {
                dbContext.WebsiteAuthors.Remove(storyAuthorsAndWebsites);
            }

            await dbContext.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Gets the list of templates for a given connection ID.
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public async Task<IActionResult> GetTemplates(Guid Id)
        {
            var connection = await this.configContext.Connections.FindAsync(Id);
            if (connection == null)
            {
                return NotFound();
            }

            var templates = await ApplicationDbContextUtilities.GetApplicationDbContext(connection)
                .Templates
                .Select(t => new { t.Id, t.Title })
                .ToListAsync();

            return Json(templates);
        }

        private bool StoryAuthorsAndWebsitesExists(Guid id)
        {
            return dbContext.WebsiteAuthors.Any(e => e.Id == id);
        }
    }
}
